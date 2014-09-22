/// Contains methods to find outdated packages.
module Paket.FindOutdated

open Paket.Logging

/// Finds all outdated packages.
let FindOutdated(dependenciesFileName) =     
    //TODO: Anything we need to do for source files here?
    let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
    let resolution = DependencyResolution.Analyze(dependenciesFile,true)
    let lockFile = LockFile.LoadFrom(dependenciesFile.FindLockfile().FullName)

    let errors = LockFileSerializer.extractErrors resolution.PackageResolution

    if errors <> "" then
        traceError errors
        []
    else
        [for kv in lockFile.ResolvedPackages do
            let package = 
                match kv.Value with
                | Resolved p -> p
                | _ -> failwithf "Resolution failed for %s" kv.Key

            match resolution.PackageResolution.[package.Name] with
            | Resolved newVersion -> 
                if package.Version <> newVersion.Version then 
                    yield package.Name,package.Version,newVersion.Version        
            | _ -> () ]

/// Prints all outdated packages.
let ListOutdated(packageFile) = 
    let allOutdated = FindOutdated packageFile
    if allOutdated = [] then
        tracefn "No outdated packages found."
    else
        tracefn "Outdated packages found:"
        for name,oldVersion,newVersion in allOutdated do
            tracefn "  * %s %s -> %s" name (oldVersion.ToString()) (newVersion.ToString())