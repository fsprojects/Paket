﻿namespace Paket

open System.IO
open System
open System.Diagnostics
open Logging


/// The .NET Standard version.
// Each time a new version is added NuGetPackageCache.CurrentCacheVersion should be bumped.
[<RequireQualifiedAccess>]
type DotNetStandardVersion =
    | V1_0
    | V1_1
    | V1_2
    | V1_3
    | V1_4
    | V1_5
    | V1_6
    | V2_0
    | V2_1
    override this.ToString() =
        match this with
        | V1_0 -> "v1.0"
        | V1_1 -> "v1.1"
        | V1_2 -> "v1.2"
        | V1_3 -> "v1.3"
        | V1_4 -> "v1.4"
        | V1_5 -> "v1.5"
        | V1_6 -> "v1.6"
        | V2_0 -> "v2.0"
        | V2_1 -> "v2.1"
    member this.ShortString() =
        match this with
        | DotNetStandardVersion.V1_0 -> "1.0"
        | DotNetStandardVersion.V1_1 -> "1.1"
        | DotNetStandardVersion.V1_2 -> "1.2"
        | DotNetStandardVersion.V1_3 -> "1.3"
        | DotNetStandardVersion.V1_4 -> "1.4"
        | DotNetStandardVersion.V1_5 -> "1.5"
        | DotNetStandardVersion.V1_6 -> "1.6"
        | DotNetStandardVersion.V2_0 -> "2.0"
        | DotNetStandardVersion.V2_1 -> "2.1"
    static member TryParse s =
        match s with
        | ""
        | "1"   -> Some(DotNetStandardVersion.V1_0)
        | "1.1" -> Some(DotNetStandardVersion.V1_1)
        | "1.2" -> Some(DotNetStandardVersion.V1_2)
        | "1.3" -> Some(DotNetStandardVersion.V1_3)
        | "1.4" -> Some(DotNetStandardVersion.V1_4)
        | "1.5" -> Some(DotNetStandardVersion.V1_5)
        | "1.6" -> Some(DotNetStandardVersion.V1_6)
        | "2"   -> Some(DotNetStandardVersion.V2_0)
        | "2.1" -> Some(DotNetStandardVersion.V2_1)
        | _ -> None

[<RequireQualifiedAccess>]
type Net5WindowsVersion =
    | V7_0
    | V8_0
    | V10_0_17763_0
    | V10_0_18362_0
    | V10_0_19041_0
    override this.ToString() =
        match this with
        | V7_0 -> "7.0"
        | V8_0 -> "8.0"
        | V10_0_17763_0 -> "10.0.17763.0"
        | V10_0_18362_0 -> "10.0.18362.0"
        | V10_0_19041_0 -> "10.0.19041.0"

    static member TryParse s =
        match s with
        | "" | "7.0" | "7" -> Some Net5WindowsVersion.V7_0
        | "8.0" | "8" -> Some Net5WindowsVersion.V8_0
        | "10.0.17763.0" | "10.0.17763" -> Some Net5WindowsVersion.V10_0_17763_0
        | "10.0.18362.0" | "10.0.18362" -> Some Net5WindowsVersion.V10_0_18362_0
        | "10.0.19041.0" | "10.0.19041" -> Some Net5WindowsVersion.V10_0_19041_0
        | _ -> None

[<RequireQualifiedAccess>]
type Net6WindowsVersion =
    | V7_0
    | V8_0
    | V10_0_17763_0
    | V10_0_18362_0
    | V10_0_19041_0
    override this.ToString() =
        match this with
        | V7_0 -> "7.0"
        | V8_0 -> "8.0"
        | V10_0_17763_0 -> "10.0.17763.0"
        | V10_0_18362_0 -> "10.0.18362.0"
        | V10_0_19041_0 -> "10.0.19041.0"

    static member TryParse s =
        match s with
        | "" | "7.0" | "7" -> Some Net6WindowsVersion.V7_0
        | "8.0" | "8" -> Some Net6WindowsVersion.V8_0
        | "10.0.17763.0" | "10.0.17763" -> Some Net6WindowsVersion.V10_0_17763_0
        | "10.0.18362.0" | "10.0.18362" -> Some Net6WindowsVersion.V10_0_18362_0
        | "10.0.19041.0" | "10.0.19041" -> Some Net6WindowsVersion.V10_0_19041_0
        | _ -> None

[<RequireQualifiedAccess>]
type Net7WindowsVersion =
    | V7_0
    | V8_0
    | V10_0_17763_0
    | V10_0_18362_0
    | V10_0_19041_0
    | V10_0_20348_0
    override this.ToString() =
        match this with
        | V7_0 -> "7.0"
        | V8_0 -> "8.0"
        | V10_0_17763_0 -> "10.0.17763.0"
        | V10_0_18362_0 -> "10.0.18362.0"
        | V10_0_19041_0 -> "10.0.19041.0"
        | V10_0_20348_0 -> "10.0.20348.0"

    static member TryParse s =
        match s with
        | "" | "7.0" | "7" -> Some Net7WindowsVersion.V7_0
        | "8.0" | "8" -> Some Net7WindowsVersion.V8_0
        | "10.0.17763.0" | "10.0.17763" -> Some Net7WindowsVersion.V10_0_17763_0
        | "10.0.18362.0" | "10.0.18362" -> Some Net7WindowsVersion.V10_0_18362_0
        | "10.0.19041.0" | "10.0.19041" -> Some Net7WindowsVersion.V10_0_19041_0
        | "10.0.20348.0" | "10.0.20348" -> Some Net7WindowsVersion.V10_0_20348_0
        | _ -> None

[<RequireQualifiedAccess>]
type Net8WindowsVersion =
    | V7_0
    | V8_0
    | V10_0_17763_0
    | V10_0_18362_0
    | V10_0_19041_0
    | V10_0_20348_0
    override this.ToString() =
        match this with
        | V7_0 -> "7.0"
        | V8_0 -> "8.0"
        | V10_0_17763_0 -> "10.0.17763.0"
        | V10_0_18362_0 -> "10.0.18362.0"
        | V10_0_19041_0 -> "10.0.19041.0"
        | V10_0_20348_0 -> "10.0.20348.0"

    static member TryParse s =
        match s with
        | "" | "7.0" | "7" -> Some Net8WindowsVersion.V7_0
        | "8.0" | "8" -> Some Net8WindowsVersion.V8_0
        | "10.0.17763.0" | "10.0.17763" -> Some Net8WindowsVersion.V10_0_17763_0
        | "10.0.18362.0" | "10.0.18362" -> Some Net8WindowsVersion.V10_0_18362_0
        | "10.0.19041.0" | "10.0.19041" -> Some Net8WindowsVersion.V10_0_19041_0
        | "10.0.20348.0" | "10.0.20348" -> Some Net8WindowsVersion.V10_0_20348_0
        | _ -> None

type Net9WindowsVersion = Net8WindowsVersion

[<RequireQualifiedAccess>]
type Net5Os =
    | Android
    | IOs
    | MacOs
    | TvOs
    | WatchOs
    override this.ToString() =
        match this with
        | Android -> "android"
        | IOs -> "ios"
        | MacOs -> "macos"
        | TvOs -> "tvos"
        | WatchOs -> "watchos"
    
    static member TryParse s =
        match s with
        | "android" -> Some Net5Os.Android
        | "ios" -> Some Net5Os.IOs
        | "macos" -> Some Net5Os.MacOs
        | "tvos" -> Some Net5Os.TvOs
        | "watchos" -> Some Net5Os.WatchOs
        | _ -> None

[<RequireQualifiedAccess>]
type Net6Os =
    | Android
    | IOs
    | MacOs
    | TvOs
    | WatchOs
    | MacCatalyst
    | Tizen
    override this.ToString() =
        match this with
        | Android -> "android"
        | IOs -> "ios"
        | MacOs -> "macos"
        | TvOs -> "tvos"
        | WatchOs -> "watchos"
        | MacCatalyst -> "maccatalyst"
        | Tizen -> "tizen"
    static member TryParse (s:string) =
        [
           ("android",Net6Os.Android)
           ("ios",Net6Os.IOs)
           ("macos",Net6Os.MacOs)
           ("tvos",Net6Os.TvOs)
           ("watchos",Net6Os.WatchOs)
           ("maccatalyst",Net6Os.MacCatalyst)
           ("tizen",Net6Os.Tizen)
         ] |> Seq.tryFind(fun (k,_) -> s.StartsWith k)
           |> Option.map snd
       
[<RequireQualifiedAccess>]
type Net7Os =
    | Android
    | IOs
    | MacOs
    | TvOs
    | WatchOs
    | MacCatalyst
    | Tizen
    override this.ToString() =
        match this with
        | Android -> "android"
        | IOs -> "ios"
        | MacOs -> "macos"
        | TvOs -> "tvos"
        | WatchOs -> "watchos"
        | MacCatalyst -> "maccatalyst"
        | Tizen -> "tizen"
           
    static member TryParse (s:string) =
        [
            ("android",Net7Os.Android)
            ("ios",Net7Os.IOs)
            ("macos",Net7Os.MacOs)
            ("tvos",Net7Os.TvOs)
            ("watchos",Net7Os.WatchOs)
            ("maccatalyst",Net7Os.MacCatalyst)
            ("tizen",Net7Os.Tizen)
        ] |> Seq.tryFind(fun (k,_) -> s.StartsWith k)
          |> Option.map snd
          
[<RequireQualifiedAccess>]
type Net8Os =
    | Android
    | IOs
    | MacOs
    | TvOs
    | WatchOs
    | MacCatalyst
    | Tizen
    override this.ToString() =
        match this with
        | Android -> "android"
        | IOs -> "ios"
        | MacOs -> "macos"
        | TvOs -> "tvos"
        | WatchOs -> "watchos"
        | MacCatalyst -> "maccatalyst"
        | Tizen -> "tizen"
           
    static member TryParse (s:string) =
        [
            "android",Net8Os.Android
            "ios"    ,Net8Os.IOs
            "macos"  ,Net8Os.MacOs
            "tvos"   ,Net8Os.TvOs
            "watchos",Net8Os.WatchOs
            "maccatalyst", Net8Os.MacCatalyst
            "tizen", Net8Os.Tizen
        ]
        |> Seq.tryFind(fun (k,_) -> s.StartsWith k)
        |> Option.map snd
         
type Net9Os = Net8Os         

[<RequireQualifiedAccess>]
/// The Framework version.
// Each time a new version is added NuGetPackageCache.CurrentCacheVersion should be bumped.
type FrameworkVersion =
    | V1
    | V1_1
    | V2
    | V3
    | V3_5
    | V4
    | V4_0_3
    | V4_5
    | V4_5_1
    | V4_5_2
    | V4_5_3
    | V4_6
    | V4_6_1
    | V4_6_2
    | V4_6_3
    | V4_7
    | V4_7_1
    | V4_7_2
    | V4_8
    | V4_8_1
    | V5
    | V6
    | V7
    | V8
    | V9
    override this.ToString() =
        match this with
        | V1        -> "v1.0"
        | V1_1      -> "v1.1"
        | V2        -> "v2.0"
        | V3        -> "v3.0"
        | V3_5      -> "v3.5"
        | V4        -> "v4.0"
        | V4_0_3    -> "v4.0.3"
        | V4_5      -> "v4.5"
        | V4_5_1    -> "v4.5.1"
        | V4_5_2    -> "v4.5.2"
        | V4_5_3    -> "v4.5.3"
        | V4_6      -> "v4.6"
        | V4_6_1    -> "v4.6.1"
        | V4_6_2    -> "v4.6.2"
        | V4_6_3    -> "v4.6.3"
        | V4_7      -> "v4.7"
        | V4_7_1    -> "v4.7.1"
        | V4_7_2    -> "v4.7.2"
        | V4_8      -> "v4.8"
        | V4_8_1    -> "v4.8.1"
        | V5        -> "v5.0"
        | V6        -> "v6.0"
        | V7        -> "v7.0"
        | V8        -> "v8.0"
        | V9        -> "v9.0"

    member this.ShortString() =
        match this with
        | FrameworkVersion.V1 -> "10"
        | FrameworkVersion.V1_1 -> "11"
        | FrameworkVersion.V2 -> "20"
        | FrameworkVersion.V3 -> "30"
        | FrameworkVersion.V3_5 -> "35"
        | FrameworkVersion.V4 -> "40"
        | FrameworkVersion.V4_0_3 -> "403"
        | FrameworkVersion.V4_5 -> "45"
        | FrameworkVersion.V4_5_1 -> "451"
        | FrameworkVersion.V4_5_2 -> "452"
        | FrameworkVersion.V4_5_3 -> "453"
        | FrameworkVersion.V4_6 -> "46"
        | FrameworkVersion.V4_6_1 -> "461"
        | FrameworkVersion.V4_6_2 -> "462"
        | FrameworkVersion.V4_6_3 -> "463"
        | FrameworkVersion.V4_7 -> "47"
        | FrameworkVersion.V4_7_1 -> "471"
        | FrameworkVersion.V4_7_2 -> "472"
        | FrameworkVersion.V4_8 -> "48"
        | FrameworkVersion.V4_8_1 -> "481"
        | FrameworkVersion.V5 -> "5.0"
        | FrameworkVersion.V6 -> "6.0"
        | FrameworkVersion.V7 -> "7.0"
        | FrameworkVersion.V8 -> "8.0"
        | FrameworkVersion.V9 -> "9.0"

    static member TryParse s =
        match s with
        | "" | "1" -> Some FrameworkVersion.V1
        | "1.1" -> Some FrameworkVersion.V1_1
        | "2" -> Some FrameworkVersion.V2
        | "3" -> Some FrameworkVersion.V3
        | "3.5" -> Some FrameworkVersion.V3_5
        | "4" -> Some FrameworkVersion.V4
        | "4.0.3" -> Some FrameworkVersion.V4_0_3
        | "4.5" -> Some FrameworkVersion.V4_5
        | "4.5.1" -> Some FrameworkVersion.V4_5_1
        | "4.5.2" -> Some FrameworkVersion.V4_5_2
        | "4.5.3" -> Some FrameworkVersion.V4_5_3
        | "4.6" -> Some FrameworkVersion.V4_6
        | "4.6.1" -> Some FrameworkVersion.V4_6_1
        | "4.6.2" -> Some FrameworkVersion.V4_6_2
        | "4.6.3" -> Some FrameworkVersion.V4_6_3
        | "4.7" -> Some FrameworkVersion.V4_7
        | "4.7.1" -> Some FrameworkVersion.V4_7_1
        | "4.7.2" -> Some FrameworkVersion.V4_7_2
        | "4.8" -> Some FrameworkVersion.V4_8
        | "4.8.1" -> Some FrameworkVersion.V4_8_1
        | "5" -> Some FrameworkVersion.V5
        | "6" -> Some FrameworkVersion.V6
        | "7" -> Some FrameworkVersion.V7
        | "8" -> Some FrameworkVersion.V8
        | "9" -> Some FrameworkVersion.V9
        | _ -> None

