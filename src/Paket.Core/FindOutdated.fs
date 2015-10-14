/// Contains methods to find outdated packages.
module Paket.FindOutdated

open Paket.Domain
open Paket.Logging
open Chessie.ErrorHandling
open System.IO

let private adjustVersionRequirements strict includingPrereleases (dependenciesFile: DependenciesFile) =
    let adjust (packageRequirement:Requirements.PackageRequirement) =
        let versionRequirement,strategy = 
            match strict,includingPrereleases with
            | true,true -> VersionRequirement.NoRestriction, packageRequirement.ResolverStrategy
            | true,false -> packageRequirement.VersionRequirement, packageRequirement.ResolverStrategy
            | false,true -> 
                match packageRequirement.VersionRequirement with
                | VersionRequirement(v,_) -> VersionRequirement.VersionRequirement(v,PreReleaseStatus.All), ResolverStrategy.Max
            | false,false -> VersionRequirement.AllReleases, ResolverStrategy.Max
        { packageRequirement with VersionRequirement = versionRequirement; ResolverStrategy = strategy}

    let groups = 
        dependenciesFile.Groups 
        |> Map.map (fun groupName group -> { group with Packages = group.Packages |> List.map adjust })

    DependenciesFile(dependenciesFile.FileName, groups, dependenciesFile.Lines)

/// Finds all outdated packages.
let FindOutdated strict includingPrereleases environment = trial {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists

    let dependenciesFile =
        environment.DependenciesFile
        |> adjustVersionRequirements strict includingPrereleases

    let getSha1 origin owner repo branch auth = RemoteDownload.getSHA1OfBranch origin owner repo branch auth |> Async.RunSynchronously
    let root = Path.GetDirectoryName dependenciesFile.FileName

    let getVersionsF sources resolverStrategy groupName packageName =
        let versions = NuGetV2.GetVersions root (sources, packageName)
                
        match resolverStrategy with
        | ResolverStrategy.Max -> List.sortDescending versions
        | ResolverStrategy.Min -> List.sort versions
        |> List.toSeq

    let newResolution = dependenciesFile.Resolve(true, getSha1, getVersionsF, NuGetV2.GetPackageDetails root true, dependenciesFile.Groups, PackageResolver.UpdateMode.UpdateAll)

    let changed = 
        [for kv in lockFile.Groups do
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

        for (GroupName groupName),packages in changed |> List.groupBy (fun (g,_,_,_) -> g) do
            tracefn "  Group: %s"  groupName
            for (_,(packageName:PackageName),oldVersion,newVersion) in packages do
                tracefn "    * %O %O -> %O"  packageName oldVersion newVersion

/// Prints all outdated packages.
let ShowOutdated strict includingPrereleases environment = trial {
    let! allOutdated = FindOutdated strict includingPrereleases environment
    printOutdated allOutdated
}