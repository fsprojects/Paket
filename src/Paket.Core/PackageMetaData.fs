module internal Paket.PackageMetaData

open Paket
open System
open System.IO
open System.Reflection
open Paket.Domain
open Paket.Logging
open System.Collections.Generic

let (|CompleteTemplate|IncompleteTemplate|) templateFile = 
    match templateFile with
    | { Contents = (CompleteInfo(core, optional)) } -> CompleteTemplate(core, optional)
    | _ -> IncompleteTemplate

let (|Title|Description|Version|InformationalVersion|Company|Ignore|) (attribute : obj) = 
    match attribute with
    | :? AssemblyTitleAttribute as title ->
        match title.Title with
        | x when String.IsNullOrWhiteSpace x ->
            Ignore
        | x ->
            Title x
    | :? AssemblyDescriptionAttribute as description ->
        match description.Description with
        | x when String.IsNullOrWhiteSpace x ->
            Ignore
        | x ->
            Description x
    | :? AssemblyVersionAttribute as version ->
        match version.Version with
        | x when String.IsNullOrWhiteSpace x ->
            Ignore
        | x -> Version(SemVer.Parse x)
    | :? AssemblyInformationalVersionAttribute as version -> 
        match version.InformationalVersion with
        | x when String.IsNullOrWhiteSpace x ->
            Ignore
        | x ->
            InformationalVersion(SemVer.Parse x)
    | :? AssemblyCompanyAttribute as company ->
        match company.Company with
        | x when String.IsNullOrWhiteSpace x ->
            Ignore
        | x -> Company x
    | _ -> Ignore

let getId (assembly : Assembly) (md : ProjectCoreInfo) = { md with Id = Some(assembly.GetName().Name) }

let getVersion (assembly : Assembly) attributes = 
    let informational = 
        attributes |> Seq.tryPick (function 
                            | InformationalVersion v -> Some v
                            | _ -> None)
    match informational with
    | Some v -> informational
    | None -> 
        let fromAssembly = 
            match assembly.GetName().Version with
            | null -> None
            | v -> Some(SemVer.Parse(v.ToString()))
        match fromAssembly with
        | Some v -> fromAssembly
        | None -> 
            attributes |> Seq.tryPick (function 
                                | Version v -> Some v
                                | _ -> None)

let getAuthors attributes = 
    attributes
    |> Seq.tryPick (function 
            | Company a -> Some a
            | _ -> None)
    |> Option.map (fun a -> 
            a.Split(',')
            |> Array.map (fun s -> s.Trim())
            |> List.ofArray)

let getDescription attributes = 
    attributes |> Seq.tryPick (function 
                      | Description d -> Some d
                      | _ -> None) 

let buildAssemblyFileName (buildConfig : string, projectFile : ProjectFile, artifactsDirectory : string option) =
    match artifactsDirectory with
    | Some path ->
        Path.Combine
            (path, projectFile.GetAssemblyName()) |> normalizePath
    | _ ->
        Path.Combine
            (Path.GetDirectoryName projectFile.FileName, projectFile.GetOutputDirectory buildConfig, 
                projectFile.GetAssemblyName()) |> normalizePath

let loadAssemblyId buildConfig (projectFile : ProjectFile, artifactsDirectory : string option) =
    let fileName = buildAssemblyFileName(buildConfig, projectFile, artifactsDirectory)

    traceVerbose <| sprintf "Loading assembly metadata for %s" fileName
    let bytes = File.ReadAllBytes fileName
    let assembly = Assembly.Load bytes

    assembly,assembly.GetName().Name,fileName

let loadAssemblyAttributes fileName (assembly:Assembly) = 
    try
        assembly.GetCustomAttributes(true)
    with
    | :? FileNotFoundException -> 
        // retrieving via path
        let assembly = Assembly.LoadFrom fileName            
        assembly.GetCustomAttributes(true)
    | exn ->
        traceWarnfn "Loading custom attributes failed for %s.%sMessage: %s" fileName Environment.NewLine exn.Message
        assembly.GetCustomAttributes(false)

