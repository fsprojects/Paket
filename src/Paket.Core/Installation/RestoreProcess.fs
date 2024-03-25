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
open Requirements

/// Combines the import targets settings from the lock file and a project's references file, so that the project settings take priority
let private combineImportTargets (resolvedSettings: InstallSettings) (packageInstallSettings: PackageInstallSettings) =
    packageInstallSettings.Settings.ImportTargets
    |> Option.orElse resolvedSettings.ImportTargets

/// Combines the content settings from the lock file and a project's references file, so that the project settings take priority
let private combineOmitContent (resolvedSettings: InstallSettings) (packageInstallSettings: PackageInstallSettings) = 
    packageInstallSettings.Settings.OmitContent
    |> Option.orElse resolvedSettings.OmitContent

// "copy_local: true" is being used to set the "PrivateAssets=All" setting for a package.
// "copy_local: false" in new SDK format is defined as "ExcludeAssets=runtime".
/// Combines the copy_local settings from the lock file and a project's references file
let private combineCopyLocal (resolvedSettings:InstallSettings) (packageInstallSettings:PackageInstallSettings) =
    match resolvedSettings.CopyLocal, packageInstallSettings.Settings.CopyLocal with
    | Some false, Some true // E.g. never copy the dll except for unit-test projects
    | None, None -> None
    | _, Some false
    | Some false, None -> Some false // Sets ExcludeAssets=runtime
    | Some true, Some true
    | Some true, None
    | None, Some true -> Some true // Sets PrivateAssets=All

/// Finds packages which would be affected by a restore, i.e. not extracted yet or with the wrong version
let FindPackagesNotExtractedYet dependenciesFileName =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    let lockFile = LockFile.LoadFrom(lockFileName.FullName)
    let root = lockFileName.Directory.FullName

    lockFile.GetGroupedResolution()
    |> Map.toList
    |> List.filter (fun ((group,package),resolved) ->
        let packSettings = defaultArg resolved.Settings.StorageConfig PackagesFolderGroupConfig.Default
        let includeVersionInPath = defaultArg resolved.Settings.IncludeVersionInPath false
        let resolvedPath = packSettings.Resolve root group package resolved.Version includeVersionInPath
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
                package.Name, version, package.Kind, includeVersionInPath, downloadLicense, force, detailed)

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
            let source =
                match package.Source with
                | NuGetV2 _ | NuGetV3 _ ->
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
                | LocalNuGet _ ->
                    package.Source
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
        let tasks =
            lockFile.Groups.[groupName].Resolution
            |> Map.filter (fun name _ -> packages.Contains name)
            |> Seq.map (fun kv -> ExtractPackage(alternativeProjectRoot, root, groupName, sources, caches, force, group.GetPackage kv.Key, Set.contains kv.Key overriden))
            |> Seq.splitInto 5
            |> Seq.map (fun tasks -> async {
                for t in tasks do
                    let! _ = t
                    () })
        let! _ =
            tasks
            |> Async.Parallel
        return ()
    }

let internal computePackageHull groupName (lockFile : LockFile) (referencesFileNames : string seq) =
    referencesFileNames
    |> Seq.collect (fun fileName ->
        lockFile.GetPackageHull(groupName,ReferencesFile.FromFile fileName)
        |> Seq.map (fun p -> (snd p.Key)))

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

type private MyAssemblyFinder () = class end

let private saveToFile newContent (targetFile:FileInfo) =
    let rec loop trials =
        try
            if not targetFile.Directory.Exists then
                targetFile.Directory.Create()

            let oldContent =
                if targetFile.Exists then
                    File.ReadAllText targetFile.FullName
                else
                    ""

            let written =
                if newContent <> oldContent then
                    if verbose then
                        tracefn " - %s created" targetFile.FullName

                    if targetFile.Exists then
                        File.SetAttributes(targetFile.FullName,IO.FileAttributes.Normal)
                    File.WriteAllText(targetFile.FullName,newContent)
                    true
                else
                    if verbose then
                        tracefn " - %s already up-to-date" targetFile.FullName
                    false

            written,targetFile.FullName
        with
        | exn when trials > 0 ->
            if verbose then
                tracefn "Failed to save file %s. Retry. Message: %s" targetFile.FullName exn.Message
            System.Threading.Thread.Sleep(100)
            loop (trials - 1)

    loop 5

