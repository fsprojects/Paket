module Paket.IntegrationTests.InitSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics

[<Test>]
#if PAKET_NETCORE
[<Ignore(".net core paket doesnt init the boostrapper")>]
#endif
let ``#1040 init should download release version of bootstrapper``() =
    use __ = paket "init" "i001040-init-downloads-bootstrapper" |> fst
    let bootstrapperPath = Path.Combine(scenarioTempPath "i001040-init-downloads-bootstrapper",".paket","paket.exe")

    let productVersion = FileVersionInfo.GetVersionInfo(bootstrapperPath).ProductVersion
    String.IsNullOrWhiteSpace productVersion |> shouldEqual false
    productVersion.Contains("-") |> shouldEqual false

[<Test>]
#if PAKET_NETCORE
[<Ignore(".net core paket doesnt init the boostrapper")>]
#endif
let ``#1743 empty log file``() =
    try
        use __ = paket "init --log-file" "i001040-init-downloads-bootstrapper" |> fst
        failwith "expected error"
    with
    | ProcessFailedWithExitCode(_, _, msgs) ->
        (msgs.Errors |> Seq.head).Contains "--log-file"
            |> shouldEqual true

[<Test>]
#if PAKET_NETCORE
[<Ignore(".net core paket doesnt init the boostrapper")>]
#endif
let ``#1240 current bootstrapper should work``() =
    CleanDir (scenarioTempPath "i001240-bootstrapper")
    let _, paketToolPath = paketBootstrapperToolPath
    CopyFile (scenarioTempPath "i001240-bootstrapper") paketToolPath

    let result =
        ExecProcessAndReturnMessages (fun info ->
          info.FileName <- scenarioTempPath "i001240-bootstrapper" </> "paket.bootstrapper.exe"
          info.WorkingDirectory <- scenarioTempPath "i001240-bootstrapper"
          info.Arguments <- "") (System.TimeSpan.FromMinutes 5.)
    if result.ExitCode <> 0 then
        let errors = String.Join(Environment.NewLine,result.Errors)
        printfn "%s" <| String.Join(Environment.NewLine,result.Messages)
        failwith errors

    String.Join(Environment.NewLine,result.Messages).Contains("latest stable")
    |> shouldEqual true

    File.Exists(scenarioTempPath "i001240-bootstrapper" </> "paket.exe")
    |> shouldEqual true

[<Test>]
let ``#1041 init api``() =
    let tempScenarioDir = scenarioTempPath "i001041-init-api"

    let url = "http://my.test/api"
    let source = Paket.PackageSources.PackageSource.NuGetV2Source(url)

    Paket.Dependencies.Init(tempScenarioDir, [source], [ "license_download: true" ], false)

    let depsPath = tempScenarioDir </> "paket.dependencies"
    File.Exists(depsPath) |> shouldEqual true

    let lines = File.ReadAllText(depsPath)

    StringAssert.Contains(url, lines);
    StringAssert.Contains("license_download: true", lines);
