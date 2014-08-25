module Paket.VersionComparisionSpecs

open Paket
open Paket.DependencyGraph
open NUnit.Framework
open FsUnit

[<Test>]
let ``should take max between two MinVersions``() = 
    Shrink(VersionRange.AtLeast "2.2", VersionRange.AtLeast "3.3") |> shouldEqual (VersionRange.AtLeast "3.3")
    Shrink(VersionRange.AtLeast "1.1", VersionRange.AtLeast "0.9") |> shouldEqual (VersionRange.AtLeast "1.1")

[<Test>]
let ``should shrink MinVersion by SpecificVersion``() = 
    Shrink(VersionRange.AtLeast "2.2", VersionRange.Exactly "3.3") |> shouldEqual (VersionRange.Exactly "3.3")
    Shrink(VersionRange.Exactly "1.1", VersionRange.AtLeast "0.9") |> shouldEqual (VersionRange.Exactly "1.1")

[<Test>]
let ``should shrink VersionRange by SpecificVersion``() = 
    Shrink(VersionRange.Between("2.2", "4.4"), VersionRange.Exactly "3.3") |> shouldEqual (VersionRange.Exactly "3.3")
    Shrink(VersionRange.Exactly "1.1", VersionRange.Between("0.9", "2.2")) |> shouldEqual (VersionRange.Exactly "1.1")

[<Test>]
let ``should not shrink SpecificVersion``() =
    Shrink(VersionRange.Exactly "1.1", VersionRange.Exactly "1.1") |> shouldEqual (VersionRange.Exactly "1.1")

[<Test>]
let ``should shrink VersionRange by VersionRange``() = 
    Shrink(VersionRange.Between("2.2", "4.4"), VersionRange.Between("2.2", "4.4")) |> shouldEqual (VersionRange.Between("2.2", "4.4"))
    Shrink(VersionRange.Between("2.2", "4.4"), VersionRange.Between("3.3", "4.4")) |> shouldEqual (VersionRange.Between("3.3", "4.4"))
    Shrink(VersionRange.Between("2.2", "4.4"), VersionRange.Between("2.2", "3.3")) |> shouldEqual (VersionRange.Between("2.2", "3.3"))
    Shrink(VersionRange.Between("2.2", "4.4"), VersionRange.Between("1.1", "3.3")) |> shouldEqual (VersionRange.Between("2.2", "3.3"))
    Shrink(VersionRange.Between("2.2", "4.4"), VersionRange.Between("1.1", "5.5")) |> shouldEqual (VersionRange.Between("2.2", "4.4"))
    Shrink(VersionRange.Between("1.1", "5.5"), VersionRange.Between("2.2", "4.4")) |> shouldEqual (VersionRange.Between("2.2", "4.4"))