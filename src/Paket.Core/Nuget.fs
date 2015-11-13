/// Contains NuGet support.
module Paket.NuGet

open Paket.Utils
open Paket.Domain
open Paket.Requirements

open System.IO

type NugetPackageCache =
    { Dependencies : (PackageName * VersionRequirement * FrameworkRestrictions) list
      PackageName : string
      SourceUrl: string
      Unlisted : bool
      DownloadUrl : string
      LicenseUrl : string
      CacheVersion: string }

    static member CurrentCacheVersion = "2.0"

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
    let cacheFile = FileInfo(Path.Combine(CacheFolder,packageUrl))
    let errorFile = FileInfo(cacheFile.FullName + ".failed")
    cacheFile,  errorFile