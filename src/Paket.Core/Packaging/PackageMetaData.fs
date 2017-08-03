module internal Paket.PackageMetaData

open Paket
open System
open System.IO
open Pri.LongPath
open System.Reflection
open Paket.Domain
open Paket.Logging
open System.Collections.Generic
open Paket.Requirements
open Paket.Xml
open InstallProcess

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
    |> Seq.tryPick (function Company a when notNullOrEmpty a -> Some a | _ -> None)
    |> Option.map (fun a ->
            a.Split(',')
            |> Array.map (fun s -> s.Trim())
            |> List.ofArray)

let getTitle attributes = 
    attributes 
    |> Seq.tryPick (function Title t -> Some t | _ -> None) 

let getDescription attributes = 
    attributes 
    |> Seq.tryPick (function Description d when notNullOrEmpty d -> Some d | _ -> None)

let readAssembly fileName =
    traceVerbose <| sprintf "Loading assembly metadata for %s" fileName
    let assemblyReader = 
        ProviderImplementation.AssemblyReader.ILModuleReaderAfterReadingAllBytes(
            fileName, 
            ProviderImplementation.AssemblyReader.mkILGlobals ProviderImplementation.AssemblyReader.EcmaMscorlibScopeRef)
   
    let versionFromAssembly = assemblyReader.ILModuleDef.ManifestOfAssembly.Version
    let id = assemblyReader.ILModuleDef.ManifestOfAssembly.Name
    assemblyReader,id,versionFromAssembly,fileName


let readAssemblyFromProjFile buildConfig buildPlatform (projectFile : ProjectFile) = 
    let root = Path.GetDirectoryName projectFile.FileName
    let subFolder = projectFile.GetOutputDirectory buildConfig buildPlatform
    let assemblyName = projectFile.GetAssemblyName()
    FileInfo(Path.Combine(root, subFolder, assemblyName) |> normalizePath).FullName
    |> readAssembly

let loadAssemblyAttributes (assemblyReader:ProviderImplementation.AssemblyReader.ILModuleReader) = 
    let getMetaData inp = 
        try
            ProviderImplementation.AssemblyReader.decodeILCustomAttribData assemblyReader.ILGlobals inp
        with
        | _ -> []

    [for inp in assemblyReader.ILModuleDef.ManifestOfAssembly.CustomAttrs.Elements do
        match getMetaData inp with
        | [] -> ()
        | args -> 
            let all = args |> Seq.map (fun (_,arg) -> if isNull arg then "" else arg.ToString())
            yield (inp.Method.EnclosingType.BasicQualifiedName, Seq.head all)]


