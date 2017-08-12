namespace Paket

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
    | V5_0          
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
        | V5_0      -> "v5.0"

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
        | FrameworkVersion.V5_0 -> "50"

[<RequireQualifiedAccess>]
/// The UAP version.
// Each time a new version is added NuGetPackageCache.CurrentCacheVersion should be bumped.
type UAPVersion = 
    | V10
    override this.ToString() =
        match this with
        | V10 -> "10.0"

    member this.ShortString() =
        match this with
        | UAPVersion.V10 -> "100"

    member this.NetCoreVersion =
        // WTF: https://github.com/onovotny/MSBuildSdkExtras/blob/8d2d4ad63b552481da06e646dbb6504abc415260/src/build/platforms/Windows.targets
        match this with
        // Assumed from C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETCore
        | UAPVersion.V10 -> "v5.0"


[<RequireQualifiedAccess>]
/// The .NET Standard version.
// Each time a new version is added NuGetPackageCache.CurrentCacheVersion should be bumped.
type DotNetCoreVersion = 
    | V1_0
    | V1_1
    | V2_0
    member private this.NumKey =
        match this with
        | V1_0 -> 0
        | V1_1 -> 1
        | V2_0 -> 2
    static member private FromNum num =
        match num with
        | 0 -> V1_0
        | 1 -> V1_1
        | 2 -> V2_0
        | _   -> failwithf "'%i' has no corresponding framework version" num
    static member (<->) (lower:DotNetCoreVersion,upper:DotNetCoreVersion) =
        if lower.NumKey < upper.NumKey then
            [ lower.NumKey .. upper.NumKey ] |> List.map DotNetCoreVersion.FromNum
        else
            [ lower.NumKey .. -1 .. upper.NumKey ] |> List.map DotNetCoreVersion.FromNum
    override this.ToString() =
        match this with
        | V1_0 -> "v1.0"
        | V1_1 -> "v1.1"
        | V2_0 -> "v2.0"
    member this.ShortString() =
        match this with
        | DotNetCoreVersion.V1_0 -> "1.0"
        | DotNetCoreVersion.V1_1 -> "1.1"
        | DotNetCoreVersion.V2_0 -> "2.0"

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

module KnownAliases =
    let Data =
        [".net", "net"
         "netframework", "net"
         ".netframework", "net"
         ".netcore", "netcore"
         "winrt", "netcore"
         "netcoreapp", "netcore"
         "silverlight", "sl"
         "windowsPhoneApp", "wpa"
         "windowsphone", "wp"
         "windows", "win"
         ".netportable", "portable"
         "netportable", "portable"
         "10.0", "100"
         "0.0", ""
         ".", ""
         " ", "" ]
        |> List.map (fun (p,r) -> p.ToLower(),r.ToLower())

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
    
[<RequireQualifiedAccess>]
type WindowsPhoneAppVersion =
    | V8
    | V8_1
    member this.ShortString() =
        match this with
        | WindowsPhoneAppVersion.V8 -> "8"
        | WindowsPhoneAppVersion.V8_1 -> "81"
    override this.ToString() =
        match this with
        | WindowsPhoneAppVersion.V8 -> "v8.0"
        | WindowsPhoneAppVersion.V8_1 -> "v8.1"

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

