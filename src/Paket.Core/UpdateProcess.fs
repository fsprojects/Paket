/// Contains methods for the update process.
module Paket.UpdateProcess

open Paket
open System.IO
open Paket.Domain
open Paket.PackageResolver
open System.Collections.Generic
open Chessie.ErrorHandling
open Paket.Logging

let addPackagesFromReferenceFiles projects (dependenciesFile : DependenciesFile) groupName =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFile.FileName
    let oldLockFile =
        if lockFileName.Exists then
            LockFile.LoadFrom(lockFileName.FullName)
        else
            LockFile.Create(lockFileName.FullName, dependenciesFile.Groups.[groupName].Options, Resolution.Ok(Map.empty), [])

    let allExistingPackages =
        oldLockFile.Groups.[groupName].Resolution
        |> Seq.map (fun d -> d.Value.Name)
        |> Set.ofSeq

    let allReferencedPackages =
        projects
        |> Seq.collect (fun (_,referencesFile) -> referencesFile.Groups.[groupName].NugetPackages)

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
                if dependenciesFile.HasPackage(groupName,dep.Name) then
                    dependenciesFile
                else
                    dependenciesFile.AddAdditionalPackage(groupName,dep.Name,"",dep.Settings)) dependenciesFile
        newDependenciesFile.Save()
        newDependenciesFile

type UpdateMode =
    | SelectiveUpdate of GroupName * PackageName
    | Install
    | UpdateAll

    static member Mode updateAll package =
        if updateAll then
            UpdateAll
        else
            match package with
            | None -> Install
            | Some(groupName,package) -> SelectiveUpdate(groupName,package)

let selectiveUpdate resolve (lockFile:LockFile) (dependenciesFile:DependenciesFile) updateAll package =
    let selectiveUpdate package =
        // TODO: this makes no sense at the moment - ask @mrinaldi
        let selectiveResolution : Map<GroupName,Resolved> = 
            dependenciesFile.Groups.[Constants.MainDependencyGroup].Packages
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
        match UpdateMode.Mode updateAll package with
        | UpdateAll -> resolve dependenciesFile None
        | Install ->
            let dependenciesFile = 
                DependencyChangeDetection.findChangesInDependenciesFile(dependenciesFile,lockFile)
                |> DependencyChangeDetection.PinUnchangedDependencies dependenciesFile lockFile
            resolve dependenciesFile None
        | SelectiveUpdate(groupName,package) -> [groupName,selectiveUpdate package] |> Map.ofList

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

    let skipVersions f (sources,packageName,vr) =
        match vr with
        | Specific v
        | OverrideAll v -> [v]
        | _ -> f (sources,packageName,vr)

    let getVersion f =
        match UpdateMode.Mode updateAll exclude with
        | UpdateAll -> f
        | SelectiveUpdate _ -> f
        | Install -> skipVersions f

    let getSha1 origin owner repo branch = RemoteDownload.getSHA1OfBranch origin owner repo branch |> Async.RunSynchronously
    let root = Path.GetDirectoryName dependenciesFile.FileName
    let groups (dependenciesFile : DependenciesFile) = 
        dependenciesFile.Groups
        |> Map.map (fun groupName group ->
            { Name = group.Name
              RemoteFiles = group.RemoteFiles
              RootDependencies = Some group.Packages
              FrameworkRestrictions = group.Options.Settings.FrameworkRestrictions
              PackageRequirements = 
                match exclude with
                | Some(currentGroup,packageName) when groupName = currentGroup -> 
                    oldLockFile.Groups.[groupName].Resolution
                    |> createPackageRequirements [packageName]
                | _ -> [] })  

    let lockFile = selectiveUpdate (fun d _ -> d.Resolve(getSha1,(fun (x,y,_) -> NuGetV2.GetVersions root (x,y)) |> getVersion,NuGetV2.GetPackageDetails root force,groups d)) oldLockFile dependenciesFile updateAll exclude
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
let UpdatePackage(dependenciesFileName, groupName, packageName : PackageName, newVersion, options : UpdaterOptions) =
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)

    if not <| dependenciesFile.HasPackage(Constants.MainDependencyGroup, packageName) then
        packageName
        |> string
        |> failwithf "Package %s was not found in paket.dependencies."

    let dependenciesFile =
        match newVersion with
        | Some v -> dependenciesFile.UpdatePackageVersion(groupName,packageName, v)
        | None -> 
            tracefn "Updating %s in %s group %O" (packageName.ToString()) dependenciesFileName groupName
            dependenciesFile

    SmartInstall(dependenciesFile, false, Some(groupName,packageName), options)

/// Update command
let Update(dependenciesFileName, options : UpdaterOptions) =
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    
    SmartInstall(dependenciesFile, true, None, options)
