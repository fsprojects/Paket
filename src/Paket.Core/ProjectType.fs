namespace Paket

open Paket.ProjectJson
open System.IO
open System.Xml
open Xml

[<RequireQualifiedAccess>]
type ProjectType =
| Project of ProjectFile
| ProjectJson of ProjectJsonFile

    with 

        static member FindCorrespondingFile (projectFile:FileInfo,correspondingFile:string) =
            let specificFile = FileInfo (Path.Combine(projectFile.Directory.FullName, projectFile.Name + "." + correspondingFile))
            if specificFile.Exists then Some specificFile.FullName else
        
            let rec findInDir (currentDir:DirectoryInfo) = 
                let generalFile = FileInfo(Path.Combine(currentDir.FullName, correspondingFile))
                if generalFile.Exists then Some generalFile.FullName
                elif (FileInfo (Path.Combine(currentDir.FullName, Constants.DependenciesFileName))).Exists then None
                elif currentDir.Parent = null then None
                else findInDir currentDir.Parent
                
            findInDir projectFile.Directory

        member this.FindCorrespondingFile (correspondingFile:string) = ProjectType.FindCorrespondingFile(FileInfo this.FileName,correspondingFile)

        member this.FindReferencesFile() = this.FindCorrespondingFile Constants.ReferencesFile

        static member FindReferencesFile(projectFile) = ProjectType.FindCorrespondingFile(projectFile, Constants.ReferencesFile)

        member this.FindTemplatesFile() = this.FindCorrespondingFile Constants.TemplateFile

        static member FindOrCreateReferencesFile projectFile =
            match ProjectType.FindReferencesFile(projectFile) with
            | None ->
                let newFileName =
                    let fi = FileInfo(Path.Combine(projectFile.Directory.FullName,Constants.ReferencesFile))
                    if fi.Exists then
                        Path.Combine(projectFile.Directory.FullName,projectFile.Name + "." + Constants.ReferencesFile)
                    else
                        fi.FullName
                ReferencesFile.New newFileName
            | Some fileName -> ReferencesFile.FromFile fileName

        
        member this.FindOrCreateReferencesFile() = ProjectType.FindOrCreateReferencesFile (FileInfo this.FileName)

        member this.FileName =
            match this with
            | Project p -> p.FileName
            | ProjectJson p -> p.FileName

        member this.GetAssemblyName() =
            match this with
            | Project p -> p.GetAssemblyName()
            | ProjectJson p -> FileInfo(p.FileName).Directory.Name + ".dll"

        member this.Save() =
            match this with
            | Project p -> p.Save()
            | ProjectJson p -> p.Save()

        member this.HasPackageInstalled(groupName,package) =
            match this.FindReferencesFile() with
            | None -> false
            | Some fileName -> 
                let referencesFile = ReferencesFile.FromFile fileName
                referencesFile.Groups.[groupName].NugetPackages
                |> Seq.exists (fun p -> p.Name = package)

        member this.ProjectsWithoutTemplates projects =
            projects
            |> Seq.filter(fun proj ->
                if proj = this then true
                else
                    let templateFilename = proj.FindTemplatesFile()
                    match templateFilename with
                    | Some tfn ->
                        TemplateFile.IsProjectType tfn |> not
                    | None -> true
            )

        member this.ProjectsWithTemplates projects =
            projects
            |> Seq.filter(fun proj ->
                if proj = this then true
                else
                    let templateFilename = proj.FindTemplatesFile()
                    match templateFilename with
                    | Some tfn -> TemplateFile.IsProjectType tfn
                    | None -> false
            )

        member this.GetInterProjectDependencies() =
            match this with
            | ProjectType.Project project -> project.GetInterProjectDependencies()
            | ProjectType.ProjectJson project -> []  // TODO: What about project.json?

        member this.GetAllReferencedProjects() = 
            let rec getProjects (project:ProjectType) = 
                seq {
                    let projects = 
                        project.GetInterProjectDependencies() 
                            |> Seq.map (fun proj -> ProjectType.Project(ProjectFile.tryLoad(proj.Path).Value))  // TODO: What about project.json?

                    yield! projects
                    for proj in projects do
                        yield! (getProjects proj)
                }
            seq { 
                yield this
                yield! getProjects this
            }
    
        member this.GetProjects includeReferencedProjects =
            seq {
                if includeReferencedProjects then
                    yield! this.GetAllReferencedProjects()
                else
                    yield this
            }

        member this.GetAllInterProjectDependenciesWithoutProjectTemplates() = this.ProjectsWithoutTemplates(this.GetAllReferencedProjects())

        member this.GetAllInterProjectDependenciesWithProjectTemplates() = this.ProjectsWithTemplates(this.GetAllReferencedProjects())

        member this.GetCompileItems (includeReferencedProjects : bool) = 
            let getItems (item: CompileItem) =
                let getItem file = match item.Link with
                                   | Some link -> {Include = file
                                                   Link = Some(item.Link.Value.Replace("%(FileName)", Path.GetFileName(file)))
                                                   BaseDir = item.BaseDir}
                                   | None -> {Include = file
                                              Link = item.Link
                                              BaseDir = item.BaseDir}
                let dir = Path.GetDirectoryName(item.Include)
                let filespec = Path.GetFileName(item.Include)

                seq {
                        for file in (Directory.GetFiles(dir, filespec)) do 
                            yield (getItem file)
                    }
        
            let getCompileItem (projfile : ProjectFile, compileNode : XmlNode) = 
                let getIncludePath (projfile : ProjectFile) (includePath : string) = 
                    Path.Combine(Path.GetDirectoryName(Path.GetFullPath(projfile.FileName)), includePath)
            
                let includePath = 
                    compileNode
                    |> getAttribute "Include"
                    |> fun a -> a.Value |> getIncludePath projfile
            
                compileNode
                |> getDescendants "Link"
                |> function 
                | [] -> 
                    { Include = includePath
                      Link = None 
                      BaseDir = Path.GetDirectoryName(Path.GetFullPath(projfile.FileName))}
                | [ link ] | link :: _ -> 
                    { Include = includePath
                      Link = Some link.InnerText 
                      BaseDir = Path.GetDirectoryName(Path.GetFullPath(projfile.FileName))}
        
            this.ProjectsWithoutTemplates(this.GetProjects includeReferencedProjects)
            |> Seq.collect (fun proj -> 
                                match proj with
                                | ProjectType.ProjectJson proj -> Seq.empty // TODO: try to detect compile items
                                | ProjectType.Project proj ->
                                    proj.Document
                                    |> getDescendants "Compile"
                                    |> Seq.collect (fun i -> (getCompileItem (proj, i) |> getItems)))


        /// Finds all project files
        static member FindAllProjects folder =
            let packagesPath = Path.Combine(folder,Constants.PackagesFolderName) |> normalizePath
            let paketPath = Path.Combine(folder,Constants.PaketFilesFolderName) |> normalizePath

            let findAllFiles (folder, pattern) = 
                let rec search (di:DirectoryInfo) = 
                    try
                        let files = di.GetFiles(pattern, SearchOption.TopDirectoryOnly)
                        di.GetDirectories()
                        |> Array.filter (fun di ->
                            try 
                                let path = di.FullName |> normalizePath
                                if path = packagesPath then false else
                                if path = paketPath then false else
                                Path.Combine(path, Constants.DependenciesFileName) 
                                |> File.Exists 
                                |> not 
                            with 
                            | _ -> false)
                        |> Array.collect search
                        |> Array.append files
                    with
                    | _ -> Array.empty

                search <| DirectoryInfo folder

            findAllFiles(folder, "*proj*")
            |> Array.choose (fun f -> 
                if f.Extension = ".csproj" || f.Extension = ".fsproj" || f.Extension = ".vbproj" || f.Extension = ".wixproj" || f.Extension = ".nproj" || f.Extension = ".vcxproj" then
                    ProjectFile.tryLoad f.FullName |> Option.map (fun p -> ProjectType.Project p)
                else if f.Name = "project.json" then
                    Some(ProjectType.ProjectJson(ProjectJsonFile.Load f.FullName))
                else None)

        static member TryFindProject(projects,projectName) =
            let isMatching p =
                match p with
                | ProjectType.Project p -> p.NameWithoutExtension = projectName || p.Name = projectName
                | _ -> false

            match projects |> Seq.tryFind isMatching with
            | Some p -> Some p
            | None ->
                try
                    let fi = FileInfo (normalizePath (projectName.Trim().Trim([|'\"'|]))) // check if we can detect the path
                    let rec checkDir (dir:DirectoryInfo) = 
                        match projects |> Seq.tryFind (fun p -> 
                            String.equalsIgnoreCase ((FileInfo p.FileName).Directory.ToString()) (dir.ToString())) with
                        | Some p -> Some p
                        | None ->
                            if isNull dir.Parent then None else
                            checkDir dir.Parent
                    checkDir fi.Directory
                with
                | _ -> None