[<RequireQualifiedAccess>]
type MonoAndroidVersion =
    | V1
    | V22
    | V23
    | V403
    | V41
    | V42
    | V43
    | V44
    | V44W
    | V5
    | V51
    | V6
    | V7
    | V71
    member this.ShortString() =
        match this with
        | MonoAndroidVersion.V1    -> ""
        | MonoAndroidVersion.V22   -> "2.2"
        | MonoAndroidVersion.V23   -> "2.3"
        | MonoAndroidVersion.V403  -> "4.0.3"
        | MonoAndroidVersion.V41   -> "4.1"
        | MonoAndroidVersion.V42   -> "4.2"
        | MonoAndroidVersion.V43   -> "4.3"
        | MonoAndroidVersion.V44   -> "4.4"
        | MonoAndroidVersion.V44W  -> "4.4W"
        | MonoAndroidVersion.V5    -> "5.0"
        | MonoAndroidVersion.V51   -> "5.1"
        | MonoAndroidVersion.V6    -> "6.0"
        | MonoAndroidVersion.V7    -> "7.0"
        | MonoAndroidVersion.V71   -> "7.1"
    override this.ToString() =
        match this with
        | MonoAndroidVersion.V1    -> "v1.0"
        | MonoAndroidVersion.V22   -> "v2.2"
        | MonoAndroidVersion.V23   -> "v2.3"
        | MonoAndroidVersion.V403  -> "v4.0.3"
        | MonoAndroidVersion.V41   -> "v4.1"
        | MonoAndroidVersion.V42   -> "v4.2"
        | MonoAndroidVersion.V43   -> "v4.3"
        | MonoAndroidVersion.V44   -> "v4.4"
        | MonoAndroidVersion.V44W  -> "v4.4W"
        | MonoAndroidVersion.V5    -> "v5.0"
        | MonoAndroidVersion.V51    -> "v5.1"
        | MonoAndroidVersion.V6    -> "v6.0"
        | MonoAndroidVersion.V7    -> "v7.0"
        | MonoAndroidVersion.V71   -> "v7.1"

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
    V3 | V4
    member this.ShortString() =
        match this with
        | V3 -> "3.0"
        | V4 -> "4.0"
    override this.ToString() =
        match this with
        | V3 -> "v3.0"
        | V4 -> "v4.0"

