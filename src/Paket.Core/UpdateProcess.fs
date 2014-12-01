/// Contains methods for the update process.
module Paket.UpdateProcess

open Paket
open System.IO
open Paket.Domain
open Paket.PackageResolver

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

let private fixOldDependencies (dependenciesFile:DependenciesFile) (package:PackageName) (oldLockFile:LockFile) =
    let allDependencies = oldLockFile.GetAllDependenciesOf package

    oldLockFile.ResolvedPackages
    |> Seq.map (fun kv -> kv.Value)
    |> Seq.filter (fun p -> not <| allDependencies.Contains p.Name)
    |> Seq.fold 
            (fun (dependenciesFile : DependenciesFile) resolvedPackage ->                 
                    dependenciesFile.AddFixedPackage(resolvedPackage.Name, "= " + resolvedPackage.Version.ToString()))
            dependenciesFile

let private update (lockFileName) force (createDependenciesFile:LockFile -> DependenciesFile) =
    let oldLockFile = LockFile.LoadFrom(lockFileName)

    let dependenciesFile = createDependenciesFile oldLockFile

    let resolution = dependenciesFile.Resolve(force)
    let newLockFile = LockFile.Create(lockFileName, dependenciesFile.Options, resolution.ResolvedPackages, oldLockFile.SourceFiles)
    dependenciesFile.Sources, newLockFile


let updateWithModifiedDependenciesFile(dependenciesFile:DependenciesFile,packageName:PackageName, force) =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFile.FileName

    if not lockFileName.Exists then 
        let resolution = dependenciesFile.Resolve(force)
        LockFile.Create(lockFileName.FullName, dependenciesFile.Options, resolution.ResolvedPackages, resolution.ResolvedSourceFiles)
    else
        fixOldDependencies dependenciesFile packageName
        |> update lockFileName.FullName force
        |> snd


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

        fixOldDependencies dependenciesFile packageName
        |> update lockFileName.FullName force
    InstallProcess.Install(sources, force, hard, lockFile)
