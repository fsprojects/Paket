module Paket.IntegrationTests.UpdatePackageSpecs

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
let ``#1018 update package in main group``() =
    paket "update nuget Newtonsoft.json" "i001018-legacy-groups-update" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001018-legacy-groups-update","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Version
    |> shouldBeGreaterThan (SemVer.Parse "6.0.3")
    lockFile.Groups.[GroupName "Legacy"].Resolution.[PackageName "Newtonsoft.Json"].Version
    |> shouldEqual (SemVer.Parse "5.0.2")

[<Test>]
let ``#1018 update package in group``() =
    paket "update nuget Newtonsoft.json group leGacy" "i001018-legacy-groups-update" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001018-legacy-groups-update","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Version
    |> shouldEqual (SemVer.Parse "6.0.3")
    lockFile.Groups.[GroupName "Legacy"].Resolution.[PackageName "Newtonsoft.Json"].Version
    |> shouldBeGreaterThan (SemVer.Parse "5.0.2")

[<Test>]
let ``#1178 update specific package``() =
    paket "update nuget NUnit" "i001178-update-with-regex" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001178-update-with-regex","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Windsor"].Version
    |> shouldEqual (SemVer.Parse "2.5.1")
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "NUnit"].Version
    |> shouldBeGreaterThan (SemVer.Parse "2.6.1")
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Microsoft.AspNet.WebApi.SelfHost"].Version
    |> shouldEqual (SemVer.Parse "5.0.1")

[<Test>]
let ``#1178 update with Mircosoft.* filter``() =
    paket "update nuget Microsoft.* --filter" "i001178-update-with-regex" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001178-update-with-regex","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Windsor"].Version
    |> shouldEqual (SemVer.Parse "2.5.1")
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "NUnit"].Version
    |> shouldEqual (SemVer.Parse "2.6.1")
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Microsoft.AspNet.WebApi.SelfHost"].Version
    |> shouldBeGreaterThan (SemVer.Parse "5.0.1")

[<Test>]
let ``#1178 update with [MN].* --filter``() =
    paket "update nuget [MN].* --filter" "i001178-update-with-regex" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001178-update-with-regex","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Windsor"].Version
    |> shouldEqual (SemVer.Parse "2.5.1")
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "NUnit"].Version
    |> shouldBeGreaterThan (SemVer.Parse "2.6.1")
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Microsoft.AspNet.WebApi.SelfHost"].Version
    |> shouldBeGreaterThan (SemVer.Parse "5.0.1")

[<Test>]
let ``#1178 update with [MN].* and without filter should fail``() =
    try
        paket "update nuget [MN].*" "i001178-update-with-regex" |> ignore
        failwithf "Paket command expected to fail"
    with
    | exn when exn.Message.Contains "Package [MN].* was not found in paket.dependencies in group Main" -> ()