/// Framework Identifier type.
// Each time a new version is added NuGetPackageCache.CurrentCacheVersion should be bumped.
type FrameworkIdentifier = 
    | DotNetFramework of FrameworkVersion
    | UAP of UAPVersion
    | DNX of FrameworkVersion
    | DNXCore of FrameworkVersion
    | DotNetStandard of DotNetStandardVersion
    | DotNetCore of DotNetCoreVersion
    | DotNetUnity of DotNetUnityVersion
    | MonoAndroid of MonoAndroidVersion
    | MonoTouch
    | MonoMac
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

    override x.ToString() = 
        match x with
        | DotNetFramework v -> "net" + v.ShortString()
        | DNX v -> "dnx" + v.ShortString()
        | DNXCore v -> "dnxcore" + v.ShortString()
        | DotNetStandard v -> "netstandard" + v.ShortString()
        | DotNetCore v -> "netcore" + v.ShortString()
        | DotNetUnity v -> "net" + v.ShortString()
        | MonoAndroid v -> "monoandroid" + v.ShortString()
        | MonoTouch -> "monotouch"
        | MonoMac -> "monomac"
        | Native(_) -> "native"
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
        | MonoAndroid MonoAndroidVersion.V22 -> [ MonoAndroid MonoAndroidVersion.V1 ]
        | MonoAndroid MonoAndroidVersion.V23 -> [ MonoAndroid MonoAndroidVersion.V22 ]
        | MonoAndroid MonoAndroidVersion.V403 -> [ MonoAndroid MonoAndroidVersion.V23 ]
        | MonoAndroid MonoAndroidVersion.V41 -> [ MonoAndroid MonoAndroidVersion.V403 ]
        | MonoAndroid MonoAndroidVersion.V42 -> [ MonoAndroid MonoAndroidVersion.V41 ]
        | MonoAndroid MonoAndroidVersion.V43 -> [ MonoAndroid MonoAndroidVersion.V42 ]
        | MonoAndroid MonoAndroidVersion.V44 -> [ MonoAndroid MonoAndroidVersion.V43 ]
        //https://stackoverflow.com/questions/28170345/what-exactly-is-android-4-4w-vs-4-4-and-what-about-5-0-1
        | MonoAndroid MonoAndroidVersion.V44W -> [ MonoAndroid MonoAndroidVersion.V44 ]
        | MonoAndroid MonoAndroidVersion.V5 -> [ MonoAndroid MonoAndroidVersion.V44W]
        | MonoAndroid MonoAndroidVersion.V51 -> [ MonoAndroid MonoAndroidVersion.V5 ]
        | MonoAndroid MonoAndroidVersion.V6 -> [ MonoAndroid MonoAndroidVersion.V51 ]
        | MonoAndroid MonoAndroidVersion.V7 -> [ MonoAndroid MonoAndroidVersion.V6; DotNetStandard DotNetStandardVersion.V1_6 ]
        | MonoAndroid MonoAndroidVersion.V71 -> [ MonoAndroid MonoAndroidVersion.V7 ]
        | MonoTouch -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | MonoMac -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | Native(_) -> [ ]
        | XamariniOS -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | XamarinMac -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | XamarinTV -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | XamarinWatch -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | UAP UAPVersion.V10 -> [ Windows WindowsVersion.V8_1; WindowsPhoneApp WindowsPhoneAppVersion.V8_1; DotNetStandard DotNetStandardVersion.V1_4  ]
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
        | DotNetFramework FrameworkVersion.V5_0 -> [ DotNetFramework FrameworkVersion.V4_7 ]
        | DNX _ -> [ ]
        | DNXCore _ -> [ ]
        | DotNetStandard DotNetStandardVersion.V1_0 -> [  ]
        | DotNetStandard DotNetStandardVersion.V1_1 -> [ DotNetStandard DotNetStandardVersion.V1_0 ]
        | DotNetStandard DotNetStandardVersion.V1_2 -> [ DotNetStandard DotNetStandardVersion.V1_1 ]
        | DotNetStandard DotNetStandardVersion.V1_3 -> [ DotNetStandard DotNetStandardVersion.V1_2 ]
        | DotNetStandard DotNetStandardVersion.V1_4 -> [ DotNetStandard DotNetStandardVersion.V1_3 ]
        | DotNetStandard DotNetStandardVersion.V1_5 -> [ DotNetStandard DotNetStandardVersion.V1_4 ]
        | DotNetStandard DotNetStandardVersion.V1_6 -> [ DotNetStandard DotNetStandardVersion.V1_5 ]
        | DotNetStandard DotNetStandardVersion.V2_0 -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | DotNetCore DotNetCoreVersion.V1_0 -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | DotNetCore DotNetCoreVersion.V1_1 -> [ DotNetCore DotNetCoreVersion.V1_0 ]
        | DotNetCore DotNetCoreVersion.V2_0 -> [ DotNetCore DotNetCoreVersion.V1_1;  DotNetStandard DotNetStandardVersion.V2_0 ]
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
        | WindowsPhoneApp WindowsPhoneAppVersion.V8 -> [ ]
        | WindowsPhoneApp WindowsPhoneAppVersion.V8_1 -> [ DotNetStandard DotNetStandardVersion.V1_2 ]
        | WindowsPhone WindowsPhoneVersion.V7 -> [ ]
        | WindowsPhone WindowsPhoneVersion.V7_1 -> [ WindowsPhone WindowsPhoneVersion.V7 ]
        | WindowsPhone WindowsPhoneVersion.V7_5 -> [ WindowsPhone WindowsPhoneVersion.V7_1 ]
        | WindowsPhone WindowsPhoneVersion.V8 -> [ WindowsPhone WindowsPhoneVersion.V7_5; DotNetStandard DotNetStandardVersion.V1_0 ]
        | WindowsPhone WindowsPhoneVersion.V8_1 -> [ WindowsPhone WindowsPhoneVersion.V8 ]
        | Tizen TizenVersion.V3 -> [ DotNetStandard DotNetStandardVersion.V1_6 ]
        | Tizen TizenVersion.V4 -> [ DotNetStandard DotNetStandardVersion.V1_6 ]

