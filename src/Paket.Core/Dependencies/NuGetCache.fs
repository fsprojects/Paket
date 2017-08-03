/// Contains NuGet support.
module Paket.NuGetCache

open System
open System.IO
open Pri.LongPath
open System.Net
open Newtonsoft.Json
open System.IO.Compression
open Pri.LongPath
open System.Xml
open System.Text.RegularExpressions
open Paket.Logging
open System.Text

open Paket.Domain
open Paket.Utils
open Paket.Xml
open Paket.PackageSources
open Paket.Requirements
open FSharp.Polyfill
open System.Runtime.ExceptionServices

open Paket.Utils
open Paket.Domain
open Paket.Requirements
open Paket.Logging

open System.IO
open Pri.LongPath
open Chessie.ErrorHandling

open Newtonsoft.Json
open System

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
        | ProtocolNotCached -> [||]
        | FailedVersionRequest _ -> [||]
    member x.IsSuccess =
        match x with
        | SuccessVersionResponse _ -> true
        | ProtocolNotCached -> false
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
                    | UnknownError err -> FailedVersionRequest { Url = url; Error = err }
            })
    static member run (r:NuGetRequestGetVersions) : Async<NuGetResponseGetVersions> =
        async {
            try
                return! r.DoRequest()
            with e -> 
                return FailedVersionRequest { Url = r.Url; Error = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture e }
        }
        

// An unparsed file in the nuget package -> still need to inspect the path for further information. After parsing an entry will be part of a "LibFolder" for example.
type UnparsedPackageFile =
    { FullPath : string
      PathWithinPackage : string }

module NuGetConfig =
    open System.Text
    
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
        if not <| File.Exists fileName then
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

    static member CurrentCacheVersion = "5.1"

// TODO: is there a better way? for now we use static member because that works with type abbreviations...
//module NuGetPackageCache =
    static member withDependencies (l:(PackageName * VersionRequirement * FrameworkRestrictions) list) d =
        { d with
            SerializedDependencies =
                l
                |> List.map (fun (n,v, restrictions) ->
                    let restrictionString = 
                        match restrictions with
                        | FrameworkRestrictions.AutoDetectFramework -> "AUTO"
                        | FrameworkRestrictions.ExplicitRestriction re ->
                            re.ToString()
                    n, v, restrictionString) }
    static member getDependencies (x:NuGetPackageCache) : (PackageName * VersionRequirement * FrameworkRestrictions) list  =
        x.SerializedDependencies
        |> List.map (fun (n,v,restrictionString) ->
            let restrictions =
                if restrictionString = "AUTO" then
                    FrameworkRestrictions.AutoDetectFramework
                else FrameworkRestrictions.ExplicitRestriction(Requirements.parseRestrictions true restrictionString)
            n, v, restrictions)

let inline normalizeUrl(url:string) = url.Replace("https://","http://").Replace("www.","")

let getCacheFiles cacheVersion nugetURL (packageName:PackageName) (version:SemVerInfo) =
    let h = nugetURL |> normalizeUrl |> hash |> abs
    let prefix = 
        sprintf "%O.%s.s%d" packageName (version.Normalize()) h
    let packageUrl = 
        sprintf "%s_v%s.json" 
           prefix cacheVersion
    let newFile = Path.Combine(Constants.NuGetCacheFolder,packageUrl)
    let oldFiles =
        Directory.EnumerateFiles(Constants.NuGetCacheFolder, sprintf "%s*.json" prefix)
        |> Seq.filter (fun p -> Path.GetFileName p <> packageUrl)
        |> Seq.toList
    FileInfo(newFile), oldFiles

type ODataSearchResult =
    | EmptyResult
    | Match of NuGetPackageCache
module ODataSearchResult =
    let get x =
        match x with
        | EmptyResult -> failwithf "Cannot call get on 'EmptyResult'"
        | Match r -> r

let tryGetDetailsFromCache force nugetURL (packageName:PackageName) (version:SemVerInfo) : ODataSearchResult option =
    let cacheFile, oldFiles = getCacheFiles NuGetPackageCache.CurrentCacheVersion nugetURL packageName version
    oldFiles |> Seq.iter (fun f -> File.Delete f)
    if not force && cacheFile.Exists then
        let json = File.ReadAllText(cacheFile.FullName)
        let cacheResult =
            try
                let cachedObject = JsonConvert.DeserializeObject<NuGetPackageCache> json
                if (PackageName cachedObject.PackageName <> packageName) ||
                    (cachedObject.Version <> version.Normalize())
                then
                    traceVerbose (sprintf "Invalidating Cache '%s:%s' <> '%s:%s'" cachedObject.PackageName cachedObject.Version packageName.Name (version.Normalize()))
                    cacheFile.Delete()
                    None
                else
                    Some cachedObject
            with
            | exn ->
                cacheFile.Delete()
                if verbose then
                    traceWarnfn "Error while loading cache: %O" exn
                else
                    traceWarnfn "Error while loading cache: %s" exn.Message
                None
        match cacheResult with
        | Some res -> Some (ODataSearchResult.Match res)
        | None -> None
    else
        None

let getDetailsFromCacheOr force nugetURL (packageName:PackageName) (version:SemVerInfo) (get : unit -> ODataSearchResult Async) : ODataSearchResult Async =
    let cacheFile, oldFiles = getCacheFiles NuGetPackageCache.CurrentCacheVersion nugetURL packageName version
    oldFiles |> Seq.iter (fun f -> File.Delete f)
    let get() =
        async {
            let! result = get()
            match result with
            | ODataSearchResult.Match result ->
                File.WriteAllText(cacheFile.FullName,JsonConvert.SerializeObject(result))
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


let inline isExtracted (directory:DirectoryInfo) fileName =
    let fi = FileInfo(fileName)
    if not fi.Exists then false else
    if not directory.Exists then false else
    directory.EnumerateFileSystemInfos()
    |> Seq.exists (fun f -> f.FullName <> fi.FullName)

let IsPackageVersionExtracted(root, groupName, packageName:PackageName, version:SemVerInfo, includeVersionInPath) =
    let targetFolder = DirectoryInfo(getTargetFolder root groupName packageName version includeVersionInPath)
    let targetFileName = packageName.ToString() + "." + version.Normalize() + ".nupkg"
    isExtracted targetFolder targetFileName

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
            if not <| Directory.Exists dir then
                Directory.CreateDirectory dir |> ignore

            File.Move(file.FullName, newFullName)


/// Extracts the given package to the user folder
let ExtractPackageToUserFolder(fileName:string, packageName:PackageName, version:SemVerInfo, isCliTool, detailed) =
    async {
        let targetFolder =
            if isCliTool then
                DirectoryInfo(Path.Combine(Constants.UserNuGetPackagesFolder,".tools",packageName.ToString(),version.Normalize()))
            else
                DirectoryInfo(Path.Combine(Constants.UserNuGetPackagesFolder,packageName.ToString(),version.Normalize()))
        
        use _ = Profile.startCategory Profile.Category.FileIO
        if isExtracted targetFolder fileName |> not then
            Directory.CreateDirectory(targetFolder.FullName) |> ignore
            let fi = FileInfo fileName
            let targetPackageFileName = Path.Combine(targetFolder.FullName,fi.Name)
            File.Copy(fileName,targetPackageFileName)

            ZipFile.ExtractToDirectory(fileName, targetFolder.FullName)

            let cachedHashFile = Path.Combine(Constants.NuGetCacheFolder,fi.Name + ".sha512")
            if not <| File.Exists cachedHashFile then
                use stream = File.OpenRead(fileName)
                let packageSize = stream.Length
                use hasher = System.Security.Cryptography.SHA512.Create() :> System.Security.Cryptography.HashAlgorithm
                let packageHash = Convert.ToBase64String(hasher.ComputeHash(stream))
                File.WriteAllText(cachedHashFile,packageHash)

            File.Copy(cachedHashFile,targetPackageFileName + ".sha512")
            cleanup targetFolder
        return targetFolder.FullName
    }

/// Extracts the given package to the ./packages folder
let ExtractPackage(fileName:string, targetFolder, packageName:PackageName, version:SemVerInfo, isCliTool, detailed) =
    async {
        use _ = Profile.startCategory Profile.Category.FileIO
        let directory = DirectoryInfo(targetFolder)
        if isExtracted directory fileName then
             if verbose then
                 verbosefn "%O %O already extracted" packageName version
        else
            Directory.CreateDirectory(targetFolder) |> ignore

            try
                fixArchive fileName
                ZipFile.ExtractToDirectory(fileName, targetFolder)
            with
            | exn ->

                let text = if detailed then sprintf "%s In rare cases a firewall might have blocked the download. Please look into the file and see if it contains text with further information." Environment.NewLine else ""
                failwithf "Error during extraction of %s.%sMessage: %s%s" (Path.GetFullPath fileName) Environment.NewLine exn.Message text


            cleanup directory
            if verbose then
                verbosefn "%O %O unzipped to %s" packageName version targetFolder
        let! _ = ExtractPackageToUserFolder(fileName, packageName, version, isCliTool, detailed)
        return targetFolder
    }

let CopyLicenseFromCache(root, groupName, cacheFileName, packageName:PackageName, version:SemVerInfo, includeVersionInPath, force) =
    async {
        try
            if String.IsNullOrWhiteSpace cacheFileName then return () else
            let cacheFile = FileInfo cacheFileName
            if cacheFile.Exists then
                let targetFile = FileInfo(Path.Combine(getTargetFolder root groupName packageName version includeVersionInPath, "license.html"))
                if not force && targetFile.Exists then
                    if verbose then
                       verbosefn "License %O %O already copied" packageName version
                else
                    use _ = Profile.startCategory Profile.Category.FileIO
                    File.Copy(cacheFile.FullName, targetFile.FullName, true)
        with
        | exn -> traceWarnfn "Could not copy license for %O %O from %s.%s    %s" packageName version cacheFileName Environment.NewLine exn.Message
    }

/// Extracts the given package to the ./packages folder
let CopyFromCache(root, groupName, cacheFileName, licenseCacheFile, packageName:PackageName, version:SemVerInfo, isCliTool, includeVersionInPath, force, detailed) =
    async {
        let targetFolder = DirectoryInfo(getTargetFolder root groupName packageName version includeVersionInPath).FullName
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
            let! extracted = ExtractPackage(targetFile.FullName,targetFolder,packageName,version,isCliTool,detailed)
            do! CopyLicenseFromCache(root, groupName, licenseCacheFile, packageName, version, includeVersionInPath, force)
            return extracted
        with
        | exn ->
            use _ = Profile.startCategory Profile.Category.FileIO
            File.Delete targetFile.FullName
            Directory.Delete(targetFolder,true)
            return! raise exn
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
