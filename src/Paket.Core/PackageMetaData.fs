module internal Paket.PackageMetaData

open Paket
open System
open System.IO
open System.Reflection
open Paket.Domain
open Paket.Logging
open System.Collections.Generic
open Paket.Requirements
open Paket.Xml

let (|CompleteTemplate|IncompleteTemplate|) templateFile = 
    match templateFile with
    | { Contents = (CompleteInfo(core, optional)) } -> CompleteTemplate(core, optional)
    | _ -> IncompleteTemplate

let (|Title|Description|Version|InformationalVersion|Company|Ignore|) (attributeName:string,attributeValue:string) = 
    try
        match attributeName with
        | "AssemblyCompanyAttribute" -> Company attributeValue
        | "AssemblyDescriptionAttribute" -> Description attributeValue
        | "AssemblyTitleAttribute" -> Title attributeValue
        | "AssemblyVersionAttribute" -> Version(attributeValue |> SemVer.Parse)
        | "AssemblyInformationalVersionAttribute" -> InformationalVersion(attributeValue|> SemVer.Parse)
        | _ -> Ignore
    with
    | _ -> Ignore

let getId (assembly : Assembly) (md : ProjectCoreInfo) = { md with Id = Some(assembly.GetName().Name) }

let getVersion versionFromAssembly attributes = 
    let informational = 
        attributes 
        |> Seq.tryPick (function InformationalVersion v -> Some v | _ -> None)
    match informational with
    | Some v -> informational
    | None -> 
        let fromAssembly = 
            match versionFromAssembly with
            | None -> None
            | Some v -> Some(SemVer.Parse(v.ToString()))
        match fromAssembly with
        | Some v -> fromAssembly
        | None -> 
            attributes 
            |> Seq.tryPick (function Version v -> Some v | _ -> None)

let getAuthors attributes = 
    attributes
    |> Seq.tryPick (function Company a -> Some a | _ -> None)
    |> Option.map (fun a -> 
            a.Split(',')
            |> Array.map (fun s -> s.Trim())
            |> List.ofArray)

let getTitle attributes = 
    attributes 
    |> Seq.tryPick (function Title t -> Some t | _ -> None) 

let getDescription attributes = 
    attributes 
    |> Seq.tryPick (function Description d -> Some d | _ -> None) 

let readAssembly fileName =
    traceVerbose <| sprintf "Loading assembly metadata for %s" fileName
    let assemblyReader = 
        ProviderImplementation.AssemblyReader.ILModuleReaderAfterReadingAllBytes(
            fileName, 
            ProviderImplementation.AssemblyReader.mkILGlobals ProviderImplementation.AssemblyReader.ecmaMscorlibScopeRef, 
            true)
   
    let versionFromAssembly = assemblyReader.ILModuleDef.ManifestOfAssembly.Version
    let id = assemblyReader.ILModuleDef.ManifestOfAssembly.Name
    assemblyReader,id,versionFromAssembly,fileName


let readAssemblyFromProjFile buildConfig buildPlatform (projectFile : ProjectFile) = 
    FileInfo(
        Path.Combine
            (Path.GetDirectoryName projectFile.FileName, projectFile.GetOutputDirectory buildConfig buildPlatform, 
                projectFile.GetAssemblyName()) 
        |> normalizePath).FullName
    |> readAssembly

let loadAssemblyAttributes (assemblyReader:ProviderImplementation.AssemblyReader.CacheValue) = 
    [for inp in assemblyReader.ILModuleDef.ManifestOfAssembly.CustomAttrs.Elements do 
         match ProviderImplementation.AssemblyReader.decodeILCustomAttribData assemblyReader.ILGlobals inp with
         | [] -> ()
         | args -> yield (inp.Method.EnclosingType.BasicQualifiedName, Seq.head [ for (_,arg) in args -> if isNull arg then "" else arg.ToString()]) ]