module FrameworkDetection =

    /// Used for script generation
    let resolveEnvironmentFramework = lazy (
        // HACK: resolve .net version based on environment
        // list of match is incomplete / inaccurate
    #if DOTNETCORE
        // Environment.Version is not supported
        //dunno what is used for, using a default
        DotNetFramework (FrameworkVersion.V4_5)
    #else
        let version = Environment.Version
        match version.Major, version.Minor, version.Build, version.Revision with
        | 4, 0, 30319, 42000 -> DotNetFramework (FrameworkVersion.V4_6)
        | 4, 0, 30319, _ -> DotNetFramework (FrameworkVersion.V4_5)
        | _ -> DotNetFramework (FrameworkVersion.V4_5) // paket.exe is compiled for framework 4.5
    #endif
        )


    open Logging
    /// parse a string to construct a Netframework, NetCore, NetStandard, or other dotnet identifier
    [<Obsolete "Use PlatformMatching.extractPlatforms instead">]
    let Extract =
        memoize 
          (fun (path:string) ->
            let path = 
                let sb = new Text.StringBuilder(path.ToLower())
                for pattern,replacement in KnownAliases.Data do
                     sb.Replace(pattern,replacement) |> ignore
                sb.ToString()

            // Each time the parsing is changed, NuGetPackageCache.CurrentCacheVersion should be bumped.
            // http://nugettoolsdev.azurewebsites.net/4.0.0/parse-framework?framework=.NETPortable%2CVersion%3Dv0.0%2CProfile%3DProfile2
            let result = 
                match path with
                | "net35-Unity Web v3.5" ->  Some (DotNetUnity DotNetUnityVersion.V3_5_Web)
                | "net35-Unity Micro v3.5" -> Some (DotNetUnity DotNetUnityVersion.V3_5_Micro)
                | "net35-Unity Subset v3.5" -> Some (DotNetUnity DotNetUnityVersion.V3_5_Subset)
                | "net35-Unity Full v3.5" -> Some (DotNetUnity DotNetUnityVersion.V3_5_Full)
                | "net10" | "net1" | "10" -> Some (DotNetFramework FrameworkVersion.V1)
                | "net11" | "11" -> Some (DotNetFramework FrameworkVersion.V1_1)
                | "net20" | "net2" | "net" | "net20-full" | "net20-client" | "20" -> Some (DotNetFramework FrameworkVersion.V2)
                | "net30" | "net3" | "30" ->  Some (DotNetFramework FrameworkVersion.V3)
                | "net35" | "net35-client" | "net35-full" | "35" -> Some (DotNetFramework FrameworkVersion.V3_5)
                | "net40-full" | "net40" | "net4" | "40" | "net40-client" | "net4-client" -> Some (DotNetFramework FrameworkVersion.V4)
                | "net403"| "net403-full"| "net403-client" -> Some (DotNetFramework FrameworkVersion.V4_0_3)
                | "net45" | "net45-full" | "45" -> Some (DotNetFramework FrameworkVersion.V4_5)
                | "net451" -> Some (DotNetFramework FrameworkVersion.V4_5_1)
                | "net452" -> Some (DotNetFramework FrameworkVersion.V4_5_2)
                | "net453" -> Some (DotNetFramework FrameworkVersion.V4_5_3)
                | "net46" -> Some (DotNetFramework FrameworkVersion.V4_6)
                | "net461" -> Some (DotNetFramework FrameworkVersion.V4_6_1)
                | "net462" -> Some (DotNetFramework FrameworkVersion.V4_6_2)
                | "net463" -> Some (DotNetFramework FrameworkVersion.V4_6_3)
                | "net47" -> Some (DotNetFramework FrameworkVersion.V4_7)
                | "uap100" -> Some (UAP UAPVersion.V10)
                | "monotouch" | "monotouch10" | "monotouch1" -> Some MonoTouch
                | "monoandroid" | "monoandroid10" | "monoandroid1.0" | "monoandroid1" -> Some (MonoAndroid MonoAndroidVersion.V1)
                | "monoandroid22" -> Some (MonoAndroid MonoAndroidVersion.V22)
                | "monoandroid23" -> Some (MonoAndroid MonoAndroidVersion.V23)
                | "monoandroid403" -> Some (MonoAndroid MonoAndroidVersion.V403)
                | "monoandroid41" -> Some (MonoAndroid MonoAndroidVersion.V41)
                | "monoandroid42" -> Some (MonoAndroid MonoAndroidVersion.V42)
                | "monoandroid43" -> Some (MonoAndroid MonoAndroidVersion.V43)
                | "monoandroid44" -> Some (MonoAndroid MonoAndroidVersion.V44)
                | "monoandroid44w" -> Some (MonoAndroid MonoAndroidVersion.V44W)
                | "monoandroid50" -> Some (MonoAndroid MonoAndroidVersion.V5)
                | "monoandroid51" -> Some (MonoAndroid MonoAndroidVersion.V51)
                | "monoandroid60" -> Some (MonoAndroid MonoAndroidVersion.V6)
                | "monoandroid70" | "monoandroid7.0"-> Some (MonoAndroid MonoAndroidVersion.V7)
                | "monoandroid71" | "monoandroid7.1"-> Some (MonoAndroid MonoAndroidVersion.V71)
                | "monomac" | "monomac10" | "monomac1" -> Some MonoMac
                | "xamarinios" | "xamarinios10" | "xamarinios1" | "xamarin.ios10" -> Some XamariniOS
                | "xamarinwatchos" | "xamarinwatchos10" | "xamarinwatchos1" | "xamarin.watchos10" -> Some XamarinWatch
                | "xamarintvos" | "xamarintvos10" | "xamarintvos1" | "xamarin.tvos10" -> Some XamarinTV
                | "xamarinmac" | "xamarinmac20" | "xamarin.mac20" -> Some XamarinMac
                | "native/x86/debug" -> Some(Native(Debug,Win32))
                | "native/x64/debug" -> Some(Native(Debug,X64))
                | "native/arm/debug" -> Some(Native(Debug,Arm))
                | "native/x86/release" -> Some(Native(Release,Win32))
                | "native/x64/release" -> Some(Native(Release,X64))
                | "native/arm/release" -> Some(Native(Release,Arm))
                | "native/address-model-32" -> Some(Native(NoBuildMode,Win32))
                | "native/address-model-64" -> Some(Native(NoBuildMode,X64))
                | "native" -> Some(Native(NoBuildMode,NoPlatform))
                | "sl"  | "sl3" | "sl30" -> Some (Silverlight SilverlightVersion.V3)
                | "sl4" | "sl40" -> Some (Silverlight SilverlightVersion.V4)
                | "sl5" | "sl50" -> Some (Silverlight SilverlightVersion.V5)
                | "win8" | "windows8" | "win80" | "netcore45" | "win" | "winv45" -> Some (Windows WindowsVersion.V8)
                | "win81" | "windows81"  | "netcore46" | "netcore451" | "winv451" -> Some (Windows WindowsVersion.V8_1)
                | "wp7" | "wp70" | "wpv7" | "wpv70" | "sl4-wp7"| "sl4-wp70" -> Some (WindowsPhone WindowsPhoneVersion.V7)
                | "wp71" | "wpv71" | "sl4-wp71" | "sl4-wp"  -> Some (WindowsPhone WindowsPhoneVersion.V7_1)
                | "wp75" | "wpv75" | "sl4-wp75" -> Some (WindowsPhone WindowsPhoneVersion.V7_5)
                | "wp8" | "wp80"  | "wpv80" -> Some (WindowsPhone WindowsPhoneVersion.V8)
                | "wpa00" | "wpa" | "wpa81" | "wpav81" | "wpapp81" | "wpapp" -> Some (WindowsPhoneApp WindowsPhoneAppVersion.V8_1)
                | "wp81"  | "wpv81" -> Some (WindowsPhone WindowsPhoneVersion.V8_1)
                | "dnx451" -> Some(DNX FrameworkVersion.V4_5_1)
                | "dnxcore50" | "netplatform50" | "netcore50" | "aspnetcore50" | "aspnet50" | "dotnet" -> Some(DNXCore FrameworkVersion.V5_0)
                | v when v.StartsWith "dotnet" -> Some(DNXCore FrameworkVersion.V5_0)
                | "netstandard" | "netstandard10" -> Some(DotNetStandard DotNetStandardVersion.V1_0)
                | "netstandard11" -> Some(DotNetStandard DotNetStandardVersion.V1_1)
                | "netstandard12" -> Some(DotNetStandard DotNetStandardVersion.V1_2)
                | "netstandard13" -> Some(DotNetStandard DotNetStandardVersion.V1_3)
                | "netstandard14" -> Some(DotNetStandard DotNetStandardVersion.V1_4)
                | "netstandard15" -> Some(DotNetStandard DotNetStandardVersion.V1_5)
                | "netstandard16" -> Some(DotNetStandard DotNetStandardVersion.V1_6)
                | "netstandard20" -> Some(DotNetStandard DotNetStandardVersion.V2_0)
                | "netcore10" -> Some (DotNetCore DotNetCoreVersion.V1_0)
                | "netcore11" -> Some (DotNetCore DotNetCoreVersion.V1_1)
                | "netcore20" -> Some (DotNetCore DotNetCoreVersion.V2_0)
                | "tizen3" -> Some (Tizen TizenVersion.V3)
                | "tizen4" -> Some (Tizen TizenVersion.V4)
                | _ -> None
            result)

    let DetectFromPath(path : string) : FrameworkIdentifier option =
        let path = path.Replace("\\", "/").ToLower()
        let fi = new FileInfo(path)
        
        if String.containsIgnoreCase ("lib/" + fi.Name) path then Some(DotNetFramework(FrameworkVersion.V1))
        else 
            let startPos = path.LastIndexOf("lib/")
            let endPos = path.LastIndexOf(fi.Name,StringComparison.OrdinalIgnoreCase)
            if startPos < 0 || endPos < 0 then None
            else 
                Extract(path.Substring(startPos + 4, endPos - startPos - 5))


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
        "portable-" +
        String.Join ("+",
            x.Frameworks
            |> List.sort
            |> List.map (fun fw -> fw.ToString()))