let (|Valid|Invalid|) md = 
    match md with
    | { ProjectCoreInfo.Id = Some id'; Version = Some v; Authors = Some a; Description = Some d } -> 
        Valid { CompleteCoreInfo.Id = id'
                Version = Some v
                Authors = a
                Description = d }
    | _ -> Invalid

let addDependency (templateFile : TemplateFile) (dependency : string * VersionRequirement) = 
    match templateFile with
    | CompleteTemplate(core, opt) -> 
        let newDeps = 
            match opt.Dependencies |> List.tryFind (fun (n,_) -> n = fst dependency) with
            | None -> dependency :: opt.Dependencies
            | _ -> opt.Dependencies
        { FileName = templateFile.FileName
          Contents = CompleteInfo(core, { opt with Dependencies = newDeps }) }
    | IncompleteTemplate -> 
        failwith "You should only try and add dependencies to template files with complete metadata."

let addFile (source : string) (target : string) (templateFile : TemplateFile) = 
    match templateFile with
    | CompleteTemplate(core, opt) -> 
        { FileName = templateFile.FileName
          Contents = CompleteInfo(core, { opt with Files = (source,target) :: opt.Files }) }
    | IncompleteTemplate -> 
        failwith "You should only try and add dependencies to template files with complete metadata."

let findDependencies (dependencies : DependenciesFile) config (template : TemplateFile) (project : ProjectFile) lockDependencies (map : Map<string, TemplateFile * ProjectFile>) (artifactsInputPath : string option)=
    let targetDir = 
        match project.OutputType with
        | ProjectOutputType.Exe -> "tools/"
        | ProjectOutputType.Library -> sprintf "lib/%s/" (project.GetTargetProfile().ToString())
    
    let projectDir = Path.GetDirectoryName project.FileName
    
    let deps, files = 
        project.GetInterProjectDependencies() 
        |> Seq.fold (fun (deps, files) p -> 
            match Map.tryFind p.Name map with
            | Some packagedRef -> packagedRef :: deps, files
            | None -> 
                let p = 
                    match ProjectFile.Load(Path.Combine(projectDir, p.Path)) with
                    | Some p -> p
                    | _ -> failwithf "Missing project reference in proj file %s" p.Path
                    
                deps, p :: files) ([], [])
    
    // Add the assembly + pdb + dll from this project
    let templateWithOutput =
        let assemblyFileName = buildAssemblyFileName(config, project, artifactsInputPath)
        let fi = FileInfo(assemblyFileName)
        let name = Path.GetFileNameWithoutExtension fi.Name

        let additionalFiles =
            fi.Directory.GetFiles(name + ".*")
            |> Array.filter (fun f -> 
                let isSameFileName = Path.GetFileNameWithoutExtension f.Name = name
                let isValidExtension = 
                    [".xml"; ".dll"; ".exe"; ".pdb"; ".mdb"] 
                    |> List.exists ((=) (f.Extension.ToLower()))

                isSameFileName && isValidExtension)        
        additionalFiles
        |> Array.fold (fun template file -> addFile file.FullName targetDir template) template
    
    // If project refs will also be packaged, add dependency
    let withDeps = 
        deps
        |> List.map (fun (templateFile,_) ->
            match templateFile with
            | CompleteTemplate(core, opt) -> 
                match core.Version with
                | Some v ->
                    let versionConstraint =
                        if not lockDependencies
                        then Minimum v
                        else Specific v
                    core.Id, VersionRequirement(versionConstraint, PreReleaseStatus.All)
                | none ->failwithf "There was no version given for %s." templateFile.FileName
            | IncompleteTemplate -> failwithf "You cannot create a dependency on a template file (%s) with incomplete metadata." templateFile.FileName)
        |> List.fold addDependency templateWithOutput
    
    // If project refs will not be packaged, add the assembly to the package
    let withDepsAndIncluded = 
        files
        |> List.fold (fun templatefile file -> addFile (buildAssemblyFileName(config, file, artifactsInputPath)) targetDir templatefile) withDeps

    let lockFile = 
        dependencies.FindLockfile().FullName
        |> LockFile.LoadFrom

    // Add any paket references
    let referenceFile = 
        FileInfo project.FileName
        |> ProjectFile.FindReferencesFile 
        |> Option.map (ReferencesFile.FromFile)

    match referenceFile with
    | Some r -> 
        r.NugetPackages
        |> List.filter (fun np ->
            try
                // TODO: it would be nice if this data would be in the NuGet OData feed,
                // then we would not need to parse every nuspec here
                let nuspec = Nuspec.Load(dependencies.RootPath,np.Name)
                not nuspec.IsDevelopmentDependency
            with
            | _ -> true)
        |> List.map (fun np ->
                let getDependencyVersionRequirement package =
                    if not lockDependencies then
                        Map.tryFind package dependencies.DirectDependencies
                        |> function
                            | Some direct -> Some direct
                            | None ->
                                // If it's a transient dependency, try to
                                // find it in `paket.lock` and set min version
                                // to current locked version
                                lockFile.ResolvedPackages
                                |> Map.tryFind (NormalizedPackageName package)
                                |> Option.map (fun transient -> transient.Version)
                                |> Option.map (fun v -> VersionRequirement(Minimum v, PreReleaseStatus.All))
                    else
                        Map.tryFind (NormalizedPackageName package) lockFile.ResolvedPackages
                        |> Option.map (fun resolvedPackage -> resolvedPackage.Version)
                        |> Option.map (fun version -> VersionRequirement(Specific version, PreReleaseStatus.All))
                let dep =
                    match getDependencyVersionRequirement np.Name with
                    | Some installed -> installed
                    | None -> failwithf "No package with id '%A' installed." np.Name
                np.Name.Id, dep)
        |> List.fold addDependency withDepsAndIncluded
    | None -> withDepsAndIncluded
