/// Contains methods for the update process.
module Paket.UpdateProcess

open Paket
open System.IO
open Paket.Domain
open Paket.PackageResolver
open System.Collections.Generic
open Chessie.ErrorHandling
open Paket.Logging

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
    let resolve (dependenciesFile : DependenciesFile) packages =
        match package with
        | None -> dependenciesFile.Groups
        | Some (groupName,_) -> dependenciesFile.Groups |> Map.filter (fun g _ -> g = groupName)
        |> Map.map (fun groupName group ->
            { Name = group.Name
              RemoteFiles = group.RemoteFiles
              RootDependencies = packages
              FrameworkRestrictions = group.Options.Settings.FrameworkRestrictions
              PackageRequirements = 
                match package with
                | Some(currentGroup,packageName) when groupName = currentGroup -> 
                    match lockFile.Groups |> Map.tryFind groupName with
                    | None -> []
                    | Some group -> group.Resolution |> createPackageRequirements [packageName]
                | _ -> [] })
        |> resolve dependenciesFile

    let selectiveUpdate (group : LockFileGroup) package =
        let selectiveResolution : Map<GroupName,Resolved> =
            dependenciesFile.GetGroup(group.Name).Packages
            |> List.filter (fun p -> package = p.Name)
            |> Some
            |> resolve dependenciesFile

        let merge destination source = 
            Map.fold (fun acc key value -> Map.add key value acc) destination source

        let resolution =
            let resolvedPackages = 
                (selectiveResolution |> Map.find group.Name).ResolvedPackages.GetModelOrFail()
                |> merge group.Resolution

            let dependencies = 
                resolvedPackages
                |> Seq.map (fun d -> d.Value.Dependencies |> Seq.map (fun (n,_,_) -> n))
                |> Seq.concat
                |> Set.ofSeq

            let isDirectDependency package = 
                dependenciesFile.GetDependenciesInGroup(group.Name)
                |> Map.exists (fun p _ -> p = package)

            let isTransitiveDependency package =
                dependencies
                |> Set.exists (fun p -> p = package)

            resolvedPackages
            |> Map.filter (fun p _ -> isDirectDependency p || isTransitiveDependency p)

        { ResolvedPackages = Resolution.Ok resolution
          ResolvedSourceFiles = group.RemoteFiles }

    let resolution =
        match UpdateMode.Mode updateAll package with
        | UpdateAll -> resolve dependenciesFile None
        | Install ->
            let changes = DependencyChangeDetection.findChangesInDependenciesFile(dependenciesFile,lockFile)
            let dependenciesFile = 
                changes
                |> DependencyChangeDetection.PinUnchangedDependencies dependenciesFile lockFile
            resolve dependenciesFile None
        | SelectiveUpdate(groupName,package) -> 
            let lockFileGroup = 
                lockFile.Groups
                |> Map.filter (fun g _ -> g = groupName)
                |> Seq.map (fun kv -> kv.Value)
                |> Seq.tryHead
            
            let lockFileGroup =
                match lockFileGroup with
                | Some g -> g
                | None ->
                    { Name = groupName
                      Options = InstallOptions.Default
                      Resolution = Map.empty
                      RemoteFiles = List.empty }

            lockFile.Groups
            |> Map.map (fun _ group ->
                { ResolvedPackages = Resolution.Ok group.Resolution
                  ResolvedSourceFiles = group.RemoteFiles })
            |> Map.add groupName (selectiveUpdate lockFileGroup package)

    let groups = 
        resolution
        |> Map.map (fun groupName group -> 
                let dependenciesGroup = dependenciesFile.GetGroup groupName
                { Name = dependenciesGroup.Name
                  Options = dependenciesGroup.Options
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

    let getSha1 origin owner repo branch auth = RemoteDownload.getSHA1OfBranch origin owner repo branch auth |> Async.RunSynchronously
    let root = Path.GetDirectoryName dependenciesFile.FileName

    let lockFile = selectiveUpdate (fun d g -> d.Resolve(force, getSha1,(fun (x,y,_) -> NuGetV2.GetVersions root (x,y)) |> getVersion,NuGetV2.GetPackageDetails root force,g)) oldLockFile dependenciesFile updateAll exclude
    lockFile.Save()
    lockFile

/// Smart install command
let SmartInstall(dependenciesFile, updateAll, exclude, options : UpdaterOptions) =
    let lockFile = SelectiveUpdate(dependenciesFile, updateAll, exclude, options.Common.Force)

    let root = Path.GetDirectoryName dependenciesFile.FileName
    let projects = InstallProcess.findAllReferencesFiles root |> returnOrFail

    if not options.NoInstall then
        InstallProcess.InstallIntoProjects(options.Common, dependenciesFile, lockFile, projects)

/// Update a single package command
let UpdatePackage(dependenciesFileName, groupName, packageName : PackageName, newVersion, options : UpdaterOptions) =
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)

    if not <| dependenciesFile.HasPackage(groupName, packageName) then
        failwithf "Package %O was not found in paket.dependencies in group %O." packageName groupName

    let dependenciesFile =
        match newVersion with
        | Some v -> dependenciesFile.UpdatePackageVersion(groupName,packageName, v)
        | None -> 
            tracefn "Updating %O in %s group %O" packageName dependenciesFileName groupName
            dependenciesFile

    SmartInstall(dependenciesFile, false, Some(groupName,packageName), options)

/// Update command
let Update(dependenciesFileName, options : UpdaterOptions) =
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    
    SmartInstall(dependenciesFile, true, None, options)
