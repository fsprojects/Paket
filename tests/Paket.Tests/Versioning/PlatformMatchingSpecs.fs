﻿module Paket.PlatformMatchingSpecs

open Paket
open NUnit.Framework
open FsUnit


[<Test>]
let ``Check that lists are updated``() =
    // If this test fails it most likely means you need to
    //  - Update the lists in KnownTargetProfiles
    //  - Update base-lines (ie run Integration-Test-Suite) with update-base-lines set to true
    //  - Review the diff + find a reviewer (on the PR) if you are unsure
    //  - (In Extreme cases) Update this test.

    let checkListEx (tagReader:obj -> int) (cases:Reflection.UnionCaseInfo[]) (l:'t list) =
        let tags = l |> List.map tagReader
        cases
        |> Seq.forall (fun case ->
            let foundCase = tags |> Seq.contains case.Tag
            if not foundCase then
                Assert.Fail (sprintf "Case '%s' was not found in KnownTargetProfiles.<type>Versions for '%s'" case.Name typeof<'t>.Name)
            foundCase)
        |> shouldEqual true
        if l.Length <> cases.Length then
            Assert.Fail (sprintf "KnownTargetProfiles.<list> doesnt't match number of cases for '%s'." typeof<'t>.Name)
    let checkList (l:'t list) =
        let tagReader = FSharp.Reflection.FSharpValue.PreComputeUnionTagReader(typeof<'t>)
        let cases = FSharp.Reflection.FSharpType.GetUnionCases(typeof<'t>)
        checkListEx tagReader cases l
    
    checkList KnownTargetProfiles.DotNetFrameworkVersions
    checkList KnownTargetProfiles.DotNet6OperatingSystems
    checkList KnownTargetProfiles.DotNet6WindowsVersions
    checkList KnownTargetProfiles.DotNet5OperatingSystems
    checkList KnownTargetProfiles.DotNet5WindowsVersions
    checkList KnownTargetProfiles.DotNetCoreAppVersions
    checkList KnownTargetProfiles.DotNetStandardVersions
    checkList KnownTargetProfiles.DotNetUnityVersions
    checkList KnownTargetProfiles.MonoAndroidVersions
    checkList KnownTargetProfiles.SilverlightVersions
    checkList KnownTargetProfiles.UAPVersions
    checkList KnownTargetProfiles.WindowsPhoneAppVersions
    checkList KnownTargetProfiles.WindowsPhoneVersions
    checkList KnownTargetProfiles.WindowsVersions

    let reader = FSharp.Reflection.FSharpValue.PreComputeUnionTagReader(typeof<PortableProfileType>)
    let unsupportedTag = reader(PortableProfileType.UnsupportedProfile [])
    let cases =
        FSharp.Reflection.FSharpType.GetUnionCases(typeof<PortableProfileType>)
        |> Array.filter(fun case -> case.Tag <> unsupportedTag)
    checkListEx reader cases KnownTargetProfiles.AllPortableProfiles

[<Test>]
let ``Can detect uap10``() =
    let p = PlatformMatching.forceExtractPlatforms "uap10"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.UAP UAPVersion.V10)))

[<Test>]
let ``Doesn't fail on profiles``() =
    let p = PlatformMatching.extractPlatforms false "profiles"
    p |> shouldEqual None

[<Test>]
let ``Can detect uap10.1``() =
    let p = PlatformMatching.forceExtractPlatforms "UAP10.1"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.UAP UAPVersion.V10_1)))

[<Test>]
let ``Can detect MonoTouch0.0``() =
    let p = PlatformMatching.forceExtractPlatforms "MonoTouch0.0"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform FrameworkIdentifier.MonoTouch))

[<Test>]
let ``Can detect netcore1.0``() =
    // Currently required for backwards compat (2017-08-20), as we wrote these incorrectly in previous versions.
    let p = PlatformMatching.forceExtractPlatforms "netcore1.0"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetCoreApp DotNetCoreAppVersion.V1_0)))

[<Test>]
let ``Can detect wpa``() =
    let p = PlatformMatching.forceExtractPlatforms "wpa"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.WindowsPhoneApp WindowsPhoneAppVersion.V8_1)))

[<Test>]
let ``Can detect wpav8.1``() =
    let p = PlatformMatching.forceExtractPlatforms "wpav8.1"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.WindowsPhoneApp WindowsPhoneAppVersion.V8_1)))

