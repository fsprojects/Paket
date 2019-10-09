/// Contains NuGet cache support.
module Paket.NuGetCache

open System
open System.IO
open Newtonsoft.Json
open System.IO.Compression
open Paket.Logging
open System.Text

open Paket
open Paket.Domain
open Paket.Utils
open Paket.PackageSources
open Paket.Requirements
open FSharp.Polyfill
open System.Runtime.ExceptionServices

open System.Net
open System.Threading.Tasks

// show the path that was too long
let FileInfo str =
    try
        FileInfo str
    with
      :? PathTooLongException as exn -> raise (PathTooLongException("Path too long: " + str, exn))

type NuGetResponseGetVersionsSuccess = string []

type NuGetResponseGetVersionsFailure =
    { Url : string; Error : ExceptionDispatchInfo }
    static member ofTuple (url,err) =
        { Url = url; Error = err }

type NuGetResponseGetVersions =
    | SuccessVersionResponse of NuGetResponseGetVersionsSuccess
    | ProtocolNotCached
    | FailedVersionRequest of NuGetResponseGetVersionsFailure

    member x.Versions =
        match x with
        | SuccessVersionResponse l -> l
        | ProtocolNotCached
        | FailedVersionRequest _ -> [||]

    member x.IsSuccess =
        match x with
        | SuccessVersionResponse _ -> true
        | ProtocolNotCached
        | FailedVersionRequest _ -> false

type NuGetResponseGetVersionsSimple = SafeWebResult<NuGetResponseGetVersionsSuccess>

type NuGetRequestGetVersions =
    { DoRequest : unit -> Async<NuGetResponseGetVersions>
      Url : string }
    static member ofFunc url f =
        { Url = url; DoRequest = f }
    static member ofSimpleFunc url (f: _ -> Async<NuGetResponseGetVersionsSimple>) =
        NuGetRequestGetVersions.ofFunc url (fun _ ->
            async {
                let! res = f()
                return
                    match res with
                    | SuccessResponse r -> SuccessVersionResponse r
                    | NotFound -> SuccessVersionResponse [||]
                    | Unauthorized -> FailedVersionRequest { Url = url; Error = ExceptionDispatchInfo.Capture(exn("Not authorized (401)")) }
                    | UnknownError err -> FailedVersionRequest { Url = url; Error = err }
            })
    static member run (r:NuGetRequestGetVersions) : Async<NuGetResponseGetVersions> =
        async {
            try
                return! r.DoRequest()
            with e ->
                return FailedVersionRequest { Url = r.Url; Error = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture (exn(sprintf "Unhandled error in request to '%O' in NuGetRequestGetVersions.run" r.Url, e)) }
        }


// An unparsed file in the NuGet package -> still need to inspect the path for further information. After parsing an entry will be part of a "LibFolder" for example.
type UnparsedPackageFile =
    { FullPath : string
      PathWithinPackage : string }
    member x.BasePath =
        x.FullPath.Substring(0, x.FullPath.Length - (x.PathWithinPackage.Length + 1))

module NuGetConfig =

    let writeNuGetConfig directory sources =
        let start = """<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
    <!--To inherit the global NuGet package sources remove the <clear/> line below -->
    <clear />
"""
        let sb = StringBuilder start

        let i = ref 1
        for source in sources do
            sb.AppendLine(sprintf "    <add key=\"source%d\" value=\"%O\" />" !i source) |> ignore

        sb.Append("""
    </packageSources>
</configuration>""") |> ignore
        let text = sb.ToString()
        let fileName = Path.Combine(directory,Constants.NuGetConfigFile)
        if not (File.Exists fileName) then
            File.WriteAllText(fileName,text)
        else
            if File.ReadAllText(fileName) <> text then
                File.WriteAllText(fileName,text)

type FrameworkRestrictionsCache = string

