module Paket.PlatformMatchingSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``Can detect uap10``() =
    let p = PlatformMatching.forceExtractPlatforms "uap10"
    p.ToTargetProfile false |> shouldEqual (Some (SinglePlatform (FrameworkIdentifier.UAP UAPVersion.V10)))

[<Test>]
let ``Can detect uap10.1``() =
    let p = PlatformMatching.forceExtractPlatforms "UAP10.1"
    p.ToTargetProfile false |> shouldEqual (Some (SinglePlatform (FrameworkIdentifier.UAP UAPVersion.V10_1)))
[<Test>]
let ``Can detect net45``() =
    let p = PlatformMatching.forceExtractPlatforms "net45"
    p.ToTargetProfile false |> shouldEqual (Some (SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_5)))

[<Test>]
let ``Can detect MonoTouch0.0``() =
    let p = PlatformMatching.forceExtractPlatforms "MonoTouch0.0"
    p.ToTargetProfile false |> shouldEqual (Some (SinglePlatform (FrameworkIdentifier.MonoTouch)))

[<Test>]
let ``Can detect MonoTouch0.00``() =
    let p = PlatformMatching.forceExtractPlatforms "MonoTouch0.00"
    p.ToTargetProfile false |> shouldEqual (Some (SinglePlatform (FrameworkIdentifier.MonoTouch)))

[<Test>]
let ``Can detect MonoAndroid0.0``() =
    let p = PlatformMatching.forceExtractPlatforms "MonoAndroid0.0"
    p.ToTargetProfile false |> shouldEqual (Some (SinglePlatform (FrameworkIdentifier.MonoAndroid MonoAndroidVersion.V1)))

[<Test>]
let ``Can detect net3.5``() =
    let p = PlatformMatching.forceExtractPlatforms "net3.5"
    p.ToTargetProfile false |> shouldEqual (Some (SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V3_5)))


[<Test>]
let ``Can detect 35``() =
    let p = PlatformMatching.forceExtractPlatforms "35"
    p.ToTargetProfile false |> shouldEqual (Some (SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V3_5)))

[<Test>]
let ``Can detect 3.5``() =
    let p = PlatformMatching.forceExtractPlatforms "3.5"
    p.ToTargetProfile false |> shouldEqual (Some (SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V3_5)))

[<Test>]
let ``Can detect net4.00.03``() =
    let p = PlatformMatching.forceExtractPlatforms "net4.00.03"
    p.ToTargetProfile false |> shouldEqual (Some (SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_0_3)))

[<Test>]
let ``Can detect netstandard1.6``() =
    let p = PlatformMatching.forceExtractPlatforms "netstandard1.6"
    p.ToTargetProfile false |> shouldEqual (Some (SinglePlatform (FrameworkIdentifier.DotNetStandard DotNetStandardVersion.V1_6)))

[<Test>]
let ``Can detect netstandard16``() =
    let p = PlatformMatching.forceExtractPlatforms "netstandard16"
    p.ToTargetProfile false |> shouldEqual (Some (SinglePlatform (FrameworkIdentifier.DotNetStandard DotNetStandardVersion.V1_6)))

[<Test>]
let ``Can detect .NETCore4.5``() =
    let p = PlatformMatching.forceExtractPlatforms ".NETCore4.5"
    p.ToTargetProfile false |> shouldEqual (Some (SinglePlatform (FrameworkIdentifier.Windows WindowsVersion.V8)))

[<Test>]
let ``Can detect net10-full``() =
    let p = PlatformMatching.forceExtractPlatforms "net10-full"
    p.ToTargetProfile false |> shouldEqual (Some (SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V1)))

[<Test>]
let ``Can detect net11-full``() =
    let p = PlatformMatching.forceExtractPlatforms "net11-full"
    p.ToTargetProfile false |> shouldEqual (Some (SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V1_1)))

[<Test>]
let ``Can detect net11-client``() =
    let p = PlatformMatching.forceExtractPlatforms "net11-client"
    p.ToTargetProfile false |> shouldEqual (Some (SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V1_1)))

[<Test>]
let ``Can detect .NETPortable4.5-Profile111``() =
    let p = PlatformMatching.forceExtractPlatforms ".NETPortable4.5-Profile111"
    p.ToTargetProfile false |> shouldEqual (Some (PortableProfile PortableProfileType.Profile111))

[<Test>]
let ``Can detect .NETPortable4.5-Profile259``() =
    let p = PlatformMatching.forceExtractPlatforms ".NETPortable4.5-Profile259"
    p.ToTargetProfile false |> shouldEqual (Some (PortableProfile PortableProfileType.Profile259))

[<Test>]
let ``Can detect .NETPortable4.6-Profile151``() =
    let p = PlatformMatching.forceExtractPlatforms ".NETPortable4.6-Profile151"
    p.ToTargetProfile false |> shouldEqual (Some (PortableProfile PortableProfileType.Profile151))