let extractElement root name =
    let a = typeof<MyAssemblyFinder>.GetTypeInfo().Assembly
    let s = a.GetManifestResourceStream name
    if isNull s then failwithf "Resource stream '%s' could not be found in the Paket.Core assembly" name
    let targetFile = FileInfo(Path.Combine(root,".paket",name))
    if not targetFile.Directory.Exists then
        targetFile.Directory.Create()

    use sr = new StreamReader(s)

    s.Seek(int64 0, SeekOrigin.Begin) |> ignore
    s.Flush()
    let newContent = sr.ReadToEnd()

    saveToFile newContent targetFile

let extractRestoreTargets root =
    let path = Path.Combine(root,".paket","Paket.Restore.targets")
    if Environment.GetEnvironmentVariable "PAKET_SKIP_RESTORE_TARGETS" <> "true" then
        // allow to be more clever than paket
        if !copiedElements then path
        else
            let fileWritten, path = extractElement root "Paket.Restore.targets"
            copiedElements := true
            if fileWritten then
                verbosefn "Extracted Paket.Restore.targets to: %s (Can be disabled with PAKET_SKIP_RESTORE_TARGETS=true)" path
            path
    else
        verbosefn "Skipping extraction of Paket.Restore.targets - if it was enabled, it would have been extracted to: %s (Can be re-enabled with PAKET_SKIP_RESTORE_TARGETS=false or deleting the environment variable to revert to default behavior)" path
        path

let CreateInstallModel(alternativeProjectRoot, root, groupName, sources, caches, force, package) =
    async {
        let! package, content = ExtractPackage(alternativeProjectRoot, root, groupName, sources, caches, force, package, false)
        let kind =
            match package.Kind with
            | ResolvedPackageKind.Package -> InstallModelKind.Package
            | ResolvedPackageKind.DotnetCliTool -> InstallModelKind.DotnetCliTool
        let model =
            InstallModel.CreateFromContent(
                package.Name,
                package.Version,
                kind,
                getExplicitRestriction package.Settings.FrameworkRestrictions,
                content.Force())
        return (groupName,package.Name), (package,model)
    }


let createAlternativeNuGetConfig (projectFile:FileInfo, objDirectory:DirectoryInfo) =
    let alternativeConfigFileInfo = FileInfo(Path.Combine(objDirectory.FullName,projectFile.Name + ".NuGet.Config"))

    let config = """<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
  </packageSources>
  <disabledPackageSources>
     <clear />
  </disabledPackageSources>
</configuration>"""

    saveToFile config alternativeConfigFileInfo |> ignore

let FSharpCore = PackageName "FSharp.Core"

