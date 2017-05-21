module Paket.Requirements.RestrictionApplicationSpecs

open Paket
open FsUnit
open NUnit.Framework
open Paket.Requirements

let dotnet x = SinglePlatform(DotNetFramework(x))

module TestTargetProfiles =
    let DotNetFrameworkVersions =
        KnownTargetProfiles.DotNetFrameworkVersions

    let DotNetFrameworkProfiles = DotNetFrameworkVersions |> List.map dotnet

    let DotNetUnityVersions = [
        DotNetUnityVersion.V3_5_Full
        DotNetUnityVersion.V3_5_Subset
        DotNetUnityVersion.V3_5_Micro
        DotNetUnityVersion.V3_5_Web
    ]

    let DotNetUnityProfiles = DotNetUnityVersions |> List.map (DotNetUnity >> SinglePlatform)

    let WindowsProfiles =
       [SinglePlatform(Windows WindowsVersion.V8)
        SinglePlatform(Windows WindowsVersion.V8_1)]

    let SilverlightProfiles =
       [SinglePlatform(Silverlight SilverlightVersion.V3)
        SinglePlatform(Silverlight SilverlightVersion.V4)
        SinglePlatform(Silverlight SilverlightVersion.V5)]

    let WindowsPhoneSilverlightProfiles =
       [SinglePlatform(WindowsPhone WindowsPhoneVersion.V7)
        SinglePlatform(WindowsPhone WindowsPhoneVersion.V7_5)
        SinglePlatform(WindowsPhone WindowsPhoneVersion.V8)
        SinglePlatform(WindowsPhone WindowsPhoneVersion.V8_1)]

    let AllProfiles =
       DotNetFrameworkProfiles @ 
       WindowsProfiles @ 
       SilverlightProfiles @
       WindowsPhoneSilverlightProfiles @
       DotNetUnityProfiles @
       [SinglePlatform(MonoAndroid)
        SinglePlatform(MonoTouch)
        SinglePlatform(XamariniOS)
        SinglePlatform(XamarinMac)
        SinglePlatform(WindowsPhoneApp WindowsPhoneAppVersion.V8_1)
       ]

[<Test>]
let ``>= net10 contains all but only dotnet versions (#1124)`` () =
    /// https://github.com/fsprojects/Paket/issues/1124
    let restrictions = FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V1))
    let restricted = applyRestrictionsToTargets restrictions TestTargetProfiles.AllProfiles
    
    restricted |> shouldEqual TestTargetProfiles.DotNetFrameworkProfiles

[<Test>]
let ``>= net452 contains 4.5.2 and following versions`` () =
    let restrictions = FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4_5_2))
    let restricted = applyRestrictionsToTargets restrictions TestTargetProfiles.AllProfiles
    let expected = [FrameworkVersion.V4_5_2; FrameworkVersion.V4_5_3; FrameworkVersion.V4_6] |> List.map dotnet

    restricted |> shouldEqual expected

[<Test>]
let ``>= net40 < net451 contains 4.0 and 4.5`` () =
    let restrictions = FrameworkRestriction.Between(DotNetFramework(FrameworkVersion.V4), DotNetFramework(FrameworkVersion.V4_5_1))
    let restricted = applyRestrictionsToTargets restrictions TestTargetProfiles.AllProfiles
    let expected = [FrameworkVersion.V4; FrameworkVersion.V4_5] |> List.map dotnet

    restricted |> shouldEqual expected

[<Test>]
let ``>= sl30 contains all but only silverlight versions`` () =
    let restrictions = FrameworkRestriction.AtLeast(Silverlight SilverlightVersion.V3)
    let restricted = applyRestrictionsToTargets restrictions TestTargetProfiles.AllProfiles
    
    restricted |> shouldEqual TestTargetProfiles.SilverlightProfiles