type NuGetPackageCache =
    { SerializedDependencies : (PackageName * VersionRequirement * FrameworkRestrictionsCache) list
      PackageName : string
      SourceUrl: string
      Unlisted : bool
      DownloadUrl : string
      LicenseUrl : string
      Version: string
      CacheVersion: string }

    static member CurrentCacheVersion = "5.148"

    member this.WithDependencies (dependencies : (PackageName * VersionRequirement * FrameworkRestrictions) list) =
        { this with
            SerializedDependencies =
                dependencies
                |> List.map (fun (n,v, restrictions) ->
                    let restrictionString =
                        match restrictions with
                        | FrameworkRestrictions.AutoDetectFramework -> "AUTO"
                        | FrameworkRestrictions.ExplicitRestriction re -> re.ToString()
                    n, v, restrictionString) }

    member this.GetDependencies() : (PackageName * VersionRequirement * FrameworkRestrictions) Set =
        this.SerializedDependencies
        |> List.map (fun (n,v,restrictionString) ->
            let restrictions =
                if restrictionString = "AUTO" then
                    FrameworkRestrictions.AutoDetectFramework
                else
                    let restrictions = Requirements.parseRestrictions restrictionString |> fst
                    FrameworkRestrictions.ExplicitRestriction restrictions
            n, v, restrictions)
        |> Set.ofList

let inline normalizeUrl(url:string) = url.Replace("https://","http://").Replace("www.","")

let getCacheFiles force cacheVersion nugetURL (packageName:PackageName) (version:SemVerInfo) =
    let h = nugetURL |> normalizeUrl |> hash |> abs
    let prefix = sprintf "%O.%s.s%d" packageName (version.Normalize()) h
    let packageUrl = sprintf "%s_v%s.json" prefix cacheVersion
    let newFile = Path.Combine(Constants.NuGetCacheFolder, packageUrl)
    if force then // cleanup only on slow-path
        try
            let oldFiles =
                Directory.EnumerateFiles(Constants.NuGetCacheFolder, sprintf "%s*.json" prefix)
                |> Seq.filter (fun p -> Path.GetFileName p <> packageUrl)
                |> Seq.toList
            for f in oldFiles do
                File.Delete f
        with
        | ex -> traceErrorfn "Cannot cleanup '%s': %O" (sprintf "%s*.json" prefix) ex
    FileInfo(newFile)

type ODataSearchResult =
    | EmptyResult
    | Match of NuGetPackageCache

module ODataSearchResult =
    let get x =
        match x with
        | EmptyResult -> failwithf "Cannot call get on 'EmptyResult'"
        | Match r -> r

let tryGetDetailsFromCache force nugetURL (packageName:PackageName) (version:SemVerInfo) : ODataSearchResult option =
    let cacheFile = getCacheFiles force NuGetPackageCache.CurrentCacheVersion nugetURL packageName version
    if not force && cacheFile.Exists then
        try
            let json = File.ReadAllText(cacheFile.FullName)

            try
                let cacheResult =
                    let cachedObject = JsonConvert.DeserializeObject<NuGetPackageCache> json
                    if (PackageName cachedObject.PackageName <> packageName) ||
                        (cachedObject.Version <> version.Normalize())
                    then
                        if verbose then
                            traceVerbose (sprintf "Invalidating Cache '%s:%s' <> '%s:%s'" cachedObject.PackageName cachedObject.Version packageName.Name (version.Normalize()))
                        cacheFile.Delete()
                        None
                    else
                        Some cachedObject

                match cacheResult with
                | Some res -> Some (ODataSearchResult.Match res)
                | None -> None
            with
            | exn ->
                try cacheFile.Delete() with | _ -> ()
                if verbose then
                    traceWarnfn "Error while loading cache: %O" exn
                None
        with
        | exn ->
            if verbose then
                traceWarnfn "Error while reading cache file: %O" exn
            None
    else
        None

let getDetailsFromCacheOr force nugetURL (packageName:PackageName) (version:SemVerInfo) (get : unit -> ODataSearchResult Async) : ODataSearchResult Async =
    let get() =
        async {
            let! result = get()
            match result with
            | ODataSearchResult.Match result ->
                let cacheFile = getCacheFiles force NuGetPackageCache.CurrentCacheVersion nugetURL packageName version
                let serialized = JsonConvert.SerializeObject(result)
                let cachedData =
                    try
                        if cacheFile.Exists then
                            use cacheReader = cacheFile.OpenText()
                            cacheReader.ReadToEnd()
                        else ""
                    with
                    | ex ->
                        traceWarnfn "Can't read cache file %O:%s Message: %O" cacheFile Environment.NewLine ex
                        ""
                if String.CompareOrdinal(serialized, cachedData) <> 0 then
                    File.WriteAllText(cacheFile.FullName, serialized)
            | _ ->
                // TODO: Should we cache 404? Probably not.
                ()
            return result
        }
    async {
        match tryGetDetailsFromCache force nugetURL packageName version with
        | None -> return! get()
        | Some res -> return res
    }


