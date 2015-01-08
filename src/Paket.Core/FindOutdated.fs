/// Contains methods to find outdated packages.
module Paket.FindOutdated

open Paket.Domain
open Paket.Logging
open Paket.Rop

let private adjustVersionRequirements strict includingPrereleases (dependenciesFile: DependenciesFile) =
    //TODO: Anything we need to do for source files here?
    let newPackages =
        dependenciesFile.Packages
        |> List.map (fun p ->
            let v = p.VersionRequirement 
            let requirement,strategy =
                match strict,includingPrereleases with
                | true,true -> VersionRequirement.NoRestriction, p.ResolverStrategy
                | true,false -> v, p.ResolverStrategy
                | false,true -> 
                    match v with
                    | VersionRequirement(v,_) -> VersionRequirement(v,PreReleaseStatus.All), Max
                | false,false -> VersionRequirement.AllReleases, Max
            { p with VersionRequirement = requirement; ResolverStrategy = strategy})

    DependenciesFile(dependenciesFile.FileName, dependenciesFile.Options, dependenciesFile.Sources, newPackages, dependenciesFile.RemoteFiles)

let private detectOutdated (oldResolution: PackageResolver.PackageResolution) (newResolution: PackageResolver.PackageResolution) =
    [for kv in oldResolution do
        let package = kv.Value
        match newResolution |> Map.tryFind (NormalizedPackageName package.Name) with
        | Some newVersion -> 
            if package.Version <> newVersion.Version then 
                yield package.Name,package.Version,newVersion.Version
        | _ -> ()]

/// Finds all outdated packages.
let FindOutdated strict includingPrereleases environment = rop {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists

    let dependenciesFile =
        environment.DependenciesFile
        |> adjustVersionRequirements strict includingPrereleases

    let resolution = dependenciesFile.Resolve(true)
    let resolvedPackages = resolution.ResolvedPackages.GetModelOrFail()

    return detectOutdated lockFile.ResolvedPackages resolvedPackages
}

/// Finds all outdated packages.
let FindOutdatedOld(dependenciesFileName,strict,includingPrereleases) =
    let dependenciesFile =
        DependenciesFile.ReadFromFile dependenciesFileName
        |> adjustVersionRequirements strict includingPrereleases

    let resolution = dependenciesFile.Resolve(true)
    let resolvedPackages = resolution.ResolvedPackages.GetModelOrFail()
    let lockFile = LockFile.LoadFrom(dependenciesFile.FindLockfile().FullName)

    detectOutdated lockFile.ResolvedPackages resolvedPackages

/// Prints all outdated packages.
let ShowOutdated(dependenciesFileName,strict,includingPrereleases) =
    let allOutdated = FindOutdatedOld(dependenciesFileName,strict,includingPrereleases)
    if allOutdated = [] then
        tracefn "No outdated packages found."
    else
        tracefn "Outdated packages found:"
        for (PackageName name),oldVersion,newVersion in allOutdated do
            tracefn "  * %s %s -> %s" name (oldVersion.ToString()) (newVersion.ToString())