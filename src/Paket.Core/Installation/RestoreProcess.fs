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

/// Finds packages which would be affected by a restore, i.e. not extracted yet or with the wrong version
let FindPackagesNotExtractedYet(dependenciesFileName) =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    let lockFile = LockFile.LoadFrom(lockFileName.FullName)
    let root = lockFileName.Directory.FullName

    lockFile.GetGroupedResolution()
    |> Map.toList
    |> List.filter (fun ((group,package),resolved) ->
        let packSetting = defaultArg resolved.Settings.StorageConfig PackagesFolderGroupConfig.Default
        let includeVersionInPath = defaultArg resolved.Settings.IncludeVersionInPath false
        let resolvedPath = packSetting.Resolve root group package resolved.Version includeVersionInPath
        NuGetCache.IsPackageVersionExtracted(resolvedPath, package, resolved.Version) |> not)
    |> List.map fst


let CopyToCaches force caches fileName =
    for cache in caches do
        try
            NuGetCache.CopyToCache(cache,fileName,force)
        with
        | exn ->
            if verbose then
                traceWarnfn "Could not copy %s to cache %s%s%s" fileName cache.Location Environment.NewLine exn.Message

/// returns - package, libs files, props files, targets files, analyzers files
let private extractPackage caches (package:PackageInfo) alternativeProjectRoot root isLocalOverride source groupName version includeVersionInPath downloadLicense force =
    let downloadAndExtract force detailed = async {
        let cfg = defaultArg package.Settings.StorageConfig PackagesFolderGroupConfig.Default

        let! fileName,folder = 
            NuGet.DownloadAndExtractPackage(
                alternativeProjectRoot, root, isLocalOverride, cfg, source, caches, groupName,
                package.Name, version, package.IsCliTool, includeVersionInPath, downloadLicense, force, detailed)

        CopyToCaches force caches fileName
        return package, NuGet.GetContent folder
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
let ExtractPackage(alternativeProjectRoot, root, groupName, sources, caches, force, package : PackageInfo, localOverride) = 
    async { 
        let storage = defaultArg package.Settings.StorageConfig PackagesFolderGroupConfig.Default
        let v = package.Version
        let includeVersionInPath = defaultArg package.Settings.IncludeVersionInPath false
        let downloadLicense = defaultArg package.Settings.LicenseDownload false
        let resolvedStorage = storage.Resolve root groupName package.Name package.Version includeVersionInPath

        let targetDir, overridenFile, force =
            match resolvedStorage.Path with
            | Some targetDir ->
                let overridenFile = FileInfo(Path.Combine(targetDir, "paket.overriden"))
                targetDir, overridenFile, (if (localOverride || overridenFile.Exists) then true else force)
            | None ->
                if localOverride then
                    failwithf "Local package override without local storage (global NuGet folder) is not supported at the moment."
                let targetDir = NuGetCache.GetTargetUserFolder package.Name package.Version
                let overridenFile = FileInfo(Path.Combine(targetDir, "paket.overriden"))
                targetDir, overridenFile, force

        let! result = async {
            // TODO: Cleanup - Download gets a source and should be able to handle LocalNuGet as well, so this is duplicated
            match package.Source with
            | NuGetV2 _ | NuGetV3 _ -> 
                let source = 
                    let normalizeFeedUrl s = (normalizeFeedUrl s).Replace("https://","http://")

                    let normalized = normalizeFeedUrl package.Source.Url
                    let source =
                        sources 
                        |> List.tryPick (fun source -> 
                            match source with
                            | NuGetV2 s when normalizeFeedUrl s.Url = normalized -> Some source
                            | NuGetV3 s when normalizeFeedUrl s.Url = normalized -> Some source
                            | _ -> None)

                    match source with
                    | None -> failwithf "The NuGet source %s for package %O was not found in the paket.dependencies file with sources %A" package.Source.Url package.Name sources
                    | Some s -> s 

                return! extractPackage caches package alternativeProjectRoot root localOverride source groupName v includeVersionInPath downloadLicense force

            | LocalNuGet(path,_) as source ->
                return! extractPackage caches package alternativeProjectRoot root localOverride source groupName v includeVersionInPath downloadLicense force
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
        let group = lockFile.Groups.[groupName]
        let! _ = 
            lockFile.Groups.[groupName].Resolution
            |> Map.filter (fun name _ -> packages.Contains name)
            |> Seq.map (fun kv -> ExtractPackage(alternativeProjectRoot, root, groupName, sources, caches, force, group.GetPackage kv.Key, Set.contains kv.Key overriden))
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
                Some(ok (p, ReferencesFile.FromFile fileName))
            with e ->
                Some(fail (ReferencesFileParseError (FileInfo fileName, e)))
        | None ->
            None
            
    ProjectFile.FindAllProjects root
    |> Array.choose findRefFile
    |> collect

let copiedElements = ref false

let extractElement root name =
    let a = Assembly.GetEntryAssembly()
    let s = a.GetManifestResourceStream name
    let targetFile = FileInfo(Path.Combine(root,".paket",name))
    if not targetFile.Directory.Exists then
        targetFile.Directory.Create()

    use sr = new StreamReader(s)
    
    s.Seek(int64 0, SeekOrigin.Begin) |> ignore
    s.Flush()
    let newContent = sr.ReadToEnd()
    let oldContent = 
        if targetFile.Exists then
            File.ReadAllText targetFile.FullName
        else
            ""
    if newContent <> oldContent then
        File.WriteAllText(targetFile.FullName,newContent)
    targetFile.FullName

let extractRestoreTargets root =
    if !copiedElements then
        Path.Combine(root,".paket","Paket.Restore.targets")
    else
        let result = extractElement root "Paket.Restore.targets"
        copiedElements := true
        result

let CreateInstallModel(alternativeProjectRoot, root, groupName, sources, caches, force, package) =
    async {
        let! (package, content) = ExtractPackage(alternativeProjectRoot, root, groupName, sources, caches, force, package, false)
        let model = 
                InstallModel.CreateFromContent(
                    package.Name, 
                    package.Version, 
                    Requirements.getExplicitRestriction package.Settings.FrameworkRestrictions, 
                    content.Force())
        return (groupName,package.Name), (package,model)
    }

let createAlternativeNuGetConfig (projectFile:FileInfo) =
    let alternativeConfigFileInfo = FileInfo(Path.Combine(projectFile.Directory.FullName,"obj",projectFile.Name + ".NuGet.Config"))
    if not alternativeConfigFileInfo.Directory.Exists then
        alternativeConfigFileInfo.Directory.Create()
    
    let config = """<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
  </packageSources>
  <disabledPackageSources>
     <clear />
  </disabledPackageSources>
</configuration>"""
    if not alternativeConfigFileInfo.Exists || File.ReadAllText(alternativeConfigFileInfo.FullName) <> config then 
        File.WriteAllText(alternativeConfigFileInfo.FullName,config) 
        if verbose then
            tracefn " - %s created" alternativeConfigFileInfo.FullName

let createPaketPropsFile (cliTools:ResolvedPackage seq) restoreSuccess (fileInfo:FileInfo) =
    let cliParts =
        if Seq.isEmpty cliTools then
            ""
        else
            cliTools
            |> Seq.map (fun cliTool -> sprintf """        <DotNetCliToolReference Include="%O" Version="%O" />""" cliTool.Name cliTool.Version)
            |> fun xs -> String.Join(Environment.NewLine,xs)
            |> fun s -> "    <ItemGroup>" + Environment.NewLine + s + Environment.NewLine + "    </ItemGroup>"

            
    let content = 
        sprintf """<?xml version="1.0" encoding="utf-8" standalone="no"?>
<Project ToolsVersion="14.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    <PropertyGroup>
    <MSBuildAllProjects>$(MSBuildAllProjects);$(MSBuildThisFileFullPath)</MSBuildAllProjects>
        %s
    </PropertyGroup>
%s
</Project>""" 
            (if restoreSuccess then 
                "<!-- <RestoreSuccess>False</RestoreSuccess> -->" 
             else 
                "<RestoreSuccess>False</RestoreSuccess>")
            cliParts

    if not fileInfo.Exists || File.ReadAllText(fileInfo.FullName) <> content then 
        File.WriteAllText(fileInfo.FullName,content)
        if verbose then
            tracefn " - %s created" fileInfo.FullName
    else
        if verbose then
            tracefn " - %s already up-to-date" fileInfo.FullName

let createProjectReferencesFiles (lockFile:LockFile) (projectFile:ProjectFile) (referencesFile:ReferencesFile) (resolved:Lazy<Map<GroupName*PackageName,PackageInfo>>) (groups:Map<GroupName,LockFileGroup>) =
    let projectFileInfo = FileInfo projectFile.FileName
    let hulls =
        groups
        |> Seq.map (fun kv ->
            let hull,cliToolsInGroup = lockFile.GetOrderedPackageHull(kv.Key,referencesFile)
            kv.Key, (hull, cliToolsInGroup))
        |> dict

    let targets =
        ProjectFile.getTargetFramework projectFile
        |> Option.toList
        |> List.append (ProjectFile.getTargetFrameworks projectFile |> Option.toList |> List.collect (fun item -> String.split [|';'|] item |> List.ofArray))
        |> List.map (fun s -> s, (PlatformMatching.forceExtractPlatforms s |> fun p -> p.ToTargetProfile true))
        |> List.choose (fun (s, c) -> c |> Option.map (fun d -> s, d))

    // delete stale entries (otherwise we might not recognize stale data on a change later)
    // scenario: remove a target framework -> change references -> add back target framework
    // -> We reached an invalid state
    let objDir = DirectoryInfo(Path.Combine(projectFileInfo.Directory.FullName,"obj"))
    for f in objDir.GetFiles(sprintf "%s*.paket.resolved" projectFileInfo.Name) do
        try f.Delete() with | _ -> ()

    // fable 1.0 compat
    let oldReferencesFile = FileInfo(Path.Combine(projectFileInfo.Directory.FullName,"obj",projectFileInfo.Name + ".references"))
    if oldReferencesFile.Exists then oldReferencesFile.Delete()

    for originalTargetProfileString, targetProfile in targets do
        let list = System.Collections.Generic.List<_>()
        for kv in groups do
            let hull,_ = hulls.[kv.Key]
            let allDirectPackages =
                match referencesFile.Groups |> Map.tryFind kv.Key with
                | Some g -> g.NugetPackages |> List.map (fun p -> p.Name) |> Set.ofList
                | None -> Set.empty

            for (key,_,_) in hull do
                let restore =
                    let resolvedPackage = resolved.Force().[key]

                    match resolvedPackage.Settings.FrameworkRestrictions with
                    | Requirements.ExplicitRestriction restrictions ->
                        Requirements.isTargetMatchingRestrictions(restrictions, targetProfile)
                    | _ -> true

                if restore then
                    let _,packageName = key
                    let direct = allDirectPackages.Contains packageName
                    let package = resolved.Force().[key]
                    let line =
                        packageName.ToString() + "," +
                        package.Version.ToString() + "," +
                        (if direct then "Direct" else "Transitive") + "," +
                        kv.Key.ToString()

                    list.Add line

        let output = String.Join(Environment.NewLine,list)
        let newFileName = FileInfo(Path.Combine(projectFileInfo.Directory.FullName,"obj",projectFileInfo.Name + "." + originalTargetProfileString + ".paket.resolved"))
        if not newFileName.Directory.Exists then
            newFileName.Directory.Create()

        elif not newFileName.Exists || File.ReadAllText(newFileName.FullName) <> output then
            if not (File.Exists(oldReferencesFile.FullName)) || targetProfile = TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetStandard DotNetStandardVersion.V1_6) then
                // compat with old targets and fable - always write but prefer netstandard16.
                File.WriteAllText(oldReferencesFile.FullName,output)
            File.WriteAllText(newFileName.FullName,output)
            if verbose then
                tracefn " - %s created" newFileName.FullName
        else
            if verbose then
                tracefn " - %s already up-to-date" newFileName.FullName

    let cliTools = System.Collections.Generic.List<_>()
    for kv in groups do
        let _,cliToolsInGroup = hulls.[kv.Key]
        cliTools.AddRange cliToolsInGroup
    
    createPaketPropsFile cliTools true (ProjectFile.getPaketPropsFileInfo projectFileInfo)

    // Write "cached" file, this way msbuild can check if the references file has changed.
    let paketCachedReferencesFileName = FileInfo(Path.Combine(projectFileInfo.Directory.FullName,"obj",projectFileInfo.Name + ".paket.references.cached"))
    if File.Exists (referencesFile.FileName) then
        File.Copy(referencesFile.FileName, paketCachedReferencesFileName.FullName, true)
    else
        // it can happen that the references file doesn't exist if paket doesn't find one in that case we update the cache by deleting it.
        if paketCachedReferencesFileName.Exists then paketCachedReferencesFileName.Delete()

let CreateScriptsForGroups dependenciesFileName (lockFile:LockFile) (groups:Map<GroupName,LockFileGroup>) =
    let groupsToGenerate =
        groups
        |> Seq.map (fun kvp -> kvp.Value)
        |> Seq.filter (fun g -> g.Options.Settings.GenerateLoadScripts = Some true)
        |> Seq.map (fun g -> g.Name)
        |> Seq.toList

    if not (List.isEmpty groupsToGenerate) then
        let depsCache = DependencyCache(dependenciesFileName,lockFile)
        let rootPath = DirectoryInfo lockFile.RootPath

        let scripts = LoadingScripts.ScriptGeneration.constructScriptsFromData depsCache groupsToGenerate [] []
        for sd in scripts do
            sd.Save rootPath

let FindOrCreateReferencesFile (projectFile:ProjectFile) =
    match projectFile.FindReferencesFile() with
    | Some fileName -> 
        try
            ReferencesFile.FromFile fileName
        with e ->
            failwith ((ReferencesFileParseError (FileInfo fileName,e)).ToString())
    | None ->
        let fileName = 
            let fi = FileInfo(projectFile.FileName)
            Path.Combine(fi.Directory.FullName,Constants.ReferencesFile)

        ReferencesFile.New fileName

let RestoreNewSdkProject lockFile resolved groups (projectFile:ProjectFile) =
    let referencesFile = FindOrCreateReferencesFile projectFile
    let projectFileInfo = FileInfo projectFile.FileName

    createAlternativeNuGetConfig projectFileInfo
    createProjectReferencesFiles lockFile projectFile referencesFile resolved groups
    referencesFile

let Restore(dependenciesFileName,projectFile,force,group,referencesFileNames,ignoreChecks,failOnChecks,targetFrameworks: string option) = 
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    let localFileName = DependenciesFile.FindLocalfile dependenciesFileName
    let root = lockFileName.Directory.FullName
    let alternativeProjectRoot = None
    if not lockFileName.Exists then 
        failwithf "%s doesn't exist." lockFileName.FullName

    // Shortcut if we already restored before
    let newContents = File.ReadAllText(lockFileName.FullName)
    let restoreCacheFile = Path.Combine(root, Constants.PaketFilesFolderName, Constants.RestoreHashFile)
    let isFullRestore = targetFrameworks = None && projectFile = None && group = None && referencesFileNames = []
    let inline isEarlyExit () =
        // We ignore our check when we do a partial restore, this way we can
        // fixup project specific changes (like an additional target framework or a changed references file)
        // We could still skip the actual "restore" work, but that is left as an exercise for the interesting reader.
        if isFullRestore && File.Exists restoreCacheFile then
            let oldContents = File.ReadAllText(restoreCacheFile)
            oldContents = newContents
        else false

    let lockFile,localFile,hasLocalFile =
        let lockFile = LockFile.LoadFrom(lockFileName.FullName)
        if not localFileName.Exists then
            lockFile,LocalFile.empty,false
        else
            let localFile =
                LocalFile.readFile localFileName.FullName
                |> returnOrFail
            LocalFile.overrideLockFile localFile lockFile,localFile,true

    if not (hasLocalFile || force) && isEarlyExit () then
        if verbose then
            tracefn "Last restore is still up to date."
    else
        if projectFile = None then
            extractRestoreTargets root |> ignore

        let targetFilter = 
            targetFrameworks
            |> Option.map (fun s -> 
                s.Split([|';'|], StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun s -> s.Trim())
                |> Array.choose FrameworkDetection.Extract)
        
        
        let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)

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
            | Some projectFileName ->
                let projectFile = ProjectFile.LoadFromFile projectFileName

                let referencesFile = RestoreNewSdkProject lockFile resolved groups projectFile

                [referencesFile.FileName]
            | None ->
                if referencesFileNames = [] && group = None then
                    // Restore all projects
                    let allProjects =
                        ProjectFile.FindAllProjects root
                        |> Seq.filter (fun proj -> proj.GetToolsVersion() >= 15.0)

                    for proj in allProjects do
                        RestoreNewSdkProject lockFile resolved groups proj |> ignore

                referencesFileNames

        let tasks =
            groups
            |> Seq.map (fun kv ->
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
                        | Some targets ->
                            let key = kv.Key,p
                            let resolvedPackage = resolved.Force().[key]

                            match resolvedPackage.Settings.FrameworkRestrictions with
                            | Requirements.ExplicitRestriction restrictions ->
                                targets
                                |> Array.exists (fun target -> Requirements.isTargetMatchingRestrictions(restrictions, TargetProfile.SinglePlatform target))
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

                    restore(alternativeProjectRoot, root, kv.Key, depFileGroup.Sources, depFileGroup.Caches, force, lockFile, packages, overriden))
            |> Seq.toArray
 
        RunInLockedAccessMode(
            root,
            (fun () ->
                for task in tasks do
                    task
                    |> Async.RunSynchronously
                    |> ignore

                CreateScriptsForGroups dependenciesFileName lockFile groups
                if isFullRestore then
                    File.WriteAllText(restoreCacheFile, newContents)))