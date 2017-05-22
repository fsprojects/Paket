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
       


type NuGetPackageCache =
    { Dependencies : (PackageName * VersionRequirement * FrameworkRestrictions) list
      PackageName : string
      SourceUrl: string
      Unlisted : bool
      DownloadUrl : string
      LicenseUrl : string
      Version: string
      CacheVersion: string }

    static member CurrentCacheVersion = "4.0"

let inline normalizeUrl(url:string) = url.Replace("https://","http://").Replace("www.","")

let getCacheFileName nugetURL (packageName:PackageName) (version:SemVerInfo) =
    let h = nugetURL |> normalizeUrl |> hash |> abs
    let packageUrl = 
        sprintf "%O.%s.s%d.json" 
           packageName (version.Normalize()) h
    FileInfo(Path.Combine(Constants.NuGetCacheFolder,packageUrl))

let getDetailsFromCacheOr force nugetURL (packageName:PackageName) (version:SemVerInfo) (get : unit -> NuGetPackageCache Async) : NuGetPackageCache Async = 
    let cacheFile = getCacheFileName nugetURL packageName version
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
                    
                if (cachedObject.CacheVersion <> NuGetPackageCache.CurrentCacheVersion) ||
                  (PackageName cachedObject.PackageName <> packageName) ||
                  (cachedObject.Version <> version.Normalize())
                then
                    cacheFile.Delete()
                    return! get()
                else
                    return cachedObject
            with
            | exn -> return! get()
        else
            return! get()
    }