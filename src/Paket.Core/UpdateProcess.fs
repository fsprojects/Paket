/// Contains methods for the update process.
module Paket.UpdateProcess

open Paket
open System.IO
open Paket.Domain
open Paket.PackageResolver
open System.Collections.Generic
open Chessie.ErrorHandling

let addPackagesFromReferenceFiles projects (dependenciesFile:DependenciesFile) =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFile.FileName
    if not <| lockFileName.Exists then 
        dependenciesFile
    else
        let oldLockFile = LockFile.LoadFrom(lockFileName.FullName)
            
        let allExistingPackages =
            oldLockFile.ResolvedPackages
            |> Seq.map (fun d -> NormalizedPackageName d.Value.Name)
            |> Set.ofSeq

        let allReferencedPackages = 
            projects
            |> Seq.collect (fun (_,referencesFile) -> referencesFile.NugetPackages)

        let diff =
            allReferencedPackages
            |> Seq.filter (fun p ->
                NormalizedPackageName p.Name
                |> allExistingPackages.Contains
                |> not)

        if Seq.isEmpty diff then 
            dependenciesFile
        else
            let newDependenciesFile =
                diff
                |> Seq.fold (fun (dependenciesFile:DependenciesFile) dep -> 
                    if dependenciesFile.HasPackage dep.Name then 
                        dependenciesFile
                    else
                        dependenciesFile.AddAdditionalPackage(dep.Name,"",dep.Settings)) dependenciesFile
            newDependenciesFile.Save()
            newDependenciesFile

let SelectiveUpdate(dependenciesFile:DependenciesFile, exclude, force) =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFile.FileName

    let resolution =
        if not lockFileName.Exists then 
            dependenciesFile.Resolve(force)
        else
            let oldLockFile = LockFile.LoadFrom(lockFileName.FullName)
            let changedDependencies = DependencyChangeDetection.findChangesInDependenciesFile(dependenciesFile,oldLockFile)

            let changed =
                match exclude with
                | None -> changedDependencies
                | Some package -> Set.add package changedDependencies

            let dependenciesFile = DependencyChangeDetection.PinUnchangedDependencies dependenciesFile oldLockFile changed

            dependenciesFile.Resolve(force)
    LockFile.Create(lockFileName.FullName, dependenciesFile.Options, resolution.ResolvedPackages, resolution.ResolvedSourceFiles)

/// Smart install command
let SmartInstall(dependenciesFileName, exclude, force, hard, withBindingRedirects) = 
    let root = Path.GetDirectoryName dependenciesFileName
    let projects = InstallProcess.findAllReferencesFiles root |> returnOrFail
    let dependenciesFile = 
        DependenciesFile.ReadFromFile(dependenciesFileName)
        |> addPackagesFromReferenceFiles projects
        
    let lockFile = SelectiveUpdate(dependenciesFile,exclude,force)
     
    InstallProcess.InstallIntoProjects(
        dependenciesFile.GetAllPackageSources(),
        force,
        hard,
        withBindingRedirects,
        lockFile,
        projects)
        
/// Update a single package command
let UpdatePackage(dependenciesFileName, packageName : PackageName, newVersion, force, hard, withBindingRedirects) =  
    match newVersion with
    | Some v -> 
        DependenciesFile.ReadFromFile(dependenciesFileName)
            .UpdatePackageVersion(packageName, v)
            .Save()
    | None -> ()

    SmartInstall(dependenciesFileName,Some(NormalizedPackageName packageName),force,hard,withBindingRedirects)

/// Update command
let Update(dependenciesFileName, force, hard, withBindingRedirects) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    if lockFileName.Exists then
        lockFileName.Delete()
    
    SmartInstall(dependenciesFileName,None,force,hard,withBindingRedirects)