let (|Valid|Invalid|) md = 
    match md with
    | { ProjectCoreInfo.Id = Some id'; Version = Some v; Authors = Some a; Description = Some d; Symbols = s } -> 
        Valid { CompleteCoreInfo.Id = id'
                Version = Some v
                Authors = a
                Description = d
                Symbols = s }
    | _ -> Invalid

let addDependencyToFrameworkGroup framework dependencyGroups dependency =
    dependencyGroups
    |> List.tryFind (fun g -> g.Framework = framework)
    |> function
    | None -> { Dependencies = [dependency]; Framework = framework } :: dependencyGroups
    | _ ->
        dependencyGroups
        |> List.map (fun g ->
            if g.Framework <> framework then g
            else { g with Dependencies = dependency :: g.Dependencies })

let addDependency (templateFile : TemplateFile) (dependency : PackageName * VersionRequirement) =
    match templateFile with
    | CompleteTemplate(core, opt) -> 
        let packageName = dependency |> (fun (n,_) -> n)
        let newDeps =
            opt.DependencyGroups
            |> List.tryFind (fun g ->
                g.Dependencies
                |> List.tryFind (fun (n, _) -> n = packageName)
                |> Option.isSome)
            |> function
            | Some _ -> opt.DependencyGroups
            | None -> dependency |> addDependencyToFrameworkGroup None opt.DependencyGroups


        { FileName = templateFile.FileName
          Contents = CompleteInfo(core, { opt with DependencyGroups = newDeps }) }
    | IncompleteTemplate -> 
        failwith (sprintf "You should only try to add dependencies to template files with complete metadata.%sFile: %s" Environment.NewLine templateFile.FileName)

let excludeDependency (templateFile : TemplateFile) (exclude : PackageName) = 
    match templateFile with
    | CompleteTemplate(core, opt) -> 
        let newExcludes = 
            opt.ExcludedDependencies |> Set.add exclude
        { FileName = templateFile.FileName
          Contents = CompleteInfo(core, { opt with ExcludedDependencies = newExcludes }) }
    | IncompleteTemplate -> 
        failwith (sprintf "You should only try to exclude dependencies to template files with complete metadata.%sFile: %s" Environment.NewLine templateFile.FileName)

let toFile config platform (p : ProjectFile) = 
    Path.Combine(Path.GetDirectoryName p.FileName, p.GetOutputDirectory config platform, p.GetAssemblyName())

let addFile (source : string) (target : string) (templateFile : TemplateFile) = 
    match templateFile with
    | CompleteTemplate(core, opt) -> 
        { FileName = templateFile.FileName
          Contents = CompleteInfo(core, { opt with Files = (source,target) :: opt.Files }) }
    | IncompleteTemplate -> 
        failwith (sprintf "You should only try and add files to template files with complete metadata.%sFile: %s" Environment.NewLine templateFile.FileName)

let findDependencies (dependenciesFile : DependenciesFile) config platform (template : TemplateFile) (project : ProjectFile) lockDependencies minimumFromLockFile pinProjectReferences (map : Map<string, TemplateFile * ProjectFile>) includeReferencedProjects (version :SemVerInfo option) specificVersions (projDeps) =
    let includeReferencedProjects = template.IncludeReferencedProjects || includeReferencedProjects
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
        let interProjectDeps = 
            if includeReferencedProjects then 
                project.GetAllInterProjectDependenciesWithoutProjectTemplates projDeps
            else 
                project.GetAllInterProjectDependenciesWithProjectTemplates projDeps
            |> Seq.toList

        interProjectDeps
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
    
    // Add the assembly + {.dll, .pdb, .xml, /*/.resources.dll} from this project
    let templateWithOutput =
        let projects =                 
            if includeReferencedProjects then 
                project.GetAllInterProjectDependenciesWithoutProjectTemplates projDeps 
                |> Seq.toList 
            else 
                [ project ]

        let satelliteDlls =
            seq {
                for project in projects do
                    let satelliteAssemblyName = Path.GetFileNameWithoutExtension(project.GetAssemblyName()) + ".resources.dll"
                    let projectDir = Path.GetDirectoryName(Path.GetFullPath(project.FileName))
                    let outputDir = Path.Combine(projectDir, project.GetOutputDirectory config platform)
                    for language in project.FindLocalizedLanguageNames() do
                        let fileName = Path.Combine(outputDir, language, satelliteAssemblyName)
                        if File.Exists fileName then
                            let satelliteTargetDir = Path.Combine(targetDir, language)
                            yield (FileInfo fileName, satelliteTargetDir)
                        else
                            traceWarnfn "Did not find satellite assembly for (%s) try building and running pack again." language 
            }

        let template =
            satelliteDlls 
            |> Seq.fold (fun template (dllFile, targetDir) -> addFile dllFile.FullName targetDir template) template

        let assemblyNames = 
            projects
            |> List.map (fun proj -> proj.GetAssemblyName())

        let additionalFiles = 
            assemblyNames
            |> Seq.collect (fun assemblyFileName -> 
                let assemblyfi = FileInfo(assemblyFileName)
                let name = Path.GetFileNameWithoutExtension assemblyfi.Name

                let path = Path.Combine(projectDir, project.GetOutputDirectory config platform)

                Directory.GetFiles(path, name + ".*")
                |> Array.map (fun f -> FileInfo f)
                |> Array.filter (fun fi -> 
                                    let isSameFileName = (Path.GetFileNameWithoutExtension fi.Name) = name
                                    let validExtensions = 
                                        match template.Contents with
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

    let templateWithOutputAndExcludes =
        match template.Contents with
        | CompleteInfo(_, optional) -> optional.ExcludedGroups
        | ProjectInfo(_, optional) -> optional.ExcludedGroups
        |> Seq.collect dependenciesFile.GetDependenciesInGroup
        |> Seq.fold (fun templatefile package -> excludeDependency templatefile package.Key) templateWithOutput
    
    // If project refs will also be packaged, add dependency
    let withDeps = 
        deps
        |> List.map (fun (templateFile, _) -> 
               match templateFile with
               | CompleteTemplate(core, _) -> 
                   match core.Version with
                   | Some v -> 
                       let versionConstraint = if lockDependencies || pinProjectReferences then Specific v else Minimum v
                       PackageName core.Id, VersionRequirement(versionConstraint, getPreReleaseStatus v)
                   | None -> failwithf "There was no version given for %s." templateFile.FileName
               | IncompleteTemplate -> 
                   failwithf "You cannot create a dependency on a template file (%s) with incomplete metadata." templateFile.FileName)
        |> List.fold addDependency templateWithOutputAndExcludes
    
    // If project refs will not be packaged, add the assembly to the package
    let withDepsAndIncluded = 
        files
        |> List.fold (fun templatefile file -> addFile (toFile config platform file) targetDir templatefile) withDeps

    let lockFile = 
        dependenciesFile.FindLockfile().FullName
        |> LockFile.LoadFrom

    let allReferences = 
        let getPackages (proj:ProjectFile) = 
            match proj.FindReferencesFile () with
            | Some f -> 
                let refFile = ReferencesFile.FromFile f
                refFile.Groups
                |> Seq.map (fun kv -> kv.Value.NugetPackages |> List.map (fun p -> Some kv.Key, p, None))
                |> List.concat
            | None -> []
          
        [if includeReferencedProjects then
            for proj in project.GetAllReferencedProjects(false,projDeps) |> Seq.filter ((<>) project) do
                match proj.FindTemplatesFile() with
                | Some templateFileName when TemplateFile.IsProjectType templateFileName ->
                    match TemplateFile.Load(templateFileName, lockFile, None, Seq.empty |> Map.ofSeq).Contents with
                    | CompleteInfo(_) ->
                        yield! getPackages proj
                    | ProjectInfo(core, _) ->
                        let name = 
                            match core.Id with
                            | Some name -> name
                            | None -> proj.GetAssemblyName().Replace(".dll","").Replace(".exe","")

                        let versionConstraint = 
                            match core.Version with
                            | Some v -> 
                                let vr = if lockDependencies || pinProjectReferences then Specific v else Minimum v
                                VersionRequirement(vr, getPreReleaseStatus v)
                            | None -> VersionRequirement.AllReleases

                        yield None, { Name = PackageName name; Settings = InstallSettings.Default }, Some versionConstraint
                | _ -> yield! getPackages proj

         yield! getPackages project]
    
    // filter out any references that are transitive
    let distinctRefs = allReferences |> List.distinct
    let refs = 
        distinctRefs
        |> List.filter (fun (group, settings: Paket.PackageInstallSettings, _) ->
            let isDependencyOfAnyOtherDependency packageName =
                distinctRefs 
                |> List.exists (fun (group, settings2,_) -> 
                    settings2.Name <> packageName && 
                        match group with
                        | Some groupName ->
                            match lockFile.GetAllDependenciesOfSafe(groupName, settings2.Name) with
                            | Some packages -> packages.Contains packageName
                            | _ -> false
                        | None -> false)

            match group with
            | None -> true
            | Some groupName -> 
                match dependenciesFile.Groups |> Map.tryFind groupName with
                | None -> true
                | Some group ->
                    group.Packages |> List.exists (fun p -> p.Name = settings.Name) ||
                        isDependencyOfAnyOtherDependency settings.Name |> not)
        |> List.sortByDescending (fun (_, settings,_) -> settings.Name)
    
    match refs with
    | [] -> withDepsAndIncluded
    | _ -> 
        let deps =
            refs
            |> List.filter (fun (group, np, _) ->
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
                            let nuspec = Nuspec.Load(dependenciesFile.RootPath,groupName,rp.Version,defaultArg rp.Settings.IncludeVersionInPath false,np.Name)
                            not nuspec.IsDevelopmentDependency
                    with
                    | _ -> true)
            |> List.map (fun (group, np, specificVersionRequirement) ->
                let specificVersionRequirement = defaultArg specificVersionRequirement VersionRequirement.AllReleases
                match group with
                | None ->
                    match version with
                    | Some v -> 
                        np.Name,VersionRequirement.Parse (v.ToString())
                    | None -> 
                        if minimumFromLockFile then
                            let groupName =
                                lockFile.GetDependencyLookupTable()
                                |> Seq.filter (fun m -> snd m.Key = np.Name)
                                |> Seq.map (fun m -> m.Key)
                                |> Seq.tryHead
                                |> Option.map fst

                            match groupName with
                            | None -> np.Name,specificVersionRequirement
                            | Some groupName -> 
                                let group = lockFile.GetGroup groupName

                                let lockedVersion = 
                                    match Map.tryFind np.Name group.Resolution with
                                    | Some resolvedPackage -> VersionRequirement(GreaterThan resolvedPackage.Version, getPreReleaseStatus resolvedPackage.Version)
                                    | None -> specificVersionRequirement

                                np.Name,lockedVersion
                        else
                            np.Name,specificVersionRequirement
                | Some groupName ->
                    let dependencyVersionRequirement =
                        if not lockDependencies then
                            match dependenciesFile.Groups |> Map.tryFind groupName with
                            | None -> None
                            | Some group ->
                                match List.tryFind (fun r -> r.Name = np.Name) group.Packages with
                                | Some requirement ->
                                    
                                    if minimumFromLockFile || requirement.VersionRequirement = VersionRequirement.NoRestriction then
                                        match lockFile.Groups |> Map.tryFind groupName with
                                        | None -> Some requirement.VersionRequirement
                                        | Some group ->
                                            match Map.tryFind np.Name group.Resolution with
                                            | Some resolvedPackage -> 
                                                let pre = if minimumFromLockFile then getPreReleaseStatus resolvedPackage.Version else requirement.VersionRequirement.PreReleases
                                                match requirement.VersionRequirement.Range with 
                                                | OverrideAll v -> 
                                                    if v <> resolvedPackage.Version then
                                                        failwithf "Versions in %s and %s are not identical for package %O." lockFile.FileName dependenciesFile.FileName np.Name
                                                    Some(VersionRequirement(Specific resolvedPackage.Version,pre))
                                                | Specific v -> 
                                                    if v <> resolvedPackage.Version then
                                                        failwithf "Versions in %s and %s are not identical for package %O." lockFile.FileName dependenciesFile.FileName np.Name
                                                    Some(VersionRequirement(Specific resolvedPackage.Version,pre))
                                                | Maximum v ->
                                                    if v = resolvedPackage.Version then
                                                        Some(VersionRequirement(Specific resolvedPackage.Version,pre))
                                                    else
                                                        Some(VersionRequirement(VersionRange.Range(VersionRangeBound.Including,resolvedPackage.Version,v,VersionRangeBound.Including),pre))
                                                | Range(_,_,v2,ub) ->
                                                    Some(VersionRequirement(VersionRange.Range(VersionRangeBound.Including,resolvedPackage.Version,v2,ub),pre))
                                                | _ -> Some(VersionRequirement(Minimum resolvedPackage.Version,pre))
                                            | None -> Some requirement.VersionRequirement
                                    else
                                        Some requirement.VersionRequirement
                                | None ->
                                    match lockFile.Groups |> Map.tryFind groupName with
                                    | None -> None
                                    | Some group ->
                                        // If it's a transitive dependency, try to
                                        // find it in `paket.lock` and set min version
                                        // to current locked version
                                        group.Resolution
                                        |> Map.tryFind np.Name
                                        |> Option.map (fun transitive -> VersionRequirement(Minimum transitive.Version, getPreReleaseStatus transitive.Version))
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
                        | None -> failwithf "No package with id '%O' installed in group %O." np.Name groupName
                     
                    np.Name, dep)

        deps
        |> List.fold addDependency withDepsAndIncluded
