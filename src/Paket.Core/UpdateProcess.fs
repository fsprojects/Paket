/// Contains methods for the update process.
module Paket.UpdateProcess

open Paket
open System.IO
open Paket.Domain
open Paket.PackageResolver
open System.Collections.Generic

/// Update command
let Update(dependenciesFileName, force, hard, withBindingRedirects) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    
    let sources, lockFile = 
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        
        let resolution = dependenciesFile.Resolve(force)
        let lockFile = LockFile.Create(lockFileName.FullName, dependenciesFile.Options, resolution.ResolvedPackages, resolution.ResolvedSourceFiles)
        dependenciesFile.Sources, lockFile

    InstallProcess.Install(sources, force, hard, withBindingRedirects, lockFile)


let private update lockFileName force (createDependenciesFile:LockFile -> DependenciesFile) =
    let oldLockFile = LockFile.LoadFrom(lockFileName)

    let dependenciesFile = createDependenciesFile oldLockFile

    let resolution = dependenciesFile.Resolve(force)
    let newLockFile = LockFile.Create(lockFileName, dependenciesFile.Options, resolution.ResolvedPackages, oldLockFile.SourceFiles)
    dependenciesFile.Sources, newLockFile


let SelectiveUpdate(dependenciesFile:DependenciesFile, force) =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFile.FileName

    if not lockFileName.Exists then 
        let resolution = dependenciesFile.Resolve(force)
        LockFile.Create(lockFileName.FullName, dependenciesFile.Options, resolution.ResolvedPackages, resolution.ResolvedSourceFiles)
    else
        DependencyChangeDetection.fixOldDependencies dependenciesFile
        |> update lockFileName.FullName force
        |> snd

/// Update a single package command
let UpdatePackage(dependenciesFileName, packageName : PackageName, newVersion, force, hard) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    if not lockFileName.Exists then Update(dependenciesFileName, force, hard, false) else
    
    let sources, lockFile = 
        let dependenciesFile = 
            let depFile = DependenciesFile.ReadFromFile dependenciesFileName
            match newVersion with
            | Some newVersion -> 
                let depFile = depFile.UpdatePackageVersion(packageName, newVersion)
                depFile.Save()
                depFile
            | None -> depFile

        DependencyChangeDetection.fixOldDependencies dependenciesFile
        |> update lockFileName.FullName force
    InstallProcess.Install(sources, force, hard, false, lockFile)
