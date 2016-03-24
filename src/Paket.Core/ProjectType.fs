namespace Paket

open Paket.ProjectJson
open System.IO
open System.Xml
open Xml

[<RequireQualifiedAccess>]
type ProjectTypeReference =
| Project of ProjectReference
| ProjectJson of ProjectJsonReference

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

        member this.Save(forceTouch) =
            match this with
            | Project p -> p.Save(forceTouch)
            | ProjectJson p -> p.Save(forceTouch)

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
            | ProjectType.Project project -> project.GetInterProjectDependencies() |> List.map (fun p -> ProjectTypeReference.Project p)
            | ProjectType.ProjectJson project -> project.GetGlobalInterProjectDependencies() |> List.map (fun p -> ProjectTypeReference.ProjectJson p)

        member this.GetAllReferencedProjects() = 
            let rec getProjects (project:ProjectType) = 
                seq {
                    let projects = 
                        project.GetInterProjectDependencies() 
                            |> Seq.map (fun proj -> 
                                match proj with
                                | ProjectTypeReference.Project proj -> ProjectType.Project(ProjectFile.tryLoad(proj.Path).Value)
                                | ProjectTypeReference.ProjectJson p-> 
                                    let directory = FileInfo(this.FileName).Directory.Parent
                                    let p = Path.Combine(directory.FullName,p.Name.ToString(),"project.json")
                                    ProjectType.ProjectJson(ProjectJsonFile.Load p))

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
            let getCompileRefs projectFile =
                projectFile.Document
                |> getDescendants "Compile"
                |> Seq.map (fun compileNode -> projectFile, compileNode)

            let getCompileItem (projectFile, compileNode) =
                let projectFolder = projectFile.FileName |> Path.GetFullPath |> Path.GetDirectoryName
                let sourceFile =
                    compileNode
                    |> getAttribute "Include"
                    |> fun attr -> attr.Value
                    |> normalizePath
                    |> fun relPath -> Path.Combine(projectFolder, relPath)
                let destPath =
                    compileNode
                    |> getDescendants "Link"
                    |> function
                        | [] -> createRelativePath (projectFolder + string Path.DirectorySeparatorChar) sourceFile
                        | linkNode :: _ -> linkNode.InnerText
                    |> normalizePath
                    |> Path.GetDirectoryName
                {
                    SourceFile = sourceFile
                    DestinationPath = destPath
                    BaseDir = projectFolder
                }

            let getRealItems compileItem =
                let sourceFolder = Path.GetDirectoryName(compileItem.SourceFile)
                let filespec = Path.GetFileName(compileItem.SourceFile)
                Directory.GetFiles(sourceFolder, filespec)
                |> Seq.map (fun realFile ->
                {
                    SourceFile = realFile
                    DestinationPath = compileItem.DestinationPath.Replace("%(FileName)", Path.GetFileName(realFile))
                    BaseDir = compileItem.BaseDir
                })

            this.GetProjects includeReferencedProjects
            |> this.ProjectsWithoutTemplates
            |> Seq.collect (fun x -> match x with | ProjectType.Project p -> getCompileRefs p | _ -> Seq.empty)
            |> Seq.map getCompileItem
            |> Seq.collect getRealItems

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