let fixDatesInArchive fileName =
    try
        use zipToOpen = new FileStream(fileName, FileMode.Open)
        use archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update)
        let maxTime = DateTimeOffset.Now

        for e in archive.Entries do
            try
                let d = min maxTime e.LastWriteTime
                e.LastWriteTime <- d
            with
            | _ -> e.LastWriteTime <- maxTime
    with
    | exn -> traceWarnfn "Could not fix timestamps in %s. Error: %s" fileName exn.Message


let fixArchive fileName =
    if isMonoRuntime then
        fixDatesInArchive fileName

let GetLicenseFileName (packageName:PackageName) (version:SemVerInfo) = packageName.CompareString + "." + version.Normalize() + ".license.html"
let GetPackageFileName (packageName:PackageName) (version:SemVerInfo) = packageName.CompareString + "." + version.Normalize() + ".nupkg"

let inline isExtracted (directory:DirectoryInfo) (packageName:PackageName) (version:SemVerInfo) =
    let inDir f = Path.Combine(directory.FullName, f)
    let packFile = GetPackageFileName packageName version |> inDir
    let licenseFile = GetLicenseFileName packageName version |> inDir
    let fi = FileInfo(packFile)
    if not fi.Exists then false else
    if not directory.Exists then false else
    directory.EnumerateFileSystemInfos()
    |> Seq.exists (fun f ->
        (not (String.equalsIgnoreCase f.FullName fi.FullName)) &&
          (not (String.equalsIgnoreCase f.FullName licenseFile)))

let IsPackageVersionExtracted(config:ResolvedPackagesFolder, packageName:PackageName, version:SemVerInfo) =
    match config.Path with
    | Some target ->
        let targetFolder = DirectoryInfo(target)
        isExtracted targetFolder packageName version
    | None ->
        // Need to extract in .nuget dir?
        true

// cleanup folder structure
let rec private cleanup (dir : DirectoryInfo) =
    for sub in dir.GetDirectories() do
        let newName = Uri.UnescapeDataString(sub.FullName).Replace("%2B","+")
        let di = DirectoryInfo newName
        if sub.FullName <> newName && not di.Exists then
            if not di.Parent.Exists then
                di.Parent.Create()
            try
                Directory.Move(sub.FullName, newName)
            with
            | exn -> failwithf "Could not move %s to %s%sMessage: %s" sub.FullName newName Environment.NewLine exn.Message

            cleanup (DirectoryInfo newName)
        else
            cleanup sub

    for file in dir.GetFiles() do
        let newName = Uri.UnescapeDataString(file.Name).Replace("%2B","+")
        if newName.Contains "..\\" || newName.Contains "../" then
          failwithf "Relative paths are not supported. Please tell the package author to fix the package to not use relative paths. The invalid file was '%s'" file.FullName
        if newName.Contains "\\" || newName.Contains "/" then
          traceWarnfn "File '%s' contains back- or forward-slashes, probably because it wasn't properly packaged (for example with windows paths in nuspec on a unix like system). Please tell the package author to fix it." file.FullName
        let newFullName = Path.Combine(file.DirectoryName, newName)
        if file.Name <> newName && not (File.Exists newFullName) then
            let dir = Path.GetDirectoryName newFullName
            if not (Directory.Exists dir) then
                Directory.CreateDirectory dir |> ignore

            File.Move(file.FullName, newFullName)


let GetTargetUserFolder (packageName:PackageName) (version:SemVerInfo) =
    DirectoryInfo(Path.Combine(Constants.UserNuGetPackagesFolder,packageName.CompareString,version.Normalize())).FullName

let GetTargetUserNupkg (packageName:PackageName) (version:SemVerInfo) =
    let normalizedNupkgName = GetPackageFileName packageName version
    let path = GetTargetUserFolder packageName version
    Path.Combine(path, normalizedNupkgName)

let GetTargetUserToolsFolder (packageName:PackageName) (version:SemVerInfo) =
    DirectoryInfo(Path.Combine(Constants.UserNuGetPackagesFolder,".tools",packageName.CompareString,version.Normalize())).FullName