[<RequireQualifiedAccess>]
/// The UAP version.
// Each time a new version is added NuGetPackageCache.CurrentCacheVersion should be bumped.
type UAPVersion =
    | V10
    | V10_0_10240
    | V10_0_14393
    | V10_0_15138
    | V10_0_16299
    | V10_0_16300
    | V10_0_18362
    | V10_1
    override this.ToString() =
        match this with
        | V10 -> "10.0"
        | V10_0_10240 -> "10.0.10240"
        | V10_0_14393 -> "10.0.14393"
        | V10_0_15138 -> "10.0.15138"
        | V10_0_16299 -> "10.0.16299"
        | V10_0_16300 -> "10.0.16300"
        | V10_0_18362 -> "10.0.18362"
        | V10_1 -> "10.1"

    member this.ShortString() =
        match this with
        | UAPVersion.V10 -> "10.0"
        | UAPVersion.V10_0_10240 -> "10.0.10240"
        | UAPVersion.V10_0_14393 -> "10.0.14393"
        | UAPVersion.V10_0_15138 -> "10.0.15138"
        | UAPVersion.V10_0_16299 -> "10.0.16299"
        | UAPVersion.V10_0_16300 -> "10.0.16300"
        | UAPVersion.V10_0_18362 -> "10.0.18362"
        | UAPVersion.V10_1 -> "10.1"

    member this.NetCoreVersion =
        // WTF: https://github.com/onovotny/MSBuildSdkExtras/blob/8d2d4ad63b552481da06e646dbb6504abc415260/src/build/platforms/Windows.targets
        match this with
        // Assumed from C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETCore
        | UAPVersion.V10
        | UAPVersion.V10_0_10240
        | UAPVersion.V10_0_14393
        | UAPVersion.V10_0_15138
        | UAPVersion.V10_0_16299
        | UAPVersion.V10_0_16300
        | UAPVersion.V10_0_18362 -> "v5.0"
        // No idea, for now use 5.0 to keep project files constant
        // If someone starts complaining fix this and update the baselines.
        | UAPVersion.V10_1 -> "v5.0"

    static member TryParse s =
        match s with
        | "" | "1" | "10" -> Some UAPVersion.V10
        | "10.0.10240" -> Some UAPVersion.V10_1
        | "10.0.14393" -> Some UAPVersion.V10_1
        | "10.0.15138" -> Some UAPVersion.V10_1
        | "10.0.16299" -> Some UAPVersion.V10_1
        | "10.0.16300" -> Some UAPVersion.V10_1
        | "10.0.18362" -> Some UAPVersion.V10_1
        | "10.1" -> Some UAPVersion.V10_1
        | _ -> None

[<RequireQualifiedAccess>]
/// The .NET Standard version.
// Each time a new version is added NuGetPackageCache.CurrentCacheVersion should be bumped.
type DotNetCoreAppVersion =
    | V1_0
    | V1_1
    | V2_0
    | V2_1
    | V2_2
    | V3_0
    | V3_1

    member private this.NumKey =
        match this with
        | V1_0 -> 0
        | V1_1 -> 1
        | V2_0 -> 2
        | V2_1 -> 3
        | V2_2 -> 4
        | V3_0 -> 5
        | V3_1 -> 6

    static member private FromNum num =
        match num with
        | 0 -> V1_0
        | 1 -> V1_1
        | 2 -> V2_0
        | 3 -> V2_1
        | 4 -> V2_2
        | 5 -> V3_0
        | 6 -> V3_1
        | _   -> failwithf "'%i' has no corresponding framework version" num

    static member (<->) (lower:DotNetCoreAppVersion,upper:DotNetCoreAppVersion) =
        if lower.NumKey < upper.NumKey then
            [ lower.NumKey .. upper.NumKey ]
        else
            [ lower.NumKey .. -1 .. upper.NumKey ]
        |> List.map DotNetCoreAppVersion.FromNum

    override this.ToString() =
        match this with
        | V1_0 -> "v1.0"
        | V1_1 -> "v1.1"
        | V2_0 -> "v2.0"
        | V2_1 -> "v2.1"
        | V2_2 -> "v2.2"
        | V3_0 -> "v3.0"
        | V3_1 -> "v3.1"

    member this.ShortString() =
        match this with
        | DotNetCoreAppVersion.V1_0 -> "1.0"
        | DotNetCoreAppVersion.V1_1 -> "1.1"
        | DotNetCoreAppVersion.V2_0 -> "2.0"
        | DotNetCoreAppVersion.V2_1 -> "2.1"
        | DotNetCoreAppVersion.V2_2 -> "2.2"
        | DotNetCoreAppVersion.V3_0 -> "3.0"
        | DotNetCoreAppVersion.V3_1 -> "3.1"

    static member TryParse s =
        match s with
        | _ when s = "" || s = "1" -> Some DotNetCoreAppVersion.V1_0
        | _ when s = "1.1" -> Some DotNetCoreAppVersion.V1_1
        | _ when s = "2" || s = "2.0" -> Some DotNetCoreAppVersion.V2_0
        | _ when s = "2.1" -> Some DotNetCoreAppVersion.V2_1
        | _ when s = "2.2" -> Some DotNetCoreAppVersion.V2_2
        | _ when s = "3" || s = "3.0" -> Some DotNetCoreAppVersion.V3_0
        | _ when s = "3.1" -> Some DotNetCoreAppVersion.V3_1
        | _ -> None

[<RequireQualifiedAccess>]
/// The Framework version.
// Each time a new version is added NuGetPackageCache.CurrentCacheVersion should be bumped.
type DotNetUnityVersion =
    | V3_5_Web
    | V3_5_Micro
    | V3_5_Subset
    | V3_5_Full
    override this.ToString() =
        match this with
        | V3_5_Web    -> "net35-Unity Web v3.5"
        | V3_5_Micro  -> "net35-Unity Micro v3.5"
        | V3_5_Subset -> "net35-Unity Subset v3.5"
        | V3_5_Full   -> "net35-Unity Full v3.5"

    member this.ShortString() =
        match this with
        | DotNetUnityVersion.V3_5_Web -> "35-Unity Web v3.5"
        | DotNetUnityVersion.V3_5_Micro -> "35-Unity Micro v3.5"
        | DotNetUnityVersion.V3_5_Subset -> "35-Unity Subset v3.5"
        | DotNetUnityVersion.V3_5_Full -> "35-Unity Full v3.5"

[<RequireQualifiedAccess>]
type XCodeVersion =
    | V10
    | V11
    override this.ToString() =
        match this with
        | V10 -> "XCODE10"
        | V11 -> "XCODE11"

    member this.ShortString() =
        match this with
        | XCodeVersion.V10 -> "XCODE10"
        | XCodeVersion.V11 -> "XCODE11"

    static member TryParse s =
        match s with
        | "1" -> Some XCodeVersion.V10
        | "10" -> Some XCodeVersion.V10
        | "1.1" -> Some XCodeVersion.V11
        | "11" -> Some XCodeVersion.V11
        | _ -> None


module KnownAliases =
    let Data =
        [".netframework", "net"
         ".netcore", "netcore"
         ".netplatform", "dotnet"
         ".netportable", "portable"
         "netframework", "net"
         "netplatform", "dotnet"
         "winrt", "netcore"
         "silverlight", "sl"
         "windowsphoneapp", "wpa"
         "windowsphone", "wp"
         "windows", "win"
         "xamarin.", "xamarin"
         "netportable", "portable"
         ".net", "net"
         " ", "" ]
        |> List.map (fun (p,r) -> p.ToLower(),r.ToLower())
    let normalizeFramework (path:string) =
        let sb = new Text.StringBuilder(path.ToLower())
        for pattern,replacement in Data do
            sb.Replace(pattern,replacement) |> ignore
        sb.ToString()

type BuildMode =
    | Debug
    | Release
    | NoBuildMode
    | UnknownBuildMode of string
    member x.AsString =
        match x with
        | Debug -> "Debug"
        | Release -> "Release"
        | NoBuildMode -> ""
        | UnknownBuildMode s -> s
    override x.ToString() = x.AsString

type Platform =
    | Arm
    | X64
    | Win32
    | NoPlatform
    | UnknownPlatform of string
    member x.AsString =
        match x with
        | Arm -> "arm"
        | X64 -> "x64"
        | Win32 -> "Win32"
        | NoPlatform -> ""
        | UnknownPlatform s -> s
    override x.ToString() = x.AsString

[<RequireQualifiedAccess>]
type WindowsPhoneVersion =
    | V7
    | V7_1
    | V7_5
    | V8
    | V8_1
    member this.ShortString() =
        match this with
        | WindowsPhoneVersion.V7 -> "7"
        | WindowsPhoneVersion.V7_1 -> "71"
        | WindowsPhoneVersion.V7_5 -> "75"
        | WindowsPhoneVersion.V8 -> "8"
        | WindowsPhoneVersion.V8_1 -> "81"
    override this.ToString() =
        match this with
        | WindowsPhoneVersion.V7 -> "v7.0"
        | WindowsPhoneVersion.V7_1 -> "v7.1"
        | WindowsPhoneVersion.V7_5 -> "v7.5"
        | WindowsPhoneVersion.V8 -> "v8.0"
        | WindowsPhoneVersion.V8_1 -> "v8.1"
    static member TryParse s =
        match s with
        | "" | "7" -> Some WindowsPhoneVersion.V7
        | "7.1" -> Some WindowsPhoneVersion.V7_1
        | "7.5" -> Some WindowsPhoneVersion.V7_5
        | "8" -> Some WindowsPhoneVersion.V8
        | "8.1" -> Some WindowsPhoneVersion.V8_1
        | _ -> None
[<RequireQualifiedAccess>]
type WindowsPhoneAppVersion =
    | V8_1
    member this.ShortString() =
        match this with
        | WindowsPhoneAppVersion.V8_1 -> "81"
    override this.ToString() =
        match this with
        | WindowsPhoneAppVersion.V8_1 -> "v8.1"
    static member TryParse s =
        match s with
        | "" | "8.1" -> Some WindowsPhoneAppVersion.V8_1
        | _ -> None
[<RequireQualifiedAccess>]
type SilverlightVersion =
    | V3
    | V4
    | V5
    member this.ShortString() =
        match this with
        | SilverlightVersion.V3 -> "3"
        | SilverlightVersion.V4 -> "4"
        | SilverlightVersion.V5 -> "5"
    override this.ToString() =
        match this with
        | SilverlightVersion.V3 -> "v3.0"
        | SilverlightVersion.V4 -> "v4.0"
        | SilverlightVersion.V5 -> "v5.0"
    static member TryParse s =
        match s with
        | ""  | "3" -> Some SilverlightVersion.V3
        | "4" -> Some SilverlightVersion.V4
        | "5"  -> Some SilverlightVersion.V5
        | _ -> None
[<RequireQualifiedAccess>]
type MonoAndroidVersion =
    | V1
    | V2_2
    | V2_3
    | V4_0_3
    | V4_1
    | V4_2
    | V4_3
    | V4_4
    | V5
    | V5_1
    | V6
    | V7
    | V7_1
    | V8
    | V8_1
    | V9
    | V10
    | V11
    | V12
    member this.ShortString() =
        match this with
        | MonoAndroidVersion.V1    -> ""
        | MonoAndroidVersion.V2_2   -> "2.2"
        | MonoAndroidVersion.V2_3   -> "2.3"
        | MonoAndroidVersion.V4_0_3  -> "4.0.3"
        | MonoAndroidVersion.V4_1   -> "4.1"
        | MonoAndroidVersion.V4_2   -> "4.2"
        | MonoAndroidVersion.V4_3   -> "4.3"
        | MonoAndroidVersion.V4_4   -> "4.4"
        | MonoAndroidVersion.V5    -> "5.0"
        | MonoAndroidVersion.V5_1   -> "5.1"
        | MonoAndroidVersion.V6    -> "6.0"
        | MonoAndroidVersion.V7    -> "7.0"
        | MonoAndroidVersion.V7_1   -> "7.1"
        | MonoAndroidVersion.V8   -> "8.0"
        | MonoAndroidVersion.V8_1   -> "8.1"
        | MonoAndroidVersion.V9   -> "9.0"
        | MonoAndroidVersion.V10 -> "10.0"
        | MonoAndroidVersion.V11 -> "11.0"
        | MonoAndroidVersion.V12 -> "12.0"
    override this.ToString() =
        match this with
        | MonoAndroidVersion.V1    -> "v1.0"
        | MonoAndroidVersion.V2_2   -> "v2.2"
        | MonoAndroidVersion.V2_3   -> "v2.3"
        | MonoAndroidVersion.V4_0_3  -> "v4.0.3"
        | MonoAndroidVersion.V4_1   -> "v4.1"
        | MonoAndroidVersion.V4_2   -> "v4.2"
        | MonoAndroidVersion.V4_3   -> "v4.3"
        | MonoAndroidVersion.V4_4   -> "v4.4"
        | MonoAndroidVersion.V5    -> "v5.0"
        | MonoAndroidVersion.V5_1    -> "v5.1"
        | MonoAndroidVersion.V6    -> "v6.0"
        | MonoAndroidVersion.V7    -> "v7.0"
        | MonoAndroidVersion.V7_1   -> "v7.1"
        | MonoAndroidVersion.V8   -> "v8.0"
        | MonoAndroidVersion.V8_1   -> "v8.1"
        | MonoAndroidVersion.V9   -> "v9.0"
        | MonoAndroidVersion.V10 -> "v10.0"
        | MonoAndroidVersion.V11 -> "v11.0"
        | MonoAndroidVersion.V12 -> "v12.0"

    static member TryParse s =
        match s with
        | "" | "1" -> Some MonoAndroidVersion.V1
        | "2.2" -> Some MonoAndroidVersion.V2_2
        | "2.3" -> Some MonoAndroidVersion.V2_3
        | "4.0.3" -> Some MonoAndroidVersion.V4_0_3
        | "4.1" -> Some MonoAndroidVersion.V4_1
        | "4.2" -> Some MonoAndroidVersion.V4_2
        | "4.3" -> Some MonoAndroidVersion.V4_3
        | "4.4" -> Some MonoAndroidVersion.V4_4
        | "5" -> Some MonoAndroidVersion.V5
        | "5.1" -> Some MonoAndroidVersion.V5_1
        | "6" -> Some MonoAndroidVersion.V6
        | "7"
        | "7.0" -> Some MonoAndroidVersion.V7
        | "7.1" -> Some MonoAndroidVersion.V7_1
        | "8"
        | "8.0" -> Some MonoAndroidVersion.V8
        | "8.1" -> Some MonoAndroidVersion.V8_1
        | "9"
        | "9.0" -> Some MonoAndroidVersion.V9
        | "10"
        | "10.0" -> Some MonoAndroidVersion.V10
        | "11"
        | "11.0" -> Some MonoAndroidVersion.V11
        | "12"
        | "12.0" -> Some MonoAndroidVersion.V12
        | _ -> None

