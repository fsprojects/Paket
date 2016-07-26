module Paket.IntegrationTests.UpdateGroupSpecs
open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics
open Paket
open Paket.Domain


[<Test>]
let ``#1018 update main group``() =
    paket "update group Main" "i001018-legacy-groups-update" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001018-legacy-groups-update","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Version
    |> shouldBeGreaterThan (SemVer.Parse "6.0.3")
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "NUnit"].Version
    |> shouldBeGreaterThan (SemVer.Parse "2.6.1")
    lockFile.Groups.[GroupName "Legacy"].Resolution.[PackageName "Newtonsoft.Json"].Version
    |> shouldEqual (SemVer.Parse "5.0.2")

[<Test>]
let ``#1018 update group legacy``() =
    paket "update group leGacy" "i001018-legacy-groups-update" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001018-legacy-groups-update","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Version
    |> shouldEqual (SemVer.Parse "6.0.3")
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "NUnit"].Version
    |> shouldEqual (SemVer.Parse "2.6.1")
    lockFile.Groups.[GroupName "Legacy"].Resolution.[PackageName "Newtonsoft.Json"].Version
    |> shouldBeGreaterThan (SemVer.Parse "5.0.2")

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