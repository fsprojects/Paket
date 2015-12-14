/// Contains NuGet support.
module Paket.NuGet

open Paket.Utils
open Paket.Domain
open Paket.Requirements
open Paket.Logging

open System.IO
open Chessie.ErrorHandling

open Newtonsoft.Json

type NugetPackageCache =
    { Dependencies : (PackageName * VersionRequirement * FrameworkRestrictions) list
      PackageName : string
      SourceUrl: string
      Unlisted : bool
      DownloadUrl : string
      LicenseUrl : string
      CacheVersion: string }

    static member CurrentCacheVersion = "2.2"

/// The NuGet cache folder.
let CacheFolder = 
    let appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData)
    let di = DirectoryInfo(Path.Combine(Path.Combine(appData, "NuGet"), "Cache"))
    if not di.Exists then
        di.Create()
    di.FullName

let inline normalizeUrl(url:string) = url.Replace("https","http").Replace("www.","")
let cacheFile nugetURL (packageName:PackageName) (version:SemVerInfo) =
    let h = nugetURL |> normalizeUrl |> hash |> abs
    let packageUrl = sprintf "%O.%s.s%d.json" packageName (version.Normalize()) h
    FileInfo(Path.Combine(CacheFolder,packageUrl))


let getDetailsFromCacheOr force nugetURL (packageName:PackageName) (version:SemVerInfo) (get : unit -> NugetPackageCache Async) : NugetPackageCache Async = 
    let cacheFile = cacheFile nugetURL packageName version
    let get() = 
        async {
            let! result = get()
            File.WriteAllText(cacheFile.FullName,JsonConvert.SerializeObject(result))
            return result
        }
    async {
        if not force && cacheFile.Exists then
            let json = File.ReadAllText(cacheFile.FullName)
            let result =
                try
                    let cachedObject = JsonConvert.DeserializeObject<NugetPackageCache> json
                    ok cachedObject
                with
                | exn -> 
                    fail exn
            return!
                match result with
                | Ok (cachedObject, _) -> 
                    if cachedObject.CacheVersion <> NugetPackageCache.CurrentCacheVersion then
                        cacheFile.Delete()
                        get()
                    else
                        async { return cachedObject }
                | _ -> get()
        else
            return! get()
    }