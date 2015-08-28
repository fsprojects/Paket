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
            LockFile.Create(lockFileName.FullName, dependenciesFile.Groups.[Constants.MainDependencyGroup].Options, Resolution.Ok(Map.empty), [])

    let allExistingPackages =
        oldLockFile.GetCompleteResolution()
        |> Seq.map (fun d -> d.Value.Name)
        |> Set.ofSeq

    let allReferencedPackages =
        projects
        |> Seq.collect (fun (_,referencesFile) -> referencesFile.NugetPackages)

    let diff =
        allReferencedPackages
        |> Seq.filter (fun p ->
            p.Name
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

let selectiveUpdate resolve (lockFile:LockFile) (dependenciesFile:DependenciesFile) updateAll package =
    let selectiveUpdate package =
        // TODO: this makes no sense at the moment - ask @mrinaldi
        let selectiveResolution : Map<GroupName,Resolved> = 
            dependenciesFile.Packages
            |> List.filter (fun p -> package = p.Name)
            |> Some
            |> resolve dependenciesFile            

        let merge destination source = 
            Map.fold (fun acc key value -> Map.add key value acc) destination source

        let resolution =    
            let resolvedPackages = 
                (selectiveResolution |> Map.find Constants.MainDependencyGroup).ResolvedPackages.GetModelOrFail()
                |> merge (lockFile.GetCompleteResolution())

            let dependencies = 
                resolvedPackages
                |> Seq.map (fun d -> d.Value.Dependencies |> Seq.map (fun (n,_,_) -> n))
                |> Seq.concat
                |> Set.ofSeq

            let isDirectDependency package = 
                dependenciesFile.GetDependenciesInGroup(Constants.MainDependencyGroup)
                |> Map.exists (fun p _ -> p = package)

            let isTransitiveDependency package =
                dependencies
                |> Set.exists (fun p -> p = package)

            resolvedPackages
            |> Map.filter (fun p _ -> isDirectDependency p || isTransitiveDependency p)

        { ResolvedPackages = Resolution.Ok(resolution)
          ResolvedSourceFiles = lockFile.Groups.[Constants.MainDependencyGroup].RemoteFiles }

    let resolution =
        if updateAll then
            resolve dependenciesFile None 
        else
            match package with
            | None -> 
                let dependenciesFile = 
                    DependencyChangeDetection.findChangesInDependenciesFile(dependenciesFile,lockFile)
                    |> DependencyChangeDetection.PinUnchangedDependencies dependenciesFile lockFile
                resolve dependenciesFile None
            | Some package -> [Constants.MainDependencyGroup,selectiveUpdate package] |> Map.ofList

    let groups = 
        resolution
        |> Map.map (fun groupName group -> 
                { Name = dependenciesFile.Groups.[groupName].Name
                  Options = dependenciesFile.Groups.[groupName].Options
                  Resolution = group.ResolvedPackages.GetModelOrFail()
                  RemoteFiles = group.ResolvedSourceFiles })
    
    LockFile(lockFile.FileName, groups)

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
            oldLockFile.GetCompleteResolution()
            |> createPackageRequirements [e]
        | None -> []

    let getSha1 origin owner repo branch = RemoteDownload.getSHA1OfBranch origin owner repo branch |> Async.RunSynchronously
    let root = Path.GetDirectoryName dependenciesFile.FileName
    let groups = 
        dependenciesFile.Groups
        |> Map.map (fun groupName group ->
            { Name = group.Name
              RemoteFiles = group.RemoteFiles
              RootDependencies = Some group.Packages
              FrameworkRestrictions = group.Options.Settings.FrameworkRestrictions
              PackageRequirements = requirements })  

    let lockFile = selectiveUpdate (fun d _ -> d.Resolve(getSha1,NuGetV2.GetVersions root,NuGetV2.GetPackageDetails root force,groups)) oldLockFile dependenciesFile updateAll exclude
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

    SmartInstall(dependenciesFile, false, Some packageName, options)

/// Update command
let Update(dependenciesFileName, options : UpdaterOptions) =
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    
    SmartInstall(dependenciesFile, true, None, options)
