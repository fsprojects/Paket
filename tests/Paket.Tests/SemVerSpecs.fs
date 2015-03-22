module Paket.SemVerSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``can parse semver strings and print the result``() = 
    (SemVer.Parse "0.1.2").ToString() |> shouldEqual "0.1.2"
    (SemVer.Parse "1.0.2").ToString() |> shouldEqual "1.0.2"
    (SemVer.Parse "1.0").ToString() |> shouldEqual "1.0"
    (SemVer.Parse "1.0.0-alpha.1").ToString() |> shouldEqual "1.0.0-alpha.1"
    (SemVer.Parse "1.0.0-beta.2").ToString() |> shouldEqual "1.0.0-beta.2"
    (SemVer.Parse "1.0.0-alpha.beta").ToString() |> shouldEqual "1.0.0-alpha.beta"
    (SemVer.Parse "1.0.0-rc.1").ToString() |> shouldEqual "1.0.0-rc.1"
    (SemVer.Parse "1.2.3-foo").ToString() |> shouldEqual "1.2.3-foo"
    (SemVer.Parse "6.0.1302.0-Preview").PreRelease |> shouldEqual (PreRelease.TryParse "Preview")
    (SemVer.Parse "1.2.3").ToString() |> shouldEqual "1.2.3"
    (SemVer.Parse "1.2.3.0").ToString() |> shouldEqual "1.2.3.0"
    (SemVer.Parse "1.2.3.0").Patch |> shouldEqual 3
    (SemVer.Parse "1.2.3").Patch |> shouldEqual 3
    (SemVer.Parse "1.2.3.0").Build |> shouldEqual "0"
    (SemVer.Parse "1.2.3").Build |> shouldEqual "0"
    (SemVer.Parse "3.1.1.1").Build |> shouldEqual "1"
    (SemVer.Parse "1.0.0-rc.3").PreReleaseBuild |> shouldEqual "3"
    (SemVer.Parse "1.0.0-rc.1").PreReleaseBuild |> shouldEqual "1"

[<Test>]
let ``can parse semver strings``() = 
    let semVer = SemVer.Parse("1.2.3-alpha.beta")
    semVer.Major |> shouldEqual 1
    semVer.Minor |> shouldEqual 2
    semVer.Patch |> shouldEqual 3
    semVer.PreRelease |> shouldEqual (Some { Origin = "alpha"
                                             Name = "alpha"
                                             Number = None })
    semVer.PreReleaseBuild |> shouldEqual "beta"

[<Test>]
let ``can compare semvers``() =
    (SemVer.Parse "1.2.3") |> shouldEqual (SemVer.Parse "1.2.3")
    (SemVer.Parse "1.0.0-rc.3") |> shouldBeGreaterThan (SemVer.Parse "1.0.0-rc.1")
    (SemVer.Parse "1.0.0-alpha.3") |> shouldBeGreaterThan (SemVer.Parse "1.0.0-alpha.2")
    (SemVer.Parse "1.2.3-alpha.3") |> shouldEqual (SemVer.Parse "1.2.3-alpha.3")
    (SemVer.Parse "1.0.0-alpha") |> shouldBeSmallerThan (SemVer.Parse "1.0.0-alpha.1")
    (SemVer.Parse "1.0.0-alpha.1") |> shouldBeSmallerThan (SemVer.Parse "1.0.0-alpha.beta")
    (SemVer.Parse "1.0.0-alpha.beta") |> shouldBeSmallerThan (SemVer.Parse "1.0.0-beta")
    (SemVer.Parse "1.0.0-beta") |> shouldBeSmallerThan (SemVer.Parse "1.0.0-beta.2")
    (SemVer.Parse "1.0.0-beta.2") |> shouldBeSmallerThan (SemVer.Parse "1.0.0-beta.11")
    (SemVer.Parse "1.0.0-beta.11") |> shouldBeSmallerThan (SemVer.Parse "1.0.0-rc.1")
    (SemVer.Parse "1.0.0-rc.1") |> shouldBeSmallerThan (SemVer.Parse "1.0.0")
    (SemVer.Parse "2.3.4") |> shouldBeGreaterThan (SemVer.Parse "2.3.4-alpha")
    (SemVer.Parse "1.5.0-rc.1") |> shouldBeGreaterThan (SemVer.Parse "1.5.0-beta.2")
    (SemVer.Parse "2.3.4-alpha2") |> shouldBeGreaterThan (SemVer.Parse "2.3.4-alpha")
    (SemVer.Parse "2.3.4-alpha003") |> shouldBeGreaterThan (SemVer.Parse "2.3.4-alpha2")
    (SemVer.Parse "2.3.4-rc") |> shouldBeGreaterThan (SemVer.Parse "2.3.4-beta2")

[<Test>]
let ``can compare 4-parts semvers``() =
    (SemVer.Parse "1.0.0.2420") |> shouldBeGreaterThan (SemVer.Parse "1.0")

[<Test>]
let ``trailing zeros are equal``() =
    (SemVer.Parse "1.0.0") |> shouldEqual (SemVer.Parse "1.0")
    (SemVer.Parse "1.0.0") |> shouldEqual (SemVer.Parse "1")
    (SemVer.Parse "1.2.3.0") |> shouldEqual (SemVer.Parse "1.2.3")
    (SemVer.Parse "1.2.0") |> shouldEqual (SemVer.Parse "1.2")

[<Test>]
let ``can parse strange versions``() = 
    (SemVer.Parse "2.1-alpha10").ToString() |> shouldEqual "2.1-alpha10"
    (SemVer.Parse "2-alpha100").ToString() |> shouldEqual "2-alpha100"
    (SemVer.Parse "0.5.0-ci1411131947").ToString() |> shouldEqual "0.5.0-ci1411131947"