[<Test>]
let ``Can detect wpv8.0``() =
    let p = PlatformMatching.forceExtractPlatforms "wpv8.0"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.WindowsPhone WindowsPhoneVersion.V8)))

[<Test>]
let ``Can detect winv4.5``() =
    let p = PlatformMatching.forceExtractPlatforms "winv4.5"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.Windows WindowsVersion.V8)))

[<Test>]
let ``Can detect uap101``() =
    // Currently required for backwards compat (2017-08-20), as we wrote these incorrectly in previous versions.
    let p = PlatformMatching.forceExtractPlatforms "uap101"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (UAP UAPVersion.V10_1)))


[<Test>]
let ``Can detect MonoTouch0.00``() =
    let p = PlatformMatching.forceExtractPlatforms "MonoTouch0.00"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform FrameworkIdentifier.MonoTouch))

[<Test>]
let ``Can detect WindowsPhoneApp0.0``() =
    let p = PlatformMatching.forceExtractPlatforms "WindowsPhoneApp0.0"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.WindowsPhoneApp WindowsPhoneAppVersion.V8_1)))

[<Test>]
let ``Can detect WindowsPhone8.0``() =
    let p = PlatformMatching.forceExtractPlatforms "WindowsPhone8.0"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.WindowsPhone WindowsPhoneVersion.V8)))

[<Test>]
let ``Can detect Windows8.0``() =
    let p = PlatformMatching.forceExtractPlatforms "Windows8.0"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.Windows WindowsVersion.V8)))

[<Test>]
let ``Can detect .NETFramework4.0-Client``() =
    let p = PlatformMatching.forceExtractPlatforms ".NETFramework4.0-Client"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4)))

[<Test>]
let ``Can detect MonoAndroid0.0``() =
    let p = PlatformMatching.forceExtractPlatforms "MonoAndroid0.0"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.MonoAndroid MonoAndroidVersion.V1)))

[<Test>]
let ``Can detect net3.5``() =
    let p = PlatformMatching.forceExtractPlatforms "net3.5"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V3_5)))

[<Test>]
let ``Can detect 35``() =
    let p = PlatformMatching.forceExtractPlatforms "35"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V3_5)))

[<Test>]
let ``Can detect 3.5``() =
    let p = PlatformMatching.forceExtractPlatforms "3.5"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V3_5)))

[<Test>]
let ``Can detect net4.00.03``() =
    let p = PlatformMatching.forceExtractPlatforms "net4.00.03"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_0_3)))

[<Test>]
let ``Can detect net45``() =
    let p = PlatformMatching.forceExtractPlatforms "net45"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_5)))

[<Test>]
let ``Can detect net46``() =
    let p = PlatformMatching.forceExtractPlatforms "net46"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_6)))

[<Test>]
let ``Can detect net461``() =
    let p = PlatformMatching.forceExtractPlatforms "net461"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_6_1)))

[<Test>]
let ``Can detect net47``() =
    let p = PlatformMatching.forceExtractPlatforms "net47"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_7)))

[<Test>]
let ``Can detect net471``() =
    let p = PlatformMatching.forceExtractPlatforms "net471"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_7_1)))

[<Test>]
let ``Can detect net48``() =
    let p = PlatformMatching.forceExtractPlatforms "net48"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_8)))

[<Test>]
let ``Can detect net481``() =
    let p = PlatformMatching.forceExtractPlatforms "net481"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_8_1)))

[<Test>]
let ``Can detect net5.0``() =
    let p = PlatformMatching.forceExtractPlatforms "net5.0"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V5)))

[<Test>]
let ``Can detect net5``() =
    let p = PlatformMatching.forceExtractPlatforms "net5"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V5)))

[<Test>]
let ``Can detect net5000``() =
    let p = PlatformMatching.forceExtractPlatforms "net5000"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V5)))

[<Test>]
let ``Can detect net5.0-windows``() =
    let p = PlatformMatching.forceExtractPlatforms "net5.0-windows"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet5Windows Net5WindowsVersion.V7_0)))

[<Test>]
let ``Can detect net5-windows``() =
    let p = PlatformMatching.forceExtractPlatforms "net5-windows"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet5Windows Net5WindowsVersion.V7_0)))