let createPaketPropsFile (lockFile:LockFile) (cliTools:ResolvedPackage seq) (referencesFile:ReferencesFile) (packages:((GroupName * PackageName) * PackageInstallSettings * _)seq) (fileInfo:FileInfo) =
    let cliParts =
        if Seq.isEmpty cliTools then
            ""
        else
            cliTools
            |> Seq.map (fun cliTool -> sprintf """        <DotNetCliToolReference Include="%O" Version="%O" />""" cliTool.Name cliTool.Version)
            |> fun xs -> String.Join(Environment.NewLine,xs)
            |> fun s -> "    <ItemGroup>" + Environment.NewLine + s + Environment.NewLine + "    </ItemGroup>"


    let allDirectPackages = 
        referencesFile.Groups.Values
        |> Seq.collect (fun g -> g.NugetPackages)
        |> Seq.map (fun p -> p.Name)
        |> Set.ofSeq

    let packagesParts =
        if Seq.isEmpty packages then
            ""
        else
            packages
            |> Seq.map (fun ((groupName,packageName),packageSettings,_) ->
                let group = lockFile.Groups.[groupName]
                let p = group.Resolution.[packageName]
                let restrictions =
                    match p.Settings.FrameworkRestrictions with
                    | ExplicitRestriction FrameworkRestriction.HasNoRestriction -> group.Options.Settings.FrameworkRestrictions
                    | ExplicitRestriction fw -> ExplicitRestriction fw
                    | _ -> group.Options.Settings.FrameworkRestrictions
                let condition = getExplicitRestriction restrictions
                p,condition,packageSettings)
            |> Seq.groupBy (fun (_,c,__) -> c)
            |> Seq.collect (fun (condition,packages) ->
                let condition =
                    match condition with
                    | FrameworkRestriction.HasNoRestriction -> ""
                    | restrictions -> restrictions.ToMSBuildCondition()
                let condition =
                    if condition = "" || condition = "true" then "" else
                    sprintf " AND (%s)" condition

                let packageReferences =
                    packages
                    |> Seq.collect (fun (p,_,packageSettings) ->
                        let directReferenceCondition = 
                            if not(allDirectPackages.Contains p.Name) then 
                                "Condition=\" '$(ManagePackageVersionsCentrally)' != 'true' \""
                            else ""

                        [yield sprintf """        <PackageReference %s Include="%O">""" directReferenceCondition p.Name
                         yield sprintf """            <Version Condition=" '$(ManagePackageVersionsCentrally)' != 'true' ">%O</Version>""" p.Version
                         let excludeAssets =
                            [ if combineCopyLocal p.Settings packageSettings = Some false then yield "runtime"
                              if combineOmitContent p.Settings packageSettings = Some ContentCopySettings.Omit then yield "contentFiles"
                              // on explicit 'do not import' settings, exclude build props/targets
                              if combineImportTargets p.Settings packageSettings = Some false then yield! [ "build"; "buildMultitargeting"; "buildTransitive" ] ]
                         match excludeAssets with
                         | [] -> ()
                         | tags -> yield sprintf """            <ExcludeAssets>%s</ExcludeAssets>""" (tags |> String.concat ";")

                         match combineCopyLocal p.Settings packageSettings with
                         | Some true -> yield sprintf """            <PrivateAssets>All</PrivateAssets>"""
                         | _ -> ()

                         yield """        </PackageReference>"""])

                let packageVersions =
                    packages
                    |> Seq.collect (fun (p,_,__) ->
                        [yield sprintf """        <PackageVersion Include="%O">""" p.Name
                         yield sprintf """            <Version>%O</Version>""" p.Version
                         yield """        </PackageVersion>"""])

                [yield sprintf "    <ItemGroup Condition=\"($(DesignTimeBuild) == true)%s\">" condition
                 yield! packageReferences
                 yield! packageVersions
                 yield "    </ItemGroup>"])
            |> fun xs -> String.Join(Environment.NewLine,xs)


    // When updating the PaketPropsVersion be sure to update the Paket.Restore.targets which checks this value
    let content =
        sprintf """<?xml version="1.0" encoding="utf-8" standalone="no"?>
<Project ToolsVersion="14.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    <PropertyGroup>
        <MSBuildAllProjects>$(MSBuildAllProjects);$(MSBuildThisFileFullPath)</MSBuildAllProjects>
        <PaketPropsVersion>6.0.0</PaketPropsVersion>
        <PaketPropsLoaded>true</PaketPropsLoaded>
    </PropertyGroup>
%s
%s
</Project>"""
            cliParts
            packagesParts

    saveToFile content fileInfo

let createPaketCLIToolsFile (cliTools:ResolvedPackage seq) (fileInfo:FileInfo) =
    if Seq.isEmpty cliTools then
        if fileInfo.Exists then
            File.Delete(fileInfo.FullName)
    else
        let cliParts =
            cliTools
            |> Seq.map (fun package ->
                package.Name.ToString() + "," +
                package.Version.ToString())

        let content = String.Join(Environment.NewLine,cliParts)

        saveToFile content fileInfo |> ignore

let ImplicitPackages = Set.ofList [ PackageName "NETStandard.Library" ]

