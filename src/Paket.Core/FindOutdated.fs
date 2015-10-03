/// Contains methods to find outdated packages.
module Paket.FindOutdated

open Paket.Domain
open Paket.Logging
open Chessie.ErrorHandling
open System.IO

let private adjustVersionRequirements strict includingPrereleases (dependenciesFile: DependenciesFile) =
    let groups =
        dependenciesFile.Groups
        |> Map.map (fun groupName group ->
            let newPackages =
                group.Packages
                |> List.map (fun p ->
                    let v = p.VersionRequirement 
                    let requirement,strategy =
                        match strict,includingPrereleases with
                        | true,true -> VersionRequirement.NoRestriction, p.ResolverStrategy
                        | true,false -> v, p.ResolverStrategy
                        | false,true -> 
                            match v with
                            | VersionRequirement(v,_) -> 
                                VersionRequirement.VersionRequirement(v,PreReleaseStatus.All), ResolverStrategy.Max
                        | false,false -> VersionRequirement.AllReleases, ResolverStrategy.Max
                    { p with VersionRequirement = requirement; ResolverStrategy = strategy})
            { group with Packages = newPackages })

    DependenciesFile(dependenciesFile.FileName, groups, dependenciesFile.Lines)

/// Finds all outdated packages.
let FindOutdated strict includingPrereleases environment = trial {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists

    let dependenciesFile =
        environment.DependenciesFile
        |> adjustVersionRequirements strict includingPrereleases

    let getSha1 origin owner repo branch auth = RemoteDownload.getSHA1OfBranch origin owner repo branch auth |> Async.RunSynchronously
    let root = Path.GetDirectoryName dependenciesFile.FileName

    let groups = 
        dependenciesFile.Groups
        |> Map.map (fun groupName group -> dependenciesFile.Groups.[groupName].Packages)

    let newResolution = dependenciesFile.Resolve(true,getSha1,(fun (x,y,_) -> NuGetV2.GetVersions root (x,y)),NuGetV2.GetPackageDetails root true,groups)

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
    if changed = [] then
        tracefn "No outdated packages found."
    else
        tracefn "Outdated packages found:"

        for (GroupName groupName),packages in changed |> List.groupBy (fun (g,_,_,_) -> g) do
            tracefn "  Group: %s"  groupName
            for (_,(PackageName name),oldVersion,newVersion) in packages do
                tracefn "    * %s %O -> %O"  name oldVersion newVersion

/// Prints all outdated packages.
let ShowOutdated strict includingPrereleases environment = trial {
    let! allOutdated = FindOutdated strict includingPrereleases environment
    printOutdated allOutdated
}