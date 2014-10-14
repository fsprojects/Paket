/// Contains methods for the restore process.
module Paket.RestoreProcess

open Paket
open System.IO
open Paket.Logging
open Paket.ModuleResolver
open Paket.PackageResolver
open Paket.PackageSources
open FSharp.Control.AsyncExtensions

/// Retores the given packages from the lock file.
let internal restore(sources,force, lockFile:LockFile) = 
    let sourceFileDownloads =
        lockFile.SourceFiles
        |> Seq.map (fun file -> NugetDownload.DownloadSourceFile(Path.GetDirectoryName lockFile.FileName, file))        
        |> Async.Parallel

    let packageDownloads = 
        lockFile.ResolvedPackages
        |> Seq.map (fun kv -> NugetDownload.ExtractPackage(sources,force,kv.Value))
        |> Async.Parallel

    Async.Parallel(sourceFileDownloads,packageDownloads) 

let Restore(force) = 
    let lockFileName = DependenciesFile.FindLockfile Constants.DependenciesFile
    
    let sources, lockFile = 
        if not lockFileName.Exists then 
            failwithf "paket.lock doesn't exist."
        else 
            let sources = 
                Constants.DependenciesFile
                |> File.ReadAllLines
                |> PackageSourceParser.getSources
            sources, LockFile.LoadFrom(lockFileName.FullName)

    restore(sources, force, lockFile) 
    |> Async.RunSynchronously
    |> ignore

    UpdateProcess.Update(false,false,false)