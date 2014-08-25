module Paket.FilterVersionSpecs

open Paket
open Paket.DependencyGraph
open NUnit.Framework
open FsUnit

[<Test>]
let ``can check if in range``() = 
    (VersionRange.AtLeast "2.2").IsInRange "2.2" |> shouldEqual true
    (VersionRange.AtLeast "2.2").IsInRange "1.2" |> shouldEqual false
    (VersionRange.AtLeast "2.2").IsInRange "2.3" |> shouldEqual true
    (VersionRange.Exactly "2.2").IsInRange "2.2" |> shouldEqual true
    (VersionRange.Exactly "2.2").IsInRange "2.3" |> shouldEqual false
    (VersionRange.Exactly "2.2").IsInRange "2.0" |> shouldEqual false
    (VersionRange.Between("2.2","3.0")).IsInRange "2.2" |> shouldEqual true
    (VersionRange.Between("2.2","3.0")).IsInRange "2.3" |> shouldEqual true
    (VersionRange.Between("2.2","3.0")).IsInRange "2.0" |> shouldEqual false
    (VersionRange.Between("2.2","3.0")).IsInRange "3.0" |> shouldEqual false