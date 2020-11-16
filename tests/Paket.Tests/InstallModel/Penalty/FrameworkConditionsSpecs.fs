module Paket.InstallModel.FrameworkConditionsSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.Domain

open Paket.PlatformMatching

let getCondition cond targets = getCondition cond targets

[<Test>]
let ``should create empty condition for empty profile list``() = 
    getCondition None Set.empty
    |> shouldEqual ""

[<Test>]
let ``should create simple condition for simple .NET Framework``() = 
    getCondition None (TargetProfile.SinglePlatform(DotNetFramework FrameworkVersion.V3) |> Set.singleton)
    |> shouldEqual "$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v3.0'"

[<Test>]
let ``should create nested condition for two .NET Frameworks``() = 
    getCondition None ([TargetProfile.SinglePlatform(DotNetFramework FrameworkVersion.V3); TargetProfile.SinglePlatform(DotNetFramework FrameworkVersion.V4_5)] |> Set.ofSeq)
    |> shouldEqual "$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v3.0' Or $(TargetFrameworkVersion) == 'v4.5')"

[<Test>]
let ``should create nested condition for two .NET Frameworks in different order``() = 
    getCondition None ([TargetProfile.SinglePlatform(DotNetFramework FrameworkVersion.V4_5); TargetProfile.SinglePlatform(DotNetFramework FrameworkVersion.V3)] |> Set.ofSeq)
    |> shouldEqual "$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v3.0' Or $(TargetFrameworkVersion) == 'v4.5')"

[<Test>]
let ``should create nested condition for multiple .NET Frameworks``() = 
    getCondition None ([TargetProfile.SinglePlatform(DotNetFramework FrameworkVersion.V3); TargetProfile.SinglePlatform(DotNetFramework FrameworkVersion.V4_5); TargetProfile.SinglePlatform(DotNetFramework FrameworkVersion.V2)] |> Set.ofSeq)
    |> shouldEqual "$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v2.0' Or $(TargetFrameworkVersion) == 'v3.0' Or $(TargetFrameworkVersion) == 'v4.5')"

[<Test>]
let ``should minimize condition if we have all .NET Frameworks``() = 
    getCondition None (KnownTargetProfiles.DotNetFrameworkProfiles |> Set.ofSeq)
    |> shouldEqual "$(TargetFrameworkIdentifier) == '.NETFramework'"

[<Test>]
let ``should minimize condition if we have all WindowsProfiles``() = 
    getCondition None (KnownTargetProfiles.WindowsProfiles |> Set.ofSeq)
    |> shouldEqual "$(TargetFrameworkIdentifier) == '.NETCore'"

[<Test>]
let ``should create nested condition for .NET Framework and Silverlight``() = 
    [TargetProfile.SinglePlatform(DotNetFramework FrameworkVersion.V3)
     TargetProfile.SinglePlatform(DotNetFramework FrameworkVersion.V4_5)
     TargetProfile.SinglePlatform(Silverlight SilverlightVersion.V3)] 
    |> Set.ofSeq
    |> getCondition None
    |> shouldEqual "($(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v3.0' Or $(TargetFrameworkVersion) == 'v4.5')) Or ($(TargetFrameworkIdentifier) == 'Silverlight' And $(TargetFrameworkVersion) == 'v3.0')"

[<Test>]
let ``should create nested condition for full .NET Framework and Silverlight``() = 
    TargetProfile.SinglePlatform(Silverlight SilverlightVersion.V3) :: KnownTargetProfiles.DotNetFrameworkProfiles
    |> Set.ofSeq
    |> getCondition None
    |> shouldEqual "($(TargetFrameworkIdentifier) == '.NETFramework') Or ($(TargetFrameworkIdentifier) == 'Silverlight' And $(TargetFrameworkVersion) == 'v3.0')"