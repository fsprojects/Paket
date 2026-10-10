module Paket.IntegrationTests.DotnetToolSpecs

open System
open System.IO
open Fake
open NUnit.Framework
open FsUnit

// Paket.Restore.targets has to find the Paket .NET tool on its own: these tests install the
// package the build just packed and leave PaketExePath unset.

let scenario = "dotnet-tool-locator"

/// Restores the project of the scenario and checks that the installed tool did the Paket restore
let restoreShouldUseTool version env =
    let scenarioPath = scenarioTempPath scenario
    let output =
        directDotnetEx env false "restore" (scenarioPath @@ "project")
        |> Seq.map (fun msg -> msg.Message)
        |> String.concat Environment.NewLine
    output |> shouldContainText $"Paket version %s{version}"
    File.Exists(scenarioPath @@ "paket-files" @@ "paket.restore.cached") |> shouldEqual true

[<Test>]
let ``restore finds paket as a local tool of the manifest``() =
    let cleanup, version, env = prepareSdkForTool scenario
    use __ = cleanup
    let scenarioPath = scenarioTempPath scenario
    directDotnetEx env false $"tool install paket --local --version %s{version} --configfile nuget.config" scenarioPath |> ignore

    restoreShouldUseTool version env

[<Test>]
let ``restore finds paket installed with --tool-path .paket``() =
    let cleanup, version, env = prepareSdkForTool scenario
    use __ = cleanup
    let scenarioPath = scenarioTempPath scenario
    directDotnetEx env false $"tool install paket --tool-path .paket --version %s{version} --configfile nuget.config" scenarioPath |> ignore

    restoreShouldUseTool version env

[<Test>]
let ``restore finds paket on the PATH like a global tool``() =
    let cleanup, version, env = prepareSdkForTool scenario
    use __ = cleanup
    let scenarioPath = scenarioTempPath scenario
    let toolPath = scenarioPath @@ "global-tools"
    directDotnetEx env false $"tool install paket --tool-path \"%s{toolPath}\" --version %s{version} --configfile nuget.config" scenarioPath |> ignore

    let path = toolPath + string Path.PathSeparator + Environment.GetEnvironmentVariable "PATH"
    restoreShouldUseTool version (("PATH", path) :: env)
