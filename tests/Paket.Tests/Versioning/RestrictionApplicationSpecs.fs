module Paket.Requirements.RestrictionApplicationSpecs

open Paket
open FsUnit
open NUnit.Framework
open Paket.Requirements

let dotnet x = TargetProfile.SinglePlatform(DotNetFramework(x))

module TestTargetProfiles =
    let DotNetFrameworkVersions =
       [FrameworkVersion.V1
        FrameworkVersion.V1_1
        FrameworkVersion.V2
        FrameworkVersion.V3
        FrameworkVersion.V3_5
        FrameworkVersion.V4
        FrameworkVersion.V4_5
        FrameworkVersion.V4_5_1
        FrameworkVersion.V4_5_2
        FrameworkVersion.V4_5_3
        FrameworkVersion.V4_6]
        

    let DotNetFrameworkProfiles = DotNetFrameworkVersions |> List.map dotnet

    let DotNetUnityVersions = [
        DotNetUnityVersion.V3_5_Full
        DotNetUnityVersion.V3_5_Subset
        DotNetUnityVersion.V3_5_Micro
        DotNetUnityVersion.V3_5_Web
    ]

    let DotNetUnityProfiles = DotNetUnityVersions |> List.map (DotNetUnity >> TargetProfile.SinglePlatform)

    let WindowsProfiles =
       [TargetProfile.SinglePlatform(Windows WindowsVersion.V8)
        TargetProfile.SinglePlatform(Windows WindowsVersion.V8_1)]

    let SilverlightProfiles =
       [TargetProfile.SinglePlatform(Silverlight SilverlightVersion.V3)
        TargetProfile.SinglePlatform(Silverlight SilverlightVersion.V4)
        TargetProfile.SinglePlatform(Silverlight SilverlightVersion.V5)]

    let WindowsPhoneSilverlightProfiles =
       [TargetProfile.SinglePlatform(WindowsPhone WindowsPhoneVersion.V7)
        TargetProfile.SinglePlatform(WindowsPhone WindowsPhoneVersion.V7_5)
        TargetProfile.SinglePlatform(WindowsPhone WindowsPhoneVersion.V8)
        TargetProfile.SinglePlatform(WindowsPhone WindowsPhoneVersion.V8_1)]


    let MonoAndroidProfiles =
       [TargetProfile.SinglePlatform(MonoAndroid MonoAndroidVersion.V1)]

    let AllProfiles =
       DotNetFrameworkProfiles @ 
       WindowsProfiles @ 
       SilverlightProfiles @
       WindowsPhoneSilverlightProfiles @
       MonoAndroidProfiles @
       DotNetUnityProfiles @
       [TargetProfile.SinglePlatform(MonoTouch)
        TargetProfile.SinglePlatform(XamariniOS)
        TargetProfile.SinglePlatform(XamarinMac)
        TargetProfile.SinglePlatform(WindowsPhoneApp WindowsPhoneAppVersion.V8_1)
       ]|> Set.ofList

[<Test>]
let ``>= net10 contains all but only dotnet versions (#1124)`` () =
    /// https://github.com/fsprojects/Paket/issues/1124
    let restrictions = FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V1))
    let restricted = applyRestrictionsToTargets restrictions TestTargetProfiles.AllProfiles
    
    restricted |> shouldEqual (TestTargetProfiles.DotNetFrameworkProfiles |> Set.ofList)

[<Test>]
let ``>= net452 contains 4.5.2 and following versions`` () =
    let restrictions = FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4_5_2))
    let restricted = applyRestrictionsToTargets restrictions TestTargetProfiles.AllProfiles
    let expected = [FrameworkVersion.V4_5_2; FrameworkVersion.V4_5_3; FrameworkVersion.V4_6] |> List.map dotnet |> Set.ofList

    restricted |> shouldEqual expected

[<Test>]
let ``>= net40 < net451 contains 4.0 and 4.5`` () =
    let restrictions = FrameworkRestriction.Between(DotNetFramework(FrameworkVersion.V4), DotNetFramework(FrameworkVersion.V4_5_1))
    let restricted = applyRestrictionsToTargets restrictions TestTargetProfiles.AllProfiles
    let expected = [FrameworkVersion.V4; FrameworkVersion.V4_5] |> List.map dotnet|> Set.ofList

    restricted |> shouldEqual expected

[<Test>]
let ``>= sl30 contains all but only silverlight versions`` () =
    let restrictions = FrameworkRestriction.AtLeast(Silverlight SilverlightVersion.V3)
    let restricted = applyRestrictionsToTargets restrictions TestTargetProfiles.AllProfiles
    
    restricted |> shouldEqual (TestTargetProfiles.SilverlightProfiles|> Set.ofList)