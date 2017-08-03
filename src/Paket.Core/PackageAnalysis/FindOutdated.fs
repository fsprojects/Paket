/// Contains methods to find outdated packages.
module Paket.FindOutdated

open Paket.Domain
open Paket.Logging
open Chessie.ErrorHandling
open System.IO
open Pri.LongPath

let private adjustVersionRequirements strict includingPrereleases (dependenciesFile: DependenciesFile) =
    let adjust (packageRequirement:Requirements.PackageRequirement) =
        let versionRequirement,strategy = 
            match strict,includingPrereleases with
            | true,true -> VersionRequirement.NoRestriction, packageRequirement.ResolverStrategyForTransitives
            | true,false -> packageRequirement.VersionRequirement, packageRequirement.ResolverStrategyForTransitives
            | false,true -> 
                match packageRequirement.VersionRequirement with
                | VersionRequirement(v,_) -> VersionRequirement.VersionRequirement(v,PreReleaseStatus.All), Some ResolverStrategy.Max
            | false,false -> VersionRequirement.AllReleases, Some ResolverStrategy.Max
        { packageRequirement with VersionRequirement = versionRequirement; ResolverStrategyForTransitives = strategy}

    let groups = 
        dependenciesFile.Groups 
        |> Map.map (fun groupName group -> { group with Packages = group.Packages |> List.map adjust })

    DependenciesFile(dependenciesFile.FileName, groups, dependenciesFile.Lines)

/// Finds all outdated packages.
let FindOutdated strict force includingPrereleases groupNameFilter environment = trial {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists

    let dependenciesFile =
        environment.DependenciesFile
        |> adjustVersionRequirements strict includingPrereleases

    let getSha1 origin owner repo branch auth = RemoteDownload.getSHA1OfBranch origin owner repo branch auth |> Async.RunSynchronously
    let root = Path.GetDirectoryName dependenciesFile.FileName
    let alternativeProjectRoot = None

    let getVersionsF sources groupName packageName = async {
        let! versions = NuGet.GetVersions force alternativeProjectRoot root (sources, packageName)
        return versions |> List.toSeq }
    let getPreferredVersionsF sources resolverStrategy groupName packageName = []
    let dependenciesFile = UpdateProcess.detectProjectFrameworksForDependenciesFile dependenciesFile
    let checkedDepsGroups = 
        match groupNameFilter with
        | None -> dependenciesFile.Groups
        | Some gname -> dependenciesFile.Groups |> Map.filter(fun k g -> k.ToString() = gname)

    let newResolution = dependenciesFile.Resolve(force, getSha1, getVersionsF, getPreferredVersionsF, NuGet.GetPackageDetails alternativeProjectRoot root true, RuntimeGraph.getRuntimeGraphFromNugetCache root, checkedDepsGroups, PackageResolver.UpdateMode.UpdateAll)

    let checkedLockGroups = 
        match groupNameFilter with
        | None -> lockFile.Groups
        | Some gname -> lockFile.Groups |> Map.filter(fun k g -> k.ToString() = gname)

    let changed = 
        [for kv in checkedLockGroups do
            match newResolution |> Map.tryFind kv.Key with
            | Some group ->
                let newPackages = group.ResolvedPackages.GetModelOrFail()
                for kv' in kv.Value.Resolution do
                    let package = kv'.Value
                    match newPackages |> Map.tryFind package.Name with
                    | Some newVersion -> 
                        if package.Version <> newVersion.Version then 
                            yield kv.Key,package.Name,package.Version,newVersion.Version
                    | _ -> ()
            | _ -> ()]

    return changed
}

let private printOutdated changed =
    if List.isEmpty changed then
        tracefn "No outdated packages found."
    else
        tracefn "Outdated packages found:"

        for (groupName:GroupName),packages in changed |> List.groupBy (fun (g,_,_,_) -> g) do
            tracefn "  Group: %O"  groupName
            for (_,packageName:PackageName,oldVersion,newVersion) in packages do
                tracefn "    * %O %O -> %O" packageName oldVersion newVersion

/// Prints all outdated packages.
let ShowOutdated strict force includingPrereleases groupName environment = trial {
    let! allOutdated = FindOutdated strict force includingPrereleases groupName environment
    printOutdated allOutdated
}