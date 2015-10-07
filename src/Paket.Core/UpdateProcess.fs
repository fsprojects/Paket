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
    | UpdatePackage of  GroupName * PackageName
    | UpdateGroup of GroupName
    | Install
    | UpdateAll

let selectiveUpdate force getSha1 getSortedVersionsF getPackageDetailsF (lockFile:LockFile) (dependenciesFile:DependenciesFile) updateMode =
    let noAdditionalRequirements _ _ = None
    let resolve getVersionsF (dependenciesFile:DependenciesFile) g = dependenciesFile.Resolve(force, getSha1, getVersionsF, getPackageDetailsF, g)

    let allVersions = Dictionary<PackageName,SemVerInfo list>()
    let getSortedAndCachedVersionsF sources resolverStrategy groupName packageName =
        match allVersions.TryGetValue(packageName) with
        | false,_ ->
            let versions = 
                verbosefn "  - fetching versions for %O" packageName
                getSortedVersionsF sources resolverStrategy groupName packageName

            if Seq.isEmpty versions then
                failwithf "Couldn't retrieve versions for %O." packageName
            allVersions.Add(packageName,versions)
            versions
        | true,versions -> versions

    let getPreferredVersionsF preferredVersions sources resolverStrategy groupName packageName =
        let versions = getSortedAndCachedVersionsF sources resolverStrategy groupName packageName
                
        match preferredVersions |> Map.tryFind (groupName,packageName) with
        | Some v -> v :: versions
        | None -> versions

    let noPreferredVersions = Map.empty

    let getVersionsF,groupsToUpdate =
        match updateMode with
        | UpdateAll -> 
            let groups =
                dependenciesFile.Groups
                |> Map.map noAdditionalRequirements
            getSortedAndCachedVersionsF,groups
        | UpdateGroup groupName ->
            let groups =
                dependenciesFile.Groups
                |> Map.filter (fun k _ -> k = groupName)
                |> Map.map noAdditionalRequirements
            getSortedAndCachedVersionsF,groups
        | Install ->
            let changes = DependencyChangeDetection.findChangesInDependenciesFile(dependenciesFile,lockFile)

            let preferredVersions = DependencyChangeDetection.GetUnchangedDependenciesPins lockFile changes

            let groups =
                dependenciesFile.Groups
                |> Map.map noAdditionalRequirements

            (getPreferredVersionsF preferredVersions),groups
        | UpdatePackage(groupName,packageName) ->
            let changes =
                lockFile.GetAllNormalizedDependenciesOf(groupName,packageName)
                |> Set.ofSeq

            let preferredVersions = DependencyChangeDetection.GetUnchangedDependenciesPins lockFile changes

            let groups =
                dependenciesFile.Groups
                |> Map.filter (fun key _ -> key = groupName)
                |> Map.map (fun _ _ -> Some packageName)

            (getPreferredVersionsF preferredVersions),groups

    let resolution = dependenciesFile.Resolve(force, getSha1, getVersionsF, getPackageDetailsF, groupsToUpdate)

    let groups = 
        dependenciesFile.Groups
        |> Map.map (fun groupName dependenciesGroup -> 
                match resolution |> Map.tryFind groupName with
                | Some group ->
                    { Name = dependenciesGroup.Name
                      Options = dependenciesGroup.Options
                      Resolution = group.ResolvedPackages.GetModelOrFail()
                      RemoteFiles = group.ResolvedSourceFiles }
                | None -> lockFile.GetGroup groupName) // just copy from lockfile
    
    LockFile(lockFile.FileName, groups)

let SelectiveUpdate(dependenciesFile : DependenciesFile, updateMode, force) =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFile.FileName
    let oldLockFile,updateMode =
        if not lockFileName.Exists then
            LockFile.Parse(lockFileName.FullName, [||]),UpdateAll // Change updateMode to UpdateAll
        else
            LockFile.LoadFrom lockFileName.FullName,updateMode

    let getSha1 origin owner repo branch auth = RemoteDownload.getSHA1OfBranch origin owner repo branch auth |> Async.RunSynchronously
    let root = Path.GetDirectoryName dependenciesFile.FileName
    let inline getVersionsF sources resolverStrategy groupName packageName = 
        let versions = NuGetV2.GetVersions root (sources, packageName)
        match resolverStrategy with
        | ResolverStrategy.Max -> List.sortDescending versions
        | ResolverStrategy.Min -> List.sort versions

    let lockFile = 
        selectiveUpdate
            force 
            getSha1
            getVersionsF
            (NuGetV2.GetPackageDetails root force)
            oldLockFile 
            dependenciesFile 
            updateMode
    lockFile.Save()
    lockFile

/// Smart install command
let SmartInstall(dependenciesFile, updateMode, options : UpdaterOptions) =
    let lockFile = SelectiveUpdate(dependenciesFile, updateMode, options.Common.Force)

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

    SmartInstall(dependenciesFile, UpdatePackage(groupName,packageName), options)

/// Update a single group command
let UpdateGroup(dependenciesFileName, groupName,  options : UpdaterOptions) =
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)

    if not <| dependenciesFile.Groups.ContainsKey groupName then

        failwithf "Group %O was not found in paket.dependencies." groupName
    tracefn "Updating group %O in %s" groupName dependenciesFileName

    SmartInstall(dependenciesFile, UpdateGroup groupName, options)

/// Update command
let Update(dependenciesFileName, options : UpdaterOptions) =
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    
    SmartInstall(dependenciesFile, UpdateAll, options)
