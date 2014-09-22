/// Contains methods to find outdated packages.
module Paket.FindOutdated

open Paket.Logging

/// Finds all outdated packages.
let FindOutdated(dependenciesFileName) =     
    //TODO: Anything we need to do for source files here?
    let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
    let resolution = dependenciesFile.Resolve(true) |> UpdateProcess.getResolvedPackagesOrFail
    let lockFile = LockFile.LoadFrom(dependenciesFile.FindLockfile().FullName)

    [for kv in lockFile.ResolvedPackages do
        let package = kv.Value
        match resolution |> Map.tryFind package.Name with
        | Some newVersion -> 
            if package.Version <> newVersion.Version then 
                yield package.Name,package.Version,newVersion.Version        
        | _ -> ()]

/// Prints all outdated packages.
let ListOutdated(packageFile) = 
    let allOutdated = FindOutdated packageFile
    if allOutdated = [] then
        tracefn "No outdated packages found."
    else
        tracefn "Outdated packages found:"
        for name,oldVersion,newVersion in allOutdated do
            tracefn "  * %s %s -> %s" name (oldVersion.ToString()) (newVersion.ToString())