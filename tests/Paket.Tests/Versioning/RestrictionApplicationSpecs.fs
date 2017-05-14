module Paket.Requirements.RestrictionApplicationSpecs

open Paket
open FsUnit
open NUnit.Framework
open Paket.Requirements

let dotnet x = SinglePlatform(DotNetFramework(x))

module TestTargetProfiles =
    let DotNetFrameworkVersions =
       [FrameworkVersion.V1
        FrameworkVersion.V1_1
        FrameworkVersion.V2
        FrameworkVersion.V3
        FrameworkVersion.V3_5
        FrameworkVersion.V4_Client
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

    let DotNetUnityProfiles = DotNetUnityVersions |> List.map (DotNetUnity >> SinglePlatform)

    let WindowsProfiles =
       [SinglePlatform(Windows "v4.5")
        SinglePlatform(Windows "v4.5.1")]

    let SilverlightProfiles =
       [SinglePlatform(Silverlight "v3.0")
        SinglePlatform(Silverlight "v4.0")
        SinglePlatform(Silverlight "v5.0")]

    let WindowsPhoneSilverlightProfiles =
       [SinglePlatform(WindowsPhoneSilverlight "v7.0")
        SinglePlatform(WindowsPhoneSilverlight "v7.1")
        SinglePlatform(WindowsPhoneSilverlight "v8.0")
        SinglePlatform(WindowsPhoneSilverlight "v8.1")]

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
        SinglePlatform(WindowsPhoneApp "v8.1")
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
    let restrictions = FrameworkRestriction.AtLeast(Silverlight "v3.0")
    let restricted = applyRestrictionsToTargets restrictions TestTargetProfiles.AllProfiles
    
    restricted |> shouldEqual TestTargetProfiles.SilverlightProfiles