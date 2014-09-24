/// Contains methods for the update process.
module Paket.UpdateProcess

open Paket
open System

let getResolvedPackagesOrFail resolution =
    match resolution with
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