type TargetProfile =
    | SinglePlatform of FrameworkIdentifier
    | PortableProfile of PortableProfileType
    override this.ToString() =
        match this with
        | SinglePlatform x -> x.ToString()
        | PortableProfile p -> p.FolderName

module KnownTargetProfiles =
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
    ]

    let DotNetFrameworkIdentifiers =
       DotNetFrameworkVersions
       |> List.map DotNetFramework

    let DotNetFrameworkProfiles =
       DotNetFrameworkIdentifiers
       |> List.map SinglePlatform

    let DotNetStandardVersions = [
        DotNetStandardVersion.V1_0
        DotNetStandardVersion.V1_1
        DotNetStandardVersion.V1_2
        DotNetStandardVersion.V1_3
        DotNetStandardVersion.V1_4
        DotNetStandardVersion.V1_5
        DotNetStandardVersion.V1_6
        DotNetStandardVersion.V2_0
    ]
        

    let DotNetStandardProfiles =
       DotNetStandardVersions
       |> List.map (DotNetStandard >> SinglePlatform)
       
    let DotNetCoreVersions = [
        DotNetCoreVersion.V1_0
        DotNetCoreVersion.V1_1
        DotNetCoreVersion.V2_0
    ]

    let DotNetUnityVersions = [
        DotNetUnityVersion.V3_5_Full
        DotNetUnityVersion.V3_5_Subset
        DotNetUnityVersion.V3_5_Micro
        DotNetUnityVersion.V3_5_Web
    ]
       
    let DotNetCoreProfiles =
       DotNetCoreVersions
       |> List.map (DotNetCore >> SinglePlatform)

    let WindowsVersions = [
        WindowsVersion.V8
        WindowsVersion.V8_1
        WindowsVersion.V10
    ]

    let WindowsProfiles =
       WindowsVersions
       |> List.map (Windows >> SinglePlatform)

    let DotNetUnityProfiles = 
       DotNetUnityVersions
       |> List.map (DotNetUnity >> SinglePlatform)
       
    let SilverlightVersions = [
        SilverlightVersion.V3
        SilverlightVersion.V4
        SilverlightVersion.V5
    ]

    let SilverlightProfiles =
       SilverlightVersions
       |> List.map (Silverlight >> SinglePlatform)

    let MonoAndroidVersions = [
        MonoAndroidVersion.V1
        MonoAndroidVersion.V22
        MonoAndroidVersion.V23
        MonoAndroidVersion.V403
        MonoAndroidVersion.V41
        MonoAndroidVersion.V42
        MonoAndroidVersion.V43
        MonoAndroidVersion.V44
        MonoAndroidVersion.V44W
        MonoAndroidVersion.V5
        MonoAndroidVersion.V51
        MonoAndroidVersion.V6
        MonoAndroidVersion.V7
        MonoAndroidVersion.V71
    ]

    let MonoAndroidProfiles =
       MonoAndroidVersions
       |> List.map (MonoAndroid >> SinglePlatform)

    let UAPProfiles =
       [SinglePlatform(UAP UAPVersion.V10)]

    let WindowsPhoneVersions = [
        WindowsPhoneVersion.V7
        WindowsPhoneVersion.V7_1
        WindowsPhoneVersion.V7_5
        WindowsPhoneVersion.V8
        WindowsPhoneVersion.V8_1
    ]

    let WindowsPhoneSilverlightProfiles =
       WindowsPhoneVersions
       |> List.map (WindowsPhone >> SinglePlatform)

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
       DotNetUnityProfiles @ 
       WindowsProfiles @ 
       UAPProfiles @
       SilverlightProfiles @
       WindowsPhoneSilverlightProfiles @
       MonoAndroidProfiles @
       [SinglePlatform(MonoTouch)
        SinglePlatform(XamariniOS)
        SinglePlatform(XamarinMac)
        SinglePlatform(XamarinTV)
        SinglePlatform(XamarinWatch)
        SinglePlatform(WindowsPhoneApp WindowsPhoneAppVersion.V8_1)] @
       (AllPortableProfiles |> List.map PortableProfile)

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

    let AllProfiles = 
        (AllNativeProfiles |> List.map SinglePlatform) @ 
          AllDotNetStandardAndCoreProfiles @
          AllDotNetProfiles
        |> Set.ofList

    let TryFindPortableProfile (name:string) =
        let lowerName = name.ToLowerInvariant()
        AllProfiles
        |> Set.toSeq
        |> Seq.tryPick (function
            | PortableProfile p when p.ProfileName.ToLowerInvariant() = lowerName -> Some (PortableProfile p)
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
        |> List.map PortableProfile
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
                let supportList = buildSupportMap supportMap curPos // supportMap.[curPos] // 
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
        |> List.choose (function PortableProfile p -> Some p | _ -> failwithf "Expected portable")
        
    let createInitialSupportMap () =
        KnownTargetProfiles.AllPortableProfiles
        |> List.map (fun p -> p, getSupportedPortables p)
        |> ofSeq

    let mutable private supportMap = optimizeSupportMap (createInitialSupportMap())

    let getSupportedPreCalculated (p:PortableProfileType) =
        match supportMap.TryGetValue p with
        | true, v -> v
        | _ ->
            match p with
            | UnsupportedProfile tfs ->
                match supportMap.TryGetValue p with
                | true, v -> v
                | _ ->
                    let clone = supportMap |> toSeq |> ofSeq
                    clone.[p] <- getSupportedPortables p
                    let opt = optimizeSupportMap clone
                    let result = opt.[p]
                    supportMap <- opt
                    result
            | _ -> failwithf "Expected that default profiles are already created."
    
    let findPortable =
        memoize (fun (fws: _ list) ->
            if fws.Length = 0 then failwithf "can not find portable for an empty list (Details: Empty lists need to be handled earlier with a warning)!"
            let fallback = PortableProfile (UnsupportedProfile (fws |> List.sort))
            let minimal =
                fws
                |> List.filter (function
                    | MonoTouch
                    | DNXCore _
                    | UAP _
                    | MonoAndroid _ -> false
                    | DotNetCore _
                    | DotNetStandard _
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
                | Some p -> PortableProfile p
                | None ->
                    traceWarnfn "The profile '%O' is not a known profile. Please tell the package author." fallback
                    fallback
            else
                traceWarnfn "The profile '%O' is not a known profile. Please tell the package author." fallback
                fallback)

    let getSupportedPlatforms x =
        match x with
        | SinglePlatform tf ->
            let rawSupported =
                tf.RawSupportedPlatforms
                |> List.map SinglePlatform
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
                        |> List.filter (fun (p) ->
                            // Portable profile is compatible as soon as if contains us
                            p.Frameworks
                            |> List.exists (fun fw -> fw = tf))
                    profiles
                |> List.map PortableProfile
            rawSupported @ profilesSupported
        | PortableProfile p ->
            getSupportedPreCalculated p
            |> List.map PortableProfile
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
        | PortableProfile (PortableProfileType.UnsupportedProfile xs' as x') ->
            // custom profiles are not in our lists -> custom logic
            match y with
            | PortableProfile y' ->
                x' = y' ||
                isSupportedNotEqual y' x'
            | SinglePlatform y' ->
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
        | SinglePlatform fw -> [fw]
        | PortableProfile p -> p.Frameworks
    static member FindPortable (fws: _ list) = SupportCalculation.findPortable fws
    
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