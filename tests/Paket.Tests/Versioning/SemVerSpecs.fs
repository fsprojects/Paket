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
    (SemVer.Parse "1.2.3.0").Patch |> shouldEqual 3u
    (SemVer.Parse "1.2.3").Patch |> shouldEqual 3u
    (SemVer.Parse "1.2.3.0").Build |> shouldEqual 0I
    (SemVer.Parse "1.2.3").Build |> shouldEqual 0I
    (SemVer.Parse "3.1.1.1").Build |> shouldEqual 1I
    (SemVer.Parse "1.0.0-rc.3").PreRelease.Value.Values.[1] |> shouldEqual (Numeric (bigint 3))
    (SemVer.Parse "1.0.0-rc.1").PreRelease.Value.Values.[1] |> shouldEqual (Numeric (bigint 1))
    (SemVer.Parse "1.2.3-4").Build |> shouldEqual 0I
    (SemVer.Parse "1.2.3-4").PreRelease.Value.Values.[0] |> shouldEqual (Numeric (bigint 4))
    (SemVer.Parse "1.2.3-4.item78.9").Build |> shouldEqual 0I
    (SemVer.Parse "1.2.3-4.item78.9").PreRelease.Value.Name |> shouldEqual "item78"

[<Test>]
let ``can parse semver strings``() = 
    let semVer = SemVer.Parse("1.2.3-alpha.beta")
    semVer.Major |> shouldEqual 1u
    semVer.Minor |> shouldEqual 2u
    semVer.Patch |> shouldEqual 3u
    semVer.PreRelease |> shouldEqual (Some { Origin = "alpha.beta"
                                             Name = "alpha"
                                             Values = [ PreReleaseSegment.AlphaNumeric "alpha"; PreReleaseSegment.AlphaNumeric "beta" ] })
                                             
