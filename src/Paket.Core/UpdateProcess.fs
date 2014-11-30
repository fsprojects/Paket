/// Contains methods for the update process.
module Paket.UpdateProcess

open Paket
open System.IO
open Paket.Domain

/// Update command
let Update(dependenciesFileName, forceResolution, force, hard) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    
    let sources, lockFile = 
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        if forceResolution || not lockFileName.Exists then 
            let resolution = dependenciesFile.Resolve(force)
            let lockFile = LockFile.Create(lockFileName.FullName, dependenciesFile.Options, resolution.ResolvedPackages, resolution.ResolvedSourceFiles)
            dependenciesFile.Sources, lockFile
        else 
            let sources = dependenciesFile.GetAllPackageSources()
            sources, LockFile.LoadFrom(lockFileName.FullName)

    InstallProcess.Install(sources, force, hard, lockFile)

let private fixOldDependencies (oldLockFile:LockFile) (dependenciesFile:DependenciesFile) (package:PackageName) =
    let packageKeys = dependenciesFile.DirectDependencies |> Seq.map (fun kv -> NormalizedPackageName kv.Key) |> Set.ofSeq
    oldLockFile.ResolvedPackages 
    |> Seq.fold 
            (fun (dependenciesFile : DependenciesFile) kv -> 
                let resolvedPackage = kv.Value
                let name = NormalizedPackageName resolvedPackage.Name
                if name = NormalizedPackageName package || not <| packageKeys.Contains name then dependenciesFile else 
                dependenciesFile.AddFixedPackage(resolvedPackage.Name, "= " + resolvedPackage.Version.ToString()))
            dependenciesFile

let updateWithModifiedDependenciesFile(dependenciesFile:DependenciesFile,packageName:PackageName, force) =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFile.FileName

    if not lockFileName.Exists then 
        let resolution = dependenciesFile.Resolve(force)
        LockFile.Create(lockFileName.FullName, dependenciesFile.Options, resolution.ResolvedPackages, resolution.ResolvedSourceFiles)
    else
        let oldLockFile = LockFile.LoadFrom(lockFileName.FullName)

        let updatedDependenciesFile = fixOldDependencies oldLockFile dependenciesFile packageName
        
        let resolution = updatedDependenciesFile.Resolve(force)
        LockFile.Create(lockFileName.FullName, updatedDependenciesFile.Options, resolution.ResolvedPackages, oldLockFile.SourceFiles)


/// Update a single package command
let UpdatePackage(dependenciesFileName, packageName : PackageName, newVersion, force, hard) = 
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
        let newLockFile = LockFile.Create(lockFileName.FullName, updatedDependenciesFile.Options, resolution.ResolvedPackages, oldLockFile.SourceFiles)
        updatedDependenciesFile.Sources, newLockFile
    InstallProcess.Install(sources, force, hard, lockFile)