/// Contains methods for the update process.
module Paket.UpdateProcess

open Paket

/// Update command
let Update(dependenciesFileName, forceResolution, force, hard) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    
    let lockFile = 
        if forceResolution || not lockFileName.Exists then 
            let resolution = DependencyResolution.Analyze(dependenciesFileName, force)
            let lockFile = LockFile(lockFileName.FullName, resolution.DependenciesFile.Strict, resolution)
            lockFile.Save()
            lockFile
        else DependenciesFile.ReadFromFile dependenciesFileName |> LockFile.LoadFrom
    InstallProcess.Install(force, hard, lockFile)