[<Test>]
let ``can parse FSharp.Data versions``() = 
    (SemVer.Parse "2.1.0-beta3").ToString() |> shouldEqual "2.1.0-beta3"
    
[<Test>]
let ``can normalize versions``() =
    (SemVer.Parse "2.3") |> shouldEqual (SemVer.Parse "2.3.0")
    (SemVer.Parse "2.3").Normalize() |> shouldEqual ((SemVer.Parse "2.3.0").ToString())
    (SemVer.Parse "3.1.1.1").Normalize() |> shouldEqual "3.1.1.1"
    (SemVer.Parse "3.1.1.1").Normalize() |> shouldEqual ((SemVer.Parse "3.1.1.1").ToString())
    (SemVer.Parse "1.2.3").Normalize() |> shouldEqual ((SemVer.Parse "1.2.3").ToString())
    (SemVer.Parse "1.0.0-rc.3").Normalize() |> shouldEqual ((SemVer.Parse "1.0.0-rc.3").ToString())
    (SemVer.Parse "1.2.3-alpha.3").Normalize() |> shouldEqual ((SemVer.Parse "1.2.3-alpha.3").ToString())
    (SemVer.Parse "1.0.0-alpha").Normalize() |> shouldEqual ((SemVer.Parse "1.0.0-alpha").ToString())
    (SemVer.Parse "1.0.0-alpha.1").Normalize() |> shouldEqual ((SemVer.Parse "1.0.0-alpha.1").ToString())

[<Test>]
let ``can normalize build zeros``() =
    (SemVer.Parse "2.0.30506.0").Normalize() |> shouldEqual ((SemVer.Parse "2.0.30506").ToString())


[<Test>]
let ``can normalize build zeros in prerelease``() =
    (SemVer.Parse "6.0.1302.0-Preview").Normalize() |> shouldEqual "6.0.1302-Preview"

[<Test>]
let ``can normalize CI versions in prerelease``() =
    (SemVer.Parse "0.5.0-ci1411131947").Normalize() |> shouldEqual "0.5.0-ci1411131947"


[<Test>]
let ``should parse very large prerelease numbers (aka timestamps)``() =
    (SemVer.Parse "0.22.0-pre20150223185624").Normalize() |> shouldEqual "0.22.0-pre20150223185624"


[<Test>]
let ``version core elements must be non-negative (SemVer 2.0.0/2)`` () =
    shouldFail<exn>(fun () -> SemVer.Parse "1.1.-1" |> ignore)
    shouldFail<exn>(fun () -> SemVer.Parse "1.-1.1" |> ignore)
    shouldFail<exn>(fun () -> SemVer.Parse "-1.1.1" |> ignore)
    

[<Test>]
let ``version core elements should accept leading zeroes (NuGet compat)`` () =
    SemVer.Parse "01.1.1" |> ignore
    SemVer.Parse "1.01.1" |> ignore
    SemVer.Parse "1.1.01" |> ignore

[<Test>]
let ``pre-release identifiers must not contain invalid characters (SemVer 2.0.0/9)`` () =
    shouldFail<exn>(fun () -> SemVer.Parse "1.0.0-a.!.c" |> ignore)

[<Test>]
let ``pre-release identifiers must not be empty (SemVer 2.0.0/9)`` () =
    shouldFail<exn>(fun () -> SemVer.Parse "1.0.0-a..c" |> ignore)

// Precedence

[<Test>]
let ``core version exhibits correct (numeric) precedence (SemVer 2.0.0/11)`` () =
    (SemVer.Parse "2.1.1") |> shouldBeGreaterThan (SemVer.Parse "2.1.0")
    (SemVer.Parse "2.1.0") |> shouldBeGreaterThan (SemVer.Parse "2.0.0")
    (SemVer.Parse "2.0.0") |> shouldBeGreaterThan (SemVer.Parse "1.0.0")

[<Test>]
let ``pre-release versions have lower precedence (SemVer 2.0.0/9,11)`` () =
    (SemVer.Parse "1.0.0") |> shouldBeGreaterThan (SemVer.Parse "1.0.0-alpha")

[<Test>]
let ``larger pre-release identifiers have higher precedence (SemVer 2.0.0/11)`` () =
    (SemVer.Parse "1.0.0-alpha") |> shouldBeSmallerThan (SemVer.Parse "1.0.0-alpha.1")

[<Test>]
let ``alpha pre-release identifiers have higher precedence than numeric (SemVer 2.0.0/11)`` () =
    (SemVer.Parse "1.0.0-alpha.1") |> shouldBeSmallerThan (SemVer.Parse "1.0.0-alpha.beta")

[<Test>]
let ``earlier pre-release identifiers have higher precedence (SemVer 2.0.0/11)`` () =
    (SemVer.Parse "1.0.0-alpha.beta") |> shouldBeSmallerThan (SemVer.Parse "1.0.0-beta")

[<Test>]
let ``numeric pre-release identifiers exhibit correct (numeric) precedence (SemVer 2.0.0/11)`` () =
    (SemVer.Parse "1.0.0-beta.2") |> shouldBeSmallerThan (SemVer.Parse "1.0.0-beta.11")

[<Test>]
let ``should accept SemVer2 prereleases`` () =
    let semVer = SemVer.Parse("1.0.0+foobar")
    semVer.Major |> shouldEqual 1
    semVer.Minor |> shouldEqual 0
    semVer.Patch |> shouldEqual 0
    semVer.PreRelease |> shouldEqual (Some { Origin = "foobar"
                                             Name = "foobar"
                                             Number = None })