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
    if verbose then
        traceVerbose (sprintf "Loading assembly metadata for %s" fileName)
    let assemblyReader =
        ProviderImplementation.AssemblyReader.ILModuleReaderAfterReadingAllBytes(
            fileName,
            ProviderImplementation.AssemblyReader.mkILGlobals ProviderImplementation.AssemblyReader.EcmaMscorlibScopeRef)

    let versionFromAssembly = assemblyReader.ILModuleDef.ManifestOfAssembly.Version
    let id = assemblyReader.ILModuleDef.ManifestOfAssembly.Name
    assemblyReader,id,versionFromAssembly,fileName


let readAssemblyFromProjFile buildConfig buildPlatform (projectFile : ProjectFile) =
    let root = Path.GetDirectoryName projectFile.FileName
    let subFolder = projectFile.GetOutputDirectory buildConfig buildPlatform None
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

let addDependencyToFrameworkGroup framework dependencyGroups dependencyWithFwRestriction =
    let dependency, (fwRestriction: FrameworkRestrictions) =
        let pkgName, version, fwRestriction = dependencyWithFwRestriction
        (pkgName, version), fwRestriction

    let isCompatibleWith (fwOption: FrameworkIdentifier option) =
        match fwOption with
        | None -> true
        | Some fw ->
            if fwRestriction.IsSupersetOf (FrameworkRestriction.Exactly fw) then
                true
            else
                false

    dependencyGroups
    |> List.tryFind (fun g -> g.Framework = framework)
    |> function
    | None ->
        if isCompatibleWith framework then
            let newGroup = OptionalDependencyGroup.For framework [dependency]
            newGroup :: dependencyGroups
        else
            dependencyGroups
    | _ ->
        dependencyGroups
        |> List.map (fun g ->
            if g.Framework <> framework then g
            else
                if isCompatibleWith g.Framework then
                    { g with Dependencies = dependency :: g.Dependencies }
                else
                    g)

let addDependency (templateFile : TemplateFile) (dependency : PackageName * VersionRequirement * FrameworkRestrictions) =
    match templateFile with
    | CompleteTemplate(core, opt) ->
        let packageName = dependency |> (fun (n,_,_) -> n)
        let newDeps =
            opt.DependencyGroups
            |> List.tryFind (fun g ->
                g.Dependencies
                |> List.tryFind (fun (n, _) -> n = packageName)
                |> Option.isSome)
            |> function
            | Some _ -> opt.DependencyGroups
            | None ->
                match opt.DependencyGroups |> List.map (fun { Framework = tfm } -> tfm) with
                | [] ->
                    dependency |> addDependencyToFrameworkGroup None opt.DependencyGroups
                | tfms ->
                    // add to all dependency groups
                    (opt.DependencyGroups, tfms)
                    ||> List.fold (fun groups tfm -> dependency |> addDependencyToFrameworkGroup tfm groups)

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

let toFileWithOutputDirectory (p : ProjectFile) outputDirectory =
    Path.Combine(Path.GetDirectoryName p.FileName, outputDirectory, p.GetAssemblyName())

let toFile config platform (p : ProjectFile) targetProfile =
    p.GetOutputDirectory config platform targetProfile |> toFileWithOutputDirectory p

let addFile (source : string) (target : string) (templateFile : TemplateFile) =
    match templateFile with
    | CompleteTemplate(core, opt) ->
        { FileName = templateFile.FileName
          Contents = CompleteInfo(core, { opt with Files = (source,target) :: opt.Files }) }
    | IncompleteTemplate ->
        failwith (sprintf "You should only try and add files to template files with complete metadata.%sFile: %s" Environment.NewLine templateFile.FileName)

let findBestDependencyTargetProfile dependencyTargetProfiles (targetProfile : TargetProfile option) =
    match targetProfile with
    | None -> None
    | Some targetProfile ->
        let exactMatch =
             dependencyTargetProfiles
             |> List.tryFind ((=) targetProfile)
        match exactMatch with
        | Some x -> Some x
        | None ->
            dependencyTargetProfiles
            |> List.filter targetProfile.IsAtLeast
            |> List.sortDescending
            |> List.tryHead

let getTargetDir (project : ProjectFile) targetProfile =
    match project.BuildOutputTargetFolder with
    | Some x -> x
    | None ->
        match project.OutputType with
        | ProjectOutputType.Exe -> "tools/"
        | ProjectOutputType.Library ->
            match targetProfile with
            | Some targetProfile -> sprintf "lib/%O/" targetProfile
            | None -> "lib/"

