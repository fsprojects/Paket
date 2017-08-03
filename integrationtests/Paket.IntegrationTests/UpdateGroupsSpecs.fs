module Paket.IntegrationTests.UpdateGroupSpecs
open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Pri.LongPath
open System.Diagnostics
open Paket
open Paket.Domain


[<Test>]
let ``#1711 update main group with correct source``() =
    update "i001711-wrong-source" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001711-wrong-source","paket.lock"))
    let p1 = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Test"]
    p1.Version |> shouldEqual (SemVer.Parse "0.0.1")
    p1.Source.Url |> shouldEqual "TestA"

[<Test>]
let ``#1711 update main group with correct source with multiple groups``() =
    update "i001711-wrong-groupsource" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001711-wrong-groupsource","paket.lock"))
    let p1 = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Test"]
    p1.Version |> shouldEqual (SemVer.Parse "0.0.1")
    p1.Source.Url |> shouldEqual "TestA"

    let p2 = lockFile.Groups.[GroupName "Cumulus.1.0"].Resolution.[PackageName "Test"]
    p2.Version |> shouldEqual (SemVer.Parse "0.0.1")
    p2.Source.Url |> shouldEqual "TestB"