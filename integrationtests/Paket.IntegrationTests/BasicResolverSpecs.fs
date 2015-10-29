module Paket.IntegrationTests.BasicResolverSpecs

open Fake
open Paket
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Paket.Domain

[<Test>]
let ``#49 windsor should resolve correctly``() =
    let lockFile = update "i000049-resolve-windsor-correctly"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Windsor"].Version
    |> shouldEqual (SemVer.Parse "3.2.1")

[<Test>]
let ``#51 should resolve with pessimistic strategy correctly``() =
    let lockFile = update "i000051-resolve-with-pessimistic-strategy"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Windsor-log4net"].Version
    |> shouldEqual (SemVer.Parse "3.2.0.1")

[<Test>]
let ``#55 should resolve with pessimistic strategy correctly``() =
    let lockFile = update "i000055-resolve-with-pessimistic-strategy"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Windsor"].Version
    |> shouldEqual (SemVer.Parse "3.2.1")

[<Test>]
let ``#71 should ignore trailing zero during resolve``() =
    let lockFile = update "i000071-ignore-trailing-zero-during-resolve"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Version
    |> shouldEqual (SemVer.Parse "6.0.5.0")

[<Test>]
let ``#83 should resolve jquery``() =
    let lockFile = update "i000083-resolve-jquery"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "jQuery"].Version
    |> shouldBeGreaterThan (SemVer.Parse "1.9")
