module Paket.VersionComparisionSpecs

open Paket
open NUnit.Framework
open Paket.ConfigDSL
open FsUnit

[<Test>]
let ``should take max between two MinVersions``() = 
    Shrink(Version.AtLeast "2.2", Version.AtLeast "3.3") |> shouldEqual (Version.AtLeast "3.3")
    Shrink(Version.AtLeast "1.1", Version.AtLeast "0.9") |> shouldEqual (Version.AtLeast "1.1")

[<Test>]
let ``should shrink MinVersion by SpecificVersion``() = 
    Shrink(Version.AtLeast "2.2", Version.Exactly "3.3") |> shouldEqual (Version.Exactly "3.3")
    Shrink(Version.Exactly "1.1", Version.AtLeast "0.9") |> shouldEqual (Version.Exactly "1.1")

[<Test>]
let ``should shrink VersionRange by SpecificVersion``() = 
    Shrink(Version.Between("2.2", "4.4"), Version.Exactly "3.3") |> shouldEqual (Version.Exactly "3.3")
    Shrink(Version.Exactly "1.1", Version.Between("0.9", "2.2")) |> shouldEqual (Version.Exactly "1.1")

[<Test>]
let ``should shrink VersionRange by VersionRange``() = 
    Shrink(Version.Between("2.2", "4.4"), Version.Between("2.2", "4.4")) |> shouldEqual (Version.Between("2.2", "4.4"))
    Shrink(Version.Between("2.2", "4.4"), Version.Between("3.3", "4.4")) |> shouldEqual (Version.Between("3.3", "4.4"))
    Shrink(Version.Between("2.2", "4.4"), Version.Between("2.2", "3.3")) |> shouldEqual (Version.Between("2.2", "3.3"))
    Shrink(Version.Between("2.2", "4.4"), Version.Between("1.1", "3.3")) |> shouldEqual (Version.Between("2.2", "3.3"))
    Shrink(Version.Between("2.2", "4.4"), Version.Between("1.1", "5.5")) |> shouldEqual (Version.Between("2.2", "4.4"))
    Shrink(Version.Between("1.1", "5.5"), Version.Between("2.2", "4.4")) |> shouldEqual (Version.Between("2.2", "4.4"))