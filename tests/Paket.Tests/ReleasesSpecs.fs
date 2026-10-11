module Paket.ReleasesSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``releaseVersion drops the commit of the informational version``() =
    Releases.releaseVersion "11.0.1+5b8bbfdfac28a031d511c40aa3789fab48634013"
    |> shouldEqual (SemVer.Parse "11.0.1")

[<Test>]
let ``releaseVersion keeps the prerelease``() =
    Releases.releaseVersion "11.1.0-alpha001+5b8bbfdfac28a031d511c40aa3789fab48634013"
    |> string
    |> shouldEqual "11.1.0-alpha001"

[<Test>]
let ``releaseVersion reads a version without build metadata``() =
    Releases.releaseVersion "11.0.0"
    |> string
    |> shouldEqual "11.0.0"
