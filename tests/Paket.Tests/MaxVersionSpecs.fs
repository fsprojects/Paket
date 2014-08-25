module Paket.FilterVersionSpecs

open Paket
open Paket.DependencyGraph
open NUnit.Framework
open FsUnit

let versions = ["1.0";"1.1";"2.0";"2.1";"2.2";"3.0";"3.1"]


[<Test>]
let ``can check if in range``() = 
    (VersionRange.AtLeast "2.2").IsInRange "2.2" |> shouldEqual true
    (VersionRange.AtLeast "2.2").IsInRange "1.2" |> shouldEqual false
    (VersionRange.AtLeast "2.2").IsInRange "2.3" |> shouldEqual true
    (VersionRange.SpecificVersion "2.2").IsInRange "2.2" |> shouldEqual true
    (VersionRange.SpecificVersion "2.2").IsInRange "2.3" |> shouldEqual false
    (VersionRange.SpecificVersion "2.2").IsInRange "2.0" |> shouldEqual false

    (VersionRange.Between("2.2","3.0")).IsInRange "2.2" |> shouldEqual true
    (VersionRange.Between("2.2","3.0")).IsInRange "2.3" |> shouldEqual true
    (VersionRange.Between("2.2","3.0")).IsInRange "2.0" |> shouldEqual false
    (VersionRange.Between("2.2","3.0")).IsInRange "3.0" |> shouldEqual false

[<Test>]
let ``should filter by range``() = 
    filterVersions(VersionRange.AtLeast "2.2") versions 
    |> shouldEqual ["2.2";"3.0";"3.1"]

    filterVersions(VersionRange.SpecificVersion "2.2") versions 
    |> shouldEqual ["2.2"]