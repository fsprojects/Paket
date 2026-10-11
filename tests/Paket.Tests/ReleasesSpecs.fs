module Paket.ReleasesSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``releaseVersion drops the commit of the informational version``() =
    Releases.releaseVersion "11.0.1+5b8bbfdfac28a031d511c40aa3789fab48634013"
    |> string
    |> shouldEqual "11.0.1"

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

let private releaseToDownload running published =
    Releases.releaseToDownload (SemVer.Parse running) (published |> List.map SemVer.Parse)
    |> string

[<Test>]
let ``releaseToDownload takes the latest release before one that isn't published yet``() =
    releaseToDownload "11.0.1" ["10.3.1"; "11.0.0"]
    |> shouldEqual "11.0.0"

[<Test>]
let ``releaseToDownload takes the running release once published``() =
    releaseToDownload "11.0.1" ["11.0.0"; "11.0.1"; "12.0.0"]
    |> shouldEqual "11.0.1"

[<Test>]
let ``releaseToDownload takes the latest Paket 11 for a build without a version``() =
    releaseToDownload "1.0.0" ["1.0.0"; "11.0.0"; "12.0.0"]
    |> shouldEqual "11.0.0"

[<Test>]
let ``releaseToDownload takes a prerelease when Paket runs one``() =
    releaseToDownload "11.1.0-alpha001" ["11.0.1"; "11.1.0-alpha001"; "12.0.0-alpha001"]
    |> shouldEqual "11.1.0-alpha001"

[<Test>]
let ``releaseToDownload skips prereleases when Paket runs a release``() =
    releaseToDownload "11.0.1" ["11.0.0"; "11.0.1-alpha001"]
    |> shouldEqual "11.0.0"

[<Test>]
let ``releaseToDownload keeps the running version when no version is listed``() =
    releaseToDownload "11.0.1" []
    |> shouldEqual "11.0.1"

[<Test>]
let ``releaseToDownload falls back to Paket 11 for a build without a version when no version is listed``() =
    releaseToDownload "1.0.0" []
    |> shouldEqual "11.0.0"
