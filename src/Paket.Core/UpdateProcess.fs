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
            let resolution = dependenciesFile.Resolve(force)
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

/// Update a single package command
let UpdatePackage(packageName : string, force, hard) = 
    let lockFileName = DependenciesFile.FindLockfile Constants.DependenciesFile
    
    let sources, lockFile = 
        let dependenciesFile = DependenciesFile.ReadFromFile Constants.DependenciesFile
        let oldLockFile = LockFile.LoadFrom(lockFileName.FullName)
        
        let updatedDependenciesFile = 
            oldLockFile.ResolvedPackages 
            |> Seq.fold 
                   (fun (dependenciesFile : DependenciesFile) kv -> 
                   let resolvedPackage = kv.Value
                   if resolvedPackage.Name.ToLower() = packageName.ToLower() then dependenciesFile
                   else 
                       dependenciesFile.AddAdditionionalPackage
                           (resolvedPackage.Name, "== " + resolvedPackage.Version.ToString())) dependenciesFile
        
        let resolution = updatedDependenciesFile.Resolve(force)
        let newLockFile = 
            LockFile
                (lockFileName.FullName, updatedDependenciesFile.Options, resolution.ResolvedPackages.GetModelOrFail(), 
                 oldLockFile.SourceFiles)
        newLockFile.Save()
        updatedDependenciesFile.Sources, newLockFile
    InstallProcess.Install(sources, force, hard, lockFile)