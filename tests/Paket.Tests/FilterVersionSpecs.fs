module Paket.FilterVersionSpecs

open Paket
open NUnit.Framework
open FsUnit

let isInRange (versionRange:VersionRange) semVer =
    versionRange.IsInRange (SemVer.parse semVer)

[<Test>]
let ``can check if in range for Specific``() =
    "2.2" |> isInRange (VersionRange.Specific (SemVer.parse "2.2")) |> shouldEqual true
    "2.4" |> isInRange (VersionRange.Specific (SemVer.parse "2.2")) |> shouldEqual false
    "2.2" |> isInRange (VersionRange.Specific (SemVer.parse "2.4")) |> shouldEqual false
    
[<Test>]
let ``can check if in range for Minimum``() =
    "2.1" |> isInRange (VersionRange.Minimum (SemVer.parse "2.2")) |> shouldEqual false
    "2.2" |> isInRange (VersionRange.Minimum (SemVer.parse "2.2")) |> shouldEqual true
    "3.0" |> isInRange (VersionRange.Minimum (SemVer.parse "2.2")) |> shouldEqual true
    
[<Test>]
let ``can check if in range for GreaterThan``() =
    "2.1" |> isInRange (VersionRange.GreaterThan (SemVer.parse "2.2")) |> shouldEqual false
    "2.2" |> isInRange (VersionRange.GreaterThan (SemVer.parse "2.2")) |> shouldEqual false
    "3.0" |> isInRange (VersionRange.GreaterThan (SemVer.parse "2.2")) |> shouldEqual true

[<Test>]
let ``can check if in range for Maximum``() =
    "2.0" |> isInRange (VersionRange.Maximum (SemVer.parse "2.2")) |> shouldEqual true
    "2.2" |> isInRange (VersionRange.Maximum (SemVer.parse "2.2")) |> shouldEqual true
    "3.0" |> isInRange (VersionRange.Maximum (SemVer.parse "2.2")) |> shouldEqual false

[<Test>]
let ``can check if in range for LessThan``() =
    "2.0" |> isInRange (VersionRange.LessThan (SemVer.parse "2.2")) |> shouldEqual true
    "2.2" |> isInRange (VersionRange.LessThan (SemVer.parse "2.2")) |> shouldEqual false
    "3.0" |> isInRange (VersionRange.LessThan (SemVer.parse "2.2")) |> shouldEqual false
    
[<Test>]
let ``can check if in range for Range``() =
    "2.1" |> isInRange (VersionRange.Range (Open, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Open)) |> shouldEqual false
    "2.2" |> isInRange (VersionRange.Range (Open, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Open)) |> shouldEqual false
    "2.5" |> isInRange (VersionRange.Range (Open, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Open)) |> shouldEqual true
    "3.0" |> isInRange (VersionRange.Range (Open, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Open)) |> shouldEqual false
    "3.2" |> isInRange (VersionRange.Range (Open, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Open)) |> shouldEqual false

    "2.1" |> isInRange (VersionRange.Range (Open, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Closed)) |> shouldEqual false
    "2.2" |> isInRange (VersionRange.Range (Open, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Closed)) |> shouldEqual false
    "2.5" |> isInRange (VersionRange.Range (Open, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Closed)) |> shouldEqual true
    "3.0" |> isInRange (VersionRange.Range (Open, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Closed)) |> shouldEqual true
    "3.2" |> isInRange (VersionRange.Range (Open, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Closed)) |> shouldEqual false

    "2.1" |> isInRange (VersionRange.Range (Closed, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Open)) |> shouldEqual false
    "2.2" |> isInRange (VersionRange.Range (Closed, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Open)) |> shouldEqual true
    "2.5" |> isInRange (VersionRange.Range (Closed, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Open)) |> shouldEqual true
    "3.0" |> isInRange (VersionRange.Range (Closed, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Open)) |> shouldEqual false
    "3.2" |> isInRange (VersionRange.Range (Closed, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Open)) |> shouldEqual false

    "2.1" |> isInRange (VersionRange.Range (Closed, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Closed)) |> shouldEqual false
    "2.2" |> isInRange (VersionRange.Range (Closed, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Closed)) |> shouldEqual true
    "2.5" |> isInRange (VersionRange.Range (Closed, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Closed)) |> shouldEqual true
    "3.0" |> isInRange (VersionRange.Range (Closed, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Closed)) |> shouldEqual true
    "3.2" |> isInRange (VersionRange.Range (Closed, (SemVer.parse "2.2"), (SemVer.parse "3.0"), Closed)) |> shouldEqual false

[<Test>]
let ``can check if in range for 4-parts range``() =    
    "1.0.0.3108" |> isInRange (DependenciesFileParser.parseVersionRange "1.0.0.3108") |> shouldEqual true
    "1.0.0.2420" |> isInRange (DependenciesFileParser.parseVersionRange "~> 1.0") |> shouldEqual true

[<Test>]
let ``can support trailing 0``() =    
    "1.2.3" |> isInRange (DependenciesFileParser.parseVersionRange "1.2.3.0") |> shouldEqual true    