[<RequireQualifiedAccess>]
type WindowsVersion =
    | V8
    | V8_1
    | V10
    member this.NetCoreVersion =
        // WTF: https://github.com/onovotny/MSBuildSdkExtras/blob/8d2d4ad63b552481da06e646dbb6504abc415260/src/build/platforms/Windows.targets
        match this with
        | WindowsVersion.V8 -> "v4.5"
        | WindowsVersion.V8_1 -> "v4.5.1"
        // Assumed from C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETCore
        | WindowsVersion.V10 -> "v5.0"
    member this.ShortString() =
        match this with
        | WindowsVersion.V8 -> "8"
        | WindowsVersion.V8_1 -> "81"
        | WindowsVersion.V10 -> "10"
    override this.ToString() =
        match this with
        | WindowsVersion.V8 -> "v8.0"
        | WindowsVersion.V8_1 -> "v8.1"
        | WindowsVersion.V10 -> "v10.0"

[<RequireQualifiedAccess>]
type TizenVersion =
    | V3
    | V4
    | V6_5
    | V7
    | V8
    member this.ShortString() =
        match this with
        | V3 -> "3.0"
        | V4 -> "4.0"
        | V6_5 -> "6.5"
        | V7 -> "7.0"
        | V8 -> "8.0"
    override this.ToString() =
        match this with
        | V3 -> "v3.0"
        | V4 -> "v4.0"
        | V6_5 -> "v6.5"
        | V7 -> "v7.0"
        | V8 -> "v8.0"
    static member TryParse s =
        match s with
        | "" | "3" -> Some TizenVersion.V3
        | "4" -> Some TizenVersion.V4
        | "6.5" -> Some TizenVersion.V6_5
        | "7" -> Some TizenVersion.V7
        | "8" -> Some TizenVersion.V8
        | _ -> None

