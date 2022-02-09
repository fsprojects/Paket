/// Contains methods for the update process.
module Paket.UpdateProcess

open Paket
open System.IO
open Paket.Domain
open Paket.PackageResolver
open Chessie.ErrorHandling
open Paket.Logging

let selectiveUpdate force getSha1 getVersionsF getPackageDetailsF getRuntimeGraphFromPackage (lockFile:LockFile) (dependenciesFile:DependenciesFile) updateMode semVerUpdateMode =
    let dependenciesFile =
        let processFile createRequirementF =
            lockFile.GetGroupedResolution()
            |> Map.fold (fun (dependenciesFile:DependenciesFile) (groupName,packageName) resolvedPackage ->
                let settings =
                    match dependenciesFile.Groups |> Map.tryFind groupName with
                    | None -> resolvedPackage.Settings
                    | Some group ->
                        match group.Packages |> List.tryFind (fun p -> p.Name = packageName) with
                        | None -> resolvedPackage.Settings
                        | Some p -> p.Settings

                dependenciesFile.AddFixedPackage(
                    groupName,
                    packageName,
                    createRequirementF resolvedPackage.Version,
                    settings)) dependenciesFile

        let formatPrerelease (v:SemVerInfo) =
            match v.PreRelease with
            | Some p -> sprintf " prerelease"
            | None -> ""

        match semVerUpdateMode with
        | SemVerUpdateMode.NoRestriction -> dependenciesFile
        | SemVerUpdateMode.KeepMajor -> processFile (fun v -> sprintf "~> %d.%d" v.Major v.Minor + formatPrerelease v)
        | SemVerUpdateMode.KeepMinor -> processFile (fun v -> sprintf "~> %d.%d.%d" v.Major v.Minor v.Patch + formatPrerelease v)
        | SemVerUpdateMode.KeepPatch -> processFile (fun v -> sprintf "~> %d.%d.%d.%A" v.Major v.Minor v.Patch v.Build + formatPrerelease v)

    let getPreferredVersionsF,getPackageDetailsF,groupsToUpdate =
        let changes,groups =
            match updateMode with
            | UpdateAll ->
                let changes =
                    lockFile.GetGroupedResolution()
                    |> Seq.map (fun k -> k.Key)
                    |> Set.ofSeq

                changes,dependenciesFile.Groups
            | UpdateGroup groupName ->
                let changes =
                    lockFile.GetGroupedResolution()
                    |> Seq.map (fun k -> k.Key)
                    |> Seq.filter (fun (g,_) -> g = groupName)
                    |> Set.ofSeq

                let groups =
                    dependenciesFile.Groups
                    |> Map.filter (fun k _ -> k = groupName)

                changes,groups
            | UpdateFiltered (groupName, filter) ->
                let changes =
                    lockFile.GetGroupedResolution()
                    |> Seq.map (fun k -> k.Key)
                    |> Seq.filter (fun (g, p) -> g = groupName && filter.Match p)
                    |> Set.ofSeq
                    |> fun s ->
                        match filter with
                        | PackageFilter.PackageName name -> Set.add (groupName,name) s
                        | _ -> s

                let groups =
                    dependenciesFile.Groups
                    |> Map.filter (fun k _ -> k = groupName || changes |> Seq.exists (fun (g,_) -> g = k))

                changes,groups
            | InstallGroup groupName ->
                let hasAnyChanges,nuGetChanges,remoteFileChanges,hasChanges = DependencyChangeDetection.GetChanges(dependenciesFile,lockFile,true)

                let hasChanges groupName x =
                    let hasChanges = hasChanges groupName x
                    if not hasChanges then
                        tracefn "Skipping resolver for group %O since it is already up-to-date" groupName
                    hasChanges

                let groups =
                    dependenciesFile.Groups
                    |> Map.filter (fun k _ -> k = groupName)
                    |> Map.filter hasChanges

                nuGetChanges
                |> Set.map (fun (f,s,_) -> f,s)
                |> Set.filter (fun (g,_) -> g = groupName), groups
            | Install ->
                let hasAnyChanges,nuGetChanges,remoteFileChanges,hasChanges = DependencyChangeDetection.GetChanges(dependenciesFile,lockFile,true)

                let hasChanges groupName x =
                    let hasChanges = hasChanges groupName x
                    if not hasChanges then
                        tracefn "Skipping resolver for group %O since it is already up-to-date" groupName
                    hasChanges

                let groups =
                    dependenciesFile.Groups
                    |> Map.filter hasChanges

                nuGetChanges
                |> Set.map (fun (f,s,_) -> f,s), groups

        let preferredVersions =
            match updateMode with
            | UpdateAll ->
                Map.empty
            | UpdateGroup _ ->
                Map.empty
            | UpdateFiltered (groupName, filter) ->
                DependencyChangeDetection.GetPreferredNuGetVersions(dependenciesFile,lockFile)
                |> Map.filter (fun (g, p) _ -> g = groupName || not (filter.Match p))
            | InstallGroup groupName ->
                DependencyChangeDetection.GetPreferredNuGetVersions(dependenciesFile,lockFile)
                |> Map.filter (fun (g, p) _ -> g = groupName)
            | Install ->
                DependencyChangeDetection.GetPreferredNuGetVersions(dependenciesFile,lockFile)
            |> Map.map (fun (groupName,_packageName) (v,s) ->
                let caches =
                    match dependenciesFile.Groups |> Map.tryFind groupName with
                    | None -> []
                    | Some group -> group.Caches

                v,s :: (List.map PackageSources.PackageSource.FromCache caches))

        let getPreferredVersionsF resolverStrategy (parameters:GetPackageVersionsParameters) =
            let key = parameters.Package.GroupName, parameters.Package.PackageName
            match preferredVersions |> Map.tryFind key, resolverStrategy with
            | Some x, ResolverStrategy.Min -> [x]
            | Some x, _ ->
                match dependenciesFile.TryGetPackage key with
                | None -> [x]
                | _ -> if not (changes |> Set.contains key) then [x] else []
            | _ ->
                []

        let getPackageDetailsF (parameters:GetPackageDetailsParameters) = async {
            let! (exploredPackage:PackageDetails) = getPackageDetailsF parameters
            match preferredVersions |> Map.tryFind (parameters.Package.GroupName, parameters.Package.PackageName) with
            | Some (preferedVersion,_) when parameters.Version = preferedVersion -> return { exploredPackage with Unlisted = false }
            | _ -> return exploredPackage }

        getPreferredVersionsF,getPackageDetailsF,groups

    let resolution = dependenciesFile.Resolve(force, getSha1, getVersionsF, getPreferredVersionsF, getPackageDetailsF, getRuntimeGraphFromPackage, groupsToUpdate, updateMode)

    let groups =
        dependenciesFile.Groups
        |> Seq.choose (fun kv ->
            let groupName = kv.Key
            match resolution |> Map.tryFind groupName with
            | Some group ->
                let model = group.ResolvedPackages.GetModelOrFail()
                for x in model do
                    if x.Value.Unlisted then
                        traceWarnfn "The owner of %O %A has unlisted the package. This could mean that the package version is deprecated or shouldn't be used anymore." x.Value.Name x.Value.Version
                let dependenciesGroup = kv.Value
                Some
                    (groupName,
                        { Name = dependenciesGroup.Name
                          Options = dependenciesGroup.Options
                          Resolution = model
                          RemoteFiles = group.ResolvedSourceFiles })
            | None -> lockFile.Groups |> Map.tryFind groupName |> Option.map (fun g -> groupName,g)) // just copy from lockfile
        |> Map.ofSeq

    LockFile(lockFile.FileName, groups),groupsToUpdate