[<Test>]
let ``Can detect net5000-windows``() =
    let p = PlatformMatching.forceExtractPlatforms "net5000-windows"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet5Windows Net5WindowsVersion.V7_0)))

[<Test>]
let ``Can detect net5.0-windows10.0.19041.0``() =
    let p = PlatformMatching.forceExtractPlatforms "net5.0-windows10.0.19041.0"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet5Windows Net5WindowsVersion.V10_0_19041_0)))

[<Test>]
let ``Can detect net5.0-windows10.0.19041``() =
    let p = PlatformMatching.forceExtractPlatforms "net5.0-windows10.0.19041"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet5Windows Net5WindowsVersion.V10_0_19041_0)))

[<Test>]
let ``Can detect net5-windows10.0.19041``() =
    let p = PlatformMatching.forceExtractPlatforms "net5-windows10.0.19041"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet5Windows Net5WindowsVersion.V10_0_19041_0)))

[<Test>]
let ``Can detect net5000-windows10.0.19041``() =
    let p = PlatformMatching.forceExtractPlatforms "net5000-windows10.0.19041"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet5Windows Net5WindowsVersion.V10_0_19041_0)))

[<Test>]
let ``Can detect a bunch of net6 platforms``() =
  let testSet = [
      "net6"                       , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V6)
      "net6000"                    , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V6)
      "net6.0-windows"             , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet6Windows Net6WindowsVersion.V7_0)
      "net6-windows"               , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet6Windows Net6WindowsVersion.V7_0)
      "net6.0-windows10.0.19041.0" , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet6Windows Net6WindowsVersion.V10_0_19041_0)
      "net6.0-windows10.0.19041"   , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet6Windows Net6WindowsVersion.V10_0_19041_0)
      "net6-windows10.0.19041"     , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet6Windows Net6WindowsVersion.V10_0_19041_0)
      "net6000-windows10.0.19041"  , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet6Windows Net6WindowsVersion.V10_0_19041_0)
      "net6.0-android30.0"         , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet6WithOs Net6Os.Android)
    ]

  let errors = [
    for p, expected in testSet do
      let parsed = (PlatformMatching.forceExtractPlatforms p).ToTargetProfile false
      if parsed <> Some expected then
        sprintf "%s resulted into %A instead of %A" p parsed expected
  ]

  if not (List.isEmpty errors) then
    failwith (String.concat "\n" errors)

[<Test>]
let ``Can detect a bunch of net7 platforms``() =
  let testSet = [
      "net7"                       , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V7)
      "net7000"                    , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V7)
      "net7.0-windows"             , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet7Windows Net7WindowsVersion.V7_0)
      "net7-windows"               , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet7Windows Net7WindowsVersion.V7_0)
      "net7.0-windows10.0.19041.0" , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet7Windows Net7WindowsVersion.V10_0_19041_0)
      "net7.0-windows10.0.19041"   , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet7Windows Net7WindowsVersion.V10_0_19041_0)
      "net7-windows10.0.19041"     , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet7Windows Net7WindowsVersion.V10_0_19041_0)
      "net7000-windows10.0.19041"  , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet7Windows Net7WindowsVersion.V10_0_19041_0)
      "net7.0-windows10.0.20348"   , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet7Windows Net7WindowsVersion.V10_0_20348_0)
      "net7.0-android30.0"         , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet7WithOs Net7Os.Android)
      "net7.0-maccatalyst16.1"     , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet7WithOs Net7Os.MacCatalyst)
      "net7.0-tizen7.0"            , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet7WithOs Net7Os.Tizen)
    ]

  let errors = [
    for p, expected in testSet do
      let parsed = (PlatformMatching.forceExtractPlatforms p).ToTargetProfile false
      if parsed <> Some expected then
        sprintf "%s resulted into %A instead of %A" p parsed expected
  ]

  if not (List.isEmpty errors) then
    failwith (String.concat "\n" errors)