let createProjectReferencesFiles (lockFile:LockFile) (projectFile:ProjectFile) (referencesFile:ReferencesFile) (resolved:Lazy<Map<GroupName*PackageName,PackageInfo>>) (groups:Map<GroupName,LockFileGroup>) (targetFrameworks: string option) (objDir: DirectoryInfo) =
    let projectFileInfo = FileInfo projectFile.FileName

    let targets =
        let monikers =
            ProjectFile.getTargetFramework projectFile
            |> Option.toList
            |> List.append (ProjectFile.getTargetFrameworksParsed projectFile)
            |> List.append (targetFrameworks |> Option.toList)

        monikers
        |> List.collect (fun item -> item.Split([|';'|],StringSplitOptions.RemoveEmptyEntries) |> Array.map (fun x -> x.Trim()) |> List.ofArray)
        |> List.map (fun s -> s, (PlatformMatching.forceExtractPlatforms s |> fun p -> p.ToTargetProfile true))
        |> List.choose (fun (s, c) -> c |> Option.map (fun d -> s, d))

    let objDirFullName = objDir.FullName

    // delete stale entries (otherwise we might not recognize stale data on a change later)
    // scenario: remove a target framework -> change references -> add back target framework
    // -> We reached an invalid state
    for f in objDir.GetFiles(sprintf "%s*.paket.resolved" projectFileInfo.Name) do
        try f.Delete() with | _ -> ()

    // fable 1.0 compat
    let oldReferencesFile = FileInfo(Path.Combine(objDirFullName,projectFileInfo.Name + ".references"))
    if oldReferencesFile.Exists then try oldReferencesFile.Delete() with | _ -> ()

    for originalTargetProfileString, targetProfile in targets do
        let list = System.Collections.Generic.List<_>()
        for kv in groups do
            let hull,_ = lockFile.GetOrderedPackageHull(kv.Key,referencesFile,Some targetProfile)

            let excludes,allDirectPackages =
                match referencesFile.Groups |> Map.tryFind kv.Key with
                | Some g ->
                    let excludes =
                        g.NugetPackages
                        |> List.collect (fun p -> p.Settings.Excludes)
                        |> Seq.map PackageName
                        |> Set.ofSeq

                    let packages =
                        g.NugetPackages
                        |> List.map (fun p -> p.Name)
                        |> Set.ofList
                    excludes,packages
                | None -> Set.empty,Set.empty

            for key,packageSettings,_ in hull do
                let resolvedPackage = resolved.Force().[key]
                let _,packageName = key
                let restore =
                    packageName <> PackageName "Microsoft.Azure.WebJobs.Script.ExtensionsMetadataGenerator" && // #3345
                     not (excludes.Contains resolvedPackage.Name) &&
                     not (ImplicitPackages.Contains resolvedPackage.Name) &&
                        match resolvedPackage.Settings.FrameworkRestrictions with
                        | Requirements.ExplicitRestriction restrictions ->
                            Requirements.isTargetMatchingRestrictions(restrictions, targetProfile)
                        | _ -> true

                if restore then
                    let direct = allDirectPackages.Contains packageName
                    let package = resolved.Force().[key]
                    let combinedCopyLocal = combineCopyLocal resolvedPackage.Settings packageSettings
                    let combinedOmitContent = combineOmitContent resolvedPackage.Settings packageSettings
                    let combinedImportTargets = combineImportTargets resolvedPackage.Settings packageSettings
                    let aliases = if direct then packageSettings.Settings.Aliases |> Seq.tryHead else None
                    
                    let privateAssetsAll =
                        match combinedCopyLocal with
                        | Some true -> "true"
                        | Some false
                        | None -> "false"
                    let copyLocal =
                        match combinedCopyLocal with
                        | Some false -> "false"
                        | Some true
                        | None -> "true"
                    let omitContent =
                        match combinedOmitContent with
                        | Some ContentCopySettings.Omit -> "true"
                        | _ -> "false"
                    let importTargets =
                        // we want to import msbuild targets by default
                        match combinedImportTargets with
                        | Some false -> "false"
                        | _ -> "true"

                    let alias =
                        match aliases with
                        | Some(x) -> x.Value
                        | _ -> ""
                    let line =
                        [ packageName.ToString()
                          package.Version.ToString()
                          if direct then "Direct" else "Transitive"
                          kv.Key.ToString()
                          privateAssetsAll
                          copyLocal
                          omitContent
                          importTargets
                          alias]
                        |> String.concat ","

                    list.Add line

        let output = String.Join(Environment.NewLine,list)

        let newFileName = FileInfo(Path.Combine(objDirFullName,projectFileInfo.Name + "." + originalTargetProfileString + ".paket.resolved"))
        let rec loop trials =
            try
                if not newFileName.Directory.Exists then
                    newFileName.Directory.Create()

                if not newFileName.Exists || File.ReadAllText(newFileName.FullName) <> output then
                    if not (File.Exists(oldReferencesFile.FullName)) || targetProfile = TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetStandard DotNetStandardVersion.V1_6) then
                        // compat with old targets and fable - always write but prefer netstandard16.
                        File.WriteAllText(oldReferencesFile.FullName,output)
                    File.WriteAllText(newFileName.FullName,output)
                    if verbose then
                        tracefn " - %s created" newFileName.FullName
                else
                    if verbose then
                        tracefn " - %s already up-to-date" newFileName.FullName
            with
            | exn when trials > 0 ->
                if verbose then
                    tracefn "Failed to save resolved file %s. Retry. Message: %s" newFileName.FullName exn.Message
                System.Threading.Thread.Sleep(100)
                loop (trials - 1)

        loop 5

    let cliTools = System.Collections.Generic.List<_>()
    let packages = System.Collections.Generic.List<_>()
    for kv in groups do
        let packagesInGroup,cliToolsInGroup = lockFile.GetOrderedPackageHull(kv.Key,referencesFile)
        cliTools.AddRange cliToolsInGroup
        packages.AddRange packagesInGroup

    let paketCLIToolsFileName = FileInfo(Path.Combine(objDirFullName,projectFileInfo.Name + ".paket.clitools"))
    createPaketCLIToolsFile cliTools paketCLIToolsFileName

    let propsFile = FileInfo(Path.Combine(objDirFullName, projectFileInfo.Name + ".paket.props"))
    let written,_ = createPaketPropsFile lockFile cliTools referencesFile packages propsFile
    if written then
        try
            let fi = FileInfo(Path.Combine(objDirFullName,"project.assets.json"))
            if fi.Exists then
                fi.Delete()
        with
        | _ -> ()

    // Write "cached" file, this way msbuild can check if the references file has changed.
    let paketCachedReferencesFileName = FileInfo(Path.Combine(objDirFullName,projectFileInfo.Name + ".paket.references.cached"))
    let rec loop trials =
        try
            if File.Exists referencesFile.FileName then
                // The existing cached file can be read-only if it was copied from a read-only file.
                // For example, when using Team Foundation Version Control with Server workspaces.
                if Utils.isWindows then
                    paketCachedReferencesFileName.Refresh()
                    if paketCachedReferencesFileName.Exists && paketCachedReferencesFileName.IsReadOnly then
                        paketCachedReferencesFileName.IsReadOnly <- false

                File.Copy(referencesFile.FileName, paketCachedReferencesFileName.FullName, true)

                if Utils.isWindows then
                    paketCachedReferencesFileName.Refresh()
                    paketCachedReferencesFileName.IsReadOnly <- false
            else
                // it can happen that the references file doesn't exist if paket doesn't find one in that case we update the cache by deleting it.
                if paketCachedReferencesFileName.Exists then paketCachedReferencesFileName.Delete()
        with
        | exn when trials > 0 ->
            if verbose then
                tracefn "Failed to save cached file %s. Retry. Message: %s" paketCachedReferencesFileName.FullName exn.Message
            System.Threading.Thread.Sleep(100)
            loop (trials - 1)

    loop 5

