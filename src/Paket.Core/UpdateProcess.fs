/// Contains methods for the update process.
module Paket.UpdateProcess

open Paket
open System.IO
open Paket.Domain
open Paket.PackageResolver
open System.Collections.Generic

/// Update command
let Update(dependenciesFileName, forceResolution, force, hard, withBindingRedirects) = 
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

    InstallProcess.Install(sources, force, hard, withBindingRedirects, lockFile)

let fixOldDependencies failOnMissingPackage (dependenciesFile:DependenciesFile) (package:PackageName) (oldLockFile:LockFile) =
    let allDependencies = 
        if failOnMissingPackage || oldLockFile.ResolvedPackages.ContainsKey(NormalizedPackageName package) then
            oldLockFile.GetAllNormalizedDependenciesOf package
        else
            Set.empty

    oldLockFile.ResolvedPackages
    |> Seq.map (fun kv -> kv.Value)
    |> Seq.filter (fun p -> not <| allDependencies.Contains(NormalizedPackageName p.Name))
    |> Seq.fold 
            (fun (dependenciesFile : DependenciesFile) resolvedPackage ->                 
                    dependenciesFile.AddFixedPackage(resolvedPackage.Name, "= " + resolvedPackage.Version.ToString()))
            dependenciesFile

let private update lockFileName force (createDependenciesFile:LockFile -> DependenciesFile) =
    let oldLockFile = LockFile.LoadFrom(lockFileName)

    let dependenciesFile = createDependenciesFile oldLockFile

    let resolution = dependenciesFile.Resolve(force)
    let newLockFile = LockFile.Create(lockFileName, dependenciesFile.Options, resolution.ResolvedPackages, oldLockFile.SourceFiles)
    dependenciesFile.Sources, newLockFile


let updateWithModifiedDependenciesFile(failOnMissingPackage,dependenciesFile:DependenciesFile,packageName:PackageName, force) =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFile.FileName

    if not lockFileName.Exists then 
        let resolution = dependenciesFile.Resolve(force)
        LockFile.Create(lockFileName.FullName, dependenciesFile.Options, resolution.ResolvedPackages, resolution.ResolvedSourceFiles)
    else
        fixOldDependencies failOnMissingPackage dependenciesFile packageName
        |> update lockFileName.FullName force
        |> snd

/// Update a single package command
let UpdatePackage(dependenciesFileName, packageName : PackageName, newVersion, force, hard) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    if not lockFileName.Exists then Update(dependenciesFileName, true, force, hard, false) else
    
    let sources, lockFile = 
        let dependenciesFile = 
            let depFile = DependenciesFile.ReadFromFile dependenciesFileName
            match newVersion with
            | Some newVersion -> 
                let depFile = depFile.UpdatePackageVersion(packageName, newVersion)
                depFile.Save()
                depFile
            | None -> depFile

        fixOldDependencies true dependenciesFile packageName
        |> update lockFileName.FullName force
    InstallProcess.Install(sources, force, hard, false, lockFile)