[<Test>]
let ``Can detect a bunch of net8 platforms``() =
  let testSet = [
      "net8"                       , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V8)
      "net8000"                    , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V8)
      "net8.0-windows"             , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet8Windows Net8WindowsVersion.V7_0)
      "net8-windows"               , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet8Windows Net8WindowsVersion.V7_0)
      "net8.0-windows10.0.19041.0" , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet8Windows Net8WindowsVersion.V10_0_19041_0)
      "net8.0-windows10.0.19041"   , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet8Windows Net8WindowsVersion.V10_0_19041_0)
      "net8-windows10.0.19041"     , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet8Windows Net8WindowsVersion.V10_0_19041_0)
      "net8000-windows10.0.19041"  , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet8Windows Net8WindowsVersion.V10_0_19041_0)
      "net8.0-windows10.0.20348"   , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet8Windows Net8WindowsVersion.V10_0_20348_0)
      "net8.0-android30.0"         , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet8WithOs Net8Os.Android)
      "net8.0-android30.0"         , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet8WithOs Net8Os.Android)
      "net8.0-maccatalyst16.1"     , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet8WithOs Net8Os.MacCatalyst)
      "net8.0-tizen7.0"            , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet8WithOs Net8Os.Tizen)
    ]

  let errors = [
    for p, expected in testSet do
      let parsed = (PlatformMatching.forceExtractPlatforms p).ToTargetProfile false
      if parsed <> Some expected then
        sprintf "%s resulted into %A instead of %A" p parsed expected
  ]

  if not (List.isEmpty errors) then
    failwith (String.concat "\n" errors)

[<Test>]
let ``Can detect a bunch of net9 platforms``() =
  let testSet = [
      "net9"                       , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V9)
      "net9000"                    , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V9)
      "net9.0-windows"             , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet9Windows Net9WindowsVersion.V7_0)
      "net9-windows"               , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet9Windows Net9WindowsVersion.V7_0)
      "net9.0-windows10.0.19041.0" , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet9Windows Net9WindowsVersion.V10_0_19041_0)
      "net9.0-windows10.0.19041"   , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet9Windows Net9WindowsVersion.V10_0_19041_0)
      "net9-windows10.0.19041"     , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet9Windows Net9WindowsVersion.V10_0_19041_0)
      "net9000-windows10.0.19041"  , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet9Windows Net9WindowsVersion.V10_0_19041_0)
      "net9.0-android30.0"         , TargetProfile.SinglePlatform (FrameworkIdentifier.DotNet9WithOs Net9Os.Android)
    ]

  let errors = [
    for p, expected in testSet do
      let parsed = (PlatformMatching.forceExtractPlatforms p).ToTargetProfile false
      if parsed <> Some expected then
        sprintf "%s resulted into %A instead of %A" p parsed expected
  ]

  if not (List.isEmpty errors) then
    failwith (String.concat "\n" errors)

[<Test>]
let ``Can detect netstandard1.6``() =
    let p = PlatformMatching.forceExtractPlatforms "netstandard1.6"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetStandard DotNetStandardVersion.V1_6)))

[<Test>]
let ``Can detect netstandard16``() =
    let p = PlatformMatching.forceExtractPlatforms "netstandard16"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetStandard DotNetStandardVersion.V1_6)))

[<Test>]
let ``Can detect .NETCore4.5``() =
    let p = PlatformMatching.forceExtractPlatforms ".NETCore4.5"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.Windows WindowsVersion.V8)))

[<Test>]
let ``Can detect net10-full``() =
    let p = PlatformMatching.forceExtractPlatforms "net10-full"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V1)))

[<Test>]
let ``Can detect net11-full``() =
    let p = PlatformMatching.forceExtractPlatforms "net11-full"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V1_1)))

[<Test>]
let ``Can detect net11-client``() =
    let p = PlatformMatching.forceExtractPlatforms "net11-client"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V1_1)))

[<Test>]
let ``Can detect .NETPortable4.5-Profile111``() =
    let p = PlatformMatching.forceExtractPlatforms ".NETPortable4.5-Profile111"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.PortableProfile PortableProfileType.Profile111))

[<Test>]
let ``Can detect .NETPortable4.5-Profile259``() =
    let p = PlatformMatching.forceExtractPlatforms ".NETPortable4.5-Profile259"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.PortableProfile PortableProfileType.Profile259))

[<Test>]
let ``Can detect .NETPortable4.6-Profile151``() =
    let p = PlatformMatching.forceExtractPlatforms ".NETPortable4.6-Profile151"
    p.ToTargetProfile false |> shouldEqual (Some (TargetProfile.PortableProfile PortableProfileType.Profile151))