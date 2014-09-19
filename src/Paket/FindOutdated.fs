/// Contains methods to find outdated packages.
module Paket.FindOutdated

open System.IO

/// Finds all outdated packages.
let FindOutdated(dependenciesFile) = 
    let lockFile = LockFile.findLockfile dependenciesFile

    //TODO: Anything we need to do for source files here?    
    let _,newPackages, _ = LockFile.Create(true, dependenciesFile)
    let lockFile  =
        if lockFile.Exists then LockFile.LockFile.Parse(File.ReadAllLines lockFile.FullName) else LockFile.LockFile(false,[],[])

    [for p in lockFile.ResolvedPackages do
        match newPackages.[p.Name] with
        | Resolved newVersion -> 
            if p.Version <> newVersion.Version then 
                yield p.Name,p.Version,newVersion.Version

        | Conflict(_) -> failwith "version conflict handling not implemented" ]

/// Prints all outdated packages.
let ListOutdated(packageFile) = 
    let allOutdated = FindOutdated packageFile
    if allOutdated = [] then
        tracefn "No outdated packages found."
    else
        tracefn "Outdated packages found:"
        for name,oldVersion,newVersion in allOutdated do
            tracefn "  * %s %s -> %s" name (oldVersion.ToString()) (newVersion.ToString())