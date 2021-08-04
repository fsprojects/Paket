module Paket.FrameworkCompatibilitySpecs

open System.IO
open Paket
open Paket.Domain
open Chessie.ErrorHandling
open FsUnit
open NUnit.Framework
open TestHelpers
open Paket.Requirements


(*
    Ensure that projects targeting NETFramework can properly add netstandard references in
    accordance with this chart

   ---------------------------------------------------------------------------------
   | Platform Name  | Alias                                                        |
   |-------------------------------------------------------------------------------|
   | .NET Standard  | netstandard  | 1.0 | 1.1 | 1.2 | 1.3 | 1.4 | 1.5 | 1.6 | 2.0 |
   |-------------------------------------------------------------------------------|
   | .NET Core      | netcoreapp   |  →  |  →  |  →  |  →  | →   |  →  | 1.0 | 2.0 |
   |-------------------------------------------------------------------------------|
   | .NET Framework | net          |  →  | 4.5 |4.5.1| 4.6 |4.6.1|4.6.2|vNext|4.6.1|
   ---------------------------------------------------------------------------------

   - https://docs.microsoft.com/en-us/dotnet/articles/standard/library
*)


[<Test>]
let ``net46 should be compatible with netstandard13``() =
    (TargetProfile.SinglePlatform (DotNetFramework FrameworkVersion.V4_6)).IsAtLeast (TargetProfile.SinglePlatform (DotNetStandard DotNetStandardVersion.V1_3))
    |> shouldEqual true

    (TargetProfile.SinglePlatform (DotNetStandard DotNetStandardVersion.V1_3)).IsSupportedBy (TargetProfile.SinglePlatform (DotNetFramework FrameworkVersion.V4_6))
    |> shouldEqual true

    (TargetProfile.SinglePlatform (DotNetStandard DotNetStandardVersion.V1_3)).IsSmallerThan (TargetProfile.SinglePlatform (DotNetFramework FrameworkVersion.V4_6))
    |> shouldEqual true


[<Test>]
let ``net462 should be compatible with net45``() =
    let net45 = TargetProfile.SinglePlatform (DotNetFramework FrameworkVersion.V4_5)
    let net462 = TargetProfile.SinglePlatform (DotNetFramework FrameworkVersion.V4_6_2)

    net462.IsAtLeast net45
    |> shouldEqual true

    net45.IsSupportedBy net462
    |> shouldEqual true

    net45.IsSmallerThan net462
    |> shouldEqual true

[<Test>]
let ``netcoreapp2.1 should be compatible with netcoreapp2.0``() =
    let ``netcoreapp2.0`` = TargetProfile.SinglePlatform (DotNetCoreApp DotNetCoreAppVersion.V2_0)
    let ``netcoreapp2.1`` = TargetProfile.SinglePlatform (DotNetCoreApp DotNetCoreAppVersion.V2_1)

    ``netcoreapp2.1``.IsAtLeast ``netcoreapp2.0``
    |> shouldEqual true

    ``netcoreapp2.0``.IsSupportedBy ``netcoreapp2.1``
    |> shouldEqual true

    ``netcoreapp2.0``.IsSmallerThan ``netcoreapp2.1``
    |> shouldEqual true

[<Test>]
let ``netcoreapp2.1 should be compatible with netstandard2.0``() =
    let ``netstandard2.0`` = TargetProfile.SinglePlatform (DotNetStandard DotNetStandardVersion.V2_0)
    let ``netcoreapp2.1`` = TargetProfile.SinglePlatform (DotNetCoreApp DotNetCoreAppVersion.V2_1)

    ``netcoreapp2.1``.IsAtLeast ``netstandard2.0``
    |> shouldEqual true

    ``netstandard2.0``.IsSupportedBy ``netcoreapp2.1``
    |> shouldEqual true

    ``netstandard2.0``.IsSmallerThan ``netcoreapp2.1``
    |> shouldEqual true

