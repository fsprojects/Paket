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