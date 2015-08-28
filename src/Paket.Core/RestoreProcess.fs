/// Contains methods for the restore process.
module Paket.RestoreProcess

open Paket
open System.IO
open Paket.Domain
open Paket.Logging
open Paket.PackageResolver
open Paket.PackageSources
open FSharp.Polyfill
open System

/// Downloads and extracts a package.
let ExtractPackage(root, groupName, sources, force, package : ResolvedPackage) = 
    async { 
        let (PackageName name) = package.Name
        let v = package.Version
        let includeVersionInPath = defaultArg package.Settings.IncludeVersionInPath false
        match package.Source with
        | Nuget source -> 
            let auth = 
                sources |> List.tryPick (fun s -> 
                               match s with
                               | Nuget s -> s.Authentication |> Option.map toBasicAuth
                               | _ -> None)
            try 
                let! folder = NuGetV2.DownloadPackage(root, auth, source.Url, groupName, name, v, includeVersionInPath, force)
                return package, NuGetV2.GetLibFiles folder, NuGetV2.GetTargetsFiles folder
            with _ when not force -> 
                tracefn "Something went wrong with the download of %s %A - automatic retry with --force." name v
                let! folder = NuGetV2.DownloadPackage(root, auth, source.Url, groupName, name, v, includeVersionInPath, true)
                return package, NuGetV2.GetLibFiles folder, NuGetV2.GetTargetsFiles folder
        | LocalNuget path ->         
            let path = Utils.normalizeLocalPath path
            let di = Utils.getDirectoryInfo path root
            let nupkg = NuGetV2.findLocalPackage di.FullName name v

            let! folder = NuGetV2.CopyFromCache(root, groupName, nupkg.FullName, "", name, v, includeVersionInPath, force) // TODO: Restore license
            return package, NuGetV2.GetLibFiles folder, NuGetV2.GetTargetsFiles folder
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
        | Some g -> [g,lockFile.Groups.[g]] |> Map.ofList            

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