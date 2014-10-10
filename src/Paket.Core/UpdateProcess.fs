/// Contains methods for the update process.
module Paket.UpdateProcess

open Paket
open System.IO

/// Update command
let Update(forceResolution, force, hard) = 
    let lockFileName = DependenciesFile.FindLockfile Constants.DependenciesFile
    
    let sources, lockFile = 
        if forceResolution || not lockFileName.Exists then 
            let dependenciesFile = DependenciesFile.ReadFromFile Constants.DependenciesFile
            let resolution = dependenciesFile.Resolve force
            let lockFile = 
                LockFile
                    (lockFileName.FullName, dependenciesFile.Options, resolution.ResolvedPackages.GetModelOrFail(), 
                     resolution.ResolvedSourceFiles)
            lockFile.Save()
            dependenciesFile.Sources, lockFile
        else 
            let sources = 
                Constants.DependenciesFile
                |> File.ReadAllLines
                |> PackageSourceParser.getSources
            sources, LockFile.LoadFrom(lockFileName.FullName)
    InstallProcess.Install(sources, force, hard, lockFile)