let detectProjectFrameworksForDependenciesFile (dependenciesFile:DependenciesFile) =
    let root = Path.GetDirectoryName dependenciesFile.FileName
    let groups =
        let targetFrameworks = lazy (
            let rawRestrictions =
                RestoreProcess.findAllReferencesFiles root |> returnOrFail
                |> List.collect (fun (p,_) ->
                    p.GetTargetProfiles()
                    |> List.map Requirements.FrameworkRestriction.ExactlyPlatform)
                |> List.distinct
            if rawRestrictions.IsEmpty then Paket.Requirements.FrameworkRestriction.NoRestriction
            else rawRestrictions |> Seq.fold Paket.Requirements.FrameworkRestriction.combineRestrictionsWithOr Paket.Requirements.FrameworkRestriction.EmptySet)

        dependenciesFile.Groups
        |> Map.map (fun groupName group ->
            let restrictions =
                match group.Options.Settings.FrameworkRestrictions with
                | Requirements.FrameworkRestrictions.AutoDetectFramework ->
                    Requirements.FrameworkRestrictions.ExplicitRestriction (targetFrameworks.Force())
                | x -> x

            let settings = { group.Options.Settings with FrameworkRestrictions = restrictions }
            let options = { group.Options with Settings = settings }
            { group with Options = options })

    DependenciesFile(dependenciesFile.FileName,groups,dependenciesFile.Lines)