let CreateScriptsForGroups (lockFile:LockFile) (groups:Map<GroupName,LockFileGroup>) =
    let groupsToGenerate =
        groups
        |> Seq.map (fun kvp -> kvp.Value)
        |> Seq.filter (fun g -> g.Options.Settings.GenerateLoadScripts = Some true)
        |> Seq.map (fun g -> g.Name)
        |> Seq.toList

    if not (List.isEmpty groupsToGenerate) then
        let depsCache = DependencyCache(lockFile)
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

let objDirectory (projectFileInfo:FileInfo, outputPath:DirectoryInfo option) : DirectoryInfo =
    match outputPath with
    | Some outputPath -> outputPath
    | None -> DirectoryInfo(Path.Combine(projectFileInfo.Directory.FullName, "obj"))

let RestoreNewSdkProject lockFile resolved groups (projectFile:ProjectFile) targetFrameworks (outputPath:DirectoryInfo option) =
    let referencesFile = FindOrCreateReferencesFile projectFile
    let projectFileInfo = FileInfo projectFile.FileName
    let objDirectory = objDirectory(projectFileInfo, outputPath)

    RunInLockedAccessMode (
        objDirectory.FullName,
        (fun () ->        
            tracefn "Restoring %O" projectFile.FileName
            createAlternativeNuGetConfig (projectFileInfo, objDirectory)
            createProjectReferencesFiles lockFile projectFile referencesFile resolved groups targetFrameworks objDirectory            
            false
        )
    )
    referencesFile

