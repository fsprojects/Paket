/// Contains methods for the restore process.
module Paket.RestoreProcess

open Paket
open System.IO
open Paket.Domain
open Paket.Logging
open Paket.PackageResolver
open Paket.PackageSources
open FSharp.Polyfill

let private extractPackage package root auth source groupName version includeVersionInPath force =
    async {
        try 
            let! folder = NuGetV2.DownloadPackage(root, auth, source, groupName, package.Name, version, includeVersionInPath, force)
            return package, NuGetV2.GetLibFiles folder, NuGetV2.GetTargetsFiles folder, NuGetV2.GetAnalyzerFiles folder
        with _ when not force -> 
            tracefn "Something went wrong while downloading %O %A - Trying again." package.Name version
            let! folder = NuGetV2.DownloadPackage(root, auth, source, groupName, package.Name, version, includeVersionInPath, true)
            return package, NuGetV2.GetLibFiles folder, NuGetV2.GetTargetsFiles folder, NuGetV2.GetAnalyzerFiles folder
    }
/// Downloads and extracts a package.
let ExtractPackage(root, groupName, sources, force, package : ResolvedPackage) = 
    async { 
        let v = package.Version
        let includeVersionInPath = defaultArg package.Settings.IncludeVersionInPath false
        match package.Source with
        | Nuget _ | NugetV3 _ -> 
            let auth = 
                sources |> List.tryPick (fun s -> 
                               match s with
                               | Nuget s -> s.Authentication |> Option.map toBasicAuth
                               | _ -> None)
            let! result =
                extractPackage package root auth package.Source groupName v includeVersionInPath force 
            return result
        | LocalNuget path ->
            let path = Utils.normalizeLocalPath path
            let di = Utils.getDirectoryInfo path root
            let nupkg = NuGetV2.findLocalPackage di.FullName package.Name v

            let! folder = NuGetV2.CopyFromCache(root, groupName, nupkg.FullName, "", package.Name, v, includeVersionInPath, force)
            return package, NuGetV2.GetLibFiles folder, NuGetV2.GetTargetsFiles folder, NuGetV2.GetAnalyzerFiles folder
    }

/// Restores the given dependencies from the lock file.
let internal restore(root, groupName, sources, force, lockFile:LockFile, packages:Set<PackageName>) = 
    let sourceFileDownloads = 
        [| yield RemoteDownload.DownloadSourceFiles(Path.GetDirectoryName lockFile.FileName, groupName, force, lockFile.Groups.[groupName].RemoteFiles) |]
        |> Async.Parallel

    let packageDownloads = 
        lockFile.Groups.[groupName].Resolution
        |> Map.filter (fun name _ -> packages.Contains name)
        |> Seq.map (fun kv -> ExtractPackage(root,groupName,sources,force,kv.Value))
        |> Async.Parallel

    Async.Parallel(sourceFileDownloads,packageDownloads) 

let internal computePackageHull groupName (lockFile : LockFile) (referencesFileNames : string seq) =
    referencesFileNames
    |> Seq.map (fun fileName ->
        lockFile.GetPackageHull(groupName,ReferencesFile.FromFile fileName)
        |> Seq.map (fun p -> (snd p.Key)))
    |> Seq.concat

let Restore(dependenciesFileName,force,group,referencesFileNames) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    let root = lockFileName.Directory.FullName

    if not lockFileName.Exists then 
        failwithf "%s doesn't exist." lockFileName.FullName        

    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    let lockFile = LockFile.LoadFrom(lockFileName.FullName)
   
    let groups =
        match group with
        | None -> lockFile.Groups 
        | Some groupName -> 
            match lockFile.Groups |> Map.tryFind groupName with
            | None -> failwithf "The group %O was not found in the paket.lock file." groupName
            | Some group -> [groupName,group] |> Map.ofList

    groups
    |> Seq.map (fun kv -> 
        let packages = 
            if referencesFileNames = [] then 
                kv.Value.Resolution
                |> Seq.map (fun kv -> kv.Key) 
            else
                referencesFileNames
                |> List.toSeq
                |> computePackageHull kv.Key lockFile

        restore(root, kv.Key, dependenciesFile.Groups.[kv.Value.Name].Sources, force, lockFile,Set.ofSeq packages))
    |> Seq.toArray
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore