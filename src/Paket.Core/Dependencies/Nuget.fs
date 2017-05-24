/// Contains NuGet support.
module Paket.NuGet

open Paket.Utils
open Paket.Domain
open Paket.Requirements
open Paket.Logging

open System.IO
open Chessie.ErrorHandling

open Newtonsoft.Json
open System

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

    static member CurrentCacheVersion = "4.0"

module NuGetPackageCache =
    let withDependencies (l:(PackageName * VersionRequirement * FrameworkRestrictions) list) d =
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
    let getDependencies (x:NuGetPackageCache) : (PackageName * VersionRequirement * FrameworkRestrictions) list  =
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
let getDetailsFromCacheOr force nugetURL (packageName:PackageName) (version:SemVerInfo) (get : unit -> NuGetPackageCache Async) : NuGetPackageCache Async = 
    let cacheFile, oldFiles = getCacheFiles NuGetPackageCache.CurrentCacheVersion nugetURL packageName version
    oldFiles |> Seq.iter (fun f -> File.Delete f)
    let get() = 
        async {
            let! result = get()
            File.WriteAllText(cacheFile.FullName,JsonConvert.SerializeObject(result))
            return result
        }
    async {
        if not force && cacheFile.Exists then
            let json = File.ReadAllText(cacheFile.FullName)
            try
                let cachedObject = JsonConvert.DeserializeObject<NuGetPackageCache> json
                    
                if (PackageName cachedObject.PackageName <> packageName) ||
                  (cachedObject.Version <> version.Normalize())
                then
                    traceVerbose (sprintf "Invalidating Cache '%s:%s' <> '%s:%s'" cachedObject.PackageName cachedObject.Version packageName.Name (version.Normalize()))
                    cacheFile.Delete()
                    return! get()
                else
                    return cachedObject
            with
            | exn ->
                cacheFile.Delete()
                if verbose then
                    traceWarnfn "Error while loading cache: %O" exn
                else
                    traceWarnfn "Error while loading cache: %s" exn.Message
                return! get()
        else
            return! get()
    }