/// Framework Identifier type.
// Each time a new version is added NuGetPackageCache.CurrentCacheVersion should be bumped.
type FrameworkIdentifier =
    | DotNetFramework of FrameworkVersion
    | DotNet5WithOs of Net5Os
    | DotNet5Windows of Net5WindowsVersion
    | DotNet6WithOs of Net6Os
    | DotNet6Windows of Net6WindowsVersion
    | DotNet7WithOs of Net7Os
    | DotNet7Windows of Net7WindowsVersion
    | DotNet8WithOs of Net8Os
    | DotNet8Windows of Net8WindowsVersion
    | DotNet9WithOs of Net9Os
    | DotNet9Windows of Net9WindowsVersion
    | UAP of UAPVersion
    | DotNetStandard of DotNetStandardVersion
    | DotNetCoreApp of DotNetCoreAppVersion
    | DotNetUnity of DotNetUnityVersion
    | MonoAndroid of MonoAndroidVersion
    | MonoTouch
    | MonoMac
    | MacCatalyst
    | Native of BuildMode * Platform
    | XamarinTV
    | XamarinWatch
    | XamariniOS
    | XamarinMac
    | Windows of WindowsVersion
    | WindowsPhone of WindowsPhoneVersion
    | WindowsPhoneApp of WindowsPhoneAppVersion
    | Silverlight of SilverlightVersion
    | Tizen of TizenVersion
    | XCode of XCodeVersion
    | Unsupported of string

    override x.ToString() =
        match x with
        | DotNetFramework v -> "net" + v.ShortString()
        | DotNet9WithOs o  -> "net9.0-" + o.ToString()
        | DotNet9Windows v -> "net9.0-windows" + v.ToString()
        | DotNet8WithOs o  -> "net8.0-" + o.ToString()
        | DotNet8Windows v -> "net8.0-windows" + v.ToString()
        | DotNet7WithOs o  -> "net7.0-" + o.ToString()
        | DotNet7Windows v -> "net7.0-windows" + v.ToString()
        | DotNet6WithOs o -> "net6.0-" + o.ToString()
        | DotNet6Windows v -> "net6.0-windows" + v.ToString()
        | DotNet5WithOs o -> "net5.0-" + o.ToString()
        | DotNet5Windows v -> "net5.0-windows" + v.ToString()
        | DotNetStandard v -> "netstandard" + v.ShortString()
        | DotNetCoreApp v -> "netcoreapp" + v.ShortString()
        | DotNetUnity v -> "net" + v.ShortString()
        | MonoAndroid v -> "monoandroid" + v.ShortString()
        | MonoTouch -> "monotouch"
        | MonoMac -> "monomac"
        | MacCatalyst -> "maccatalyst"
        | Native(BuildMode.NoBuildMode, Platform.NoPlatform) -> "native"
        | Native(mode, platform) -> sprintf "native(%s,%s)" mode.AsString platform.AsString
        | XamarinTV -> "xamarintvos"
        | XamarinWatch -> "xamarinwatchos"
        | XamariniOS -> "xamarinios"
        | UAP v -> "uap" + v.ShortString()
        | XamarinMac -> "xamarinmac"
        | Windows v -> "win" + v.ShortString()
        | WindowsPhone v -> "wp" + v.ShortString()
        | WindowsPhoneApp v -> "wpa" + v.ShortString()
        | Silverlight v -> "sl" + v.ShortString()
        | Tizen v -> "tizen" + v.ShortString()
        | XCode v -> "xcode" + v.ShortString()
        | Unsupported s -> s


    member internal x.RawSupportedPlatformsTransitive =
        let findNewPlats (known:FrameworkIdentifier list) (lastStep:FrameworkIdentifier list) =
            lastStep
            |> List.collect (fun k -> k.RawSupportedPlatforms)
            |> List.filter (fun k -> known |> Seq.contains k |> not)

        Seq.initInfinite (fun _ -> 1)
        |> Seq.scan (fun state _ ->
            match state with
            | Some (known, lastStep) ->
                match findNewPlats known lastStep with
                | [] -> None
                | items -> Some (known @ items, items)
            | None -> None) (Some ([x], [x]))
        |> Seq.takeWhile (fun i -> i.IsSome)
        |> Seq.choose id
        |> Seq.last
        |> fst

    // returns a list of compatible platforms that this platform also supports
    member internal x.RawSupportedPlatforms =
        match x with
        | MonoAndroid MonoAndroidVersion.V1 -> []
        | MonoAndroid MonoAndroidVersion.V2_2 -> [ MonoAndroid MonoAndroidVersion.V1 ]
        | MonoAndroid MonoAndroidVersion.V2_3 -> [ MonoAndroid MonoAndroidVersion.V2_2 ]
        | MonoAndroid MonoAndroidVersion.V4_0_3 -> [ MonoAndroid MonoAndroidVersion.V2_3 ]
        | MonoAndroid MonoAndroidVersion.V4_1 -> [ MonoAndroid MonoAndroidVersion.V4_0_3 ]
        | MonoAndroid MonoAndroidVersion.V4_2 -> [ MonoAndroid MonoAndroidVersion.V4_1 ]
        | MonoAndroid MonoAndroidVersion.V4_3 -> [ MonoAndroid MonoAndroidVersion.V4_2 ]
        | MonoAndroid MonoAndroidVersion.V4_4 -> [ MonoAndroid MonoAndroidVersion.V4_3 ]
        //https://stackoverflow.com/questions/28170345/what-exactly-is-android-4-4w-vs-4-4-and-what-about-5-0-1
        //| MonoAndroid MonoAndroidVersion.V44W -> [ MonoAndroid MonoAndroidVersion.V44 ]
        | MonoAndroid MonoAndroidVersion.V5 -> [ MonoAndroid MonoAndroidVersion.V4_4]
        | MonoAndroid MonoAndroidVersion.V5_1 -> [ MonoAndroid MonoAndroidVersion.V5 ]
        | MonoAndroid MonoAndroidVersion.V6 -> [ MonoAndroid MonoAndroidVersion.V5_1 ]
        | MonoAndroid MonoAndroidVersion.V7 -> [ MonoAndroid MonoAndroidVersion.V6; DotNetStandard DotNetStandardVersion.V1_6 ]
        | MonoAndroid MonoAndroidVersion.V7_1 -> [ MonoAndroid MonoAndroidVersion.V7 ]
        | MonoAndroid MonoAndroidVersion.V8 -> [ MonoAndroid MonoAndroidVersion.V7_1; DotNetStandard DotNetStandardVersion.V2_0 ]
        | MonoAndroid MonoAndroidVersion.V8_1 -> [ MonoAndroid MonoAndroidVersion.V8 ]
        | MonoAndroid MonoAndroidVersion.V9 -> [ MonoAndroid MonoAndroidVersion.V8_1 ]
        | MonoAndroid MonoAndroidVersion.V10 -> [ MonoAndroid MonoAndroidVersion.V9; DotNetStandard DotNetStandardVersion.V2_1 ]
        | MonoAndroid MonoAndroidVersion.V11 -> [ MonoAndroid MonoAndroidVersion.V10 ]
        | MonoAndroid MonoAndroidVersion.V12 -> [ MonoAndroid MonoAndroidVersion.V11 ]
        | MonoTouch -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | MonoMac -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | Native _ -> [ ]
        | XamariniOS -> [ DotNetStandard DotNetStandardVersion.V1_6; DotNetStandard DotNetStandardVersion.V2_0; DotNetStandard DotNetStandardVersion.V2_1 ]
        | XamarinMac -> [ DotNetStandard DotNetStandardVersion.V1_6; DotNetStandard DotNetStandardVersion.V2_0; DotNetStandard DotNetStandardVersion.V2_1 ]
        | XamarinTV -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | XamarinWatch -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | UAP UAPVersion.V10 -> [ Windows WindowsVersion.V8_1; WindowsPhoneApp WindowsPhoneAppVersion.V8_1; DotNetStandard DotNetStandardVersion.V1_4  ]
        | UAP UAPVersion.V10_0_10240 -> [ UAP UAPVersion.V10 ]
        | UAP UAPVersion.V10_0_14393 -> [ UAP UAPVersion.V10 ]
        | UAP UAPVersion.V10_0_15138 -> [ UAP UAPVersion.V10 ]
        | UAP UAPVersion.V10_0_16299 -> [ UAP UAPVersion.V10; DotNetStandard DotNetStandardVersion.V2_0 ]
        | UAP UAPVersion.V10_0_16300 -> [ UAP UAPVersion.V10; DotNetStandard DotNetStandardVersion.V2_0 ]
        | UAP UAPVersion.V10_0_18362 -> [ UAP UAPVersion.V10; DotNetStandard DotNetStandardVersion.V2_0 ]
        | UAP UAPVersion.V10_1 -> [ UAP UAPVersion.V10_0_15138 ]
        | DotNetFramework FrameworkVersion.V1 -> [ ]
        | DotNetFramework FrameworkVersion.V1_1 -> [ DotNetFramework FrameworkVersion.V1 ]
        | DotNetFramework FrameworkVersion.V2 -> [ DotNetFramework FrameworkVersion.V1_1 ]
        | DotNetFramework FrameworkVersion.V3 -> [ DotNetFramework FrameworkVersion.V2 ]
        | DotNetFramework FrameworkVersion.V3_5 -> [ DotNetFramework FrameworkVersion.V3 ]
        | DotNetFramework FrameworkVersion.V4 -> [ DotNetFramework FrameworkVersion.V3_5 ]
        | DotNetFramework FrameworkVersion.V4_0_3 -> [ DotNetFramework FrameworkVersion.V4 ]
        | DotNetFramework FrameworkVersion.V4_5 -> [ DotNetFramework FrameworkVersion.V4_0_3; DotNetStandard DotNetStandardVersion.V1_1 ]
        | DotNetFramework FrameworkVersion.V4_5_1 -> [ DotNetFramework FrameworkVersion.V4_5; DotNetStandard DotNetStandardVersion.V1_2 ]
        | DotNetFramework FrameworkVersion.V4_5_2 -> [ DotNetFramework FrameworkVersion.V4_5_1; DotNetStandard DotNetStandardVersion.V1_2 ]
        | DotNetFramework FrameworkVersion.V4_5_3 -> [ DotNetFramework FrameworkVersion.V4_5_2; DotNetStandard DotNetStandardVersion.V1_2 ]
        | DotNetFramework FrameworkVersion.V4_6 -> [ DotNetFramework FrameworkVersion.V4_5_3; DotNetStandard DotNetStandardVersion.V1_3 ]
        | DotNetFramework FrameworkVersion.V4_6_1 -> [ DotNetFramework FrameworkVersion.V4_6; DotNetStandard DotNetStandardVersion.V1_4 ]
        | DotNetFramework FrameworkVersion.V4_6_2 -> [ DotNetFramework FrameworkVersion.V4_6_1; DotNetStandard DotNetStandardVersion.V1_5 ]
        | DotNetFramework FrameworkVersion.V4_6_3 -> [ DotNetFramework FrameworkVersion.V4_6_2 ]
        | DotNetFramework FrameworkVersion.V4_7 -> [ DotNetFramework FrameworkVersion.V4_6_3]
        | DotNetFramework FrameworkVersion.V4_7_1 -> [ DotNetFramework FrameworkVersion.V4_7; DotNetStandard DotNetStandardVersion.V2_0 ]
        | DotNetFramework FrameworkVersion.V4_7_2 -> [ DotNetFramework FrameworkVersion.V4_7_1 ]
        | DotNetFramework FrameworkVersion.V4_8 -> [ DotNetFramework FrameworkVersion.V4_7_2 ]
        | DotNetFramework FrameworkVersion.V4_8_1 -> [ DotNetFramework FrameworkVersion.V4_8 ]
        | DotNetFramework FrameworkVersion.V5 -> [ DotNetCoreApp DotNetCoreAppVersion.V3_1; DotNetStandard DotNetStandardVersion.V2_1 ]
        | DotNetFramework FrameworkVersion.V6 -> [ DotNetFramework FrameworkVersion.V5 ]
        | DotNetFramework FrameworkVersion.V7 -> [ DotNetFramework FrameworkVersion.V6 ]
        | DotNetFramework FrameworkVersion.V8 -> [ DotNetFramework FrameworkVersion.V7 ]
        | DotNetFramework FrameworkVersion.V9 -> [ DotNetFramework FrameworkVersion.V8 ]
        | DotNet5WithOs Net5Os.Android -> [ DotNetFramework FrameworkVersion.V5; MonoAndroid MonoAndroidVersion.V12 ]
        | DotNet5WithOs Net5Os.IOs -> [ DotNetFramework FrameworkVersion.V5; XamariniOS ]
        | DotNet5WithOs Net5Os.MacOs -> [ DotNetFramework FrameworkVersion.V5; XamarinMac ]
        | DotNet5WithOs Net5Os.TvOs -> [ DotNetFramework FrameworkVersion.V5; XamarinTV ]
        | DotNet5WithOs Net5Os.WatchOs -> [ DotNetFramework FrameworkVersion.V5; XamarinWatch ]
        | DotNet5Windows Net5WindowsVersion.V7_0 -> [ DotNetFramework FrameworkVersion.V5 ]
        | DotNet5Windows Net5WindowsVersion.V8_0 -> [ DotNetFramework FrameworkVersion.V5; DotNet5Windows Net5WindowsVersion.V7_0 ]
        | DotNet5Windows Net5WindowsVersion.V10_0_17763_0 -> [ DotNetFramework FrameworkVersion.V5; DotNet5Windows Net5WindowsVersion.V8_0 ]
        | DotNet5Windows Net5WindowsVersion.V10_0_18362_0 -> [ DotNetFramework FrameworkVersion.V5; DotNet5Windows Net5WindowsVersion.V10_0_17763_0 ]
        | DotNet5Windows Net5WindowsVersion.V10_0_19041_0 -> [ DotNetFramework FrameworkVersion.V5; DotNet5Windows Net5WindowsVersion.V10_0_18362_0 ]
        | DotNet6WithOs  Net6Os.Android -> [ DotNetFramework FrameworkVersion.V6; MonoAndroid MonoAndroidVersion.V12 ]
        | DotNet6WithOs  Net6Os.IOs     -> [ DotNetFramework FrameworkVersion.V6; XamariniOS ]
        | DotNet6WithOs  Net6Os.MacOs   -> [ DotNetFramework FrameworkVersion.V6; XamarinMac ]
        | DotNet6WithOs  Net6Os.TvOs    -> [ DotNetFramework FrameworkVersion.V6; XamarinTV ]
        | DotNet6WithOs  Net6Os.WatchOs -> [ DotNetFramework FrameworkVersion.V6; XamarinWatch ]
        | DotNet6WithOs  Net6Os.MacCatalyst -> [ DotNetFramework FrameworkVersion.V6; MacCatalyst ]
        | DotNet6WithOs  Net6Os.Tizen -> [ DotNetFramework FrameworkVersion.V6; Tizen TizenVersion.V6_5 ]
        | DotNet7WithOs  Net7Os.Android -> [ DotNetFramework FrameworkVersion.V7; MonoAndroid MonoAndroidVersion.V12 ]
        | DotNet7WithOs  Net7Os.IOs     -> [ DotNetFramework FrameworkVersion.V7; XamariniOS ]
        | DotNet7WithOs  Net7Os.MacOs   -> [ DotNetFramework FrameworkVersion.V7; XamarinMac ]
        | DotNet7WithOs  Net7Os.TvOs    -> [ DotNetFramework FrameworkVersion.V7; XamarinTV ]
        | DotNet7WithOs  Net7Os.WatchOs -> [ DotNetFramework FrameworkVersion.V7; XamarinWatch ]
        | DotNet7WithOs  Net7Os.MacCatalyst -> [ DotNetFramework FrameworkVersion.V7; MacCatalyst ]
        | DotNet7WithOs  Net7Os.Tizen -> [ DotNetFramework FrameworkVersion.V7; Tizen TizenVersion.V7 ]
        | DotNet8WithOs  Net8Os.Android -> [ DotNetFramework FrameworkVersion.V8; MonoAndroid MonoAndroidVersion.V12 ]
        | DotNet8WithOs  Net8Os.IOs     -> [ DotNetFramework FrameworkVersion.V8; XamariniOS ]
        | DotNet8WithOs  Net8Os.MacOs   -> [ DotNetFramework FrameworkVersion.V8; XamarinMac ]
        | DotNet8WithOs  Net8Os.TvOs    -> [ DotNetFramework FrameworkVersion.V8; XamarinTV ]
        | DotNet8WithOs  Net8Os.WatchOs -> [ DotNetFramework FrameworkVersion.V8; XamarinWatch ]
        | DotNet8WithOs  Net8Os.MacCatalyst -> [ DotNetFramework FrameworkVersion.V8; MacCatalyst ]
        | DotNet8WithOs  Net8Os.Tizen -> [ DotNetFramework FrameworkVersion.V8; Tizen TizenVersion.V8 ]
        | DotNet9WithOs  Net9Os.Android -> [ DotNetFramework FrameworkVersion.V9; MonoAndroid MonoAndroidVersion.V12 ]
        | DotNet9WithOs  Net9Os.IOs     -> [ DotNetFramework FrameworkVersion.V9; XamariniOS ]
        | DotNet9WithOs  Net9Os.MacOs   -> [ DotNetFramework FrameworkVersion.V9; XamarinMac ]
        | DotNet9WithOs  Net9Os.TvOs    -> [ DotNetFramework FrameworkVersion.V9; XamarinTV ]
        | DotNet9WithOs  Net9Os.WatchOs -> [ DotNetFramework FrameworkVersion.V9; XamarinWatch ]
        | DotNet9WithOs  Net9Os.MacCatalyst -> [ DotNetFramework FrameworkVersion.V9; MacCatalyst ]
        | DotNet9WithOs  Net9Os.Tizen -> [ DotNetFramework FrameworkVersion.V9; Tizen TizenVersion.V8 ]
        | DotNet6Windows Net6WindowsVersion.V7_0          -> [ DotNetFramework FrameworkVersion.V6 ]
        | DotNet6Windows Net6WindowsVersion.V8_0          -> [ DotNetFramework FrameworkVersion.V6; DotNet6Windows Net6WindowsVersion.V7_0 ]
        | DotNet6Windows Net6WindowsVersion.V10_0_17763_0 -> [ DotNetFramework FrameworkVersion.V6; DotNet6Windows Net6WindowsVersion.V8_0 ]
        | DotNet6Windows Net6WindowsVersion.V10_0_18362_0 -> [ DotNetFramework FrameworkVersion.V6; DotNet6Windows Net6WindowsVersion.V10_0_17763_0 ]
        | DotNet6Windows Net6WindowsVersion.V10_0_19041_0 -> [ DotNetFramework FrameworkVersion.V6; DotNet6Windows Net6WindowsVersion.V10_0_18362_0 ]
        | DotNet7Windows Net7WindowsVersion.V7_0          -> [ DotNetFramework FrameworkVersion.V7 ]
        | DotNet7Windows Net7WindowsVersion.V8_0          -> [ DotNetFramework FrameworkVersion.V7; DotNet7Windows Net7WindowsVersion.V7_0 ]
        | DotNet7Windows Net7WindowsVersion.V10_0_17763_0 -> [ DotNetFramework FrameworkVersion.V7; DotNet7Windows Net7WindowsVersion.V8_0 ]
        | DotNet7Windows Net7WindowsVersion.V10_0_18362_0 -> [ DotNetFramework FrameworkVersion.V7; DotNet7Windows Net7WindowsVersion.V10_0_17763_0 ]
        | DotNet7Windows Net7WindowsVersion.V10_0_19041_0 -> [ DotNetFramework FrameworkVersion.V7; DotNet7Windows Net7WindowsVersion.V10_0_18362_0 ]
        | DotNet7Windows Net7WindowsVersion.V10_0_20348_0 -> [ DotNetFramework FrameworkVersion.V7; DotNet7Windows Net7WindowsVersion.V10_0_19041_0 ]
        | DotNet8Windows Net8WindowsVersion.V7_0          -> [ DotNetFramework FrameworkVersion.V8; ]
        | DotNet8Windows Net8WindowsVersion.V8_0          -> [ DotNetFramework FrameworkVersion.V8; DotNet8Windows Net8WindowsVersion.V7_0]
        | DotNet8Windows Net8WindowsVersion.V10_0_17763_0 -> [ DotNetFramework FrameworkVersion.V8; DotNet8Windows Net8WindowsVersion.V8_0]
        | DotNet8Windows Net8WindowsVersion.V10_0_18362_0 -> [ DotNetFramework FrameworkVersion.V8; DotNet8Windows Net8WindowsVersion.V10_0_17763_0 ]
        | DotNet8Windows Net8WindowsVersion.V10_0_19041_0 -> [ DotNetFramework FrameworkVersion.V8; DotNet8Windows Net8WindowsVersion.V10_0_18362_0 ]
        | DotNet8Windows Net8WindowsVersion.V10_0_20348_0 -> [ DotNetFramework FrameworkVersion.V8; DotNet8Windows Net8WindowsVersion.V10_0_19041_0 ]
        | DotNet9Windows Net9WindowsVersion.V7_0          -> [ DotNetFramework FrameworkVersion.V9; ]
        | DotNet9Windows Net9WindowsVersion.V8_0          -> [ DotNetFramework FrameworkVersion.V9; DotNet9Windows Net9WindowsVersion.V7_0]
        | DotNet9Windows Net9WindowsVersion.V10_0_17763_0 -> [ DotNetFramework FrameworkVersion.V9; DotNet9Windows Net9WindowsVersion.V8_0]
        | DotNet9Windows Net9WindowsVersion.V10_0_18362_0 -> [ DotNetFramework FrameworkVersion.V9; DotNet9Windows Net9WindowsVersion.V10_0_17763_0 ]
        | DotNet9Windows Net9WindowsVersion.V10_0_19041_0 -> [ DotNetFramework FrameworkVersion.V9; DotNet9Windows Net9WindowsVersion.V10_0_18362_0 ]
        | DotNet9Windows Net9WindowsVersion.V10_0_20348_0 -> [ DotNetFramework FrameworkVersion.V9; DotNet9Windows Net9WindowsVersion.V10_0_19041_0 ]
        // remark: for now, windows version for net 9 is alias to 8
        | DotNetStandard DotNetStandardVersion.V1_0 -> [  ]
        | DotNetStandard DotNetStandardVersion.V1_1 -> [ DotNetStandard DotNetStandardVersion.V1_0 ]
        | DotNetStandard DotNetStandardVersion.V1_2 -> [ DotNetStandard DotNetStandardVersion.V1_1 ]
        | DotNetStandard DotNetStandardVersion.V1_3 -> [ DotNetStandard DotNetStandardVersion.V1_2 ]
        | DotNetStandard DotNetStandardVersion.V1_4 -> [ DotNetStandard DotNetStandardVersion.V1_3 ]
        | DotNetStandard DotNetStandardVersion.V1_5 -> [ DotNetStandard DotNetStandardVersion.V1_4 ]
        | DotNetStandard DotNetStandardVersion.V1_6 -> [ DotNetStandard DotNetStandardVersion.V1_5 ]
        | DotNetStandard DotNetStandardVersion.V2_0 -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | DotNetStandard DotNetStandardVersion.V2_1 -> [ DotNetStandard DotNetStandardVersion.V2_0 ]
        | DotNetCoreApp DotNetCoreAppVersion.V1_0 -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | DotNetCoreApp DotNetCoreAppVersion.V1_1 -> [ DotNetCoreApp DotNetCoreAppVersion.V1_0 ]
        | DotNetCoreApp DotNetCoreAppVersion.V2_0 -> [ DotNetCoreApp DotNetCoreAppVersion.V1_1; DotNetStandard DotNetStandardVersion.V2_0 ]
        | DotNetCoreApp DotNetCoreAppVersion.V2_1 -> [ DotNetCoreApp DotNetCoreAppVersion.V2_0 ]
        | DotNetCoreApp DotNetCoreAppVersion.V2_2 -> [ DotNetCoreApp DotNetCoreAppVersion.V2_1 ]
        | DotNetCoreApp DotNetCoreAppVersion.V3_0 -> [ DotNetCoreApp DotNetCoreAppVersion.V2_2; DotNetStandard DotNetStandardVersion.V2_1 ]
        | DotNetCoreApp DotNetCoreAppVersion.V3_1 -> [ DotNetCoreApp DotNetCoreAppVersion.V3_0 ]
        | DotNetUnity DotNetUnityVersion.V3_5_Full -> [ ]
        | DotNetUnity DotNetUnityVersion.V3_5_Subset -> [ ]
        | DotNetUnity DotNetUnityVersion.V3_5_Micro -> [ ]
        | DotNetUnity DotNetUnityVersion.V3_5_Web -> [ ]
        | Silverlight SilverlightVersion.V3 -> [ ]
        | Silverlight SilverlightVersion.V4 -> [ Silverlight SilverlightVersion.V3 ]
        | Silverlight SilverlightVersion.V5 -> [ Silverlight SilverlightVersion.V4 ]
        | Windows WindowsVersion.V8 -> [ ]
        | Windows WindowsVersion.V8_1 -> [ Windows WindowsVersion.V8 ]
        | Windows WindowsVersion.V10 -> [ Windows WindowsVersion.V8_1 ]
        | WindowsPhoneApp WindowsPhoneAppVersion.V8_1 -> [ DotNetStandard DotNetStandardVersion.V1_2 ]
        | WindowsPhone WindowsPhoneVersion.V7 -> [ ]
        | WindowsPhone WindowsPhoneVersion.V7_1 -> [ WindowsPhone WindowsPhoneVersion.V7 ]
        | WindowsPhone WindowsPhoneVersion.V7_5 -> [ WindowsPhone WindowsPhoneVersion.V7_1 ]
        | WindowsPhone WindowsPhoneVersion.V8 -> [ WindowsPhone WindowsPhoneVersion.V7_5; DotNetStandard DotNetStandardVersion.V1_0 ]
        | WindowsPhone WindowsPhoneVersion.V8_1 -> [ WindowsPhone WindowsPhoneVersion.V8 ]
        | Tizen TizenVersion.V3 -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | Tizen TizenVersion.V4 -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | Tizen TizenVersion.V6_5 -> []
        | Tizen TizenVersion.V7 -> []
        | Tizen TizenVersion.V8 -> []
        | XCode XCodeVersion.V10 -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | XCode XCodeVersion.V11 -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | MacCatalyst -> []
        | Unsupported _ -> []

