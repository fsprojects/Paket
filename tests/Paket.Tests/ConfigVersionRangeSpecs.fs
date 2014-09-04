module Paket.ConfigVersionRangeSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``can detect minimum version``() = 
    ConfigHelpers.parseVersionRange ">= 2.2" |> shouldEqual (VersionRange.AtLeast "2.2")
    ConfigHelpers.parseVersionRange ">= 1.2" |> shouldEqual (VersionRange.AtLeast "1.2")

[<Test>]
let ``can detect specific version``() = 
    ConfigHelpers.parseVersionRange "2.2" |> shouldEqual (VersionRange.Exactly "2.2")
    ConfigHelpers.parseVersionRange "1.2" |> shouldEqual (VersionRange.Exactly "1.2")

    ConfigHelpers.parseVersionRange "= 2.2" |> shouldEqual (VersionRange.Exactly "2.2")
    ConfigHelpers.parseVersionRange "= 1.2" |> shouldEqual (VersionRange.Exactly "1.2")

[<Test>]
let ``can detect ordinary Between``() = 
    ConfigHelpers.parseVersionRange "~> 2.2" |> shouldEqual (VersionRange.Between("2.2","3.0"))
    ConfigHelpers.parseVersionRange "~> 1.2" |> shouldEqual (VersionRange.Between("1.2","2.0"))