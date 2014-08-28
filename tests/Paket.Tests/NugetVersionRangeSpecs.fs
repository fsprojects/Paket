module Paket.NugetVersionRangeSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``can detect minimum version``() = 
    Nuget.parseVersionRange "2.2" |> shouldEqual (AtLeast "2.2")
    Nuget.parseVersionRange "1.2" |> shouldEqual (AtLeast "1.2")

[<Test>]
let ``can detect specific version``() = 
    Nuget.parseVersionRange "[2.2]" |> shouldEqual (Exactly "2.2")
    Nuget.parseVersionRange "[1.2]" |> shouldEqual (Exactly "1.2")