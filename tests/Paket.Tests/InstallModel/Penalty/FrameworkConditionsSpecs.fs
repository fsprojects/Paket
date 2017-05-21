module Paket.InstallModel.FrameworkConditionsSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.Domain

open Paket.PlatformMatching

let getCondition cond targets = getCondition cond [targets] targets

[<Test>]
let ``should create empty condition for empty profile list``() = 
    getCondition None []
    |> shouldEqual ""

[<Test>]
let ``should create simple condition for simple .NET Framework``() = 
    getCondition None [SinglePlatform(DotNetFramework FrameworkVersion.V3)]
    |> shouldEqual "$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v3.0'"

[<Test>]
let ``should create nested condition for two .NET Frameworks``() = 
    getCondition None [SinglePlatform(DotNetFramework FrameworkVersion.V3); SinglePlatform(DotNetFramework FrameworkVersion.V4_5)]
    |> shouldEqual "$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v3.0' Or $(TargetFrameworkVersion) == 'v4.5')"

[<Test>]
let ``should create nested condition for two .NET Frameworks in different order``() = 
    getCondition None [SinglePlatform(DotNetFramework FrameworkVersion.V4_5); SinglePlatform(DotNetFramework FrameworkVersion.V3)]
    |> shouldEqual "$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v3.0' Or $(TargetFrameworkVersion) == 'v4.5')"

[<Test>]
let ``should create nested condition for multiple .NET Frameworks``() = 
    getCondition None [SinglePlatform(DotNetFramework FrameworkVersion.V3); SinglePlatform(DotNetFramework FrameworkVersion.V4_5); SinglePlatform(DotNetFramework FrameworkVersion.V2)]
    |> shouldEqual "$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v2.0' Or $(TargetFrameworkVersion) == 'v3.0' Or $(TargetFrameworkVersion) == 'v4.5')"

[<Test>]
let ``should minimize condition if we have all .NET Frameworks``() = 
    getCondition None KnownTargetProfiles.DotNetFrameworkProfiles
    |> shouldEqual "$(TargetFrameworkIdentifier) == '.NETFramework'"

[<Test>]
let ``should minimize condition if we have all WindowsProfiles``() = 
    getCondition None KnownTargetProfiles.WindowsProfiles
    |> shouldEqual "$(TargetFrameworkIdentifier) == '.NETCore'"

[<Test>]
let ``should create nested condition for .NET Framework and Silverlight``() = 
    [SinglePlatform(DotNetFramework FrameworkVersion.V3)
     SinglePlatform(DotNetFramework FrameworkVersion.V4_5)
     SinglePlatform(Silverlight SilverlightVersion.V3)]
    |> getCondition None
    |> shouldEqual "($(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v3.0' Or $(TargetFrameworkVersion) == 'v4.5')) Or ($(TargetFrameworkIdentifier) == 'Silverlight' And $(TargetFrameworkVersion) == 'v3.0')"

[<Test>]
let ``should create nested condition for full .NET Framework and Silverlight``() = 
    SinglePlatform(Silverlight SilverlightVersion.V3) :: KnownTargetProfiles.DotNetFrameworkProfiles
    |> getCondition None
    |> shouldEqual "($(TargetFrameworkIdentifier) == '.NETFramework') Or ($(TargetFrameworkIdentifier) == 'Silverlight' And $(TargetFrameworkVersion) == 'v3.0')"