[<TestCase("1.2.3-0", 1u, 2u, 3u, 0u, "")>]
[<TestCase("1.2.3-4.5", 1u, 2u, 3u, 0u, "")>]
[<TestCase("1.2.3-alpha045", 1u, 2u, 3u, 0u, "alpha")>]
[<TestCase("1.2.3.alpha045", 1u, 2u, 3u, 0u, "alpha")>]
[<TestCase("1.2.3-alpha.45", 1u, 2u, 3u, 0u, "alpha")>]
[<TestCase("1.2.3.alpha.45", 1u, 2u, 3u, 0u, "alpha")>]
[<TestCase("1.2.3-alpha045.67", 1u, 2u, 3u, 0u, "alpha045")>]
[<TestCase("1.2.3.alpha045.67", 1u, 2u, 3u, 0u, "alpha045")>]
[<TestCase("1.2.3.4-alpha045", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4.alpha045", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4-alpha.45", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4.alpha.45", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4-alpha-45", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4.alpha-45", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4-alpha045-67", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4.alpha045-67", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4-alpha045.67", 1u, 2u, 3u, 4u, "alpha045")>]
[<TestCase("1.2.3.4.alpha045.67", 1u, 2u, 3u, 4u, "alpha045")>]
[<TestCase("1.2.3.4-alpha.45-67", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4.alpha.45-67", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4-alpha-45.67", 1u, 2u, 3u, 4u, "alpha-45")>]
[<TestCase("1.2.3.4.alpha-45.67", 1u, 2u, 3u, 4u, "alpha-45")>]
// 5-segment semver-like, "5" is the 1st prerelease segment
[<TestCase("1.2.3.4-5", 1u, 2u, 3u, 4u, "")>] // build & unnamed
[<TestCase("1.2.3.4.5", 1u, 2u, 3u, 4u, "")>] // unnamed prerelease
[<TestCase("1.2.3.4.5-alpha", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4.5.alpha", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4.5-alpha-45", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4.5.alpha-45", 1u, 2u, 3u, 4u, "alpha-45")>] 
[<TestCase("1.2.3.4.5.alpha.45", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4.5-alpha-45-67", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4.5.alpha.45-67", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4.5.alpha.45.67", 1u, 2u, 3u, 4u, "alpha")>]
[<TestCase("1.2.3.4.5.alpha-45.67", 1u, 2u, 3u, 4u, "alpha-45")>]
[<TestCase("1.2.3.4.5-alpha-45.67", 1u, 2u, 3u, 4u, "5-alpha-45")>]
[<TestCase("1.2.3.4.5.alpha-45-67", 1u, 2u, 3u, 4u, "alpha-45-67")>]
let ``can parse semver2 complex version strings`` str major minor patch (build:uint32) prerelease =
    let semVer = SemVer.Parse(str)
    semVer.Major |> shouldEqual major
    semVer.Minor |> shouldEqual minor
    semVer.Patch |> shouldEqual patch
    semVer.Build |> shouldEqual (bigint(build))
    match semVer.PreRelease with
    | Some pre -> pre.Name |> shouldEqual prerelease
    | None -> Assert.Fail "PreRelease was expected"

[<Test>]
let ``can parse MBrace semver strings``() = 
    let semVer = SemVer.Parse("0.9.8-alpha")
    semVer.Major |> shouldEqual 0u
    semVer.Minor |> shouldEqual 9u
    semVer.Patch |> shouldEqual 8u
    semVer.PreRelease |> shouldEqual (Some { Origin = "alpha"
                                             Name = "alpha"
                                             Values = [ PreReleaseSegment.AlphaNumeric "alpha" ] })

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
    (SemVer.Parse "2.3.4-alpha003") |> shouldBeSmallerThan (SemVer.Parse "2.3.4-alpha2") // lexical sort on the full prerelease string
    (SemVer.Parse "2.3.4-alpha.003") |> shouldBeGreaterThan (SemVer.Parse "2.3.4-alpha.2") // numeric sort on the second prerelease segment
    (SemVer.Parse "2.3.4-rc") |> shouldBeGreaterThan (SemVer.Parse "2.3.4-beta2")
    (SemVer.Parse "1.0.12-build0025") |> shouldBeGreaterThan (SemVer.Parse "1.0.11")
    (SemVer.Parse "1.2.3-beta.11") |> shouldBeGreaterThan (SemVer.Parse "1.2.3-10")
    (SemVer.Parse "1.2.3-beta01") |> shouldBeGreaterThan (SemVer.Parse "1.2.3-1")
    (SemVer.Parse "1.2.3-beta.1") |> shouldBeGreaterThan (SemVer.Parse "1.2.3-1")
    (SemVer.Parse "1.2.3-beta.1") |> shouldBeGreaterThan (SemVer.Parse "1.2.3-beta")
    (SemVer.Parse "1.2.3-beta.2") |> shouldBeGreaterThan (SemVer.Parse "1.2.3-beta.1")
    (SemVer.Parse "1.2.3-beta.2.1") |> shouldBeGreaterThan (SemVer.Parse "1.2.3-beta.2")
    (SemVer.Parse "1.2.3-beta.zetta") |> shouldBeGreaterThan (SemVer.Parse "1.2.3-beta")
    (SemVer.Parse "1.2.3-beta.zetta") |> shouldBeGreaterThan (SemVer.Parse "1.2.3-beta.1")
    (SemVer.Parse "1.2.3-beta.2.zetta") |> shouldBeGreaterThan (SemVer.Parse "1.2.3-beta.1.zetta")
    (SemVer.Parse "1.2.3-beta.2.zetta") |> shouldBeGreaterThan (SemVer.Parse "1.2.3-beta.1.alpha")
    (SemVer.Parse "1.2.3-beta.1.zetta") |> shouldBeGreaterThan (SemVer.Parse "1.2.3-beta.1.alpha")    
    
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
    (SemVer.Parse "3.0.0-alpha-0008").Normalize() |> shouldEqual "3.0.0-alpha-0008"
    (SemVer.Parse "3.0.0-alpha123ci-0008").Normalize() |> shouldEqual "3.0.0-alpha123ci-0008"
    
[<TestCase("3.0.0--")>][<TestCase("3.0.0---")>]
[<TestCase("3.0.0-.-")>][<TestCase("3.0.0-1-")>]
[<TestCase("3.0.0-rc-")>][<TestCase("3.0.0-rc1")>]
[<TestCase("3.0.0-rc-1")>][<TestCase("3.0.0-rc1-")>]
[<TestCase("3.0.0-rc.1")>][<TestCase("3.0.0-1.rc")>]
[<TestCase("3.0.0-rc.1-")>][<TestCase("3.0.0-1.rc-")>]
[<TestCase("3.0.0-rc.-1-")>][<TestCase("3.0.0-1-.rc-")>]
[<TestCase("3.0.0-rc.-1.1")>][<TestCase("3.0.0-1-.rc.1")>]
[<TestCase("3.0.0-rc.-1.1-")>][<TestCase("3.0.0-1-.rc.-1")>]
let ``can normalize semver prereleases`` version =
    (SemVer.Parse version).Normalize() |> shouldEqual version

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
let ``should parse paket prerelease versions``() =
    let v = SemVer.Parse "1.2.3-alpha002"
    
    v.Major |> shouldEqual 1u
    v.Minor |> shouldEqual 2u
    v.Patch |> shouldEqual 3u
    v.PreRelease.Value.ToString() |> shouldEqual "alpha002"
    v.PreRelease.Value.Name |> shouldEqual "alpha"


[<Test>]
let ``should parse CoreClr prerelease versions``() =
    let v = SemVer.Parse "1.2.3-beta-22819"

    v.Major |> shouldEqual 1u
    v.Minor |> shouldEqual 2u
    v.Patch |> shouldEqual 3u
    v.PreRelease.Value.ToString() |> shouldEqual "beta-22819"
    v.PreRelease.Value.Name |> shouldEqual "beta"

[<Test>]
let ``should compare CoreClr prerelease versions``() =
    (SemVer.Parse "1.2.3-beta-22819") |> shouldBeGreaterThan (SemVer.Parse "1.2.3-beta-22818")
    (SemVer.Parse "1.2.3-beta-22817") |> shouldBeSmallerThan (SemVer.Parse "1.2.3-beta-22818")

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
let ``newer beta versions have higher precedence`` () =
    (SemVer.Parse "1.0-beta") |> shouldBeSmallerThan (SemVer.Parse "1.1-beta")

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
    semVer.Major |> shouldEqual 1u
    semVer.Minor |> shouldEqual 0u
    semVer.Patch |> shouldEqual 0u
    semVer.BuildMetaData |> shouldEqual "foobar"
    semVer.PreRelease |> shouldEqual None

[<Test>]
let ``should accept version with leading zero`` () =
    SemVer.Parse("1.0.071.9556").ToString() |> shouldEqual "1.0.071.9556"

[<Test>]
let ``should accept version with minus in prerelease`` () =
    SemVer.Parse("3.0.0-alpha-0008").ToString() |> shouldEqual "3.0.0-alpha-0008"
    (SemVer.Parse "3.0.0-alpha-0008") |> shouldBeSmallerThan (SemVer.Parse "3.0.0-alpha-0009")
    SemVer.Parse("3.0.0.alpha-0008").ToString() |> shouldEqual "3.0.0.alpha-0008"
