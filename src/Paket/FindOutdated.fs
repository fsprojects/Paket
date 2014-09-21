/// Contains methods to find outdated packages.
module Paket.FindOutdated

open Paket.Logging

/// Finds all outdated packages.
let FindOutdated(dependenciesFile) =     
    //TODO: Anything we need to do for source files here?    
    let resolution = DependencyResolution.Analyze(dependenciesFile,true)
    let lockFile = LockFile.LockFile.Parse(resolution.DependenciesFile.FindLockfile().FullName)

    let errors = LockFile.extractErrors resolution.PackageResolution

    if errors <> "" then
        traceError errors
        []
    else
        [for p in lockFile.ResolvedPackages do
            match resolution.PackageResolution.[p.Name] with
            | Resolved newVersion -> 
                if p.Version <> newVersion.Version then 
                    yield p.Name,p.Version,newVersion.Version        
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