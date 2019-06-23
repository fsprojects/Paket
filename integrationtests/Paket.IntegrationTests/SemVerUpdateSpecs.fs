module Paket.IntegrationTests.SemVerUpdateSpecs

open Fake
open Paket
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Paket.Domain

[<Test>]
let ``#1125 Should keep minor versions``() =
    use __ = paket "update --keep-minor" "i001125-update-and-keep-minor" |> fst
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001125-update-and-keep-minor","paket.lock"))
    let v = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FSharp.Formatting"].Version
    v.Major |> shouldEqual 2u
    v.Minor |> shouldEqual 14u
    v.Patch |> shouldBeGreaterThan 0u

[<Test>]
let ``#1125 Should keep major versions``() =
    use __ = paket "update --keep-major" "i001125-update-and-keep-minor" |> fst
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001125-update-and-keep-minor","paket.lock"))
    let v = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FSharp.Formatting"].Version
    v.Major |> shouldEqual 2u
    v.Minor |> shouldBeGreaterThan 4u

[<Test>]
let ``#1125 Should keep patch versions``() =
    use __ = paket "update --keep-patch" "i001125-update-and-keep-minor" |> fst
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001125-update-and-keep-minor","paket.lock"))
    let v = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FSharp.Formatting"].Version
    v.Major |> shouldEqual 2u
    v.Minor |> shouldEqual 14u
    v.Patch |> shouldEqual 2u
