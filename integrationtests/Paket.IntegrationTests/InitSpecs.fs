module Paket.IntegrationTests.InitSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO

[<Test>]
let ``#1743 empty log file``() =
    try
        use __ = paket "init --log-file" "i001743-empty-log-file" |> fst
        failwith "expected error"
    with
    | ProcessFailedWithExitCode(_, _, msgs) ->
        (msgs.Errors |> Seq.head).Contains "--log-file"
            |> shouldEqual true

[<Test>]
let ``init creates paket.dependencies``() =
    let scenario = "init-creates-dependencies-file"
    use __ = paket "init" scenario |> fst
    File.Exists(Path.Combine(scenarioTempPath scenario, "paket.dependencies")) |> shouldEqual true
    // the paket.exe of Paket 11 and earlier also downloaded the bootstrapper into .paket
    File.Exists(Path.Combine(scenarioTempPath scenario, ".paket", "paket.exe")) |> shouldEqual false

[<Test>]
let ``#1041 init api``() =
    let tempScenarioDir = scenarioTempPath "i001041-init-api"

    let url = "http://my.test/api"
    let source = Paket.PackageSources.PackageSource.NuGetV2Source(url)

    Paket.Dependencies.Init(tempScenarioDir, [source], [ "license_download: true" ])

    let depsPath = tempScenarioDir </> "paket.dependencies"
    File.Exists(depsPath) |> shouldEqual true

    let lines = File.ReadAllText(depsPath)

    StringAssert.Contains(url, lines);
    StringAssert.Contains("license_download: true", lines);
