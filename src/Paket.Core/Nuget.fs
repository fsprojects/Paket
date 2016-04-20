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

type NuGetPackageCache =
    { Dependencies : (PackageName * VersionRequirement * FrameworkRestrictions) list
      PackageName : string
      SourceUrl: string
      Unlisted : bool
      DownloadUrl : string
      LicenseUrl : string
      Version: string
      CacheVersion: string }

    static member CurrentCacheVersion = "2.5"

/// The NuGet cache folder.
let CacheFolder = 
    let appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
    let di = DirectoryInfo(Path.Combine(Path.Combine(appData, "NuGet"), "Cache"))
    if not di.Exists then
        di.Create()
    di.FullName

let inline normalizeUrl(url:string) = url.Replace("https","http").Replace("www.","")

let getCacheFileName nugetURL (packageName:PackageName) (version:SemVerInfo) =
    let h = nugetURL |> normalizeUrl |> hash |> abs
    let packageUrl = 
        sprintf "%O.%s.s%d.json" 
           packageName (version.Normalize()) h
    FileInfo(Path.Combine(CacheFolder,packageUrl))

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