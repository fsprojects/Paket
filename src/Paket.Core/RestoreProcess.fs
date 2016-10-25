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
open Chessie.ErrorHandling
open System.Reflection

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
    |> Seq.iter (fun cache -> 
        try
            NuGetV2.CopyToCache(cache,fileName,force)
        with
        | exn ->
            if verbose then
                traceWarnfn "Could not copy %s to cache %s" fileName cache.Location)

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
                    let normalizeFeedUrl s = (normalizeFeedUrl s).Replace("https://","http://")
                    let normalized = package.Source.Url |> normalizeFeedUrl
                    let source =
                        sources 
                        |> List.tryPick (fun source -> 
                                match source with
                                | NuGetV2 s when normalizeFeedUrl s.Url = normalized -> Some(source)
                                | NuGetV3 s when normalizeFeedUrl s.Url = normalized -> Some(source)
                                | _ -> None)

                    match source with
                    | None -> failwithf "The NuGet source %s for package %O was not found in the paket.dependencies file with sources %A" package.Source.Url package.Name sources
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

let findAllReferencesFiles root =
    let findRefFile (p:ProjectFile) =
        match p.FindReferencesFile() with
        | Some fileName -> 
                try
                    ok <| (p, ReferencesFile.FromFile fileName)
                with _ ->
                    fail <| ReferencesFileParseError (FileInfo fileName)
        | None ->
            let fileName = 
                let fi = FileInfo(p.FileName)
                Path.Combine(fi.Directory.FullName,Constants.ReferencesFile)

            ok <| (p, ReferencesFile.New fileName)

    ProjectFile.FindAllProjects root 
    |> Array.map findRefFile
    |> collect

let copiedElements = ref false

let extractElement root name =
    let a = Assembly.GetEntryAssembly()
    let s = a.GetManifestResourceStream(name)
    let fi = FileInfo a.FullName
    let targetFile = FileInfo(Path.Combine(root,".paket",name))
    if not targetFile.Directory.Exists then
        targetFile.Directory.Create()
    
    use fileStream = File.Create(targetFile.FullName)
    s.Seek(int64 0, SeekOrigin.Begin) |> ignore
    s.CopyTo(fileStream)
    targetFile.FullName

let extractBuildTask root =
    if !copiedElements then
        Path.Combine(root,".paket","Paket.Restore.targets")
    else
        extractElement root "PaketRestoreTask.dll" |> ignore    
        extractElement root "PaketRestoreTask.deps.json" |> ignore
    
        let result = extractElement root "Paket.Restore.targets"
        copiedElements := true
        result


let Restore(dependenciesFileName,projectFile,force,group,referencesFileNames,ignoreChecks,failOnChecks) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    let localFileName = DependenciesFile.FindLocalfile dependenciesFileName
    let root = lockFileName.Directory.FullName
    if not lockFileName.Exists then 
        failwithf "%s doesn't exist." lockFileName.FullName
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    let lockFile,localFile,hasLocalFile =
        let lockFile = LockFile.LoadFrom(lockFileName.FullName)
        if not localFileName.Exists then
            lockFile,LocalFile.empty,false
        else
            let localFile =
                LocalFile.readFile localFileName.FullName
                |> Chessie.ErrorHandling.Trial.returnOrFail
            LocalFile.overrideLockFile localFile lockFile,localFile,true

    if not hasLocalFile && not ignoreChecks then
        let hasAnyChanges,_,_,_ = DependencyChangeDetection.GetChanges(dependenciesFile,lockFile,false)

        let checkResponse = if failOnChecks then failwithf else traceWarnfn
        if hasAnyChanges then 
            checkResponse "paket.dependencies and paket.lock are out of sync in %s.%sPlease run 'paket install' or 'paket update' to recompute the paket.lock file." lockFileName.Directory.FullName Environment.NewLine

    let groups =
        match group with
        | None -> lockFile.Groups 
        | Some groupName -> 
            match lockFile.Groups |> Map.tryFind groupName with
            | None -> failwithf "The group %O was not found in the paket.lock file." groupName
            | Some group -> [groupName,group] |> Map.ofList

    let referencesFileNames =
        match projectFile with
        | Some projectFile ->
            let projectFile = ProjectFile.LoadFromFile projectFile
            let referencesFile =
                match projectFile.FindReferencesFile() with
                | Some fileName -> 
                        try
                            ReferencesFile.FromFile fileName
                        with _ ->
                            failwith ((ReferencesFileParseError (FileInfo fileName)).ToString())
                | None ->
                    let fileName = 
                        let fi = FileInfo(projectFile.FileName)
                        Path.Combine(fi.Directory.FullName,Constants.ReferencesFile)

                    ReferencesFile.New fileName

            let resolved = lockFile.GetGroupedResolution()        
            let list = System.Collections.Generic.List<_>()
            let fi = FileInfo projectFile.FileName
            let newFileName = FileInfo(Path.Combine(fi.Directory.FullName,"obj",fi.Name + ".references"))
            if not newFileName.Directory.Exists then
                newFileName.Directory.Create()

            for kv in groups do
                let hull = lockFile.GetPackageHull(kv.Key,referencesFile)

                for package in hull do
                    let _,packageName = package.Key
                    list.Add(packageName.ToString() + "," + resolved.[package.Key].Version.ToString())
                
            let output = String.Join(Environment.NewLine,list)
            if not newFileName.Exists || File.ReadAllText(newFileName.FullName) <> output then
                File.WriteAllText(newFileName.FullName,output)
                tracefn " - %s created" newFileName.FullName
            else
                tracefn " - %s already up-to-date" newFileName.FullName

            [referencesFile.FileName]
        | None -> referencesFileNames



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
            let overriden = 
                packages
                |> Set.filter (fun p -> LocalFile.overrides localFile (p,depFileGroup.Name))
            restore(root, kv.Key, depFileGroup.Sources, depFileGroup.Caches, force, lockFile, packages, overriden)
            |> Async.RunSynchronously
            |> ignore



    GarbageCollection.CleanUp(root, dependenciesFile, lockFile)
