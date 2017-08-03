module Paket.FrameworkCompatibilitySpecs

open System.IO
open Pri.LongPath
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
    (SinglePlatform (DotNetFramework FrameworkVersion.V4_6)).IsAtLeast (SinglePlatform (DotNetStandard DotNetStandardVersion.V1_3))
    |> shouldEqual true

    (SinglePlatform (DotNetStandard DotNetStandardVersion.V1_3)).IsSupportedBy (SinglePlatform (DotNetFramework FrameworkVersion.V4_6))
    |> shouldEqual true

    (SinglePlatform (DotNetStandard DotNetStandardVersion.V1_3)).IsSmallerThan (SinglePlatform (DotNetFramework FrameworkVersion.V4_6))
    |> shouldEqual true

