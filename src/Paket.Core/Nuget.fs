/// Contains NuGet support.
module Paket.NuGet

open Paket.Domain
open Paket.Requirements

type NugetPackageCache =
    { Dependencies : (PackageName * VersionRequirement * FrameworkRestrictions) list
      PackageName : string
      SourceUrl: string
      Unlisted : bool
      DownloadUrl : string
      LicenseUrl : string
      CacheVersion: string }

    static member CurrentCacheVersion = "2.0"