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
    let resolve (dependenciesFile : DependenciesFile) =
        dependenciesFile.Groups
        |> Map.map (fun groupName group ->
            { Name = group.Name
              RootDependencies = None
              PackageRequirements = 
                match package with
                | Some(currentGroup,packageName) when groupName = currentGroup -> 
                    match lockFile.Groups |> Map.tryFind groupName with
                    | None -> []
                    | Some group -> group.Resolution |> createPackageRequirements [packageName]
                | _ -> [] })
        |> resolve dependenciesFile

    let resolution =
        let dependenciesFile =
            match UpdateMode.Mode updateAll package with
            | UpdateAll -> dependenciesFile
            | Install ->
                DependencyChangeDetection.findChangesInDependenciesFile(dependenciesFile,lockFile)
                |> DependencyChangeDetection.PinUnchangedDependencies dependenciesFile lockFile
            | SelectiveUpdate(groupName,package) ->
                lockFile.GetAllNormalizedDependenciesOf(groupName,package)
                |> Set.ofSeq
                |> DependencyChangeDetection.PinUnchangedDependencies dependenciesFile lockFile
        resolve dependenciesFile

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
