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

let updateWithModifiedDependenciesFile(dependenciesFile:DependenciesFile,package:string, force) =
    let lockFileName = DependenciesFile.FindLockfile Constants.DependenciesFile

    if not lockFileName.Exists then 
        let resolution = dependenciesFile.Resolve(force)
        let resolvedPackages = resolution.ResolvedPackages.GetModelOrFail()
        let lockFile = LockFile(lockFileName.FullName, dependenciesFile.Options, resolvedPackages, resolution.ResolvedSourceFiles)
        lockFile.Save()
        lockFile
    else
        let oldLockFile = LockFile.LoadFrom(lockFileName.FullName)
        let packageKeys = dependenciesFile.DirectDependencies |> Seq.map (fun kv -> kv.Key.ToLower()) |> Set.ofSeq

        let updatedDependenciesFile = 
            oldLockFile.ResolvedPackages 
            |> Seq.fold 
                    (fun (dependenciesFile : DependenciesFile) kv -> 
                        let resolvedPackage = kv.Value
                        let name = resolvedPackage.Name.ToLower()
                        if name = package.ToLower() || not <| packageKeys.Contains name then dependenciesFile else 
                        dependenciesFile.AddFixedPackage(resolvedPackage.Name, "== " + resolvedPackage.Version.ToString()))
                    dependenciesFile
        
        let resolution = updatedDependenciesFile.Resolve(force)
        let resolvedPackages = resolution.ResolvedPackages.GetModelOrFail()
        let newLockFile = 
            LockFile(lockFileName.FullName, updatedDependenciesFile.Options, resolvedPackages, oldLockFile.SourceFiles)
        newLockFile.Save()
        newLockFile


/// Update a single package command
let UpdatePackage(packageName : string, newVersion, force, hard) = 
    let lockFileName = DependenciesFile.FindLockfile Constants.DependenciesFile
    if not lockFileName.Exists then Update(true, force, hard) else
    
    let sources, lockFile = 
        let dependenciesFile = 
            let depFile = DependenciesFile.ReadFromFile Constants.DependenciesFile
            match newVersion with
            | Some newVersion -> 
                let depFile = depFile.UpdatePackageVersion(packageName, newVersion)
                depFile.Save()
                depFile
            | None -> depFile

        let oldLockFile = LockFile.LoadFrom(lockFileName.FullName)
        
        let updatedDependenciesFile = 
            oldLockFile.ResolvedPackages 
            |> Seq.fold 
                   (fun (dependenciesFile : DependenciesFile) kv -> 
                   let resolvedPackage = kv.Value
                   if resolvedPackage.Name.ToLower() = packageName.ToLower() then dependenciesFile
                   else 
                       dependenciesFile.AddFixedPackage
                           (resolvedPackage.Name, "== " + resolvedPackage.Version.ToString())) dependenciesFile
        
        let resolution = updatedDependenciesFile.Resolve(force)
        let resolvedPackages = resolution.ResolvedPackages.GetModelOrFail()
        let newLockFile = 
            LockFile(lockFileName.FullName, updatedDependenciesFile.Options, resolvedPackages, oldLockFile.SourceFiles)
        newLockFile.Save()
        updatedDependenciesFile.Sources, newLockFile
    InstallProcess.Install(sources, force, hard, lockFile)