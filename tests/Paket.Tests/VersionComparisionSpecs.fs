module Paket.VersionComparisionSpecs

open Paket
open Paket.DependencyGraph
open NUnit.Framework
open FsUnit

let AtLeast v = { DefiningPackage = ""; DefiningVersion = "";  ReferencedPackage = ""; ReferencedVersion = AtLeast v}
let Exactly v = { DefiningPackage = ""; DefiningVersion = "";  ReferencedPackage = ""; ReferencedVersion = Exactly v}
let Between(v1,v2) = { DefiningPackage = ""; DefiningVersion = "";  ReferencedPackage = ""; ReferencedVersion = Between(v1,v2)}
let Conflict(v1,v2) = { DefiningPackage = ""; DefiningVersion = "";  ReferencedPackage = ""; ReferencedVersion = Conflict(v1,v2)}

[<Test>]
let ``should take max between two MinVersions``() = 
    Shrink(AtLeast "2.2", AtLeast "3.3") |> shouldEqual (AtLeast "3.3")
    Shrink(AtLeast "1.1", AtLeast "0.9") |> shouldEqual (AtLeast "1.1")

[<Test>]
let ``should shrink MinVersion by SpecificVersion``() = 
    Shrink(AtLeast "2.2", Exactly "3.3") |> shouldEqual (Exactly "3.3")
    Shrink(Exactly "1.1", AtLeast "0.9") |> shouldEqual (Exactly "1.1")

[<Test>]
let ``should shrink VersionRange by SpecificVersion``() = 
    Shrink(Between("2.2", "4.4"), Exactly "3.3") |> shouldEqual (Exactly "3.3")
    Shrink(Exactly "1.1", Between("0.9", "2.2")) |> shouldEqual (Exactly "1.1")

[<Test>]
let ``should not shrink SpecificVersion``() =
    Shrink(Exactly "1.1", Exactly "1.1") |> shouldEqual (Exactly "1.1")

[<Test>]
let ``should shrink VersionRange by VersionRange``() = 
    Shrink(Between("2.2", "4.4"), Between("2.2", "4.4")) |> shouldEqual (Between("2.2", "4.4"))
    Shrink(Between("2.2", "4.4"), Between("3.3", "4.4")) |> shouldEqual (Between("3.3", "4.4"))
    Shrink(Between("2.2", "4.4"), Between("2.2", "3.3")) |> shouldEqual (Between("2.2", "3.3"))
    Shrink(Between("2.2", "4.4"), Between("1.1", "3.3")) |> shouldEqual (Between("2.2", "3.3"))
    Shrink(Between("2.2", "4.4"), Between("1.1", "5.5")) |> shouldEqual (Between("2.2", "4.4"))
    Shrink(Between("1.1", "5.5"), Between("2.2", "4.4")) |> shouldEqual (Between("2.2", "4.4"))


[<Test>]
let ``should detect conflict``() =
    Shrink(Exactly "1.1", Exactly "2.1") 
    |> shouldEqual (Conflict(VersionRange.Exactly "1.1", VersionRange.Exactly "2.1"))