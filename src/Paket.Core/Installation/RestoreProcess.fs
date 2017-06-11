/// Contains methods for the restore process.
module Paket.RestoreProcess

open Paket
open System.IO
open Paket.Domain
open Paket.Logging
open Paket.PackageResolver
open Paket.PackageSources
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
    |> List.filter (fun ((group,package),resolved) -> NuGetCache.IsPackageVersionExtracted(root, group, package, resolved.Version, defaultArg resolved.Settings.IncludeVersionInPath false) |> not)
    |> List.map fst


let CopyToCaches force caches fileName =
    caches
    |> Seq.iter (fun cache -> 
        try
            NuGetCache.CopyToCache(cache,fileName,force)
        with
        | exn ->
            if verbose then
                traceWarnfn "Could not copy %s to cache %s%s%s" fileName cache.Location Environment.NewLine exn.Message)

/// returns - package, libs files, props files, targets files, analyzers files
let private extractPackage caches package alternativeProjectRoot root source groupName version includeVersionInPath force =
    let downloadAndExtract force detailed = async {
        let! fileName,folder = NuGet.DownloadPackage(alternativeProjectRoot, root, source, caches, groupName, package.Name, version, includeVersionInPath, force, detailed)
        CopyToCaches force caches fileName
        return package, NuGet.GetLibFiles folder, NuGet.GetTargetsFiles (folder,package.Name) , NuGet.GetAnalyzerFiles folder
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
/// returns - package, libs files, props files, targets files, analyzers files
let ExtractPackage(alternativeProjectRoot, root, groupName, sources, caches, force, package : ResolvedPackage, localOverride) = 
    async { 
        let v = package.Version
        let includeVersionInPath = defaultArg package.Settings.IncludeVersionInPath false
        let targetDir = getTargetFolder root groupName package.Name package.Version includeVersionInPath
        let overridenFile = FileInfo(Path.Combine(targetDir, "paket.overriden"))
        let force = if (localOverride || overridenFile.Exists) then true else force
        let! result = async {
            // TODO: Cleanup - Download gets a source and should be able to handle LocalNuGet as well, so this is duplicated
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

                return! extractPackage caches package alternativeProjectRoot root source groupName v includeVersionInPath force
            | LocalNuGet(path,_) ->
                let path = Utils.normalizeLocalPath path
                let di = Utils.getDirectoryInfoForLocalNuGetFeed path alternativeProjectRoot root
                let nupkg = NuGetLocal.findLocalPackage di.FullName package.Name v

                CopyToCaches force caches nupkg.FullName

                let! folder = NuGetCache.CopyFromCache(root, groupName, nupkg.FullName, "", package.Name, v, includeVersionInPath, force, false)
                return package, NuGet.GetLibFiles folder, NuGet.GetTargetsFiles (folder,package.Name) , NuGet.GetAnalyzerFiles folder
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
let internal restore (alternativeProjectRoot, root, groupName, sources, caches, force, lockFile : LockFile, packages : Set<PackageName>, overriden : Set<PackageName>) = 
    async { 
        RemoteDownload.DownloadSourceFiles(Path.GetDirectoryName lockFile.FileName, groupName, force, lockFile.Groups.[groupName].RemoteFiles)
        let! _ = lockFile.Groups.[groupName].Resolution
                 |> Map.filter (fun name r -> packages.Contains name)
                 |> Seq.map (fun kv -> ExtractPackage(alternativeProjectRoot, root, groupName, sources, caches, force, kv.Value, Set.contains kv.Key overriden))
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
                Some(ok <| (p, ReferencesFile.FromFile fileName))
            with _ ->
                Some(fail <| (ReferencesFileParseError (FileInfo fileName)))
        | None ->
            None
            
    ProjectFile.FindAllProjects root
    |> Array.choose findRefFile
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
        let result = extractElement root "Paket.Restore.targets"
        copiedElements := true
        result

let CreateInstallModel(alternativeProjectRoot, root, groupName, sources, caches, force, package) =
    async {
        let! (package, files, targetsFiles, analyzerFiles) = ExtractPackage(alternativeProjectRoot, root, groupName, sources, caches, force, package, false)
        let nuspec = Nuspec.Load(root,groupName,package.Version,defaultArg package.Settings.IncludeVersionInPath false,package.Name)
        let targetsFiles = targetsFiles |> Array.toList
        let model = InstallModel.CreateFromLibs(package.Name, package.Version, package.Settings.FrameworkRestrictions |> Requirements.getExplicitRestriction, files, targetsFiles, analyzerFiles, nuspec)
        return (groupName,package.Name), (package,model)
    }

let createAlternativeNuGetConfig alternativeConfigFileName =
    let config = """<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
  </packageSources>
  <disabledPackageSources>
     <clear />
  </disabledPackageSources>
</configuration>"""
    File.WriteAllText(alternativeConfigFileName,config)

let Restore(dependenciesFileName,projectFile,force,group,referencesFileNames,ignoreChecks,failOnChecks,targetFramework: string option) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    let localFileName = DependenciesFile.FindLocalfile dependenciesFileName
    let root = lockFileName.Directory.FullName
    let alternativeProjectRoot = None
    if not lockFileName.Exists then 
        failwithf "%s doesn't exist." lockFileName.FullName
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)

    let targetFilter = 
        targetFramework
        |> Option.bind FrameworkDetection.Extract

    let lockFile,localFile,hasLocalFile =
        let lockFile = LockFile.LoadFrom(lockFileName.FullName)
        if not localFileName.Exists then
            lockFile,LocalFile.empty,false
        else
            let localFile =
                LocalFile.readFile localFileName.FullName
                |> returnOrFail
            LocalFile.overrideLockFile localFile lockFile,localFile,true

    if not hasLocalFile && not ignoreChecks then
        let hasAnyChanges,nugetChanges,remoteFilechanges,hasChanges = DependencyChangeDetection.GetChanges(dependenciesFile,lockFile,false)

        let checkResponse = if failOnChecks then failwithf else traceWarnfn
        if hasAnyChanges then 
            checkResponse "paket.dependencies and paket.lock are out of sync in %s.%sPlease run 'paket install' or 'paket update' to recompute the paket.lock file." lockFileName.Directory.FullName Environment.NewLine
            for (group, package, changes) in nugetChanges do
                traceWarnfn "Changes were detected for %s/%s" (group.ToString()) (package.ToString())
                for change in changes do
                     traceWarnfn "    - %A" change

    let groups =
        match group with
        | None -> lockFile.Groups 
        | Some groupName -> 
            match lockFile.Groups |> Map.tryFind groupName with
            | None -> failwithf "The group %O was not found in the paket.lock file." groupName
            | Some group -> [groupName,group] |> Map.ofList

    let resolved = lazy (lockFile.GetGroupedResolution())

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

            let list = System.Collections.Generic.List<_>()
            let fi = FileInfo projectFile.FileName
            let newFileName = FileInfo(Path.Combine(fi.Directory.FullName,"obj",fi.Name + ".references"))            
            let alternativeConfigFileName = FileInfo(Path.Combine(fi.Directory.FullName,"obj",fi.Name + ".NuGet.Config"))
            
            if not newFileName.Directory.Exists then
                newFileName.Directory.Create()

            createAlternativeNuGetConfig alternativeConfigFileName.FullName

            
            for kv in groups do
                let hull = lockFile.GetOrderedPackageHull(kv.Key,referencesFile)
                let depsGroup =
                    match dependenciesFile.Groups |> Map.tryFind kv.Key with
                    | Some group -> group
                    | None -> failwithf "Dependencies file '%s' does not contain group '%O' but it is used in '%s'" dependenciesFile.FileName kv.Key lockFile.FileName

                let allDirectPackages =
                    match referencesFile.Groups |> Map.tryFind kv.Key with
                    | Some g -> g.NugetPackages |> List.map (fun p -> p.Name) |> Set.ofList
                    | None -> Set.empty
                

                for (key,_,_) in hull do
                    let restore =
                        match targetFilter with
                        | None -> true
                        | Some target ->
                            let resolvedPackage = resolved.Force().[key]

                            match resolvedPackage.Settings.FrameworkRestrictions with
                            | Requirements.ExplicitRestriction restrictions ->
                                Requirements.isTargetMatchingRestrictions(restrictions, SinglePlatform target)
                            | _ -> true
                            
                    if restore then
                        let _,packageName = key
                        let direct = allDirectPackages.Contains packageName
                        let line =
                            packageName.ToString() + "," + 
                            resolved.Force().[key].Version.ToString() + "," + 
                            (if direct then "Direct" else "Transitive") + "," +
                            kv.Key.ToString()

                        list.Add line
                
            let output = String.Join(Environment.NewLine,list)
            if output = "" then
                if File.Exists(newFileName.FullName) then
                    File.Delete(newFileName.FullName)
            elif not newFileName.Exists || File.ReadAllText(newFileName.FullName) <> output then
                File.WriteAllText(newFileName.FullName,output)                
                tracefn " - %s created" newFileName.FullName
            else
                if verbose then
                    tracefn " - %s already up-to-date" newFileName.FullName

            [referencesFile.FileName]
        | None -> referencesFileNames

    for kv in groups do
        let allPackages = 
            if List.isEmpty referencesFileNames then 
                kv.Value.Resolution
                |> Seq.map (fun kv -> kv.Key) 
            else
                referencesFileNames
                |> List.toSeq
                |> computePackageHull kv.Key lockFile

        let packages =
            allPackages
            |> Seq.filter (fun p ->
                match targetFilter with
                | None -> true
                | Some target ->
                    let key = kv.Key,p
                    let resolvedPackage = resolved.Force().[key]

                    match resolvedPackage.Settings.FrameworkRestrictions with
                    | Requirements.ExplicitRestriction restrictions ->
                        Requirements.isTargetMatchingRestrictions(restrictions, SinglePlatform target)
                    | _ -> true)
 

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

            restore(alternativeProjectRoot, root, kv.Key, depFileGroup.Sources, depFileGroup.Caches, force, lockFile, packages, overriden)
            |> Async.RunSynchronously
            |> ignore


    let groupsToGenerate =
        dependenciesFile.Groups 
        |> Seq.map (fun kvp -> kvp.Value)
        |> Seq.filter (fun g -> g.Options.Settings.GenerateLoadScripts = Some true)
        |> Seq.map (fun g -> g.Name)
        |> Seq.toList

    if groupsToGenerate <> [] then
        let depsCache = DependencyCache(dependenciesFile,lockFile) 
        (LoadingScripts.ScriptGeneration.constructScriptsFromData depsCache groupsToGenerate [] [])
        |> Seq.iter (fun sd -> sd.Save (DirectoryInfo dependenciesFile.Directory))
    
    if targetFramework <> None then
        GarbageCollection.CleanUp(root, dependenciesFile, lockFile)