let findDependencies (dependenciesFile : DependenciesFile) config platform (template : TemplateFile) (project : ProjectFile) lockDependencies minimumFromLockFile pinProjectReferences interprojectReferencesConstraint (projectWithTemplates : Map<string, Lazy<'TemplateFile> * ProjectFile * bool>) includeReferencedProjects (version :SemVerInfo option) cache =
    let includeReferencedProjects = template.IncludeReferencedProjects || includeReferencedProjects

    let interprojectReferencesConstraint =
        match interprojectReferencesConstraint with
        | Some c -> c
        | None ->
            match template.InterprojectReferencesConstraint with
            | Some c -> c
            | None ->
                if pinProjectReferences || lockDependencies then
                    InterprojectReferencesConstraint.Fix
                else
                    InterprojectReferencesConstraint.Min

    let targetProfiles =
        match project.GetTargetProfiles() with
        | [] -> [None]
        | x -> List.map Some x

    let projectDir = Path.GetDirectoryName project.FileName

    let getPreReleaseStatus (v:SemVerInfo) =
        match v.PreRelease with
        | None -> PreReleaseStatus.No
        | _ -> PreReleaseStatus.All

    let deps, files =
        let interProjectDeps =
            if includeReferencedProjects then
                project.GetAllInterProjectDependenciesWithoutProjectTemplates cache
            else
                project.GetAllInterProjectDependenciesWithProjectTemplates cache
            |> Seq.toList

        interProjectDeps
        |> List.filter (fun proj -> proj <> project)
        |> List.fold (fun (deps, files) p ->
            match Map.tryFind p.FileName projectWithTemplates with
            | Some (packagedTemplate,packagedProject,true) -> (packagedTemplate,packagedProject) :: deps, files
            | _ ->
                let p =
                    match ProjectFile.TryLoad p.FileName with
                    | Some p -> p
                    | _ -> failwithf "Missing project reference in proj file %s" p.FileName

                deps, p :: files) ([], [])

    // Add the assembly + {.dll, .pdb, .xml, /*/.resources.dll} from this project
    let templateWithOutput =
        let projects =
            if includeReferencedProjects then
                project.GetAllInterProjectDependenciesWithoutProjectTemplates cache
                |> Seq.toList
            else
                [ project ]

        let satelliteDlls =
            seq {
                for targetProfile in targetProfiles do
                    let targetDir = getTargetDir project targetProfile
                    for project in projects do
                        let dependencyTargetProfiles = project.GetTargetProfiles()
                        let dependencyTargetProfile = findBestDependencyTargetProfile dependencyTargetProfiles targetProfile
                        let satelliteAssemblyName = Path.GetFileNameWithoutExtension(project.GetAssemblyName()) + ".resources.dll"
                        let projectDir = Path.GetDirectoryName(Path.GetFullPath(project.FileName))
                        let outputDir = Path.Combine(projectDir, project.GetOutputDirectory config platform dependencyTargetProfile)

                        let satelliteWithFolders =
                            Directory.GetFiles(outputDir, satelliteAssemblyName, SearchOption.AllDirectories)
                            |> Array.map (fun sa -> (sa, Directory.GetParent(sa)))
                            |> Array.filter (fun (sa, dirInfo) -> Cultures.isLanguageName dirInfo.Name)

                        let existedSatelliteLanguages =
                            satelliteWithFolders
                            |> Array.map (fun (_, dirInfo) -> dirInfo.Name)
                            |> Set.ofArray

                        project.FindLocalizedLanguageNames()
                        |> List.filter (existedSatelliteLanguages.Contains >> not)
                        |> List.iter (fun lang ->
                            traceWarnfn "Did not find satellite assembly for (%s) try building and running pack again." lang)

                        yield!
                            satelliteWithFolders
                            |> Array.map (fun (sa, dirInfo) -> (FileInfo sa, Path.Combine(targetDir, dirInfo.Name)))
            }

        let template =
            satelliteDlls
            |> Seq.fold (fun template (dllFile, targetDir) -> addFile dllFile.FullName targetDir template) template

        let assemblyNames =
            projects
            |> List.map (fun proj -> proj.GetAssemblyName())

        let additionalFiles =
            targetProfiles
            |> Seq.collect (fun targetProfile ->
                let targetDir = getTargetDir project targetProfile
                let outputDir = project.GetOutputDirectory config platform targetProfile
                let path = Path.Combine(projectDir, outputDir)
                assemblyNames
                |> Seq.collect (fun assemblyFileName ->
                    let assemblyfi = FileInfo(assemblyFileName)
                    let name = Path.GetFileNameWithoutExtension assemblyfi.Name

                    Directory.GetFiles(path, name + ".*")
                    |> Array.map (fun f -> (targetDir, FileInfo f))
                    |> Array.filter (fun (_, fi) ->
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
                        isSameFileName && isValidExtension)))
            |> Seq.toArray

        additionalFiles
        |> Array.fold (fun template (targetDir, file) -> addFile file.FullName targetDir template) template

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
               let evaluatedTemplate = templateFile.Force()
               match evaluatedTemplate with
               | CompleteTemplate(core, _) ->
                   match core.Version with
                   | Some v ->
                       let versionConstraint = interprojectReferencesConstraint.CreateVersionRequirements v
                       let anyFw = FrameworkRestrictions.ExplicitRestriction FrameworkRestriction.NoRestriction
                       PackageName core.Id, VersionRequirement(versionConstraint, getPreReleaseStatus v), anyFw
                   | None -> failwithf "There was no version given for %s." evaluatedTemplate.FileName
               | IncompleteTemplate ->
                   failwithf "You cannot create a dependency on a template file (%s) with incomplete metadata." evaluatedTemplate.FileName)
        |> List.fold addDependency templateWithOutputAndExcludes

    // If project refs will not be packaged, add the assembly to the package
    let withDepsAndIncluded =
        targetProfiles
        |> List.collect (fun targetProfile ->
                let targetDir = getTargetDir project targetProfile
                files |>
                List.map (fun file ->
                        let dependencyTargetProfiles = file.GetTargetProfiles()
                        let dependencyTargetProfile = findBestDependencyTargetProfile dependencyTargetProfiles targetProfile
                        (targetProfile, targetDir, file.GetOutputDirectory config platform dependencyTargetProfile, file)))
        |> List.fold (fun templatefile (targetProfile, targetDir, outputDir, file) -> addFile (toFileWithOutputDirectory file outputDir) targetDir templatefile) withDeps

    let lockFile =
        dependenciesFile.FindLockFile().FullName
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
            for proj in project.GetAllReferencedProjects(false, cache) |> Seq.filter ((<>) project) do
                match Map.tryFind proj.FileName projectWithTemplates with
                | Some (template, _, _) ->
                    match template.Force() with
                    | { Contents = CompleteInfo(core, _) } ->
                        let versionConstraint =
                            match core.Version with
                            | Some v ->
                                let vr = interprojectReferencesConstraint.CreateVersionRequirements v
                                VersionRequirement(vr, getPreReleaseStatus v)
                            | None -> VersionRequirement.AllReleases

                        yield None, { Name = PackageName core.Id; Settings = InstallSettings.Default }, Some versionConstraint
                    | _ -> yield! getPackages proj
                | _ -> yield! getPackages proj
         yield! getPackages project]

    // filter out any references that are transitive
    let distinctRefs = List.distinct allReferences

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
    | [] ->
        withDepsAndIncluded
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
                            let nuspec = Nuspec.LoadFromCache(np.Name,rp.Version)
                            not nuspec.IsDevelopmentDependency
                    with
                    | _ -> true)
            |> List.map (fun (group, np, specificVersionRequirement) ->
                let specificVersionRequirement = defaultArg specificVersionRequirement VersionRequirement.AllReleases
                let noFrameworkRestriction = FrameworkRestrictions.ExplicitRestriction FrameworkRestriction.NoRestriction
                match group with
                | None ->
                    match version with
                    | Some v ->
                        let vr = interprojectReferencesConstraint.CreateVersionRequirements v
                        np,VersionRequirement(vr, getPreReleaseStatus v),noFrameworkRestriction
                    | None ->
                        if minimumFromLockFile then
                            let groupName =
                                lockFile.GetDependencyLookupTable()
                                |> Seq.filter (fun m -> snd m.Key = np.Name)
                                |> Seq.map (fun m -> m.Key)
                                |> Seq.tryHead
                                |> Option.map fst

                            match groupName with
                            | None -> np,specificVersionRequirement,noFrameworkRestriction
                            | Some groupName ->
                                let group = lockFile.GetGroup groupName

                                let lockedVersion, fwRestriction =
                                    match Map.tryFind np.Name group.Resolution with
                                    | Some resolvedPackage -> VersionRequirement(GreaterThan resolvedPackage.Version, getPreReleaseStatus resolvedPackage.Version), resolvedPackage.Settings.FrameworkRestrictions
                                    | None -> specificVersionRequirement, noFrameworkRestriction

                                np,lockedVersion,fwRestriction
                        else
                            np,specificVersionRequirement,noFrameworkRestriction
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
                                        | None -> Some (requirement.VersionRequirement, requirement.Settings.FrameworkRestrictions)
                                        | Some group ->
                                            match Map.tryFind np.Name group.Resolution with
                                            | Some resolvedPackage ->
                                                let pre = if minimumFromLockFile then getPreReleaseStatus resolvedPackage.Version else requirement.VersionRequirement.PreReleases
                                                match requirement.VersionRequirement.Range with
                                                | OverrideAll v ->
                                                    if v <> resolvedPackage.Version then
                                                        failwithf "Versions in %s and %s are not identical for package %O." lockFile.FileName dependenciesFile.FileName np.Name
                                                    Some(VersionRequirement(Specific resolvedPackage.Version,pre), resolvedPackage.Settings.FrameworkRestrictions)
                                                | Specific v ->
                                                    if v <> resolvedPackage.Version then
                                                        failwithf "Versions in %s and %s are not identical for package %O." lockFile.FileName dependenciesFile.FileName np.Name
                                                    Some(VersionRequirement(Specific resolvedPackage.Version,pre), resolvedPackage.Settings.FrameworkRestrictions)
                                                | Maximum v ->
                                                    if v = resolvedPackage.Version then
                                                        Some(VersionRequirement(Specific resolvedPackage.Version,pre), resolvedPackage.Settings.FrameworkRestrictions)
                                                    else
                                                        Some(VersionRequirement(VersionRange.Range(VersionRangeBound.Including,resolvedPackage.Version,v,VersionRangeBound.Including),pre), resolvedPackage.Settings.FrameworkRestrictions)
                                                | Range(_,_,v2,ub) ->
                                                    Some(VersionRequirement(VersionRange.Range(VersionRangeBound.Including,resolvedPackage.Version,v2,ub),pre), resolvedPackage.Settings.FrameworkRestrictions)
                                                | _ -> Some(VersionRequirement(Minimum resolvedPackage.Version,pre), resolvedPackage.Settings.FrameworkRestrictions)
                                            | None -> Some (requirement.VersionRequirement, requirement.Settings.FrameworkRestrictions)
                                    else
                                        Some (requirement.VersionRequirement, requirement.Settings.FrameworkRestrictions)
                                | None ->
                                    match lockFile.Groups |> Map.tryFind groupName with
                                    | None -> None
                                    | Some group ->
                                        // If it's a transitive dependency, try to
                                        // find it in `paket.lock` and set min version
                                        // to current locked version
                                        group.Resolution
                                        |> Map.tryFind np.Name
                                        |> Option.map (fun transitive -> VersionRequirement(Minimum transitive.Version, getPreReleaseStatus transitive.Version), transitive.Settings.FrameworkRestrictions)
                            else
                                match lockFile.Groups |> Map.tryFind groupName with
                                | None -> None
                                | Some group ->
                                    Map.tryFind np.Name group.Resolution
                                    |> Option.map (fun resolvedPackage ->
                                        let version = resolvedPackage.Version
                                        VersionRequirement(Specific version, getPreReleaseStatus version), resolvedPackage.Settings.FrameworkRestrictions)
                    let dep, depFwRestriction =
                        match dependencyVersionRequirement with
                        | Some installed -> installed
                        | None -> failwithf "No package with id '%O' installed in group %O." np.Name groupName

                    np, dep, depFwRestriction)
            |> List.map (fun (np, dep, depFwRestriction) ->
                let fwRestriction =
                    match depFwRestriction, np.Settings.FrameworkRestrictions with
                    | AutoDetectFramework, AutoDetectFramework -> ExplicitRestriction(FrameworkRestriction.NoRestriction)
                    | AutoDetectFramework, ExplicitRestriction fr -> ExplicitRestriction fr
                    | ExplicitRestriction fr, AutoDetectFramework -> ExplicitRestriction fr
                    | ExplicitRestriction fr1, ExplicitRestriction fr2 -> ExplicitRestriction (FrameworkRestriction.combineRestrictionsWithAnd fr1 fr2)
                np.Name, dep, fwRestriction)
        deps
        |> List.fold addDependency withDepsAndIncluded