let SelectiveUpdate(dependenciesFile : DependenciesFile, alternativeProjectRoot, updateMode, semVerUpdateMode, force) =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFile.FileName
    let oldLockFile,updateMode =
        if (updateMode = UpdateMode.UpdateAll && semVerUpdateMode = SemVerUpdateMode.NoRestriction) || not lockFileName.Exists then
            LockFile.Parse(lockFileName.FullName, [||]),UpdateAll
        else
            LockFile.LoadFrom lockFileName.FullName,updateMode

    let getSha1 origin owner repo branch auth = RemoteDownload.getSHA1OfBranch origin owner repo branch auth |> Async.RunSynchronously
    let root = Path.GetDirectoryName dependenciesFile.FileName
    let inline getVersionsF (parameters:GetPackageVersionsParameters) = async {
        let! result = NuGet.GetVersions force alternativeProjectRoot root parameters
        return result |> List.toSeq }

    let dependenciesFile = detectProjectFrameworksForDependenciesFile dependenciesFile

    let lockFile,updatedGroups =
        selectiveUpdate
            force
            getSha1
            getVersionsF
            (NuGet.GetPackageDetails alternativeProjectRoot root force)
            (RuntimeGraph.getRuntimeGraphFromNugetCache root)
            oldLockFile
            dependenciesFile
            updateMode
            semVerUpdateMode
    let hasChanged = lockFile.Save()
    let touchedPackages = 
        [
            for group1 in oldLockFile.Groups do
                for package1 in group1.Value.Resolution do
                    match lockFile.Groups |> Map.tryFind group1.Key with
                    | None -> 
                        group1.Key, package1.Key, Some package1.Value.Version, None
                    | Some group2 ->
                        match group2.Resolution |> Map.tryFind package1.Key with
                        | Some package2 when package2.Version = package1.Value.Version ->
                            ()
                        | Some package2 ->
                            group1.Key, package1.Key, Some package1.Value.Version, Some package2.Version
                        | _ -> 
                            group1.Key, package1.Key, Some package1.Value.Version, None

            for group1 in lockFile.Groups do
                for package1 in group1.Value.Resolution do
                    match oldLockFile.Groups |> Map.tryFind group1.Key with
                    | None -> 
                        group1.Key, package1.Key, None, Some package1.Value.Version
                    | Some group2 ->
                        match group2.Resolution |> Map.tryFind package1.Key with
                        | Some package2 when package2.Version = package1.Value.Version ->
                            ()
                        | Some package2 ->
                            group1.Key, package1.Key, Some package2.Version, Some package1.Value.Version
                        | _ -> 
                            group1.Key, package1.Key, None, Some package1.Value.Version
        ]
        |> List.distinct
        |> List.sort

    if not (List.isEmpty touchedPackages) then
        tracefn "Updated packages:"
        for g,packages in touchedPackages |> List.groupBy (fun (g,_,_,_) -> g) do
            tracefn "  Group: %O" g
            for _,p,oldVersion,newVersion in packages do
                match oldVersion, newVersion with
                | Some oldV, Some newV -> tracefn "    - %O: %O -> %O" p oldV newV
                | None, Some newV -> tracefn "    - %O: %O (added)" p newV
                | Some oldV, None -> tracefn "    - %O: %O (removed)" p oldV
                | None, None -> tracefn "    - %O" p

    lockFile,hasChanged,updatedGroups,touchedPackages

