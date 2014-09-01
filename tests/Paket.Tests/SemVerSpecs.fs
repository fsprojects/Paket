module Paket.SemVerSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``can parse semver strings and print the result``() = 
    (SemVer.parse "0.1.2").ToString() |> shouldEqual "0.1.2"
    (SemVer.parse "1.0.2").ToString() |> shouldEqual "1.0.2"
    (SemVer.parse "1.0").ToString() |> shouldEqual "1.0"
    (SemVer.parse "1.0.0-alpha.1").ToString() |> shouldEqual "1.0.0-alpha.1"
    (SemVer.parse "1.0.0-beta.2").ToString() |> shouldEqual "1.0.0-beta.2"
    (SemVer.parse "1.0.0-alpha.beta").ToString() |> shouldEqual "1.0.0-alpha.beta"
    (SemVer.parse "1.0.0-rc.1").ToString() |> shouldEqual "1.0.0-rc.1"
    (SemVer.parse "1.2.3-foo").ToString() |> shouldEqual "1.2.3-foo"


[<Test>]
let ``can parse semver strings``() = 
    let semVer = SemVer.parse("1.2.3-alpha.beta")
    semVer.Major |> shouldEqual 1
    semVer.Minor |> shouldEqual 2
    semVer.Patch |> shouldEqual 3
    semVer.PreRelease |> shouldEqual (Some { Origin = "alpha"
                                             Name = "alpha"
                                             Number = None })
    semVer.Build |> shouldEqual "beta"

[<Test>]
let ``can compare semvers``() =
    (SemVer.parse "1.2.3") |> shouldEqual (SemVer.parse "1.2.3")
    (SemVer.parse "1.0.0-rc.3") |> shouldBeGreaterThan (SemVer.parse "1.0.0-rc.1")
    (SemVer.parse "1.0.0-alpha.3") |> shouldBeGreaterThan (SemVer.parse "1.0.0-alpha.2")
    (SemVer.parse "1.2.3-alpha.3") |> shouldEqual (SemVer.parse "1.2.3-alpha.3")
    (SemVer.parse "1.0.0-alpha") |> shouldBeSmallerThan (SemVer.parse "1.0.0-alpha.1")
    (SemVer.parse "1.0.0-alpha.1") |> shouldBeSmallerThan (SemVer.parse "1.0.0-alpha.beta")
    (SemVer.parse "1.0.0-alpha.beta") |> shouldBeSmallerThan (SemVer.parse "1.0.0-beta")
    (SemVer.parse "1.0.0-beta") |> shouldBeSmallerThan (SemVer.parse "1.0.0-beta.2")
    (SemVer.parse "1.0.0-beta.2") |> shouldBeSmallerThan (SemVer.parse "1.0.0-beta.11")
    (SemVer.parse "1.0.0-beta.11") |> shouldBeSmallerThan (SemVer.parse "1.0.0-rc.1")
    (SemVer.parse "1.0.0-rc.1") |> shouldBeSmallerThan (SemVer.parse "1.0.0")
    (SemVer.parse "2.3.4") |> shouldBeGreaterThan (SemVer.parse "2.3.4-alpha")
    (SemVer.parse "1.5.0-rc.1") |> shouldBeGreaterThan (SemVer.parse "1.5.0-beta.2")
    (SemVer.parse "2.3.4-alpha2") |> shouldBeGreaterThan (SemVer.parse "2.3.4-alpha")
    (SemVer.parse "2.3.4-alpha003") |> shouldBeGreaterThan (SemVer.parse "2.3.4-alpha2")
    (SemVer.parse "2.3.4-rc") |> shouldBeGreaterThan (SemVer.parse "2.3.4-beta2")