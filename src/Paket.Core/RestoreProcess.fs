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

// Find packages which would be affected by a restore, i.e. not extracted yet or with the wrong version
let FindPackagesNotExtractedYet(dependenciesFileName) =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    let lockFile = LockFile.LoadFrom(lockFileName.FullName)
    let root = lockFileName.Directory.FullName

    lockFile.GetGroupedResolution()
    |> Map.toList
    |> List.filter (fun ((group,package),resolved) -> NuGetV2.IsPackageVersionExtracted(root, group, package, resolved.Version, defaultArg resolved.Settings.IncludeVersionInPath false) |> not)
    |> List.map fst


let CopyToCaches force caches fileName =
    caches
    |> Seq.iter (fun cache -> NuGetV2.CopyToCache(cache,fileName,force))

let private extractPackage caches package root source groupName version includeVersionInPath force =
    let downloadAndExtract force detailed = async {
        let! fileName,folder = NuGetV2.DownloadPackage(root, source, caches, groupName, package.Name, version, includeVersionInPath, force, detailed)
        CopyToCaches force caches fileName
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
let ExtractPackage(root, groupName, sources, caches, force, package : ResolvedPackage, localOverride) = 
    async { 
        let v = package.Version
        let includeVersionInPath = defaultArg package.Settings.IncludeVersionInPath false
        let targetDir = getTargetFolder root groupName package.Name package.Version includeVersionInPath
        let overridenFile = FileInfo(Path.Combine(targetDir, "paket.overriden"))
        let force = if (localOverride || overridenFile.Exists) then true else force
        let! result = async {
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

                return! extractPackage caches package root source groupName v includeVersionInPath force
            | LocalNuGet(path,_) ->
                let path = Utils.normalizeLocalPath path
                let di = Utils.getDirectoryInfo path root
                let nupkg = NuGetV2.findLocalPackage di.FullName package.Name v

                CopyToCaches force caches nupkg.FullName

                let! folder = NuGetV2.CopyFromCache(root, groupName, nupkg.FullName, "", package.Name, v, includeVersionInPath, force, false)
                return package, NuGetV2.GetLibFiles folder, NuGetV2.GetTargetsFiles folder, NuGetV2.GetAnalyzerFiles folder
        }

        // manipulate overridenFile after package extraction
        match localOverride, overridenFile.Exists with
        | true , false -> overridenFile.Create().Dispose()
        | false, true  -> overridenFile.Delete()
        | true , true
        | false, false -> ()

        return result
    }

/// Restores the given dependencies from the lock file.
let internal restore (root, groupName, sources, caches, force, lockFile : LockFile, packages : Set<PackageName>, overriden : Set<PackageName>) = 
    async { 
        RemoteDownload.DownloadSourceFiles(Path.GetDirectoryName lockFile.FileName, groupName, force, lockFile.Groups.[groupName].RemoteFiles)
        let! _ = lockFile.Groups.[groupName].Resolution
                 |> Map.filter (fun name _ -> packages.Contains name)
                 |> Seq.map (fun kv -> ExtractPackage(root, groupName, sources, caches, force, kv.Value, Set.contains kv.Key overriden))
                 |> Async.Parallel
        return ()
    }

let internal computePackageHull groupName (lockFile : LockFile) (referencesFileNames : string seq) =
    referencesFileNames
    |> Seq.map (fun fileName ->
        lockFile.GetPackageHull(groupName,ReferencesFile.FromFile fileName)
        |> Seq.map (fun p -> (snd p.Key)))
    |> Seq.concat

let Restore(dependenciesFileName,force,group,referencesFileNames,ignoreChecks) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    let localFileName = DependenciesFile.FindLocalfile dependenciesFileName
    let root = lockFileName.Directory.FullName

    if not lockFileName.Exists then 
        failwithf "%s doesn't exist." lockFileName.FullName

    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    let localFile =
        if not localFileName.Exists then
            LocalFile.empty
        else
            LocalFile.readFile localFileName.FullName
            |> Chessie.ErrorHandling.Trial.returnOrFail
    let lockFile = 
        LockFile.LoadFrom(lockFileName.FullName)
        |> LocalFile.overrideLockFile localFile

    if not ignoreChecks then
        let hasAnyChanges,_,_,_ = DependencyChangeDetection.GetChanges(dependenciesFile,lockFile,false)

        if hasAnyChanges then 
            failwithf "paket.dependencies and paket.lock are out of sync in %s.%sPlease run 'paket install' or 'paket update' to recompute the paket.lock file." lockFileName.Directory.FullName Environment.NewLine

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

        match dependenciesFile.Groups |> Map.tryFind kv.Value.Name with
        | None ->
            failwithf 
                "The group %O was found in the %s file but not in the %s file. Please run \"paket install\" again." 
                kv.Value
                Constants.LockFileName
                Constants.DependenciesFileName
        | Some depFileGroup ->
            let packages = Set.ofSeq packages
            let overriden = Set.filter (LocalFile.overrides localFile) packages
            restore(root, kv.Key, depFileGroup.Sources, depFileGroup.Caches, force, lockFile, packages, overriden)
            |> Async.RunSynchronously
            |> ignore

    GarbageCollection.CleanUp(root, dependenciesFile, lockFile)
