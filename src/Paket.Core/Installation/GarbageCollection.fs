/// Contains methods for the garbage collection of no longer needed files.
module Paket.GarbageCollection

open Paket
open Paket.Domain
open Paket.Logging
open System.IO

type ExtractedPackage = {
    GroupName: GroupName
    PackageName: PackageName
    Path: DirectoryInfo
}

/// Discover all packages currently available in the packages folder
let discoverDirectExtractedPackages groupName groupPackagesDirectory : ExtractedPackage list =
    let packageInDir groupName (dir:DirectoryInfo) =
        match dir.GetFiles("*.nuspec") with
        | [| nuspec |] ->
            Some {
                GroupName = groupName
                PackageName = PackageName (FileInfo(nuspec.FullName).Directory.Name)
                Path = dir
            }
        | _ -> None

    let findGroupPackages groupName (groupDir:DirectoryInfo) =
        if groupDir.Exists then
            groupDir.GetDirectories()
            |> Array.choose (packageInDir groupName)
        else [||]

    let packagesFolder = groupPackagesDirectory
    findGroupPackages groupName packagesFolder
    |> List.ofArray

/// Remove all packages from the packages folder which are not part of the lock file.
let deleteUnusedPackages (lockFile:LockFile) =
    let resolution = lockFile.GetGroupedResolution()

    let extractedPackages =
        lockFile.Groups
        |> Seq.collect (fun g ->
            let groupName = g.Key
            let defaultStorage = defaultArg g.Value.Options.Settings.StorageConfig PackagesFolderGroupConfig.Default
            g.Value.Resolution
            |> Seq.map (fun r -> defaultArg r.Value.Settings.StorageConfig defaultStorage)
            |> Seq.append [defaultStorage; PackagesFolderGroupConfig.DefaultPackagesFolder]
            |> Seq.map (fun storageOption -> groupName, storageOption))
        // always consider default packages folder for GC
        |> Seq.append [Constants.MainDependencyGroup, PackagesFolderGroupConfig.DefaultPackagesFolder]
        |> Seq.distinct
        |> Seq.collect (fun (groupName, storage) ->
            match storage.ResolveGroupDir lockFile.RootPath groupName with
            | Some path ->
                discoverDirectExtractedPackages groupName (DirectoryInfo path)
                |> List.map (fun p -> p, Some path)
            | None -> [])

    for package, groupDir in extractedPackages do
        try
            let containsKey = resolution |> Map.containsKey (package.GroupName, package.PackageName)
            // Version in PATH
            let findNormalized =
                lazy
                    resolution |> Seq.tryFind (fun kv ->
                        fst kv.Key = package.GroupName &&
                            (kv.Value.Name.ToString() + "." + kv.Value.Version.ToString() = package.PackageName.ToString() ||
                             kv.Value.Name.ToString() + "." + kv.Value.Version.Normalize() = package.PackageName.ToString()))
                        |> Option.map (fun kv -> kv.Value)
            if not containsKey && findNormalized.Value |> Option.isNone then
                tracefn "Garbage collecting %O" package.Path
                Utils.deleteDir package.Path
            else
                // might be in the resultion, but the storage path changed
                let group = lockFile.Groups.[package.GroupName]
                let packageInfo =
                    match findNormalized.Value with
                    | Some value -> value
                    | None -> group.GetPackage package.PackageName
                let storageConfig = packageInfo.Settings.StorageConfig
                let packageStorage = defaultArg storageConfig PackagesFolderGroupConfig.Default
                let resolvePack = packageStorage.ResolveGroupDir lockFile.RootPath package.GroupName
                let wrongDir = groupDir <> resolvePack
                let versionInPathSetting = packageInfo.Settings.IncludeVersionInPath.IsSome && packageInfo.Settings.IncludeVersionInPath.Value
                let noVersionInPath = containsKey && versionInPathSetting
                let versionInPath = findNormalized.Value.IsSome && not versionInPathSetting
                if wrongDir || noVersionInPath || versionInPath then
                    tracefn "Garbage collecting %O" package.Path
                    Utils.deleteDir package.Path

        with
        | exn ->
            traceWarnfn "Garbage collection on '%s' failed. %s." package.Path.FullName exn.Message
            if verbose then
                traceWarnfn "Exception: %O" exn

/// Removes older packages from the cache
let removeOlderVersionsFromCache(cache:Cache, packageName:PackageName, versions:SemVerInfo seq) =
    if Cache.isInaccessible cache then
        if verbose then
            verbosefn "Cache %s is inaccessible, skipping" cache.Location
    else
        let targetFolder = DirectoryInfo(cache.Location)
        let cont =
            try
                if not targetFolder.Exists then
                    targetFolder.Create()
                true
            with
            | exn ->
                traceWarnfn "Could not garbage collect cache: %s" exn.Message
                if verbose then
                    traceWarnfn "Exception: %O" exn
                Cache.setInaccessible cache
                false
    
        if cont then
            match cache.CacheType with
            | Some CacheType.CurrentVersion ->
                let fileNames =
                    versions
                    |> Seq.map (fun v -> NuGetCache.GetPackageFileName packageName v |> normalizePath)
                    |> Set.ofSeq

                for fi in targetFolder.EnumerateFiles(packageName.ToString() + ".*.nupkg") do
                    if not (fileNames.Contains(normalizePath fi.Name)) then
                        fi.Delete()
            | _ -> ()

let cleanupCaches (dependenciesFile:DependenciesFile) (lockFile:LockFile) =
    let allCaches = dependenciesFile.Groups |> Seq.collect (fun kv -> kv.Value.Caches) |> Seq.toList
    if List.isEmpty allCaches then () else
    let allPackages = 
        lockFile.Groups 
        |> Seq.collect (fun kv -> kv.Value.Resolution |> Seq.map (fun kv -> kv.Value)) 
        |> Seq.toList
        |> Seq.groupBy (fun p -> p.Name)

    for cache in allCaches do
        for packageName,versions in allPackages do
            let versions = versions |> Seq.map (fun v -> v.Version)
            removeOlderVersionsFromCache(cache,packageName,versions)


/// Remove all packages from the packages folder which are not part of the lock file.
let CleanUp(dependenciesFile:DependenciesFile, lockFile) =
    deleteUnusedPackages lockFile

    cleanupCaches dependenciesFile lockFile