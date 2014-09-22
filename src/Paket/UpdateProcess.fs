/// Contains methods for the update process.
module Paket.UpdateProcess

open Paket
open Paket.Logging

let getResolvedPackagesOrFail resolution =
    match resolution with
    | Ok model -> model
    | Conflict(closed,stillOpen) -> 
        traceErrorfn "Resolved:"
        for x in closed do
           traceErrorfn  "  - %s %s" x.Name (x.VersionRange.ToString())

        traceErrorfn "Still open:"
        for x in stillOpen do
           traceErrorfn  "  - %s %s" x.Name (x.VersionRange.ToString())

        failwithf "Error in resolution." 


/// Update command
let Update(dependenciesFileName, forceResolution, force, hard) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    
    let lockFile = 
        if forceResolution || not lockFileName.Exists then 
            let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
            let resolution = dependenciesFile.Resolve force |> getResolvedPackagesOrFail
            let lockFile = LockFile(lockFileName.FullName, dependenciesFile.Strict, resolution, dependenciesFile.RemoteFiles)
            lockFile.Save()
            lockFile
        else LockFile.LoadFrom lockFileName.FullName
    InstallProcess.Install(force, hard, lockFile)