/// Contains methods for the update process.
module Paket.UpdateProcess

open Paket
open System.IO
open Paket.Domain
open Paket.PackageResolver
open System.Collections.Generic

let SelectiveUpdate(dependenciesFile:DependenciesFile, force) =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFile.FileName

    if not lockFileName.Exists then 
        let resolution = dependenciesFile.Resolve(force)
        LockFile.Create(lockFileName.FullName, dependenciesFile.Options, resolution.ResolvedPackages, resolution.ResolvedSourceFiles)
    else
        let oldLockFile = LockFile.LoadFrom(lockFileName.FullName)

        let dependenciesFile = DependencyChangeDetection.FixUnchangedDependencies dependenciesFile oldLockFile

        let resolution = dependenciesFile.Resolve(force)
        LockFile.Create(lockFileName.FullName, dependenciesFile.Options, resolution.ResolvedPackages, oldLockFile.SourceFiles)

/// Smart install command
let SmartInstall(dependenciesFileName, force, hard, withBindingRedirects) = 
    let dependenciesFile = 
        let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)

        let lockFileName = DependenciesFile.FindLockfile dependenciesFile.FileName
        if not <| lockFileName.Exists then 
            dependenciesFile
        else
            let oldLockFile = LockFile.LoadFrom(lockFileName.FullName)
            
            let allExistingPackages =
                oldLockFile.ResolvedPackages
                |> Seq.map (fun d -> d.Key)
                |> Set.ofSeq

            let allReferencedPackages = 
                InstallProcess.findAllReferencesFiles(Path.GetDirectoryName dependenciesFileName)
                |> Seq.collect (fun (_,referencesFile) -> referencesFile.NugetPackages)
                |> Seq.map NormalizedPackageName
                |> Set.ofSeq

            let diff = Set.difference allReferencedPackages allExistingPackages
            if Set.isEmpty diff then 
                dependenciesFile
            else

                let newDependenciesFile =
                    diff
                    |> Seq.fold (fun (dependenciesFile:DependenciesFile) dep -> dependenciesFile.Add dep) dependenciesFile
                newDependenciesFile.Save()
                newDependenciesFile

    let lockFile = SelectiveUpdate(dependenciesFile,force)
    
    let sources = dependenciesFile.GetAllPackageSources()
    InstallProcess.Install(sources, force, hard, withBindingRedirects, lockFile)
        
/// Update a single package command
let UpdatePackage(dependenciesFileName, packageName : PackageName, newVersion, force, hard, withBindingRedirects) =  
    match newVersion with
    | Some v -> 
        DependenciesFile.ReadFromFile(dependenciesFileName)
            .UpdatePackageVersion(packageName, v)
            .Save()
    | None -> ()

    SmartInstall(dependenciesFileName,force,hard,withBindingRedirects)

/// Update command
let Update(dependenciesFileName, force, hard, withBindingRedirects) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    if lockFileName.Exists then
        lockFileName.Delete()
    
    SmartInstall(dependenciesFileName,force,hard,withBindingRedirects)