let (|Valid|Invalid|) md = 
    match md with
    | { ProjectCoreInfo.Id = Some id'; Version = Some v; Authors = Some a; Description = Some d; Symbols = s } -> 
        Valid { CompleteCoreInfo.Id = id'
                Version = Some v
                Authors = a
                Description = d
                Symbols = s }
    | _ -> Invalid

let addDependency (templateFile : TemplateFile) (dependency : PackageName * VersionRequirement) = 
    match templateFile with
    | CompleteTemplate(core, opt) -> 
        let newDeps = 
            match opt.Dependencies |> List.tryFind (fun (n,_) -> n = fst dependency) with
            | None -> dependency :: opt.Dependencies
            | _ -> opt.Dependencies
        { FileName = templateFile.FileName
          Contents = CompleteInfo(core, { opt with Dependencies = newDeps }) }
    | IncompleteTemplate -> 
        failwith "You should only try to add dependencies to template files with complete metadata."

let toFile config platform (p : ProjectFile) = 
    Path.Combine(Path.GetDirectoryName p.FileName, p.GetOutputDirectory config platform, p.GetAssemblyName())

let addFile (source : string) (target : string) (templateFile : TemplateFile) = 
    match templateFile with
    | CompleteTemplate(core, opt) -> 
        { FileName = templateFile.FileName
          Contents = CompleteInfo(core, { opt with Files = (source,target) :: opt.Files }) }
    | IncompleteTemplate -> 
        failwith "You should only try and add files to template files with complete metadata."

let findDependencies (dependencies : DependenciesFile) config platform (template : TemplateFile) (project : ProjectFile) lockDependencies minimumFromLockFile (map : Map<string, TemplateFile * ProjectFile>) includeReferencedProjects (version :SemVerInfo option) specificVersions =
    let targetDir = 
        match project.OutputType with
        | ProjectOutputType.Exe -> "tools/"
        | ProjectOutputType.Library -> sprintf "lib/%O/" (project.GetTargetProfile())
    
    let projectDir = Path.GetDirectoryName project.FileName

    let getPreReleaseStatus (v:SemVerInfo) =
        match v.PreRelease with
        | None -> PreReleaseStatus.No
        | _ -> PreReleaseStatus.All
    
    let deps, files = 
        project.GetAllInterProjectDependenciesWithProjectTemplates |> Seq.toList
        |> List.filter (fun proj -> proj <> project)
        |> List.fold (fun (deps, files) p -> 
            match Map.tryFind p.FileName map with
            | Some packagedRef -> packagedRef :: deps, files
            | None -> 
                let p = 
                    match ProjectFile.TryLoad p.FileName with
                    | Some p -> p
                    | _ -> failwithf "Missing project reference in proj file %s" p.FileName
                    
                deps, p :: files) ([], [])
    
    // Add the assembly + pdb + dll from this project
    let templateWithOutput =
        let additionalFiles = 
            let assemblyNames = 
                if includeReferencedProjects then project.GetAllInterProjectDependenciesWithoutProjectTemplates |> Seq.toList else [ project ]
                |> List.map (fun proj -> proj.GetAssemblyName())
            
            assemblyNames
            |> Seq.collect (fun assemblyFileName -> 
                                let assemblyfi = FileInfo(assemblyFileName)
                                let name = Path.GetFileNameWithoutExtension assemblyfi.Name

                                let projectdir = Path.GetDirectoryName(Path.GetFullPath(project.FileName))
                                let path = Path.Combine(projectDir, project.GetOutputDirectory config platform)
                                let files = Directory.GetFiles(path, name + ".*")
                                files 
                                |> Array.map (fun f -> FileInfo f)
                                |> Array.filter (fun fi -> 
                                                    let isSameFileName = (Path.GetFileNameWithoutExtension fi.Name) = name
                                                    let validExtensions = match template.Contents with
                                                                          | CompleteInfo(core, optional) ->
                                                                              if core.Symbols || optional.IncludePdbs then [".xml"; ".dll"; ".exe"; ".pdb"; ".mdb"]
                                                                              else [".xml"; ".dll"; ".exe";]
                                                                          | ProjectInfo(core, optional) ->
                                                                              if core.Symbols  || optional.IncludePdbs then [".xml"; ".dll"; ".exe"; ".pdb"; ".mdb"]
                                                                              else [".xml"; ".dll"; ".exe";]
                                                    let isValidExtension = 
                                                        validExtensions
                                                        |> List.exists (String.equalsIgnoreCase fi.Extension)
                                                    isSameFileName && isValidExtension)
                           )
            |> Seq.toArray

        additionalFiles
        |> Array.fold (fun template file -> addFile file.FullName targetDir template) template
    
    // If project refs will also be packaged, add dependency
    let withDeps = 
        deps
        |> List.map (fun (templateFile, _) -> 
               match templateFile with
               | CompleteTemplate(core, opt) -> 
                   match core.Version with
                   | Some v -> 
                       let versionConstraint = if not lockDependencies then Minimum v else Specific v
                       PackageName core.Id, VersionRequirement(versionConstraint, getPreReleaseStatus v)
                   | None -> failwithf "There was no version given for %s." templateFile.FileName
               | IncompleteTemplate -> 
                   failwithf "You cannot create a dependency on a template file (%s) with incomplete metadata." templateFile.FileName)
        |> List.fold addDependency templateWithOutput
    
    // If project refs will not be packaged, add the assembly to the package
    let withDepsAndIncluded = 
        files
        |> List.fold (fun templatefile file -> addFile (toFile config platform file) targetDir templatefile) withDeps

    let lockFile = 
        dependencies.FindLockfile().FullName
        |> LockFile.LoadFrom

    let allReferences = 
        let getPackages proj = 
            match ProjectFile.FindReferencesFile (FileInfo proj.FileName) with
            | Some f -> 
                let refFile = ReferencesFile.FromFile f
                refFile.Groups
                |> Seq.map (fun kv -> kv.Value.NugetPackages |> List.map (fun p -> Some kv.Key, p))
                |> List.concat
            | None -> []
          
        [if includeReferencedProjects then
            for proj in project.GetRecursiveInterProjectDependencies |> Seq.filter ((<>) project) do
                let projFileInfo = FileInfo proj.FileName
                match ProjectFile.FindTemplatesFile projFileInfo with
                | Some templateFileName when TemplateFile.IsProjectType templateFileName ->
                    match TemplateFile.Load(templateFileName, lockFile, None, Seq.empty |> Map.ofSeq).Contents with
                    | CompleteInfo(core, optional) ->
                        yield! getPackages proj
                    | ProjectInfo(core, optional) ->
                        let name = 
                            match core.Id with
                            | Some name -> name
                            | None -> proj.GetAssemblyName().Replace(".dll","").Replace(".exe","")
                            
                        yield None, { Name = PackageName name; Settings = InstallSettings.Default }
                | _ -> yield! getPackages proj

         yield! getPackages project]
    
    match allReferences with
    | [] -> withDepsAndIncluded
    | _ -> 
        let deps =
            allReferences
            |> List.filter (fun (group, np) ->
                match group with
                | None ->  true
                | Some groupName ->
                    try
                        // TODO: it would be nice if this data would be in the NuGet OData feed,
                        // then we would not need to parse every nuspec here
                        let info =
                            lockFile.Groups.[groupName].Resolution
                            |> Map.tryFind np.Name
                        match info with
                        | None -> true
                        | Some rp ->
                            let nuspec = Nuspec.Load(dependencies.RootPath,groupName,rp.Version,defaultArg rp.Settings.IncludeVersionInPath false,np.Name)
                            not nuspec.IsDevelopmentDependency
                    with
                    | _ -> true)
            |> List.map (fun (group, np) ->
                match group with
                | None ->
                    match version with
                    | Some v -> 
                        tracefn "HAA => %s | %s" (np.Name.ToString()) (v.ToString())
                        np.Name,VersionRequirement.Parse (v.ToString())
                    | None -> 
                        trace "It really was null"
                        let lockedVersion() = 
                            let findLockedGroup() =
                                lockFile.GetDependencyLookupTable()
                                |> Seq.map (fun m -> m.Key)
                                |> Seq.filter (fun (g, p) -> p = np.Name)
                                |> Seq.map (fun (g, p) -> g)
                                |> Seq.head
                                |> lockFile.GetGroup

                            let group = findLockedGroup()
                            Map.tryFind np.Name group.Resolution
                            |> Option.map (fun resolvedPackage -> resolvedPackage.Version)
                            |> Option.map (fun version -> VersionRequirement(GreaterThan version, getPreReleaseStatus version))
                            |> Option.get
                        np.Name,lockedVersion()

                | Some groupName ->
                    let dependencyVersionRequirement =
                        if not lockDependencies then
                            match dependencies.Groups |> Map.tryFind groupName with
                            | None -> 
                                None
                            | Some group ->
                                let deps = 
                                    group.Packages 
                                    |> Seq.map (fun p -> p.Name, p.VersionRequirement)
                                    |> Map.ofSeq

                                Map.tryFind np.Name deps
                                |> function
                                    | Some direct -> 
                                        let versionRequirement = Map.tryFind np.Name (lockFile.GetGroup(group.Name).Resolution)
                                                                 |> Option.map (fun resolvedPackage -> resolvedPackage.Version)
                                                                 |> Option.map (fun version -> VersionRequirement(Minimum version, getPreReleaseStatus version))
                                        match versionRequirement, minimumFromLockFile with
                                        | None, _ -> Some direct
                                        | Some x, false -> Some direct
                                        | Some x, true -> Some x
                                    | None ->
                                        match lockFile.Groups |> Map.tryFind groupName with
                                        | None -> None
                                        | Some group ->
                                            // If it's a transient dependency, try to
                                            // find it in `paket.lock` and set min version
                                            // to current locked version
                                            group.Resolution
                                            |> Map.tryFind np.Name
                                            |> Option.map (fun transient -> VersionRequirement(Minimum transient.Version, getPreReleaseStatus transient.Version))
                            else
                                match lockFile.Groups |> Map.tryFind groupName with
                                | None -> None
                                | Some group ->
                                    Map.tryFind np.Name group.Resolution
                                    |> Option.map (fun resolvedPackage -> resolvedPackage.Version)
                                    |> Option.map (fun version -> VersionRequirement(Specific version, getPreReleaseStatus version))
                    let dep =
                        match dependencyVersionRequirement with
                        | Some installed -> installed
                        | None -> failwithf "No package with id '%A' installed in group %O." np.Name groupName
                    np.Name, dep)
        deps |> List.fold addDependency withDepsAndIncluded
