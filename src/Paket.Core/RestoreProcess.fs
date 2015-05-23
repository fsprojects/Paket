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
let ExtractPackage(root, sources, force, package : ResolvedPackage) = 
    async { 
        let (PackageName name) = package.Name
        let v = package.Version
        match package.Source with
        | Nuget source -> 
            let auth = 
                sources |> List.tryPick (fun s -> 
                               match s with
                               | Nuget s -> s.Authentication |> Option.map toBasicAuth
                               | _ -> None)
            try 
                let! folder = NuGetV2.DownloadPackage(root, auth, source.Url, name, v, force)
                return package, NuGetV2.GetLibFiles folder, NuGetV2.GetTargetsFiles folder
            with _ when not force -> 
                tracefn "Something went wrong with the download of %s %A - automatic retry with --force." name v
                let! folder = NuGetV2.DownloadPackage(root, auth, source.Url, name, v, true)
                return package, NuGetV2.GetLibFiles folder, NuGetV2.GetTargetsFiles folder
        | LocalNuget path ->         
            let path = Utils.normalizeLocalPath path
            let di = Utils.getDirectoryInfo path root
            let nupkg = 
                let v1 = FileInfo(Path.Combine(di.FullName, sprintf "%s.%s.nupkg" name (v.ToString())))
                if v1.Exists then v1 else
                let normalizedVersion = v.Normalize()
                let v2 = FileInfo(Path.Combine(di.FullName, sprintf "%s.%s.nupkg" name normalizedVersion))
                if v2.Exists then v2 else

                let v3 =
                    Directory.EnumerateFiles(di.FullName,"*.nupkg",SearchOption.AllDirectories)
                    |> Seq.map (fun x -> FileInfo(x))
                    |> Seq.filter (fun fi -> fi.Name.ToLower().Contains(name.ToLower()))
                    |> Seq.filter (fun fi -> fi.Name.Contains(normalizedVersion) || fi.Name.Contains(v.ToString()))
                    |> Seq.firstOrDefault

                match v3 with
                | None -> failwithf "The package %s %s can't be found in %s.%sPlease check the feed definition in your paket.dependencies file." name (v.ToString()) di.FullName Environment.NewLine
                | Some x -> x

            let! folder = NuGetV2.CopyFromCache(root, nupkg.FullName, "", name, v, force) // TODO: Restore license
            return package, NuGetV2.GetLibFiles folder, NuGetV2.GetTargetsFiles folder
    }

/// Restores the given dependencies from the lock file.
let internal restore(root, sources, force, lockFile:LockFile, packages:Set<NormalizedPackageName>) = 
    let sourceFileDownloads = RemoteDownload.DownloadSourceFiles(Path.GetDirectoryName lockFile.FileName, lockFile.SourceFiles)

    let packageDownloads = 
        lockFile.ResolvedPackages
        |> Map.filter (fun name _ -> packages.Contains name)
        |> Seq.map (fun kv -> ExtractPackage(root,sources,force,kv.Value))
        |> Async.Parallel

    Async.Parallel(sourceFileDownloads,packageDownloads) 

let Restore(dependenciesFileName,force,referencesFileNames) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    let root = lockFileName.Directory.FullName
    
    let sources, lockFile = 
        if not lockFileName.Exists then 
            failwithf "%s doesn't exist." lockFileName.FullName
        else 
            let sources = DependenciesFile.ReadFromFile(dependenciesFileName).GetAllPackageSources()
            sources, LockFile.LoadFrom(lockFileName.FullName)
    
    let packages = 
        if referencesFileNames = [] then 
            lockFile.ResolvedPackages
            |> Seq.map (fun kv -> kv.Key) 
        else
            referencesFileNames
            |> List.map (fun fileName ->
                ReferencesFile.FromFile fileName
                |> lockFile.GetPackageHull
                |> Seq.map (fun p -> NormalizedPackageName p.Key))
            |> Seq.concat

    restore(root, sources, force, lockFile,Set.ofSeq packages) 
    |> Async.RunSynchronously
    |> ignore