/// Contains methods to find outdated packages.
module Paket.FindOutdated

open Paket.Domain
open Paket.Logging
open Chessie.ErrorHandling
open System.IO

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
                    | VersionRequirement(v,_) -> VersionRequirement(v,PreReleaseStatus.All), ResolverStrategy.Max
                | false,false -> VersionRequirement.AllReleases, ResolverStrategy.Max
            { p with VersionRequirement = requirement; ResolverStrategy = strategy})

    let mainGroup = { Name = Constants.MainDependencyGroup; Options = dependenciesFile.Groups.[Constants.MainDependencyGroup].Options; Sources = dependenciesFile.Sources; Packages = newPackages; RemoteFiles = dependenciesFile.RemoteFiles }
    let groups = [Constants.MainDependencyGroup, mainGroup] |> Map.ofSeq

    DependenciesFile(dependenciesFile.FileName, groups, dependenciesFile.Lines)

let private detectOutdated (oldResolution: PackageResolver.PackageResolution) (newResolution: PackageResolver.PackageResolution) =
    [for kv in oldResolution do
        let package = kv.Value
        match newResolution |> Map.tryFind package.Name with
        | Some newVersion -> 
            if package.Version <> newVersion.Version then 
                yield package.Name,package.Version,newVersion.Version
        | _ -> ()]

/// Finds all outdated packages.
let FindOutdated strict includingPrereleases environment = trial {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists

    let dependenciesFile =
        environment.DependenciesFile
        |> adjustVersionRequirements strict includingPrereleases

    let getSha1 origin owner repo branch = RemoteDownload.getSHA1OfBranch origin owner repo branch |> Async.RunSynchronously
    let root = Path.GetDirectoryName dependenciesFile.FileName
    let mainGroup = dependenciesFile.Groups.[Constants.MainDependencyGroup]
    // TODO: This is only looking at the maingroup
    let mainGroup = 
        { Name = Constants.MainDependencyGroup
          RemoteFiles = mainGroup.RemoteFiles
          RootDependencies = Some dependenciesFile.Packages
          FrameworkRestrictions = mainGroup.Options.Settings.FrameworkRestrictions
          PackageRequirements = [] }
        
    let groups = [Constants.MainDependencyGroup, mainGroup ] |> Map.ofSeq
    let resolution = dependenciesFile.Resolve(getSha1,NuGetV2.GetVersions root,NuGetV2.GetPackageDetails root true,groups).[Constants.MainDependencyGroup]
    let resolvedPackages = resolution.ResolvedPackages.GetModelOrFail()

    return detectOutdated (lockFile.GetCompleteResolution()) resolvedPackages
}

let private printOutdated packages =
    if packages = [] then
        tracefn "No outdated packages found."
    else
        tracefn "Outdated packages found:"
        for (PackageName name),oldVersion,newVersion in packages do
            tracefn "  * %s %s -> %s" name (oldVersion.ToString()) (newVersion.ToString())

/// Prints all outdated packages.
let ShowOutdated strict includingPrereleases environment = trial {
    let! allOutdated = FindOutdated strict includingPrereleases environment
    printOutdated allOutdated
}