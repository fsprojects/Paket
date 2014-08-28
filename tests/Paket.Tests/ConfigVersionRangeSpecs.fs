module Paket.ConfigVersionRangeSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``can detect minimum version``() = 
    ConfigHelpers.parseVersionRange "2.2" |> shouldEqual (AtLeast "2.2")
    ConfigHelpers.parseVersionRange "1.2" |> shouldEqual (AtLeast "1.2")

[<Test>]
let ``can detect specific version``() = 
    ConfigHelpers.parseVersionRange "= 2.2" |> shouldEqual (Exactly "2.2")
    ConfigHelpers.parseVersionRange "= 1.2" |> shouldEqual (Exactly "1.2")

[<Test>]
let ``can detect ordinary Between``() = 
    ConfigHelpers.parseVersionRange "~> 2.2" |> shouldEqual (Between("2.2","3.0"))
    ConfigHelpers.parseVersionRange "~> 1.2" |> shouldEqual (Between("1.2","2.0"))