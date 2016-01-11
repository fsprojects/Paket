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

let private extractPackage package root source groupName version includeVersionInPath force =
    let downloadAndExtract force detailed = async {
        let! folder = NuGetV2.DownloadPackage(root, source, groupName, package.Name, version, includeVersionInPath, force, detailed)
        return package, NuGetV2.GetLibFiles folder, NuGetV2.GetTargetsFiles folder, NuGetV2.GetAnalyzerFiles folder
    }

    async {
        try 
            return! downloadAndExtract force false
        with exn -> 
            try
                tracefn "Something went wrong while downloading %O %A%sMessage: %s%s  ==> Trying again" 
                    package.Name version Environment.NewLine exn.Message Environment.NewLine
                return! downloadAndExtract true false
            with exn ->
                tracefn "Something went wrong while downloading %O %A%sMessage: %s%s  ==> Last trial" 
                    package.Name version Environment.NewLine exn.Message Environment.NewLine
                return! downloadAndExtract true true
    }

/// Downloads and extracts a package.
let ExtractPackage(root, groupName, sources, force, package : ResolvedPackage) = 
    async { 
        let v = package.Version
        let includeVersionInPath = defaultArg package.Settings.IncludeVersionInPath false
        match package.Source with
        | NuGetV2 _ | NuGetV3 _ -> 
            let source = 
                let normalized = package.Source.Url |> normalizeFeedUrl
                sources 
                    |> List.tryPick (fun source -> 
                            match source with
                            | NuGetV2 s when normalizeFeedUrl s.Url = normalized -> Some(source)
                            | NuGetV3 s when normalizeFeedUrl s.Url = normalized -> Some(source)
                            | _ -> None)
                |> function
                   | None -> failwithf "The NuGet source %s for package %O was not found in the paket.dependencies file" package.Source.Url package.Name
                   | Some s -> s 

            let! result = extractPackage package root source groupName v includeVersionInPath force 
            return result
        | LocalNuGet path ->
            let path = Utils.normalizeLocalPath path
            let di = Utils.getDirectoryInfo path root
            let nupkg = NuGetV2.findLocalPackage di.FullName package.Name v

            let! folder = NuGetV2.CopyFromCache(root, groupName, nupkg.FullName, "", package.Name, v, includeVersionInPath, force, false)
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

    for kv in groups do
        let packages = 
            if List.isEmpty referencesFileNames then 
                kv.Value.Resolution
                |> Seq.map (fun kv -> kv.Key) 
            else
                referencesFileNames
                |> List.toSeq
                |> computePackageHull kv.Key lockFile

        restore(root, kv.Key, dependenciesFile.Groups.[kv.Value.Name].Sources, force, lockFile,Set.ofSeq packages)
        |> Async.RunSynchronously
        |> ignore