[<Test>]
let ``netcoreapp2.2 should be compatible with netstandard2.0``() =
    let ``netstandard2.0`` = TargetProfile.SinglePlatform (DotNetStandard DotNetStandardVersion.V2_0)
    let ``netcoreapp2.2`` = TargetProfile.SinglePlatform (DotNetCoreApp DotNetCoreAppVersion.V2_2)

    ``netcoreapp2.2``.IsAtLeast ``netstandard2.0``
    |> shouldEqual true

    ``netstandard2.0``.IsSupportedBy ``netcoreapp2.2``
    |> shouldEqual true

    ``netstandard2.0``.IsSmallerThan ``netcoreapp2.2``
    |> shouldEqual true

[<Test>]
let ``netcoreapp3.0 should be compatible with netstandard2.1``() =
    let ``netstandard2.1`` = TargetProfile.SinglePlatform (DotNetStandard DotNetStandardVersion.V2_1)
    let ``netcoreapp3.0`` = TargetProfile.SinglePlatform (DotNetCoreApp DotNetCoreAppVersion.V3_0)

    ``netcoreapp3.0``.IsAtLeast ``netstandard2.1``
    |> shouldEqual true

    ``netstandard2.1``.IsSupportedBy ``netcoreapp3.0``
    |> shouldEqual true

    ``netstandard2.1``.IsSmallerThan ``netcoreapp3.0``
    |> shouldEqual true


[<Test>]
let ``netcoreapp3.1 should be compatible with netstandard2.1``() =
    let ``netstandard2.1`` = TargetProfile.SinglePlatform (DotNetStandard DotNetStandardVersion.V2_1)
    let ``netcoreapp3.1`` = TargetProfile.SinglePlatform (DotNetCoreApp DotNetCoreAppVersion.V3_1)

    ``netcoreapp3.1``.IsAtLeast ``netstandard2.1``
    |> shouldEqual true

    ``netstandard2.1``.IsSupportedBy ``netcoreapp3.1``
    |> shouldEqual true

    ``netstandard2.1``.IsSmallerThan ``netcoreapp3.1``
    |> shouldEqual true


[<Test>]
let ``monoandroid8.0 should be compatible with netstandard2.0``() =
    let ``netstandard2.0`` = TargetProfile.SinglePlatform (DotNetStandard DotNetStandardVersion.V2_0)
    let ``monoandroid8.0`` = TargetProfile.SinglePlatform (MonoAndroid MonoAndroidVersion.V8)

    ``monoandroid8.0``.IsAtLeast ``netstandard2.0``
    |> shouldEqual true

    ``netstandard2.0``.IsSupportedBy ``monoandroid8.0``
    |> shouldEqual true

    ``netstandard2.0``.IsSmallerThan ``monoandroid8.0``
    |> shouldEqual true


[<Test>]
let ``monoandroid10.0 should be compatible with netstandard2.1``() =
    let ``netstandard2.1`` = TargetProfile.SinglePlatform (DotNetStandard DotNetStandardVersion.V2_1)
    let ``monoandroid10.0`` = TargetProfile.SinglePlatform (MonoAndroid MonoAndroidVersion.V10)

    ``monoandroid10.0``.IsAtLeast ``netstandard2.1``
    |> shouldEqual true

    ``netstandard2.1``.IsSupportedBy ``monoandroid10.0``
    |> shouldEqual true

    ``netstandard2.1``.IsSmallerThan ``monoandroid10.0``
    |> shouldEqual true

[<Test>]
let ``Xamarin.Mac should be compatible with netstandard2.0``() =
    let ``netstandard2.0`` = TargetProfile.SinglePlatform (DotNetStandard DotNetStandardVersion.V2_0)
    let ``xamarinmac`` = TargetProfile.SinglePlatform XamarinMac

    ``xamarinmac``.IsAtLeast ``netstandard2.0``
    |> shouldEqual true

    ``netstandard2.0``.IsSupportedBy ``xamarinmac``
    |> shouldEqual true

    ``netstandard2.0``.IsSmallerThan ``xamarinmac``
    |> shouldEqual true
