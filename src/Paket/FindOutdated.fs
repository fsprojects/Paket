/// Contains methods to find outdated packages.
module Paket.FindOutdated

open Paket.Logging

/// Finds all outdated packages.
let FindOutdated(strict,includingPrereleases,dependenciesFileName) =     
    //TODO: Anything we need to do for source files here?
    let loadedFile = DependenciesFile.ReadFromFile dependenciesFileName
    let dependenciesFile =
            let newPackages =
                loadedFile.Packages
                |> List.map (fun p ->
                    let v = p.VersionRequirement 
                    let requirement =
                        match strict,includingPrereleases with
                        | true,true -> VersionRequirement.NoRestriction
                        | true,false -> v
                        | false,true -> 
                            match v with
                            | VersionRequirement(v,_) -> VersionRequirement(v,PreReleaseStatus.All)
                        | false,false -> VersionRequirement.AllReleases
                    { p with VersionRequirement = requirement})

            DependenciesFile(loadedFile.FileName,loadedFile.Strict,newPackages,loadedFile.RemoteFiles)
            
    let resolution = dependenciesFile.Resolve(true) 
    let resolvedPackages = resolution.ResolvedPackages.GetModelOrFail()
    let lockFile = LockFile.LoadFrom(dependenciesFile.FindLockfile().FullName)

    [for kv in lockFile.ResolvedPackages do
        let package = kv.Value
        match resolvedPackages |> Map.tryFind package.Name with
        | Some newVersion -> 
            if package.Version <> newVersion.Version then 
                yield package.Name,package.Version,newVersion.Version        
        | _ -> ()]

/// Prints all outdated packages.
let ListOutdated(strict,includingPrereleases,packageFile) = 
    let allOutdated = FindOutdated(strict,includingPrereleases,packageFile)
    if allOutdated = [] then
        tracefn "No outdated packages found."
    else
        tracefn "Outdated packages found:"
        for name,oldVersion,newVersion in allOutdated do
            tracefn "  * %s %s -> %s" name (oldVersion.ToString()) (newVersion.ToString())