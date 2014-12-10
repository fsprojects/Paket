/// Contains methods for the restore process.
module Paket.RestoreProcess

open Paket
open System.IO
open Paket.Domain
open Paket.Logging
open Paket.PackageResolver
open FSharp.Polyfill

/// Retores the given packages from the lock file.
let internal restore(root, sources, force, lockFile:LockFile, packages:Set<NormalizedPackageName>) = 
    let sourceFileDownloads = RemoteDownload.DownloadSourceFiles(Path.GetDirectoryName lockFile.FileName, lockFile.SourceFiles)

    let packageDownloads = 
        lockFile.ResolvedPackages
        |> Map.filter (fun name _ -> packages.Contains name)
        |> Seq.map (fun kv -> InstallProcess.ExtractPackage(root,sources,force,kv.Value))
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
    
   
    if referencesFileNames = [] then 
        InstallProcess.Install(sources,force,false,false,lockFile)
    else
        let packages =
            referencesFileNames
            |> List.map (fun fileName ->
                ReferencesFile.FromFile fileName
                |> lockFile.GetPackageHull
                |> Seq.map NormalizedPackageName)
            |> Seq.concat

        restore(root, sources, force, lockFile,Set.ofSeq packages) 
        |> Async.RunSynchronously
        |> ignore