module FrameworkDetection =

    /// Used for script generation
    let resolveEnvironmentFramework = lazy (
        // HACK: resolve .net version based on environment
        // list of match is incomplete / inaccurate
    #if DOTNETCORE
        // Environment.Version is not supported
        //dunno what is used for, using a default
        DotNetFramework FrameworkVersion.V4_5
    #else
        let version = Environment.Version
        match version.Major, version.Minor, version.Build, version.Revision with
        | 4, 0, 30319, 42000 -> DotNetFramework FrameworkVersion.V4_6
        | 4, 0, 30319, _ -> DotNetFramework FrameworkVersion.V4_5
        | _ -> DotNetFramework FrameworkVersion.V4_5 // paket.exe is compiled for framework 4.5
    #endif
        )


    open Logging

    /// parse a string to construct a Netframework, NetCore, NetStandard, or other dotnet identifier
    let internal internalExtract =
        memoize
          (fun (path:string) ->
            let path = KnownAliases.normalizeFramework path
            let rec removeTrailingZeros (s:string) =
                if s.EndsWith ".0" then removeTrailingZeros (s.Substring(0, s.Length - 2))
                else s
            let tryNormalizeVersion (s:string) =
                // XYZ -> X.Y.Z
                // XX.Y.Z -> XX.Y.Z
                // XX.Y.0 -> XX.Y
                // 0X.Y.Z -> X.Y.Z
                // 0X.0Y.Z -> X.Y.Z
                let isValid = s |> Seq.forall(fun d -> (d >= '0' && d <= '9') || d = '.')
                let simplify s =
                    s
                    |> removeTrailingZeros
                    |> fun s -> if s = "0" then "" else s
                if isValid then
                    let versionString =
                        if s.Contains "." then
                            s
                        else
                            if s.Length = 1 || s.Length = 0 then s
                            else
                                s
                                |> Seq.map(fun d -> d.ToString() + ".")
                                |> Seq.collect id
                                |> Seq.take (s.Length * 2 - 1)
                                |> Seq.toArray
                                |> fun cs -> new String(cs)
                    if simplify versionString = "" then Some ""
                    else
                        let v = new Version(if versionString.Contains "." then versionString else sprintf "%s.0" versionString)
                        Some (v.ToString() |> simplify)
                else None

            let (|MatchTfm|_|) (tfmStart: string) tryParseVersion (s:string) =
                if s.StartsWith tfmStart then
                    let versionPart = s.Substring tfmStart.Length
                    tryNormalizeVersion versionPart
                    |> Option.bind tryParseVersion
                else
                    None
            let (|MatchNetXDashOs|_|) dotnetVersionX tryParseSecondPart (s:string) =
                let parts = s.Split('-')
                if parts.Length = 2 && s.StartsWith "net" then
                    let versionPart = parts.[0].Substring 3
                    tryNormalizeVersion versionPart
                    |> function
                    | Some "5" when dotnetVersionX = 5 -> tryParseSecondPart parts.[1]
                    | Some "6" when dotnetVersionX = 6  -> tryParseSecondPart parts.[1]
                    | Some "7" when dotnetVersionX = 7  -> tryParseSecondPart parts.[1]
                    | Some "8" when dotnetVersionX = 8  -> tryParseSecondPart parts.[1]
                    | Some "9" when dotnetVersionX = 9  -> tryParseSecondPart parts.[1]
                    | _ -> None
                else
                    None
            let (|MatchNetXDashWindows|_|) dotnetVersionX tryParseVersion (s:string) =
                let parts = s.Split('-')
                if parts.Length = 2 && s.StartsWith "net" && parts.[1].StartsWith "win" then
                    let netVersionPart = parts.[0].Substring 3
                    let winVersionPart = parts.[1].Substring 3
                    tryNormalizeVersion netVersionPart
                    |> function
                    | Some "5"  when dotnetVersionX = 5 -> tryParseVersion winVersionPart
                    | Some "6"  when dotnetVersionX = 6 -> tryParseVersion winVersionPart
                    | Some "7"  when dotnetVersionX = 7 -> tryParseVersion winVersionPart
                    | Some "8"  when dotnetVersionX = 8 -> tryParseVersion winVersionPart
                    | Some "9"  when dotnetVersionX = 9 -> tryParseVersion winVersionPart
                    | _ -> None
                else
                    None
            let (|MatchTfms|_|) (tfmStarts: string seq) tryParseVersion (s:string) =
                tfmStarts
                |> Seq.tryPick (fun tfmStart ->
                    match s with
                    | MatchTfm tfmStart (tryParseVersion tfmStart) fw -> Some fw
                    | _ -> None)
            let (|ModifyMatchTfm|_|) f tfmStart tryParseVersion (s:string) =
                match f s with
                | MatchTfm tfmStart tryParseVersion fw -> Some fw
                | _ -> None
            let Bind f = (fun _ -> f)
            let skipFullAndClient (s:string) =
                if s.EndsWith "-full" then s.Substring(0, s.Length - 5)
                elif s.EndsWith "-client" then s.Substring(0, s.Length - 7)
                else s
            let parseWindows tfmStart v =
                match tfmStart with
                | "win" | "windows" ->
                    match v with
                    | ""| "8" -> Some WindowsVersion.V8
                    | "8.1" -> Some WindowsVersion.V8_1
                    | _ -> None
                | "winv" | "netcore" ->
                    match v with
                    | "" | "4.5" -> Some WindowsVersion.V8
                    | "4.5.1" | "4.6" -> Some WindowsVersion.V8_1
                    | _ -> None
                | _ -> failwithf "unknown tfm '%s'" tfmStart
            let allowVersions l v =
                if l |> Seq.contains v then
                    Some ()
                else None
            // Each time the parsing is changed, NuGetPackageCache.CurrentCacheVersion should be bumped.
            // http://nugettoolsdev.azurewebsites.net/4.0.0/parse-framework?framework=.NETPortable%2CVersion%3Dv0.0%2CProfile%3DProfile2
            let result =
                match path with
                | MatchNetXDashWindows 9 Net9WindowsVersion.TryParse fm -> Some (DotNet9Windows fm)
                | MatchNetXDashWindows 8 Net8WindowsVersion.TryParse fm -> Some (DotNet8Windows fm)
                | MatchNetXDashWindows 7 Net7WindowsVersion.TryParse fm -> Some (DotNet7Windows fm)
                | MatchNetXDashWindows 6 Net6WindowsVersion.TryParse fm -> Some (DotNet6Windows fm)
                | MatchNetXDashWindows 5 Net5WindowsVersion.TryParse fm -> Some (DotNet5Windows fm)
                | MatchNetXDashOs 9 Net9Os.TryParse fm -> Some (DotNet9WithOs fm)
                | MatchNetXDashOs 8 Net8Os.TryParse fm -> Some (DotNet8WithOs fm)
                | MatchNetXDashOs 7 Net7Os.TryParse fm -> Some (DotNet7WithOs fm)
                | MatchNetXDashOs 6 Net6Os.TryParse fm -> Some (DotNet6WithOs fm)
                | MatchNetXDashOs 5 Net5Os.TryParse fm -> Some (DotNet5WithOs fm)
                | "netcoreapp5.0" -> Some (DotNetFramework FrameworkVersion.V5)
                | "netcoreapp8.0" -> Some (DotNetFramework FrameworkVersion.V8)
                | "netcoreapp9.0" -> Some (DotNetFramework FrameworkVersion.V9)
                | "net35-Unity Web v3.5" ->  Some (DotNetUnity DotNetUnityVersion.V3_5_Web)
                | "net35-Unity Micro v3.5" -> Some (DotNetUnity DotNetUnityVersion.V3_5_Micro)
                | "net35-Unity Subset v3.5" -> Some (DotNetUnity DotNetUnityVersion.V3_5_Subset)
                | "net35-Unity Full v3.5" -> Some (DotNetUnity DotNetUnityVersion.V3_5_Full)
                | ModifyMatchTfm skipFullAndClient "net" FrameworkVersion.TryParse fm -> Some (DotNetFramework fm)
                // Backwards compat quirk (2017-08-20).
                | "uap101" -> Some (UAP UAPVersion.V10_1)
                | MatchTfm "uap" UAPVersion.TryParse fm -> Some (UAP fm)
                | MatchTfm "monotouch" (allowVersions ["";"1"]) () -> Some MonoTouch
                | MatchTfm "monoandroid" MonoAndroidVersion.TryParse fm -> Some (MonoAndroid fm)
                | MatchTfm "monomac" (allowVersions ["";"1"]) () -> Some MonoMac
                | MatchTfm "xamarinios" (allowVersions ["";"1"]) () -> Some XamariniOS
                | MatchTfm "xamarinwatchos" (allowVersions ["";"1"]) () -> Some XamarinWatch
                | MatchTfm "xamarintvos" (allowVersions ["";"1"]) () -> Some XamarinTV
                | MatchTfm "xamarinmac" (allowVersions ["";"1";"2"]) () -> Some XamarinMac
                | "native/x86/debug" -> Some(Native(Debug,Win32))
                | "native/x64/debug" -> Some(Native(Debug,X64))
                | "native/arm/debug" -> Some(Native(Debug,Arm))
                | "native/x86/release" -> Some(Native(Release,Win32))
                | "native/x64/release" -> Some(Native(Release,X64))
                | "native/arm/release" -> Some(Native(Release,Arm))
                | "native/address-model-32" -> Some(Native(NoBuildMode,Win32))
                | "native/address-model-64" -> Some(Native(NoBuildMode,X64))
                | "native" -> Some(Native(NoBuildMode,NoPlatform))
                | MatchTfm "sl" SilverlightVersion.TryParse fm -> Some (Silverlight fm)
                | MatchTfms ["win"; "windows"; "netcore"; "winv"] parseWindows fm -> Some (Windows fm)
                | "sl4-wp7" | "sl4-wp70" | "sl4-wp7.0" -> Some (WindowsPhone WindowsPhoneVersion.V7)
                | "sl4-wp71" | "sl4-wp7.1" | "sl4-wp"-> Some (WindowsPhone WindowsPhoneVersion.V7_1)
                | "sl4-wp75" | "sl4-wp7.5" -> Some (WindowsPhone WindowsPhoneVersion.V7_5)
                | MatchTfms ["wp";"wpv"] (Bind WindowsPhoneVersion.TryParse) fm -> Some (WindowsPhone fm)
                | MatchTfms ["wpa";"wpav";"wpapp"] (Bind WindowsPhoneAppVersion.TryParse) fm -> Some (WindowsPhoneApp fm)
                | MatchTfm "netstandard" DotNetStandardVersion.TryParse fm -> Some (DotNetStandard fm)
                // "netcore" is for backwards compat (2017-08-20), we wrote this incorrectly into the lockfile.
                | MatchTfms ["netcoreapp";"netcore"] (Bind DotNetCoreAppVersion.TryParse) fm -> Some (DotNetCoreApp fm)
                // "dnxcore" and "dotnet" is for backwards compat (2019-03-26), we wrote this into the lockfile.
                | MatchTfm "dnx" (allowVersions ["";"4.5.1"]) () -> Some (Unsupported path)
                | MatchTfms ["dnxcore";"netplatform";"netcore";"aspnetcore";"aspnet";"dotnet"] (Bind (allowVersions ["";"5"]))
                    () -> Some (Unsupported path)
                | v when v.StartsWith "dotnet" -> Some (Unsupported path)
                | MatchTfm "tizen" TizenVersion.TryParse fm -> Some (Tizen fm)
                | MatchTfm "xcode" XCodeVersion.TryParse fm -> Some (XCode fm)
                // Default is full framework, for example "35"
                | MatchTfm "" FrameworkVersion.TryParse fm -> Some (DotNetFramework fm)
                | _ -> None
            result)

    /// parse a string to construct a Netframework, NetCore, NetStandard, or other dotnet identifier
    [<Obsolete "Use PlatformMatching.extractPlatforms instead">]
    let Extract path = internalExtract path

    let DetectFromPath(path : string) : FrameworkIdentifier option =
        let path = path.Replace("\\", "/").ToLower()
        let fi = new FileInfo(path)

        if String.containsIgnoreCase ("lib/" + fi.Name) path then Some(DotNetFramework(FrameworkVersion.V1))
        else
            let startPos = path.LastIndexOf("lib/")
            let endPos = path.LastIndexOf(fi.Name,StringComparison.OrdinalIgnoreCase)
            if startPos < 0 || endPos < 0 then None
            else
                internalExtract(path.Substring(startPos + 4, endPos - startPos - 5))


