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
            try
                InformationalVersion(SemVer.Parse x)
            with 
            | _ -> Ignore
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

let getTitle attributes = 
    attributes |> Seq.tryPick (function 
                      | Title t -> Some t
                      | _ -> None) 

let getDescription attributes = 
    attributes |> Seq.tryPick (function 
                      | Description d -> Some d
                      | _ -> None) 

let loadAssemblyId buildConfig buildPlatform (projectFile : ProjectFile) = 
    let fileName = 
        Path.Combine
            (Path.GetDirectoryName projectFile.FileName, projectFile.GetOutputDirectory buildConfig buildPlatform, 
             projectFile.GetAssemblyName()) |> normalizePath

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
        failwith "You should only try and add dependencies to template files with complete metadata."

let toFile config platform (p : ProjectFile) = 
    Path.Combine(Path.GetDirectoryName p.FileName, p.GetOutputDirectory config platform, p.GetAssemblyName())

let addFile (source : string) (target : string) (templateFile : TemplateFile) = 
    match templateFile with
    | CompleteTemplate(core, opt) -> 
        { FileName = templateFile.FileName
          Contents = CompleteInfo(core, { opt with Files = (source,target) :: opt.Files }) }
    | IncompleteTemplate -> 
        failwith "You should only try and add files to template files with complete metadata."

let findDependencies (dependencies : DependenciesFile) config platform (template : TemplateFile) (project : ProjectFile) lockDependencies (map : Map<string, TemplateFile * ProjectFile>) =
    let targetDir = 
        match project.OutputType with
        | ProjectOutputType.Exe -> "tools/"
        | ProjectOutputType.Library -> sprintf "lib/%O/" (project.GetTargetProfile())
    
    let projectDir = Path.GetDirectoryName project.FileName
    
    let deps, files = 
        project.GetInterProjectDependencies() 
        |> Seq.fold (fun (deps, files) p -> 
            match Map.tryFind p.Path map with
            | Some packagedRef -> packagedRef :: deps, files
            | None -> 
                let p = 
                    match ProjectFile.TryLoad(Path.Combine(projectDir, p.RelativePath) |> normalizePath) with
                    | Some p -> p
                    | _ -> failwithf "Missing project reference in proj file %s" p.RelativePath
                    
                deps, p :: files) ([], [])
    
    // Add the assembly + pdb + dll from this project
    let templateWithOutput =
        let assemblyFileName = toFile config platform project
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
                    PackageName core.Id, VersionRequirement(versionConstraint, PreReleaseStatus.No)
                | None ->failwithf "There was no version given for %s." templateFile.FileName
            | IncompleteTemplate -> failwithf "You cannot create a dependency on a template file (%s) with incomplete metadata." templateFile.FileName)
        |> List.fold addDependency templateWithOutput
    
    // If project refs will not be packaged, add the assembly to the package
    let withDepsAndIncluded = 
        files
        |> List.fold (fun templatefile file -> addFile (toFile config platform file) targetDir templatefile) withDeps

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
        r.Groups
        |> Seq.map (fun kv -> kv.Value.NugetPackages |> List.map (fun p -> kv.Key,p))
        |> List.concat
        |> List.filter (fun (groupName,np) ->
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
        |> List.map (fun (groupName,np) ->
                let dependencyVersionRequirement =
                    if not lockDependencies then
                        match dependencies.Groups |> Map.tryFind groupName with
                        | None -> None
                        | Some group ->
                            let deps = 
                                group.Packages 
                                |> Seq.map (fun p -> p.Name, p.VersionRequirement)
                                |> Map.ofSeq
                            Map.tryFind np.Name deps
                            |> function
                                | Some direct -> Some direct
                                | None ->
                                    match lockFile.Groups |> Map.tryFind groupName with
                                    | None -> None
                                    | Some group ->
                                        // If it's a transient dependency, try to
                                        // find it in `paket.lock` and set min version
                                        // to current locked version
                                        group.Resolution
                                        |> Map.tryFind np.Name
                                        |> Option.map (fun transient -> VersionRequirement(Minimum transient.Version, PreReleaseStatus.No))
                        else
                            match lockFile.Groups |> Map.tryFind groupName with
                            | None -> None
                            | Some group ->
                                Map.tryFind np.Name group.Resolution
                                |> Option.map (fun resolvedPackage -> resolvedPackage.Version)
                                |> Option.map (fun version -> VersionRequirement(Specific version, PreReleaseStatus.No))
                let dep =
                    match dependencyVersionRequirement with
                    | Some installed -> installed
                    | None -> failwithf "No package with id '%A' installed in group %O." np.Name groupName
                np.Name, dep)
        |> List.fold addDependency withDepsAndIncluded
    | None -> withDepsAndIncluded
