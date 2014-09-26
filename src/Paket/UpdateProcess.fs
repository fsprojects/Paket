/// Contains methods for the update process.
module Paket.UpdateProcess

open Paket
open System

let extractResolvedPackagesOrFail (resolvedPackages:ResolvedPackages) =
    match resolvedPackages with
    | Ok model -> model
    | Conflict(closed,stillOpen) ->

        let errorText = ref ""

        let addToError text = errorText := !errorText + Environment.NewLine + text

        addToError "Error in resolution." 
        addToError "  Resolved:"
        for x in closed do
           addToError <| sprintf "    - %s %s" x.Name (x.VersionRequirement.Range.ToString())

        addToError "  Still open:"
        for x in stillOpen do
           addToError <| sprintf  "    - %s %s" x.Name (x.VersionRequirement.Range.ToString())

        failwith !errorText

let getResolvedPackagesOrFail (resolution:Resolved) = extractResolvedPackagesOrFail resolution.ResolvedPackages


/// Update command
let Update(dependenciesFileName, forceResolution, force, hard) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    
    let lockFile = 
        if forceResolution || not lockFileName.Exists then 
            let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
            let resolution = dependenciesFile.Resolve force
            let lockFile = LockFile(lockFileName.FullName, dependenciesFile.Strict, getResolvedPackagesOrFail resolution, resolution.ResolvedSourceFiles)
            lockFile.Save()
            lockFile
        else LockFile.LoadFrom lockFileName.FullName
    InstallProcess.Install(force, hard, lockFile)