type PortableProfileType =
    | UnsupportedProfile of FrameworkIdentifier list
    /// portable-net40+sl4+win8+wp7
    | Profile2
    /// portable-net40+sl4
    | Profile3
    /// portable-net45+sl4+win8+wp7
    | Profile4
    /// portable-net40+win8
    | Profile5
    /// portable-net403+win8
    | Profile6
    /// portable-net45+win8
    | Profile7
    /// portable-net40+sl5
    | Profile14
    /// portable-net403+sl4
    | Profile18
    /// portable-net403+sl5
    | Profile19
    /// portable-net45+sl4
    | Profile23
    /// portable-net45+sl5
    | Profile24
    /// portable-win81+wp81
    | Profile31
    /// portable-win81+wpa81
    | Profile32
    /// portable-net40+sl4+win8+wp8
    | Profile36
    /// portable-net40+sl5+win8
    | Profile37
    /// portable-net403+sl4+win8
    | Profile41
    /// portable-net403+sl5+win8
    | Profile42
    /// portable-net451+win81
    | Profile44
    /// portable-net45+sl4+win8
    | Profile46
    /// portable-net45+sl5+win8
    | Profile47
    /// portable-net45+wp8
    | Profile49
    /// portable-net45+win8+wp8
    | Profile78
    /// portable-wp81+wpa81
    | Profile84
    /// portable-net40+sl4+win8+wp75
    | Profile88
    /// portable-net40+win8+wpa81
    | Profile92
    /// portable-net403+sl4+win8+wp7
    | Profile95
    /// portable-net403+sl4+win8+wp75
    | Profile96
    /// portable-net403+win8+wpa81
    | Profile102
    /// portable-net45+sl4+win8+wp75
    | Profile104
    /// portable-net45+win8+wpa81
    | Profile111
    /// portable-net40+sl5+win8+wp8
    | Profile136
    /// portable-net403+sl4+win8+wp8
    | Profile143
    /// portable-net403+sl5+win8+wp8
    | Profile147
    /// portable-net451+win81+wpa81
    | Profile151
    /// portable-net45+sl4+win8+wp8
    | Profile154
    /// portable-win81+wp81+wpa81
    | Profile157
    /// portable-net45+sl5+win8+wp8
    | Profile158
    /// portable-net40+sl5+win8+wpa81
    | Profile225
    /// portable-net403+sl5+win8+wpa81
    | Profile240
    /// portable-net45+sl5+win8+wpa81
    | Profile255
    /// portable-net45+win8+wp8+wpa81
    | Profile259
    /// portable-net40+sl5+win8+wp8+wpa81
    | Profile328
    /// portable-net403+sl5+win8+wp8+wpa81
    | Profile336
    /// portable-net45+sl5+win8+wp8+wpa81
    | Profile344
    member x.IsUnsupprted =
        match x with
        | UnsupportedProfile _ -> true
        | _ -> false

    member x.ProfileName =
        match x with
        | UnsupportedProfile fws -> x.FolderName
        | Profile2   -> "Profile2"
        | Profile3   -> "Profile3"
        | Profile4   -> "Profile4"
        | Profile5   -> "Profile5"
        | Profile6   -> "Profile6"
        | Profile7   -> "Profile7"
        | Profile14  -> "Profile14"
        | Profile18  -> "Profile18"
        | Profile19  -> "Profile19"
        | Profile23  -> "Profile23"
        | Profile24  -> "Profile24"
        | Profile31  -> "Profile31"
        | Profile32  -> "Profile32"
        | Profile36  -> "Profile36"
        | Profile37  -> "Profile37"
        | Profile41  -> "Profile41"
        | Profile42  -> "Profile42"
        | Profile44  -> "Profile44"
        | Profile46  -> "Profile46"
        | Profile47  -> "Profile47"
        | Profile49  -> "Profile49"
        | Profile78  -> "Profile78"
        | Profile84  -> "Profile84"
        | Profile88  -> "Profile88"
        | Profile92  -> "Profile92"
        | Profile95  -> "Profile95"
        | Profile96  -> "Profile96"
        | Profile102 -> "Profile102"
        | Profile104 -> "Profile104"
        | Profile111 -> "Profile111"
        | Profile136 -> "Profile136"
        | Profile143 -> "Profile143"
        | Profile147 -> "Profile147"
        | Profile151 -> "Profile151"
        | Profile154 -> "Profile154"
        | Profile157 -> "Profile157"
        | Profile158 -> "Profile158"
        | Profile225 -> "Profile225"
        | Profile240 -> "Profile240"
        | Profile255 -> "Profile255"
        | Profile259 -> "Profile259"
        | Profile328 -> "Profile328"
        | Profile336 -> "Profile336"
        | Profile344 -> "Profile344"

    member x.Frameworks =
        // http://nugettoolsdev.azurewebsites.net/4.0.0/parse-framework?framework=.NETPortable%2CVersion%3Dv0.0%2CProfile%3DProfile3
        match x with
        | UnsupportedProfile fws -> fws
        | Profile2   -> [ DotNetFramework FrameworkVersion.V4; Silverlight SilverlightVersion.V4; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V7 ]
        | Profile3   -> [ DotNetFramework FrameworkVersion.V4; Silverlight SilverlightVersion.V4 ]
        | Profile4   -> [ DotNetFramework FrameworkVersion.V4_5; Silverlight SilverlightVersion.V4; Windows  WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V7 ]
        | Profile5   -> [ DotNetFramework FrameworkVersion.V4; Windows WindowsVersion.V8 ]
        | Profile6   -> [ DotNetFramework FrameworkVersion.V4_0_3; Windows WindowsVersion.V8 ]
        | Profile7   -> [ DotNetFramework FrameworkVersion.V4_5; Windows WindowsVersion.V8 ]
        | Profile14  -> [ DotNetFramework FrameworkVersion.V4; Silverlight SilverlightVersion.V5 ]
        | Profile18  -> [ DotNetFramework FrameworkVersion.V4_0_3; Silverlight SilverlightVersion.V4 ]
        | Profile19  -> [ DotNetFramework FrameworkVersion.V4_0_3; Silverlight SilverlightVersion.V5 ]
        | Profile23  -> [ DotNetFramework FrameworkVersion.V4_5; Silverlight SilverlightVersion.V4 ]
        | Profile24  -> [ DotNetFramework FrameworkVersion.V4_5; Silverlight SilverlightVersion.V5 ]
        | Profile31  -> [ Windows WindowsVersion.V8_1; WindowsPhone WindowsPhoneVersion.V8_1 ]
        | Profile32  -> [ Windows WindowsVersion.V8_1; WindowsPhoneApp WindowsPhoneAppVersion.V8_1 ]
        | Profile36  -> [ DotNetFramework FrameworkVersion.V4; Silverlight SilverlightVersion.V4; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V8 ]
        | Profile37  -> [ DotNetFramework FrameworkVersion.V4; Silverlight SilverlightVersion.V5; Windows WindowsVersion.V8 ]
        | Profile41  -> [ DotNetFramework FrameworkVersion.V4_0_3; Silverlight SilverlightVersion.V4; Windows WindowsVersion.V8 ]
        | Profile42  -> [ DotNetFramework FrameworkVersion.V4_0_3; Silverlight SilverlightVersion.V5; Windows WindowsVersion.V8 ]
        | Profile44  -> [ DotNetFramework FrameworkVersion.V4_5_1; Windows WindowsVersion.V8_1 ]
        | Profile46  -> [ DotNetFramework FrameworkVersion.V4_5; Silverlight SilverlightVersion.V4; Windows WindowsVersion.V8 ]
        | Profile47  -> [ DotNetFramework FrameworkVersion.V4_5; Silverlight SilverlightVersion.V5; Windows WindowsVersion.V8 ]
        | Profile49  -> [ DotNetFramework FrameworkVersion.V4_5; WindowsPhone WindowsPhoneVersion.V8 ]
        | Profile78  -> [ DotNetFramework FrameworkVersion.V4_5; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V8 ]
        | Profile84  -> [ WindowsPhone WindowsPhoneVersion.V8_1; WindowsPhoneApp WindowsPhoneAppVersion.V8_1 ]
        | Profile88  -> [ DotNetFramework FrameworkVersion.V4; Silverlight SilverlightVersion.V4; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V7_5 ]
        | Profile92  -> [ DotNetFramework FrameworkVersion.V4; Windows WindowsVersion.V8; WindowsPhoneApp WindowsPhoneAppVersion.V8_1 ]
        | Profile95  -> [ DotNetFramework FrameworkVersion.V4_0_3; Silverlight SilverlightVersion.V4; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V7 ]
        | Profile96  -> [ DotNetFramework FrameworkVersion.V4_0_3; Silverlight SilverlightVersion.V4; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V7_5 ]
        | Profile102 -> [ DotNetFramework FrameworkVersion.V4_0_3; Windows WindowsVersion.V8; WindowsPhoneApp WindowsPhoneAppVersion.V8_1 ]
        | Profile104 -> [ DotNetFramework FrameworkVersion.V4_5; Silverlight SilverlightVersion.V4; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V7_5 ]
        | Profile111 -> [ DotNetFramework FrameworkVersion.V4_5; Windows WindowsVersion.V8; WindowsPhoneApp WindowsPhoneAppVersion.V8_1 ]
        | Profile136 -> [ DotNetFramework FrameworkVersion.V4; Silverlight SilverlightVersion.V5; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V8 ]
        | Profile143 -> [ DotNetFramework FrameworkVersion.V4_0_3; Silverlight SilverlightVersion.V4; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V8 ]
        | Profile147 -> [ DotNetFramework FrameworkVersion.V4_0_3; Silverlight SilverlightVersion.V5; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V8 ]
        | Profile151 -> [ DotNetFramework FrameworkVersion.V4_5_1; Windows WindowsVersion.V8_1; WindowsPhoneApp WindowsPhoneAppVersion.V8_1 ]
        | Profile154 -> [ DotNetFramework FrameworkVersion.V4_5; Silverlight SilverlightVersion.V4; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V8 ]
        | Profile157 -> [ Windows WindowsVersion.V8_1; WindowsPhoneApp WindowsPhoneAppVersion.V8_1; WindowsPhone WindowsPhoneVersion.V8_1 ]
        | Profile158 -> [ DotNetFramework FrameworkVersion.V4_5; Silverlight SilverlightVersion.V5; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V8 ]
        | Profile225 -> [ DotNetFramework  FrameworkVersion.V4; Silverlight SilverlightVersion.V5; Windows WindowsVersion.V8; WindowsPhoneApp WindowsPhoneAppVersion.V8_1 ]
        | Profile240 -> [ DotNetFramework FrameworkVersion.V4_0_3; Silverlight SilverlightVersion.V5; Windows WindowsVersion.V8; WindowsPhoneApp WindowsPhoneAppVersion.V8_1 ]
        | Profile255 -> [ DotNetFramework FrameworkVersion.V4_5; Silverlight SilverlightVersion.V5; Windows WindowsVersion.V8; WindowsPhoneApp WindowsPhoneAppVersion.V8_1 ]
        | Profile259 -> [ DotNetFramework FrameworkVersion.V4_5; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V8; WindowsPhoneApp WindowsPhoneAppVersion.V8_1 ]
        | Profile328 -> [ DotNetFramework FrameworkVersion.V4; Silverlight SilverlightVersion.V5; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V8; WindowsPhoneApp WindowsPhoneAppVersion.V8_1 ]
        | Profile336 -> [ DotNetFramework FrameworkVersion.V4_0_3; Silverlight SilverlightVersion.V5; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V8; WindowsPhoneApp WindowsPhoneAppVersion.V8_1 ]
        | Profile344 -> [ DotNetFramework FrameworkVersion.V4_5; Silverlight SilverlightVersion.V5; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V8; WindowsPhoneApp WindowsPhoneAppVersion.V8_1 ]
    member x.FolderName =
       match x with
        | Profile2 -> "portable-net40+sl4+win8+wp7"
        | Profile3 -> "portable-net40+sl4"
        | Profile4 -> "portable-net45+sl4+win8+wp7"
        | Profile5 -> "portable-net40+win8"
        | Profile6 -> "portable-net403+win8"
        | Profile7 -> "portable-net45+win8"
        | Profile14 -> "portable-net40+sl5"
        | Profile18 -> "portable-net403+sl4"
        | Profile19 -> "portable-net403+sl5"
        | Profile23 -> "portable-net45+sl4"
        | Profile24 -> "portable-net45+sl5"
        | Profile31 -> "portable-win81+wp81"
        | Profile32 -> "portable-win81+wpa81"
        | Profile36 -> "portable-net40+sl4+win8+wp8"
        | Profile37 -> "portable-net40+sl5+win8"
        | Profile41 -> "portable-net403+sl4+win8"
        | Profile42 -> "portable-net403+sl5+win8"
        | Profile44 -> "portable-net451+win81"
        | Profile46 -> "portable-net45+sl4+win8"
        | Profile47 -> "portable-net45+sl5+win8"
        | Profile49 -> "portable-net45+wp8"
        | Profile78 -> "portable-net45+win8+wp8"
        | Profile84 -> "portable-wp81+wpa81"
        | Profile88 -> "portable-net40+sl4+win8+wp75"
        | Profile92 -> "portable-net40+win8+wpa81"
        | Profile95 -> "portable-net403+sl4+win8+wp7"
        | Profile96 -> "portable-net403+sl4+win8+wp75"
        | Profile102 -> "portable-net403+win8+wpa81"
        | Profile104 -> "portable-net45+sl4+win8+wp75"
        | Profile111 -> "portable-net45+win8+wpa81"
        | Profile136 -> "portable-net40+sl5+win8+wp8"
        | Profile143 -> "portable-net403+sl4+win8+wp8"
        | Profile147 -> "portable-net403+sl5+win8+wp8"
        | Profile151 -> "portable-net451+win81+wpa81"
        | Profile154 -> "portable-net45+sl4+win8+wp8"
        | Profile157 -> "portable-win81+wp81+wpa81"
        | Profile158 -> "portable-net45+sl5+win8+wp8"
        | Profile225 -> "portable-net40+sl5+win8+wpa81"
        | Profile240 -> "portable-net403+sl5+win8+wpa81"
        | Profile255 -> "portable-net45+sl5+win8+wpa81"
        | Profile259 -> "portable-net45+win8+wp8+wpa81"
        | Profile328 -> "portable-net40+sl5+win8+wp8+wpa81"
        | Profile336 -> "portable-net403+sl5+win8+wp8+wpa81"
        | Profile344 -> "portable-net45+sl5+win8+wp8+wpa81"
        | UnsupportedProfile _ ->
            "portable-" +
            String.Join ("+",
                x.Frameworks
                |> List.sort
                |> List.map (fun fw -> fw.ToString()))

