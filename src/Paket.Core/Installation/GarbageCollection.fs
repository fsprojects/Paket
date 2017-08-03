/// Contains methods for the garbage collection of no longer needed files.
module Paket.GarbageCollection

open Paket
open Paket.Domain
open Paket.Logging
open System.IO
open Pri.LongPath

type ExtractedPackage = {
    GroupName: GroupName
    PackageName: PackageName
    Path: DirectoryInfo
}

/// Discover all packages currently available in the packages folder
let discoverExtractedPackages root : ExtractedPackage list =
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
        groupDir.GetDirectories()
        |> Array.choose (packageInDir groupName)

    let packagesFolder = DirectoryInfo(Path.Combine(root, Constants.PackagesFolderName))
    [
        findGroupPackages Constants.MainDependencyGroup packagesFolder
        packagesFolder.GetDirectories() 
        |> Array.collect (fun dir -> findGroupPackages (GroupName dir.Name) dir)
    ] |> Array.concat |> List.ofArray

/// Remove all packages from the packages folder which are not part of the lock file.
let deleteUnusedPackages root (lockFile:LockFile) =
    let resolution = lockFile.GetGroupedResolution()

    for package in discoverExtractedPackages root do
        try
            if resolution |> Map.containsKey (package.GroupName, package.PackageName) |> not then
                if resolution |> Seq.exists (fun kv -> 
                                                    fst kv.Key = package.GroupName && 
                                                     (kv.Value.Name.ToString() + "." + kv.Value.Version.ToString() = package.PackageName.ToString() ||
                                                      kv.Value.Name.ToString() + "." + kv.Value.Version.Normalize() = package.PackageName.ToString())) |> not
                then
                    tracefn "Garbage collecting %O" package.Path
                    Utils.deleteDir package.Path
        with
        | exn -> traceWarnfn "Garbage collection on '%s' failed. %s." package.Path.FullName exn.Message

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
                Cache.setInaccessible cache
                false
    
        if cont then
            match cache.CacheType with
            | Some CacheType.CurrentVersion ->
                let fileNames =
                    versions
                    |> Seq.map (fun v -> packageName.ToString() + "." + v.Normalize() + ".nupkg" |> normalizePath)
                    |> Set.ofSeq

                targetFolder.EnumerateFiles(packageName.ToString() + ".*.nupkg")
                |> Seq.iter (fun fi ->            
                    if not <| fileNames.Contains(fi.Name |> normalizePath) then
                        fi.Delete())
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
let CleanUp(root, dependenciesFile:DependenciesFile, lockFile) =
    deleteUnusedPackages root lockFile

    cleanupCaches dependenciesFile lockFile