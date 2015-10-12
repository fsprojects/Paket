module Paket.Requirements.RestrictionFilterSpecs

open System.IO
open Paket
open Paket.Domain
open Chessie.ErrorHandling
open FsUnit
open NUnit.Framework
open TestHelpers
open Paket.Requirements

[<Test>]
let ``should filter net45 and >= net40``() = 
    let l1 = [FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4))]
    let l2 = [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_5))]
    filterRestrictions l1 l2
    |> shouldEqual l2

[<Test>]
let ``should filter >= net40 and net45``() = 
    let l1 = [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_5))]
    let l2 = [FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4))]    
    filterRestrictions l1 l2
    |> shouldEqual l1

[<Test>]
let ``should filter >=net40 and >= net45``() = 
    let l1 = [FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4))]
    let l2 = [FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4_5))]
    filterRestrictions l1 l2
    |> shouldEqual l2

[<Test>]
let ``should filter >= net40 and portable``() =     
    let l1 = [FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4))]
    let l2 = [FrameworkRestriction.Portable("abc")]

    filterRestrictions l1 l2
    |> shouldEqual []