type TargetProfileRaw =
    | SinglePlatformP of FrameworkIdentifier
    | PortableProfileP of PortableProfileType

    override this.ToString() =
        match this with
        | SinglePlatformP x -> x.ToString()
        | PortableProfileP p -> p.FolderName

    member x.IsUnsupportedPortable =
        match x with
        | PortableProfileP p -> p.IsUnsupprted
        | _ -> false

[<CustomEquality; CustomComparison>]
type TargetProfile =
    { RawTargetProfile : TargetProfileRaw; CompareString : string }
    override x.ToString() = x.CompareString
    member x.IsUnsupportedPortable = x.RawTargetProfile.IsUnsupportedPortable

    override x.Equals(y) =
        match y with
        | :? TargetProfile as r -> x.CompareString.Equals(r.CompareString)
        | _ -> false
    override x.GetHashCode() = x.CompareString.GetHashCode()
    interface System.IComparable with
        member x.CompareTo(y) =
            match y with
            | :? TargetProfile as r -> x.CompareString.CompareTo(r.CompareString)
            | _ -> failwith "wrong type"

module TargetProfile =
    let (|SinglePlatform|PortableProfile|) profile =
        match profile.RawTargetProfile with
        | SinglePlatformP x -> SinglePlatform x
        | PortableProfileP p -> PortableProfile p
    let OfPlatform p = { RawTargetProfile = p; CompareString = p.ToString() }
    let SinglePlatform s = OfPlatform (SinglePlatformP s)
    let PortableProfile s = OfPlatform (PortableProfileP s)

module KnownTargetProfiles =
    // These lists are used primarily when calculating stuff which requires iterating over ALL profiles
    //  - Restriction System: "NOT" function
    //  - Generation of Project-File Conditions
    //  - Penalty system (to calculate best matching framework)
    // For this reason there is a test to ensure those lists are up2date.

    let DotNetFrameworkVersions = [
        FrameworkVersion.V1
        FrameworkVersion.V1_1
        FrameworkVersion.V2
        FrameworkVersion.V3
        FrameworkVersion.V3_5
        FrameworkVersion.V4
        FrameworkVersion.V4_0_3
        FrameworkVersion.V4_5
        FrameworkVersion.V4_5_1
        FrameworkVersion.V4_5_2
        FrameworkVersion.V4_5_3
        FrameworkVersion.V4_6
        FrameworkVersion.V4_6_1
        FrameworkVersion.V4_6_2
        FrameworkVersion.V4_6_3
        FrameworkVersion.V4_7
        FrameworkVersion.V4_7_1
        FrameworkVersion.V4_7_2
        FrameworkVersion.V4_8
        FrameworkVersion.V4_8_1
        FrameworkVersion.V5
        FrameworkVersion.V6
        FrameworkVersion.V7
        FrameworkVersion.V8
        FrameworkVersion.V9
    ]

    let DotNetFrameworkIdentifiers =
       DotNetFrameworkVersions
       |> List.map DotNetFramework

    let DotNetFrameworkProfiles =
       DotNetFrameworkIdentifiers
       |> List.map TargetProfile.SinglePlatform

    let DotNet5OperatingSystems = [
        Net5Os.Android
        Net5Os.IOs
        Net5Os.MacOs
        Net5Os.TvOs
        Net5Os.WatchOs
    ]

    let DotNet5WithOsProfiles =
        DotNet5OperatingSystems
        |> List.map (DotNet5WithOs >> TargetProfile.SinglePlatform)

    let DotNet5WindowsVersions = [
        Net5WindowsVersion.V7_0
        Net5WindowsVersion.V8_0
        Net5WindowsVersion.V10_0_17763_0
        Net5WindowsVersion.V10_0_18362_0
        Net5WindowsVersion.V10_0_19041_0
    ]

    let DotNet5WindowsProfiles = 
        DotNet5WindowsVersions
        |> List.map (DotNet5Windows >> TargetProfile.SinglePlatform)

    let DotNet6OperatingSystems = [
        Net6Os.Android
        Net6Os.IOs
        Net6Os.MacOs
        Net6Os.TvOs
        Net6Os.WatchOs
    ]

    let DotNet6WithOsProfiles =
        DotNet6OperatingSystems
        |> List.map (DotNet6WithOs >> TargetProfile.SinglePlatform)

    let DotNet6WindowsVersions = [
        Net6WindowsVersion.V7_0
        Net6WindowsVersion.V8_0
        Net6WindowsVersion.V10_0_17763_0
        Net6WindowsVersion.V10_0_18362_0
        Net6WindowsVersion.V10_0_19041_0
    ]

    let DotNet6WindowsProfiles = 
        DotNet6WindowsVersions
        |> List.map (DotNet6Windows >> TargetProfile.SinglePlatform)

    let DotNet7OperatingSystems = [
        Net7Os.Android
        Net7Os.IOs
        Net7Os.MacOs
        Net7Os.TvOs
        Net7Os.WatchOs
        Net7Os.MacCatalyst
        Net7Os.Tizen
    ]

    let DotNet7WithOsProfiles =
        DotNet7OperatingSystems
        |> List.map (DotNet7WithOs >> TargetProfile.SinglePlatform)

    let DotNet7WindowsVersions = [
        Net7WindowsVersion.V7_0
        Net7WindowsVersion.V8_0
        Net7WindowsVersion.V10_0_17763_0
        Net7WindowsVersion.V10_0_18362_0
        Net7WindowsVersion.V10_0_19041_0
        Net7WindowsVersion.V10_0_20348_0
    ]

    let DotNet7WindowsProfiles = 
        DotNet7WindowsVersions
        |> List.map (DotNet7Windows >> TargetProfile.SinglePlatform)

    let DotNet8OperatingSystems = [
        Net8Os.Android
        Net8Os.IOs
        Net8Os.MacOs
        Net8Os.TvOs
        Net8Os.WatchOs
        Net8Os.MacCatalyst
        Net8Os.Tizen
    ]

    let DotNet8WithOsProfiles =
        DotNet8OperatingSystems
        |> List.map (DotNet8WithOs >> TargetProfile.SinglePlatform)

    let DotNet8WindowsVersions = [
        Net8WindowsVersion.V10_0_17763_0
        Net8WindowsVersion.V10_0_18362_0
        Net8WindowsVersion.V10_0_19041_0
        Net8WindowsVersion.V10_0_20348_0
    ]

    let DotNet8WindowsProfiles = 
        DotNet8WindowsVersions
        |> List.map (DotNet8Windows >> TargetProfile.SinglePlatform)

    let DotNet9OperatingSystems = DotNet8OperatingSystems
    let DotNet9WithOsProfiles = DotNet8WithOsProfiles
    let DotNet9WindowsVersions = DotNet8WindowsVersions
    let DotNet9WindowsProfiles = DotNet8WindowsProfiles

    let DotNetStandardVersions = [
        DotNetStandardVersion.V1_0
        DotNetStandardVersion.V1_1
        DotNetStandardVersion.V1_2
        DotNetStandardVersion.V1_3
        DotNetStandardVersion.V1_4
        DotNetStandardVersion.V1_5
        DotNetStandardVersion.V1_6
        DotNetStandardVersion.V2_0
        DotNetStandardVersion.V2_1
    ]

    let DotNetStandardProfiles =
       DotNetStandardVersions
       |> List.map (DotNetStandard >> TargetProfile.SinglePlatform)

    let DotNetCoreAppVersions = [
        DotNetCoreAppVersion.V1_0
        DotNetCoreAppVersion.V1_1
        DotNetCoreAppVersion.V2_0
        DotNetCoreAppVersion.V2_1
        DotNetCoreAppVersion.V2_2
        DotNetCoreAppVersion.V3_0
        DotNetCoreAppVersion.V3_1
    ]

    let DotNetUnityVersions = [
        DotNetUnityVersion.V3_5_Full
        DotNetUnityVersion.V3_5_Subset
        DotNetUnityVersion.V3_5_Micro
        DotNetUnityVersion.V3_5_Web
    ]

    let DotNetCoreProfiles =
       DotNetCoreAppVersions
       |> List.map (DotNetCoreApp >> TargetProfile.SinglePlatform)

    let WindowsVersions = [
        WindowsVersion.V8
        WindowsVersion.V8_1
        WindowsVersion.V10
    ]

    let WindowsProfiles =
       WindowsVersions
       |> List.map (Windows >> TargetProfile.SinglePlatform)

    let DotNetUnityProfiles =
       DotNetUnityVersions
       |> List.map (DotNetUnity >> TargetProfile.SinglePlatform)

    let SilverlightVersions = [
        SilverlightVersion.V3
        SilverlightVersion.V4
        SilverlightVersion.V5
    ]

    let SilverlightProfiles =
       SilverlightVersions
       |> List.map (Silverlight >> TargetProfile.SinglePlatform)

    let MonoAndroidVersions = [
        MonoAndroidVersion.V1
        MonoAndroidVersion.V2_2
        MonoAndroidVersion.V2_3
        MonoAndroidVersion.V4_0_3
        MonoAndroidVersion.V4_1
        MonoAndroidVersion.V4_2
        MonoAndroidVersion.V4_3
        MonoAndroidVersion.V4_4
        MonoAndroidVersion.V5
        MonoAndroidVersion.V5_1
        MonoAndroidVersion.V6
        MonoAndroidVersion.V7
        MonoAndroidVersion.V7_1
        MonoAndroidVersion.V8
        MonoAndroidVersion.V8_1
        MonoAndroidVersion.V9
        MonoAndroidVersion.V10
        MonoAndroidVersion.V11
        MonoAndroidVersion.V12
    ]

    let MonoAndroidProfiles =
       MonoAndroidVersions
       |> List.map (MonoAndroid >> TargetProfile.SinglePlatform)

    let UAPVersions = [
        UAPVersion.V10
        UAPVersion.V10_0_10240
        UAPVersion.V10_0_14393
        UAPVersion.V10_0_15138
        UAPVersion.V10_0_16299
        UAPVersion.V10_0_16300
        UAPVersion.V10_0_18362
        UAPVersion.V10_1
    ]

    let UAPProfiles =
       UAPVersions
       |> List.map (UAP >> TargetProfile.SinglePlatform)

    let WindowsPhoneVersions = [
        WindowsPhoneVersion.V7
        WindowsPhoneVersion.V7_1
        WindowsPhoneVersion.V7_5
        WindowsPhoneVersion.V8
        WindowsPhoneVersion.V8_1
    ]

    let WindowsPhoneSilverlightProfiles =
       WindowsPhoneVersions
       |> List.map (WindowsPhone >> TargetProfile.SinglePlatform)

    let WindowsPhoneAppVersions = [
        WindowsPhoneAppVersion.V8_1
    ]

    let WindowsPhoneAppProfiles =
       WindowsPhoneAppVersions
       |> List.map (WindowsPhoneApp >> TargetProfile.SinglePlatform)

    // http://nugettoolsdev.azurewebsites.net/4.0.0/parse-framework?framework=.NETPortable%2CVersion%3Dv0.0%2CProfile%3DProfile3
    let AllPortableProfiles =
       [PortableProfileType.Profile2
        PortableProfileType.Profile3
        PortableProfileType.Profile4
        PortableProfileType.Profile5
        PortableProfileType.Profile6
        PortableProfileType.Profile7
        PortableProfileType.Profile14
        PortableProfileType.Profile18
        PortableProfileType.Profile19
        PortableProfileType.Profile23
        PortableProfileType.Profile24
        PortableProfileType.Profile31
        PortableProfileType.Profile32
        PortableProfileType.Profile36
        PortableProfileType.Profile37
        PortableProfileType.Profile41
        PortableProfileType.Profile42
        PortableProfileType.Profile44
        PortableProfileType.Profile46
        PortableProfileType.Profile47
        PortableProfileType.Profile49
        PortableProfileType.Profile78
        PortableProfileType.Profile84
        PortableProfileType.Profile88
        PortableProfileType.Profile92
        PortableProfileType.Profile95
        PortableProfileType.Profile96
        PortableProfileType.Profile102
        PortableProfileType.Profile104
        PortableProfileType.Profile111
        PortableProfileType.Profile136
        PortableProfileType.Profile143
        PortableProfileType.Profile147
        PortableProfileType.Profile151
        PortableProfileType.Profile154
        PortableProfileType.Profile157
        PortableProfileType.Profile158
        PortableProfileType.Profile225
        PortableProfileType.Profile240
        PortableProfileType.Profile255
        PortableProfileType.Profile259
        PortableProfileType.Profile328
        PortableProfileType.Profile336
        PortableProfileType.Profile344 ]

    let AllDotNetProfiles =
       DotNetFrameworkProfiles @
       DotNet9WithOsProfiles @
       DotNet9WindowsProfiles @
       DotNet8WithOsProfiles @
       DotNet8WindowsProfiles @
       DotNet7WithOsProfiles @
       DotNet7WindowsProfiles @
       DotNet6WithOsProfiles @
       DotNet6WindowsProfiles @
       DotNet5WithOsProfiles @
       DotNet5WindowsProfiles @
       DotNetUnityProfiles @
       WindowsProfiles @
       WindowsPhoneAppProfiles @
       UAPProfiles @
       SilverlightProfiles @
       WindowsPhoneSilverlightProfiles @
       MonoAndroidProfiles @
       [TargetProfile.SinglePlatform(MonoTouch)
        TargetProfile.SinglePlatform(XamariniOS)
        TargetProfile.SinglePlatform(XamarinMac)
        TargetProfile.SinglePlatform(XamarinTV)
        TargetProfile.SinglePlatform(XamarinWatch)] @
       (AllPortableProfiles |> List.map TargetProfile.PortableProfile)

    let AllDotNetStandardAndCoreProfiles =
       DotNetStandardProfiles @
       DotNetCoreProfiles
       // only used in "should understand aot in runtimes" test
       // We don't support that anymore, if we add this here paket will create corresponding
       // XML elements to compile for DNXCore...
       //[SinglePlatform (DNXCore FrameworkVersion.V5_0)]

    let AllNativeProfiles =
        [ Native(NoBuildMode,NoPlatform)
          Native(NoBuildMode,Win32)
          Native(NoBuildMode,X64)
          Native(NoBuildMode,Arm)
          Native(Debug,Win32)
          Native(Debug,Arm)
          Native(Debug,X64)
          Native(Release,Win32)
          Native(Release,X64)
          Native(Release,Arm)]

    let isSupportedProfile profile =
        match profile with
        | FrameworkIdentifier.Unsupported _ -> false
        | _ -> true

    let AllProfiles =
        (AllNativeProfiles |> List.map TargetProfile.SinglePlatform) @
          AllDotNetStandardAndCoreProfiles @
          AllDotNetProfiles
        |> Set.ofList

    let TryFindPortableProfile (name:string) =
        let lowerName = name.ToLowerInvariant()
        AllProfiles
        |> Set.toSeq
        |> Seq.tryPick (function
            | TargetProfile.PortableProfile p when p.ProfileName.ToLowerInvariant() = lowerName -> Some (TargetProfile.PortableProfile p)
            | _ -> None)

    let FindPortableProfile name =
        match TryFindPortableProfile name with
        | Some s -> s
        | None -> failwithf "tried to find portable profile '%s' but it is unknown to paket" name

