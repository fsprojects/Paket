module Paket.InstallProcess.FSharpCoreRedirectsWarningSpecs

open Paket
open NUnit.Framework
open FsUnit

let private net48 = TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_8)
let private netcoreapp31 = TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetCoreApp DotNetCoreAppVersion.V3_1)
let private netstandard20 = TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetStandard DotNetStandardVersion.V2_0)

[<Test>]
let ``warns when a .NET Framework project references FSharp.Core without redirects: force``() =
    InstallProcess.shouldWarnAboutFSharpCoreRedirects [ net48 ] None
    |> shouldEqual true

[<Test>]
let ``does not warn when a .NET Framework project already has redirects: force``() =
    InstallProcess.shouldWarnAboutFSharpCoreRedirects [ net48 ] (Some Requirements.BindingRedirectsSettings.Force)
    |> shouldEqual false

[<Test>]
let ``does not warn for a pure .NET Core (netcoreapp) project without redirects: force``() =
    InstallProcess.shouldWarnAboutFSharpCoreRedirects [ netcoreapp31 ] None
    |> shouldEqual false

[<Test>]
let ``does not warn for a pure netstandard project without redirects: force``() =
    InstallProcess.shouldWarnAboutFSharpCoreRedirects [ netstandard20 ] None
    |> shouldEqual false

[<Test>]
let ``warns when multi-targeting includes a .NET Framework leg without redirects: force``() =
    InstallProcess.shouldWarnAboutFSharpCoreRedirects [ net48; netcoreapp31 ] None
    |> shouldEqual true

[<Test>]
let ``does not warn when there are no target profiles``() =
    InstallProcess.shouldWarnAboutFSharpCoreRedirects [] None
    |> shouldEqual false
