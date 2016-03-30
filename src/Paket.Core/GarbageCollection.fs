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
let discoverExtractedPackages root : ExtractedPackage list =
    let packageInDir groupName (dir:DirectoryInfo) =
        match dir.GetFiles("*.nuspec") with
        | [| nuspec |] ->
            Some {
                GroupName = groupName
                PackageName = PackageName (Path.GetFileNameWithoutExtension nuspec.Name)
                Path = dir
            }
        | _ -> None

    let findGroupPackages groupName (groupDir:DirectoryInfo) =
        groupDir.GetDirectories()
        |> Array.choose (packageInDir groupName)

    let packagesFolder = DirectoryInfo(Path.Combine(root, Constants.PackagesFolderName))
    [
        findGroupPackages Constants.MainDependencyGroup packagesFolder
        packagesFolder.GetDirectories() |> Array.collect (fun dir -> findGroupPackages (GroupName dir.Name) dir)
    ] |> Array.concat |> List.ofArray

/// Remove all packages from the packages folder which are not part of the lock file.
let deleteUnusedPackages root (lockFile:LockFile) =

    let resolutionKey package = package.GroupName, package.PackageName
    let delete package =
        try
            Utils.deleteDir package.Path
        with
        | exn -> traceWarnfn "Could not delete no longer needed directory '%s'. %s." package.Path.FullName exn.Message

    let resolutions = lockFile.GetGroupedResolution()

    discoverExtractedPackages root
    |> List.filter (fun p -> resolutions |> Map.containsKey (resolutionKey p) |> not)
    |> List.iter delete

/// Remove all packages from the packages folder which are not part of the lock file.
let CleanUp(root, dependenciesFile:DependenciesFile, lockFile) =
    deleteUnusedPackages root lockFile

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
            NuGetV2.RemoveOlderVersionsFromCache(cache,packageName,versions)