let internal getStringHash (s:string) =
    use sha256 = System.Security.Cryptography.SHA256.Create()
    s
    |> System.Text.Encoding.UTF8.GetBytes
    |> sha256.ComputeHash
    |> BitConverter.ToString
    |> fun s -> s.Replace("-", "")


type internal Hash =
    | Hash of string
    member x.HashString =
        match x with
        | Hash s -> s
    member x.IsEmpty =
        match x with
        | Hash s -> String.IsNullOrEmpty s
    static member OfString s = Hash (getStringHash s)

let internal getLockFileHashFromContent (content:string) =
    Hash.OfString content
let internal getLockFileHash (f:string) =
    getLockFileHashFromContent (File.ReadAllText f)

type internal RestoreCache =
    { PackagesDownloadedHash : Hash
      ProjectsRestoredHash : Hash }
    member x.IsPackagesDownloadUpToDate lockfileHash =
        if x.PackagesDownloadedHash.IsEmpty then false
        else x.PackagesDownloadedHash = lockfileHash
    member x.IsProjectRestoreUpToDate lockfileHash =
        if x.ProjectsRestoredHash.IsEmpty then false
        else x.ProjectsRestoredHash = lockfileHash
    static member Empty =
        { PackagesDownloadedHash = Hash ""
          ProjectsRestoredHash = Hash "" }

let internal writeRestoreCache (file:string) { PackagesDownloadedHash = Hash packagesDownloadedHash; ProjectsRestoredHash = Hash projectsRestoredHash} =
    let jobj = Newtonsoft.Json.Linq.JObject()
    jobj.["packagesDownloadedHash"] <- Newtonsoft.Json.Linq.JToken.op_Implicit packagesDownloadedHash
    jobj.["projectsRestoredHash"] <- Newtonsoft.Json.Linq.JToken.op_Implicit projectsRestoredHash
    let s = jobj.ToString()
    saveToFile s (FileInfo file) |> ignore<_*_>
    //File.WriteAllText(file, s)

let private tryReadRestoreCache (file:string) =
    if File.Exists file then
        let f = File.ReadAllText(file)
        try
            let jobj = Newtonsoft.Json.Linq.JObject.Parse(f)
            { PackagesDownloadedHash = Hash (string jobj.["packagesDownloadedHash"]); ProjectsRestoredHash = Hash(string jobj.["projectsRestoredHash"]) }
        with
        | :? Newtonsoft.Json.JsonReaderException as e -> RestoreCache.Empty
    else RestoreCache.Empty

let private readRestoreCache (lockFileName:FileInfo) =
    let root = lockFileName.Directory.FullName
    let restoreCacheFile = Path.Combine(root, Constants.PaketRestoreHashFilePath)
    tryReadRestoreCache restoreCacheFile

let internal writeGitignore restoreCacheFile =
    let folder = FileInfo(restoreCacheFile).Directory
    let rec isGitManaged (folder:DirectoryInfo) =
        if File.Exists(Path.Combine(folder.FullName, ".gitignore")) then true else
        if isNull folder.Parent then false else
        isGitManaged folder.Parent

    if isGitManaged folder.Parent then
        let restoreCacheGitIgnoreFile = Path.Combine(folder.FullName, ".gitignore")
        let contents =
            ".gitignore\npaket.restore.cached"
            |> normalizeLineEndings
        saveToFile contents (FileInfo restoreCacheGitIgnoreFile) |> ignore

