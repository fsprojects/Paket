/// Contains methods for the restore process.
module Paket.RestoreProcess

open Paket
open System.IO
open Paket.Logging
open Paket.PackageResolver
open Paket.PackageSources
open FSharp.Polyfill

/// Downloads and extracts a package.
let ExtractPackage(root, sources, force, package : ResolvedPackage) = 
    async { 
        let v = package.Version
        match package.Source with
        | Nuget source -> 
            let auth = 
                sources |> List.tryPick (fun s -> 
                               match s with
                               | Nuget s -> s.Authentication |> Option.map toBasicAuth
                               | _ -> None)
            try 
                let! folder = NuGetV2.DownloadPackage(root, auth, source.Url, package.Name, v, force)
                return package, NuGetV2.GetLibFiles folder
            with _ when force = false -> 
                tracefn "Something went wrong with the download of %s %A - automatic retry with --force." package.Name v
                let! folder = NuGetV2.DownloadPackage(root, auth, source.Url, package.Name, v, true)
                return package, NuGetV2.GetLibFiles folder
        | LocalNuget path -> 
            let packageFile = Path.Combine(root, path, sprintf "%s.%A.nupkg" package.Name v)
            let! folder = NuGetV2.CopyFromCache(root, packageFile, package.Name, v, force)
            return package, NuGetV2.GetLibFiles folder
    }

/// Retores the given packages from the lock file.
let internal restore(root, sources, force, lockFile:LockFile, packages:Set<string>) = 
    let sourceFileDownloads =
        lockFile.SourceFiles
        |> Seq.map (fun file -> RemoteDownload.DownloadSourceFile(Path.GetDirectoryName lockFile.FileName, file))        
        |> Async.Parallel

    let packageDownloads = 
        lockFile.ResolvedPackages
        |> Map.filter (fun name _ -> packages.Contains(name.ToLower()))
        |> Seq.map (fun kv -> ExtractPackage(root,sources,force,kv.Value))
        |> Async.Parallel

    Async.Parallel(sourceFileDownloads,packageDownloads) 

let Restore(dependenciesFileName,force,referencesFileNames) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    let root = lockFileName.Directory.FullName
    
    let sources, lockFile = 
        if not lockFileName.Exists then 
            failwithf "paket.lock doesn't exist."
        else 
            let sources = DependenciesFile.ReadFromFile(dependenciesFileName).GetAllPackageSources()
            sources, LockFile.LoadFrom(lockFileName.FullName)
    
    let packages = 
        if referencesFileNames = [] then lockFile.ResolvedPackages |> Seq.map (fun kv -> kv.Key.ToLower()) else
        referencesFileNames
        |> List.map (fun fileName ->
            let referencesFile = ReferencesFile.FromFile fileName
            let references = lockFile.GetPackageHull(referencesFile)
            references |> Seq.map (fun kv -> kv.Key.ToLower()))
        |> Seq.concat

    restore(root, sources, force, lockFile,Set.ofSeq packages) 
    |> Async.RunSynchronously
    |> ignore