module SupportCalculation =
    let isSupportedNotEqual (portable:PortableProfileType) (other:PortableProfileType) =
        let name, tfs = portable.ProfileName, portable.Frameworks

        let otherName, otherfws = other.ProfileName, other.Frameworks
        let weSupport =
            tfs
            |> List.collect (fun tf -> tf.RawSupportedPlatformsTransitive)

        let relevantFrameworks =
            otherfws
            |> Seq.filter (fun fw ->
                weSupport |> List.exists ((=) fw))
            |> Seq.length
        relevantFrameworks >= tfs.Length && portable <> other

    let getSupported (portable:PortableProfileType) =
        let name, tfs = portable.ProfileName, portable.Frameworks
        KnownTargetProfiles.AllPortableProfiles
        |> List.filter (fun p -> p.ProfileName <> name)
        |> List.filter (fun other -> isSupportedNotEqual portable other)
        |> List.map TargetProfile.PortableProfile
    type SupportMap = System.Collections.Concurrent.ConcurrentDictionary<PortableProfileType,PortableProfileType list>
    let ofSeq s = s|> dict |> System.Collections.Concurrent.ConcurrentDictionary
    let toSeq s = s|> Seq.map (fun (kv:System.Collections.Generic.KeyValuePair<_,_>) -> kv.Key, kv.Value)
    let rec buildSupportMap (supportMap:SupportMap) p =
        let directMap = supportMap.[p]
        directMap
        |> List.append (directMap |> List.collect (buildSupportMap supportMap))

    let filterMap pos (supportMap:SupportMap) : SupportMap =
        supportMap
        |> toSeq
        |> Seq.map (fun (profile, supported) ->
            profile,
            if supported.Length < pos + 1 then
                supported
            else
                // try to optimize on the 'pos' position
                let curPos = supported.[pos]
                let supportList = buildSupportMap supportMap curPos
                (supported |> List.take pos |> List.filter (fun s -> supportList |> List.contains s |> not))
                @ [curPos] @
                (supported
                 |> List.skip (pos + 1)
                 |> List.filter (fun s -> supportList |> List.contains s |> not))
            )
        |> ofSeq

    // Optimize support map, ie remove entries which are not needed
    let optimizeSupportMap (supportMap:SupportMap) =
        let mutable sup = supportMap
        let mutable hasChanged = true
        while hasChanged do
            hasChanged <- false
            let maxNum =  sup.Values |> Seq.map (fun l -> l.Length) |> Seq.max
            for i in 0 .. maxNum - 1 do
                let old = sup
                sup <- filterMap i sup
                if old.Count <> sup.Count then
                    hasChanged <- true
        sup

    let private getSupportedPortables p =
        getSupported p
        |> List.choose (function TargetProfile.PortableProfile p -> Some p | _ -> failwithf "Expected portable")

    let createInitialSupportMap () =
        KnownTargetProfiles.AllPortableProfiles
        |> List.map (fun p -> p, getSupportedPortables p)
        |> ofSeq

    let mutable private supportMap: SupportMap option = None

    let private getSupportMap () =
        match supportMap with
        | Some supportMap ->
            supportMap
        | None ->
            supportMap <- Some (optimizeSupportMap (createInitialSupportMap()))
            supportMap.Value

    let getSupportedPreCalculated (p:PortableProfileType) =
        match getSupportMap().TryGetValue p with
        | true, v -> v
        | _ ->
            match p with
            | UnsupportedProfile _ ->
                match getSupportMap().TryGetValue p with
                | true, v -> v
                | _ ->
                    let clone = getSupportMap() |> toSeq |> ofSeq
                    clone.[p] <- getSupportedPortables p
                    let opt = optimizeSupportMap clone
                    let result = opt.[p]
                    supportMap <- Some opt
                    result
            | _ -> failwithf "Expected that default profiles are already created."

    let private findPortablePriv =
        memoize (fun (fws: _ list) ->
            if fws.Length = 0 then failwithf "can not find portable for an empty list (Details: Empty lists need to be handled earlier with a warning)!"
            let fallback = TargetProfile.PortableProfile (UnsupportedProfile (fws |> List.sort))
            let minimal =
                fws
                |> List.filter (function
                    | MonoTouch
                    | UAP _
                    | MonoAndroid _
                    | XamariniOS
                    | XamarinTV
                    | XamarinWatch
                    | DotNetCoreApp _
                    | DotNetStandard _
                    | Unsupported _
                    | XamarinMac -> false
                    | XCode _
                    | Tizen _ -> failwithf "Unexpected framework while trying to resolve PCL Profile"
                    | _ -> true)
            if minimal.Length > 0 then
                let firstMatch =
                    KnownTargetProfiles.AllPortableProfiles
                    |> List.filter (fun p ->
                        let otherFws = p.Frameworks
                        minimal |> List.forall(fun mfw -> otherFws |> Seq.contains mfw))
                    |> List.sortBy (fun p -> p.Frameworks.Length)
                    |> List.tryHead
                match firstMatch with
                | Some p -> TargetProfile.PortableProfile p
                | None ->
                    fallback
            else
                fallback)

    let findPortable warn fws =
        let result = findPortablePriv fws
        if warn && result.IsUnsupportedPortable then
            traceWarnfn "The profile '%O' is not a known profile. Please tell the package author." result
        result

    let getSupportedPlatforms x =
        match x with
        | TargetProfile.SinglePlatform tf ->
            let rawSupported =
                tf.RawSupportedPlatforms
                |> List.map TargetProfile.SinglePlatform
            let profilesSupported =
                // See https://docs.microsoft.com/en-us/dotnet/articles/standard/library
                // NOTE: This is explicit in NuGet world (ie users explicitely need to add "imports")
                // we prefer users to build for netstandard and don't allow netstandard to be used in
                // portable projects...
                match tf with
                | DotNetStandard DotNetStandardVersion.V1_0 ->
                    [ Profile31
                      Profile49
                      Profile78
                      Profile84
                      Profile157
                      Profile259 ]
                | DotNetStandard DotNetStandardVersion.V1_1 ->
                    [ Profile7
                      Profile111 ]
                | DotNetStandard DotNetStandardVersion.V1_2 ->
                    [ Profile32
                      Profile44
                      Profile151 ]
                | MonoTouch
                | MonoAndroid _
                | XamariniOS
                | XamarinTV
                | XamarinWatch
                | XamarinMac ->
                    // http://danrigby.com/2014/05/14/supported-pcl-profiles-xamarin-for-visual-studio-2/
                    [ Profile5
                      Profile6
                      Profile7
                      Profile14
                      Profile19
                      Profile24
                      Profile37
                      Profile42
                      Profile44
                      Profile47
                      Profile49
                      Profile78
                      Profile92
                      Profile102
                      Profile111
                      Profile136
                      Profile147
                      Profile151
                      Profile158
                      Profile225
                      Profile259
                      Profile328
                      Profile336
                      Profile344 ]
                | _ ->
                    // Regular supported logic is to enumerate all profiles and select compatible ones
                    let profiles =
                        KnownTargetProfiles.AllPortableProfiles
                        |> List.filter (fun p ->
                            // Portable profile is compatible as soon as if contains us
                            p.Frameworks
                            |> List.exists (fun fw -> fw = tf))
                    profiles
                |> List.map TargetProfile.PortableProfile
            rawSupported @ profilesSupported
        | TargetProfile.PortableProfile p ->
            getSupportedPreCalculated p
            |> List.map TargetProfile.PortableProfile
        |> Set.ofList

    let getSupportedPlatformsTransitive =
        let findNewPlats (known:TargetProfile Set) (lastStep:TargetProfile Set) =
            lastStep
            |> Seq.map (fun k -> Set.difference (getSupportedPlatforms k) known)
            |> Set.unionMany

        memoize (fun x ->
            Seq.initInfinite (fun _ -> 1)
            |> Seq.scan (fun state _ ->
                match state with
                | Some (known, lastStep) ->
                    match findNewPlats known lastStep with
                    | s when s.IsEmpty -> None
                    | items -> Some (Set.union known items, items)
                | None -> None) (Some (Set.singleton x, Set.singleton x))
            |> Seq.takeWhile (fun i -> i.IsSome)
            |> Seq.choose id
            |> Seq.last
            |> fst
        )

    /// true when x is supported by y, for example netstandard15 is supported by netcore10
    let isSupportedBy x y =
        match x with
        | TargetProfile.PortableProfile (PortableProfileType.UnsupportedProfile xs' as x') ->
            // custom profiles are not in our lists -> custom logic
            match y with
            | TargetProfile.PortableProfile y' ->
                x' = y' ||
                isSupportedNotEqual y' x'
            | TargetProfile.SinglePlatform y' ->
                y'.RawSupportedPlatformsTransitive |> Seq.exists (fun y'' ->
                    xs' |> Seq.contains y'')
        | _ ->
            x = y ||
              (getSupportedPlatformsTransitive y |> Set.contains x)

    let getPlatformsSupporting =
        // http://nugettoolsdev.azurewebsites.net
        let calculate (x:TargetProfile) =
            KnownTargetProfiles.AllProfiles
            |> Set.filter (fun plat -> isSupportedBy x plat)
        memoize calculate

type TargetProfile with
    member p.Frameworks =
        match p with
        | TargetProfile.SinglePlatform fw -> [fw]
        | TargetProfile.PortableProfile p -> p.Frameworks
    static member FindPortable warnWhenUnsupported (fws: _ list) = SupportCalculation.findPortable warnWhenUnsupported fws

    member inline x.PlatformsSupporting = SupportCalculation.getPlatformsSupporting x

    /// true when x is supported by y, for example netstandard15 is supported by netcore10
    member inline x.IsSupportedBy y =
        SupportCalculation.isSupportedBy x y
    /// true when x is at least (>=) y ie when y is supported by x, for example netcore10 >= netstandard15 as netstandard15 is supported by netcore10.
    /// Note that this relation is not complete, for example for WindowsPhoneSilverlightv7.0 and Windowsv4.5 both <= and >= are false from this definition as
    /// no platform supports the other.
    member inline x.IsAtLeast (y:TargetProfile) =
        y.IsSupportedBy x

    /// Get all platforms y for which x >= y holds
    member inline x.SupportedPlatformsTransitive =
        SupportCalculation.getSupportedPlatformsTransitive x

    member inline x.SupportedPlatforms : TargetProfile Set =
        SupportCalculation.getSupportedPlatforms x

    /// x < y, see y >= x && x <> y
    member inline x.IsSmallerThan y =
        x.IsSupportedBy y && x <> y

    member inline x.IsSmallerThanOrEqual y =
        x.IsSupportedBy y

    /// Note that this returns true only when a >= x and x < b holds.
    member x.IsBetween(a,b) = x.IsAtLeast a && x.IsSmallerThan b