/// Smart install command
let SmartInstall(dependenciesFile:DependenciesFile, updateMode, options : UpdaterOptions) =
    let lockFile,hasChanged,updatedGroups,touchedPackages = SelectiveUpdate(dependenciesFile, options.Common.AlternativeProjectRoot, updateMode, options.Common.SemVerUpdateMode, options.Common.Force)

    let root = Path.GetDirectoryName dependenciesFile.FileName
    let projectsAndReferences = RestoreProcess.findAllReferencesFiles root |> returnOrFail

    if not options.NoInstall then 
        tracefn "Installing into projects:"
        let forceTouch = hasChanged && options.Common.TouchAffectedRefs
        InstallProcess.InstallIntoProjects(options.Common, forceTouch, dependenciesFile, lockFile, projectsAndReferences, updatedGroups, Some touchedPackages)
        GarbageCollection.CleanUp(dependenciesFile, lockFile)

    let shouldGenerateScripts =
        options.Common.GenerateLoadScripts ||
        // hardcoded assumption, if option is set on any of the group, generate everything
        dependenciesFile.Groups
        |> Seq.map (fun kvp -> kvp.Value)
        |> Seq.filter (fun g -> g.Options.Settings.GenerateLoadScripts = Some true)
        |> Seq.tryHead
        |> Option.isSome

    if shouldGenerateScripts then
        let groupsToGenerate =
          if options.Common.GenerateLoadScripts then [] else
          dependenciesFile.Groups
          |> Seq.map (fun kvp -> kvp.Value)
          |> Seq.filter (fun g -> g.Options.Settings.GenerateLoadScripts = Some true)
          |> Seq.map (fun g -> g.Name)
          |> Seq.toList

        let rootDir = DirectoryInfo dependenciesFile.RootPath
        let depCache= DependencyCache(lockFile)
        let scripts = LoadingScripts.ScriptGeneration.constructScriptsFromData depCache groupsToGenerate options.Common.ProvidedFrameworks options.Common.ProvidedScriptTypes
        for script in scripts do
            script.Save rootDir
            
    let mutable runDotNetRestore = false
    if not options.NoInstall then
        for project, _ in projectsAndReferences do
            let toolsVersion = project.GetToolsVersion()
            if toolsVersion >= 15.0 then
                runDotNetRestore <- true
    runDotNetRestore

/// Update a single package command
let UpdatePackage(dependenciesFileName, groupName, packageName : PackageName, newVersion, options : UpdaterOptions) =
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)

    if not (dependenciesFile.HasPackage(groupName, packageName)) then
        failwithf "Package %O was not found in paket.dependencies in group %O.%s" packageName groupName (dependenciesFile.CheckIfPackageExistsInAnyGroup packageName)

    let dependenciesFile =
        match newVersion with
        | Some v -> dependenciesFile.UpdatePackageVersion(groupName,packageName, v)
        | None ->
            tracefn "Updating %O in %s group %O" packageName dependenciesFileName groupName
            dependenciesFile

    let filter = PackageFilter.ofName packageName

    SmartInstall(dependenciesFile, UpdateFiltered(groupName, filter), options)

/// Update a filtered list of packages
let UpdateFilteredPackages(dependenciesFileName, groupName, packageName : string, newVersion, options : UpdaterOptions) =
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)

    let filter = PackageFilter.PackageFilter(PackageMatch packageName)

    let dependenciesFile =
        match newVersion with
        | Some v -> dependenciesFile.UpdateFilteredPackageVersion(groupName, filter, v)
        | None ->
            tracefn "Updating %O in %s group %O" packageName dependenciesFileName groupName
            dependenciesFile

    SmartInstall(dependenciesFile, UpdateFiltered(groupName, filter), options)

/// Update a single group command
let UpdateGroup(dependenciesFileName, groupName,  options : UpdaterOptions) =
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)

    if not (dependenciesFile.Groups.ContainsKey groupName) then
        failwithf "Group %O was not found in paket.dependencies." groupName
    tracefn "Updating group %O in %s" groupName dependenciesFileName

    SmartInstall(dependenciesFile, UpdateGroup groupName, options)

/// Update command
let Update(dependenciesFileName, options : UpdaterOptions) =
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)

    SmartInstall(dependenciesFile, UpdateAll, options)
