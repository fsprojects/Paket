/// Contains methods for the update process.
module Paket.UpdateProcess

open Paket
open System.IO

/// Update command
let Update(dependenciesFileName, forceResolution, force, hard) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    
    let sources, lockFile = 
        if forceResolution || not lockFileName.Exists then 
            let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
            let resolution = dependenciesFile.Resolve(force)
            let lockFile = 
                LockFile
                    (lockFileName.FullName, dependenciesFile.Options, resolution.ResolvedPackages.GetModelOrFail(), 
                     resolution.ResolvedSourceFiles)
            lockFile.Save()
            dependenciesFile.Sources, lockFile
        else 
            let sources = DependenciesFile.ReadFromFile(dependenciesFileName).GetAllPackageSources()
            sources, LockFile.LoadFrom(lockFileName.FullName)

    InstallProcess.Install(sources, force, hard, lockFile)

let private fixOldDependencies (oldLockFile:LockFile) (dependenciesFile:DependenciesFile) (package:string) =
    let packageKeys = dependenciesFile.DirectDependencies |> Seq.map (fun kv -> kv.Key.ToLower()) |> Set.ofSeq
    oldLockFile.ResolvedPackages 
    |> Seq.fold 
            (fun (dependenciesFile : DependenciesFile) kv -> 
                let resolvedPackage = kv.Value
                let name = resolvedPackage.Name.ToLower()
                if name = package.ToLower() || not <| packageKeys.Contains name then dependenciesFile else 
                dependenciesFile.AddFixedPackage(resolvedPackage.Name, "= " + resolvedPackage.Version.ToString()))
            dependenciesFile

let updateWithModifiedDependenciesFile(dependenciesFile:DependenciesFile,packageName:string, force) =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFile.FileName

    if not lockFileName.Exists then 
        let resolution = dependenciesFile.Resolve(force)
        let resolvedPackages = resolution.ResolvedPackages.GetModelOrFail()
        let lockFile = LockFile(lockFileName.FullName, dependenciesFile.Options, resolvedPackages, resolution.ResolvedSourceFiles)
        lockFile.Save()
        lockFile
    else
        let oldLockFile = LockFile.LoadFrom(lockFileName.FullName)

        let updatedDependenciesFile = fixOldDependencies oldLockFile dependenciesFile packageName
        
        let resolution = updatedDependenciesFile.Resolve(force)
        let resolvedPackages = resolution.ResolvedPackages.GetModelOrFail()
        let newLockFile = 
            LockFile(lockFileName.FullName, updatedDependenciesFile.Options, resolvedPackages, oldLockFile.SourceFiles)
        newLockFile.Save()
        newLockFile


/// Update a single package command
let UpdatePackage(dependenciesFileName, packageName : string, newVersion, force, hard) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    if not lockFileName.Exists then Update(dependenciesFileName, true, force, hard) else
    
    let sources, lockFile = 
        let dependenciesFile = 
            let depFile = DependenciesFile.ReadFromFile dependenciesFileName
            match newVersion with
            | Some newVersion -> 
                let depFile = depFile.UpdatePackageVersion(packageName, newVersion)
                depFile.Save()
                depFile
            | None -> depFile

        let oldLockFile = LockFile.LoadFrom(lockFileName.FullName)
        
        let updatedDependenciesFile = fixOldDependencies oldLockFile dependenciesFile packageName
        
        let resolution = updatedDependenciesFile.Resolve(force)
        let resolvedPackages = resolution.ResolvedPackages.GetModelOrFail()
        let newLockFile = 
            LockFile(lockFileName.FullName, updatedDependenciesFile.Options, resolvedPackages, oldLockFile.SourceFiles)
        newLockFile.Save()
        updatedDependenciesFile.Sources, newLockFile
    InstallProcess.Install(sources, force, hard, lockFile)