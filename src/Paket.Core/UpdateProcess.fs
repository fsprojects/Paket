/// Contains methods for the update process.
module Paket.UpdateProcess

open Paket
open System.IO
open Paket.Domain
open Paket.PackageResolver
open System.Collections.Generic
open Chessie.ErrorHandling
open Paket.Logging

let addPackagesFromReferenceFiles projects (dependenciesFile : DependenciesFile) =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFile.FileName
    let oldLockFile =
        if lockFileName.Exists then
            LockFile.LoadFrom(lockFileName.FullName)
        else
            LockFile.Create(lockFileName.FullName, dependenciesFile.Options, Resolution.Ok(Map.empty), [])

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

let selectiveUpdate resolve lockFile dependenciesFile updateAll package =
    let install () =
        let changedDependencies = DependencyChangeDetection.findChangesInDependenciesFile(dependenciesFile,lockFile)
        let dependenciesFile = DependencyChangeDetection.PinUnchangedDependencies dependenciesFile lockFile changedDependencies
        resolve dependenciesFile None

    let selectiveUpdate package =
        let selectiveResolution = 
            dependenciesFile.Packages
            |> List.filter (fun p -> package = NormalizedPackageName p.Name)
            |> Some
            |> resolve dependenciesFile

        let merge destination source = 
            Map.fold (fun acc key value -> Map.add key value acc) destination source

        let resolution =    
            let resolvedPackages = 
                selectiveResolution.ResolvedPackages.GetModelOrFail()
                |> merge lockFile.ResolvedPackages

            let dependencies = 
                resolvedPackages
                |> Seq.map (fun d -> d.Value.Dependencies |> Seq.map (fun (n,_,_) -> n))
                |> Seq.concat
                |> Set.ofSeq

            let isDirectDependency package = 
                dependenciesFile.DirectDependencies
                |> Map.exists (fun p _ -> NormalizedPackageName p = package)

            let isTransitiveDependency package =
                dependencies
                |> Set.exists (fun p -> NormalizedPackageName p = package)

            resolvedPackages
            |> Map.filter (fun p _ -> isDirectDependency p || isTransitiveDependency p)

        { ResolvedPackages = Resolution.Ok(resolution); ResolvedSourceFiles = lockFile.SourceFiles }

    let resolution =
        if updateAll then
            resolve dependenciesFile None
        else
            match package with
            | None -> install ()
            | Some package -> selectiveUpdate package

    LockFile(lockFile.FileName, dependenciesFile.Options, resolution.ResolvedPackages.GetModelOrFail(), resolution.ResolvedSourceFiles)

let SelectiveUpdate(dependenciesFile : DependenciesFile, updateAll, exclude, force) =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFile.FileName
    let oldLockFile =
        if not lockFileName.Exists then
            LockFile.Parse(lockFileName.FullName, [||])
        else
            LockFile.LoadFrom lockFileName.FullName

    let requirements =
        match exclude with
        | Some e -> 
            oldLockFile.ResolvedPackages
            |> createPackageRequirements [e]
        | None -> []

    let lockFile = selectiveUpdate (fun d p -> d.Resolve(force, p, requirements)) oldLockFile dependenciesFile updateAll exclude
    lockFile.Save()
    lockFile

/// Smart install command
let SmartInstall(dependenciesFile, updateAll, exclude, options : UpdaterOptions) =
    let lockFile = SelectiveUpdate(dependenciesFile, updateAll, exclude, options.Common.Force)

    let root = Path.GetDirectoryName dependenciesFile.FileName
    let projects = InstallProcess.findAllReferencesFiles root |> returnOrFail

    if not options.NoInstall then
        InstallProcess.InstallIntoProjects(
            dependenciesFile.GetAllPackageSources(),
            options.Common, lockFile, projects)

/// Update a single package command
let UpdatePackage(dependenciesFileName, packageName : PackageName, newVersion, options : UpdaterOptions) =
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)

    if not <| dependenciesFile.HasPackage(packageName) then
        packageName
        |> string
        |> failwithf "Package %s was not found in paket.dependencies."

    let dependenciesFile =
        match newVersion with
        | Some v -> dependenciesFile.UpdatePackageVersion(packageName, v)
        | None -> 
            tracefn "Updating %s in %s" (packageName.ToString()) dependenciesFileName
            dependenciesFile

    SmartInstall(dependenciesFile, false, Some(NormalizedPackageName packageName), options)

/// Update command
let Update(dependenciesFileName, options : UpdaterOptions) =
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    
    SmartInstall(dependenciesFile, true, None, options)