let TryGetFallbackFolderFromHardCodedPath = lazy (
    let fallbackDir =
        match isUnix with
        | true ->
            [|"/usr/share/dotnet/sdk/NuGetFallbackFolder" |]
        | false ->
            [| Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet","sdk", "NuGetFallbackFolder")
               Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet","sdk", "NuGetFallbackFolder")|]

    fallbackDir
    |> Array.tryFind Directory.Exists
)

let TryGetFallbackFolderFromBin () =
    let dotnet = if isUnix then "dotnet" else "dotnet.exe"
    ProcessHelper.tryFindFileOnPath dotnet |> Option.bind (fun fileName ->
        let dotnetDir = Path.GetDirectoryName fileName
        let fallbackDir = Path.Combine (dotnetDir, "sdk", "NuGetFallbackFolder")
        if Directory.Exists fallbackDir then Some fallbackDir else None)

let TryGetFallbackFolder () =
    TryGetFallbackFolderFromHardCodedPath.Force()
    |> Option.orElseWith TryGetFallbackFolderFromBin

let TryGetFallbackNupkg (packageName:PackageName) (version:SemVerInfo) =
    match TryGetFallbackFolder() with
    | Some folder ->
        let normalizedNupkgName = GetPackageFileName packageName version
        let fallbackFile = Path.Combine(folder, packageName.CompareString, version.Normalize(), normalizedNupkgName) |> FileInfo
        if fallbackFile.Exists && fallbackFile.Length > 0L then Some fallbackFile.FullName else None
    | None -> None

let GetPackageUserFolderDir (packageName:PackageName, version:SemVerInfo, kind:PackageResolver.ResolvedPackageKind) =
    let dir =
        match kind with
        | PackageResolver.ResolvedPackageKind.DotnetCliTool ->
            GetTargetUserToolsFolder packageName version
        | PackageResolver.ResolvedPackageKind.Package ->
            GetTargetUserFolder packageName version

    let targetFolder = DirectoryInfo(dir)
    targetFolder.FullName

/// Extracts the given package to the user folder
let rec ExtractPackageToUserFolder(fileName:string, packageName:PackageName, version:SemVerInfo, kind:PackageResolver.ResolvedPackageKind) =
    async {
        let dir = GetPackageUserFolderDir (packageName, version, kind)

        if kind = PackageResolver.ResolvedPackageKind.DotnetCliTool then
            let! _ = ExtractPackageToUserFolder(fileName, packageName, version, PackageResolver.ResolvedPackageKind.Package)
            ()

        let targetFolder = DirectoryInfo(dir)

        use _ = Profile.startCategory Profile.Category.FileIO
        if isExtracted targetFolder packageName version |> not then
            Directory.CreateDirectory(targetFolder.FullName) |> ignore
            let fi = FileInfo fileName
            let targetPackageFileName = Path.Combine(targetFolder.FullName,fi.Name)
            if normalizePath fileName <> normalizePath targetPackageFileName then
                File.Copy(fileName,targetPackageFileName,true)

            ZipFile.ExtractToDirectory(fileName, targetFolder.FullName)
            // lowercase the .nuspec file to mimic NuGet behavior
            let nuspecFileName = sprintf "%s.nuspec" (packageName.ToString())
            let nuspecLowerFileName = sprintf "%s.nuspec" (packageName.CompareString)
            let filePath = Path.Combine(targetFolder.FullName, nuspecFileName)
            let lowerFilePath = Path.Combine(targetFolder.FullName, nuspecLowerFileName)
            if isUnix then
                File.Move(filePath, lowerFilePath)
            else
                let nuspecTempFileName = sprintf "%s.nuspec.tmp" (packageName.CompareString)
                let tempFilePath = Path.Combine(targetFolder.FullName, nuspecTempFileName)
                // On windows we have to move the file twice to actually have the correct casing
                // in the filesystem, because otherwise it is noop (at least in explorer)
                File.Move(filePath, tempFilePath)
                File.Move(tempFilePath, lowerFilePath)
            let cachedHashFile = Path.Combine(Constants.NuGetCacheFolder,fi.Name + ".sha512")
            if not (File.Exists cachedHashFile) then
                let packageHash = getSha512File fileName
                File.WriteAllText(cachedHashFile,packageHash)

            File.Copy(cachedHashFile,targetPackageFileName + ".sha512")
            cleanup targetFolder
        return targetFolder.FullName
    }

/// Extracts the given package to the ./packages folder
let ExtractPackage(fileName:string, targetFolder, packageName:PackageName, version:SemVerInfo, detailed) =
    async {
        use _ = Profile.startCategory Profile.Category.FileIO
        let directory = DirectoryInfo(targetFolder)
        if isExtracted directory packageName version then
             if verbose then
                 verbosefn "%O %O already extracted" packageName version
        else
            Directory.CreateDirectory targetFolder |> ignore

            try
                fixArchive fileName
                ZipFile.ExtractToDirectory(fileName, targetFolder)
            with
            | exn ->
                let text = if detailed then sprintf "%s In rare cases a firewall might have blocked the download. Please look into the file and see if it contains text with further information." Environment.NewLine else ""
                let path = try Path.GetFullPath fileName with :? PathTooLongException -> sprintf "%s (!too long!)" fileName
                raise (Exception(sprintf "Error during extraction of %s.%s%s" path Environment.NewLine text, exn))

            cleanup directory
            if verbose then
                verbosefn "%O %O unzipped to %s" packageName version targetFolder
        return targetFolder
    }

let CopyLicenseFromCache(config:ResolvedPackagesFolder, cacheFileName, packageName:PackageName, version:SemVerInfo, force) =
    async {
        try
            if String.IsNullOrWhiteSpace cacheFileName then return () else
            match config.Path with
            | Some packagePath ->
                let cacheFile = FileInfo cacheFileName
                if cacheFile.Exists then
                    let targetFile = FileInfo(Path.Combine(packagePath, "license.html"))
                    if not force && targetFile.Exists then
                        if verbose then
                           verbosefn "License %O %O already copied" packageName version
                    else
                        use _ = Profile.startCategory Profile.Category.FileIO
                        File.Copy(cacheFile.FullName, targetFile.FullName, true)
            | None -> ()
        with
        | exn -> traceWarnfn "Could not copy license for %O %O from %s.%s    %s" packageName version cacheFileName Environment.NewLine exn.Message
    }

/// Extracts the given package to the ./packages folder
let CopyFromCache(config:ResolvedPackagesFolder, cacheFileName, licenseCacheFile, packageName:PackageName, version:SemVerInfo, force, detailed) =
    async {
        match config.Path with
        | Some target ->
            let targetFolder = DirectoryInfo(target).FullName
            let fi = FileInfo(cacheFileName)
            let targetFile = FileInfo(Path.Combine(targetFolder, fi.Name))
            if not force && targetFile.Exists then
                if verbose then
                    verbosefn "%O %O already copied" packageName version
            else
                use _ = Profile.startCategory Profile.Category.FileIO
                CleanDir targetFolder
                File.Copy(cacheFileName, targetFile.FullName)
            try
                let! extracted = ExtractPackage(targetFile.FullName,targetFolder,packageName,version,detailed)
                do! CopyLicenseFromCache(config, licenseCacheFile, packageName, version, force)
                return Some extracted
            with
            | exn ->
                use _ = Profile.startCategory Profile.Category.FileIO
                File.Delete targetFile.FullName
                Directory.Delete(targetFolder,true)
                return! raise exn
        | None -> return None
    }

/// Puts the package into the cache
let CopyToCache(cache:Cache, fileName, force) =
    try
        use __ = Profile.startCategory Profile.Category.FileIO
        if Cache.isInaccessible cache then
            if verbose then
                verbosefn "Cache %s is inaccessible, skipping" cache.Location
        else
            let targetFolder = DirectoryInfo(cache.Location)
            if not targetFolder.Exists then
                targetFolder.Create()

            let fi = FileInfo(fileName)
            let targetFile = FileInfo(Path.Combine(targetFolder.FullName, fi.Name))

            if not force && targetFile.Exists then
                if verbose then
                    verbosefn "%s already in cache %s" fi.Name targetFolder.FullName
            else
                File.Copy(fileName, targetFile.FullName, force)
    with
    | _ ->
        Cache.setInaccessible cache
        reraise()

type SendDataModification =
    { LoweredPackageId : bool; NormalizedVersion : bool }

type GetVersionFilter =
    { ToLower : bool; NormalizedVersion : bool }

type UrlId =
    | GetVersion_ById of SendDataModification
    | GetVersion_Filter of SendDataModification * GetVersionFilter

type UrlToTry =
    { UrlId : UrlId; InstanceUrl : string }

    static member From id (p:Printf.StringFormat<_,_>) =
        Printf.ksprintf (fun s -> {UrlId = id; InstanceUrl = s}) p

type BlockedCacheEntry =
    { BlockedFormats : string list }

let private tryUrlOrBlacklistI =
    let tryUrlOrBlacklistInner (f : unit -> Async<obj>, isOk : obj -> bool) (cacheKey) =
        async {
            //try
            let! res = f ()
            return isOk res, res
        }
    let memoizedBlackList = memoizeAsyncEx tryUrlOrBlacklistInner
    fun f isOk cacheKey ->
            memoizedBlackList (f, isOk) (cacheKey)

let private tryUrlOrBlacklist (f: _ -> Async<'a>) (isOk : 'a -> bool) (source:NuGetSource, id:UrlId) =
    let res =
        tryUrlOrBlacklistI
            (fun s -> async { let! r = f s in return box r })
            (fun s -> isOk (s :?> 'a))
            (source,id)
    match res with
    | SubsequentCall r -> SubsequentCall r
    | FirstCall t ->
        FirstCall (t |> Task.Map (fun (l, r) -> l, (r :?> 'a)))

type QueryResult = Choice<ODataSearchResult,System.Exception>

let tryAndBlacklistUrl doBlackList doWarn (source:NuGetSource)
    (tryAgain : QueryResult -> bool) (f : string -> Async<QueryResult>) (urls: UrlToTry list) : Async<QueryResult>=
    async {
        let! tasks, resultIndex =
            urls
            |> Seq.map (fun url -> async {
                let cached =
                    if doBlackList then
                        tryUrlOrBlacklist (fun () -> async { return! f url.InstanceUrl }) (tryAgain >> not) (source, url.UrlId)
                    else
                        async {
                            let! result = f url.InstanceUrl
                            return (tryAgain result |> not, result)
                        } |> Async.StartAsTask |> FirstCall
                match cached with
                | SubsequentCall task ->
                    let! result = task |> Async.AwaitTask
                    if result then
                        let! result = f url.InstanceUrl
                        return Choice1Of3 result
                    else
                        return Choice3Of3 () // Url Blacklisted
                | FirstCall task ->
                    let! (isOk, res) = task |> Async.AwaitTask
                    if not isOk then
                        if doWarn then
                            traceWarnIfNotBefore url.InstanceUrl "Possible Performance degradation, blacklist '%s'" url.InstanceUrl
                        return Choice2Of3 res
                    else
                        return Choice1Of3 res
                })
            |> Async.tryFindSequential
                (fun result ->
                    match result with
                    | Choice1Of3 result ->
                        match result with       // as per NuGetV2.fs ...
                        | Choice1Of2 _ -> true  // this is the only valid result ...
                        | Choice2Of2 except ->
                            match except with  // but NotFound/404 should allow other query to succeed
                            | RequestStatus HttpStatusCode.NotFound -> false
                                               // repos may not support full filter syntax (Artifactory)
                            | RequestStatus HttpStatusCode.MethodNotAllowed -> false
                            | _ -> true        // for any other exceptions, cancel the rest and return
                    | _ -> false )

        match resultIndex with
        | Some i ->
            return
                match tasks.[i].Result with
                | Choice1Of3 res -> res
                | _ -> failwithf "Unexpected value"
        | None ->
            let lastResult =
                tasks
                |> Seq.filter (fun t -> t.IsCompleted)
                |> Seq.map (fun t -> t.Result)
                |> Seq.choose (function
                    | Choice3Of3 _ -> None
                    | Choice2Of3 res -> Some res
                    | Choice1Of3 res -> Some res)
                |> Seq.tryLast

            return
                match lastResult with
                | Some res -> res
                | None ->
                    let urls = urls |> Seq.map (fun u -> u.InstanceUrl) |> fun s -> String.Join("\r\t - ", s)
                    failwithf "All possible sources are already blacklisted. \r\t - %s" urls
    }