type RestoreProjectOptions =
    | AllProjects
    | ReferenceFileList of referencesFileNames:string list
    | NoProjects
    | SingleProject of projectFile:string

let Restore(dependenciesFileName,projectFile:RestoreProjectOptions,force,group,ignoreChecks,failOnChecks,targetFrameworks: string option,outputPath:string option,skipRestoreTargetsExtraction:bool) =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    let localFileName = DependenciesFile.FindLocalfile dependenciesFileName
    let root = lockFileName.Directory.FullName
    let alternativeProjectRoot = None
    if not lockFileName.Exists then
        failwithf "%s doesn't exist." lockFileName.FullName

    let lockFile,localFile,hasLocalFile =
        // Do not parse the lockfile when we have an early exit scenario.
        let lockFile = lazy LockFile.LoadFrom(lockFileName.FullName)
        if not localFileName.Exists then
            lockFile,lazy LocalFile.empty,false
        else
            let localFile =
                lazy (LocalFile.readFile localFileName.FullName
                      |> returnOrFail)
            lazy LocalFile.overrideLockFile localFile.Value lockFile.Value,localFile,true

    // Shortcut if we already restored before
    // We ignore our check when we do a partial restore, this way we can
    // fixup project specific changes (like an additional target framework or a changed references file)

    // Check if caching makes sense (even if we only can cache parts of it)
    let canCacheRestore = 
        not (hasLocalFile || force) && 
            targetFrameworks = None && 
            (projectFile = AllProjects || projectFile = NoProjects) && 
            group = None

    if not skipRestoreTargetsExtraction && (projectFile = AllProjects || projectFile = NoProjects) then
        extractRestoreTargets root |> ignore

    let readCache () =
        if not canCacheRestore then
            None, RestoreCache.Empty, Hash "", false
        else
            let cache = readRestoreCache(lockFileName)
            let lockFileHash = getLockFileHash lockFileName.FullName
            let updatedCache =
                { PackagesDownloadedHash = lockFileHash // we always download all packages in that situation
                  ProjectsRestoredHash = if projectFile = AllProjects then lockFileHash else cache.ProjectsRestoredHash }
            let isPackagesDownloadUpToDate = cache.IsPackagesDownloadUpToDate lockFileHash
            let isProjectRestoreUpToDate = cache.IsProjectRestoreUpToDate lockFileHash
            Some updatedCache, cache, lockFileHash, (isPackagesDownloadUpToDate && isProjectRestoreUpToDate) || (projectFile = NoProjects && isPackagesDownloadUpToDate)

    let _,_,_, canEarlyExit = readCache()
    
    if canEarlyExit then        
        tracefn "The last full restore is still up to date. Nothing left to do."
    else
        let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)

        if not hasLocalFile && not ignoreChecks then
            if verbose then
                verbosefn "Checking for changes in lock file."
            let hasAnyChanges,nugetChanges,remoteFilechanges,hasChanges = DependencyChangeDetection.GetChanges(dependenciesFile,lockFile.Value,false)
            let checkResponse = if failOnChecks then failwithf else traceWarnfn
            if verbose then
                verbosefn "HasChanges: %b" hasAnyChanges

            if hasAnyChanges then
                checkResponse "paket.dependencies and paket.lock are out of sync in %s.%sPlease run 'paket install' or 'paket update' to recompute the paket.lock file." lockFileName.Directory.FullName Environment.NewLine
                for group, package, changes in nugetChanges do
                    traceWarnfn "Changes were detected for %s/%s" (group.ToString()) (package.ToString())
                    for change in changes do
                         traceWarnfn "    - %A" change

        let groups =
            match group with
            | None -> lockFile.Value.Groups
            | Some groupName ->
                match lockFile.Value.Groups |> Map.tryFind groupName with
                | None -> failwithf "The group %O was not found in the paket.lock file." groupName
                | Some group -> [groupName,group] |> Map.ofList

        let resolved = lazy (
            if verbose then
                let groupNames = groups |> Seq.map (fun kv -> kv.Key) |> Seq.toArray
                verbosefn "Gettting resolution for groups: %A" groupNames
            lockFile.Value.GetGroupedResolution()
        )

        let outputPath = outputPath |> Option.map DirectoryInfo

        let referencesFileNames =
            match projectFile with
            | SingleProject projectFileName ->
                if verbose then
                    verbosefn "Single project: %A" projectFileName

                let projectFile = ProjectFile.LoadFromFile projectFileName
                let referencesFile = RestoreNewSdkProject lockFile.Value resolved groups projectFile targetFrameworks outputPath

                [referencesFile.FileName]
            | ReferenceFileList list ->
                if verbose then
                    verbosefn "References file list: %A" list
                list
            | NoProjects ->
                if verbose then
                    verbosefn "No projects to restore"
                []
            | AllProjects ->
                if group = None then
                    if verbose then
                        verbosefn "Searching for SDK projects..."

                    // Restore all projects
                    let allSDKProjects =
                        ProjectFile.FindAllProjects root
                        |> Array.filter (fun proj -> proj.GetToolsVersion() >= 15.0)

                    if verbose then
                        verbosefn "SDK projects found: %A" allSDKProjects

                    for proj in allSDKProjects do
                        RestoreNewSdkProject lockFile.Value resolved groups proj targetFrameworks outputPath |> ignore
                []

        let targetFilter =
            targetFrameworks
            |> Option.map (fun s ->
                s.Split([|';'|], StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun s -> s.Trim())
                |> Array.choose FrameworkDetection.internalExtract
                |> Array.filter (fun x -> match x with Unsupported _ -> false | _ -> true))

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
                        |> computePackageHull kv.Key lockFile.Value

                let packages =
                    allPackages
                    |> Seq.filter (fun p ->
                        match targetFilter with
                        | None -> true
                        | Some targets ->
                            let key = kv.Key,p
                            let resolvedPackage = resolved.Force().[key]

                            match resolvedPackage.Settings.FrameworkRestrictions with
                            | ExplicitRestriction restrictions ->
                                targets
                                |> Array.exists (fun target -> isTargetMatchingRestrictions(restrictions, TargetProfile.SinglePlatform target))
                            | _ -> true)

                match dependenciesFile.Groups |> Map.tryFind kv.Value.Name with
                | None ->
                    failwithf
                        "The group %O was found in the %s file but not in the %s file. Please run \"paket install\" again."
                        kv.Value.Name
                        Constants.LockFileName
                        Constants.DependenciesFileName
                | Some depFileGroup ->
                    let packages = Set.ofSeq packages
                    let overriden =
                        packages
                        |> Set.filter (fun p -> LocalFile.overrides localFile.Value (p,depFileGroup.Name))

                    restore(alternativeProjectRoot, root, kv.Key, depFileGroup.Sources, depFileGroup.Caches, force, lockFile.Value, packages, overriden))
            |> Seq.toArray

        RunInLockedAccessMode(
            Path.Combine(root,Constants.PaketFilesFolderName),
            (fun () ->
                let updatedCache, cache, lockFileHash, canEarlyExit = readCache()
                if canEarlyExit then
                    tracefn "The last restore was successful. Nothing left to do."
                    false
                else
                    if verbose then
                        verbosefn "Checking if restore hash is up-to-date"

                    if not (cache.IsPackagesDownloadUpToDate lockFileHash) then
                        tracefn "Starting %srestore process." (if canCacheRestore then "full " else "")
                        for task in tasks do
                            task
                            |> Async.RunSynchronously
                            |> ignore

                        if verbose then
                            verbosefn "Creating script files for all groups"

                        CreateScriptsForGroups lockFile.Value groups
                    else
                        tracefn "Finished restoring projects."

                    match updatedCache with
                    | Some updatedCache ->
                        if verbose then
                            verbosefn "Writing restore hash file"

                        let restoreCacheFile = Path.Combine(root, Constants.PaketRestoreHashFilePath)
                        writeRestoreCache restoreCacheFile updatedCache
                        writeGitignore restoreCacheFile
                    | None -> 
                        ()
                    false)
            )
