module Paket.IntegrationTests.RepoToolSpecs

open NUnit.Framework
open Fake
open FsUnit
open System.IO

let directExecScript scriptPath = directToolEx false ("", scriptPath)

[<Test>]
let ``#3000 repo tool should work after restore``() =
    let scenario = "i003000-repo-tool"
    prepare scenario
    paket "restore" scenario |> ignore

    let wrappersPath = Path.Combine(scenarioTempPath scenario, "paket-files", "bin")

    let helloPath = Path.Combine(wrappersPath, (if Paket.Utils.isWindows then "hello.cmd" else "hello"))

    Assert.IsTrue(File.Exists(helloPath), (sprintf "file '%s' not found" helloPath))

    let resultCmd = directExecScript helloPath "" (scenarioTempPath scenario)
    CollectionAssert.AreEqual( [| "Hello World from F#! with args: []" |], (resultCmd |> Seq.map PaketMsg.getMessage |> Array.ofSeq) )

    let resultCmdWithArgs = directExecScript helloPath "1 2 3" (scenarioTempPath scenario)
    CollectionAssert.AreEqual( [| """Hello World from F#! with args: ["1"; "2"; "3"]""" |], (resultCmdWithArgs |> Seq.map PaketMsg.getMessage |> Array.ofSeq) )


[<Test>]
let ``#3001 repo tool should work after install``() =
    let scenario = "i003001-repo-tool-in-dep"
    prepare scenario

    paket "install" scenario |> ignore

    let wrappersPath = Path.Combine(scenarioTempPath scenario, "paket-files", "bin")

    let helloPath = Path.Combine(wrappersPath, (if Paket.Utils.isWindows then "hello.cmd" else "hello"))

    Assert.IsTrue(File.Exists(helloPath), (sprintf "file '%s' not found" helloPath))

    let resultCmd = directExecScript helloPath "" (scenarioTempPath scenario)
    CollectionAssert.AreEqual( [| "Hello World from F#! with args: []" |], (resultCmd |> Seq.map PaketMsg.getMessage |> Array.ofSeq) )

    let resultCmdWithArgs = directExecScript helloPath "1 2 3" (scenarioTempPath scenario)
    CollectionAssert.AreEqual( [| """Hello World from F#! with args: ["1"; "2"; "3"]""" |], (resultCmdWithArgs |> Seq.map PaketMsg.getMessage |> Array.ofSeq) )

[<Test>]
let ``#3002 repo tool from flatten tools dir``() =
    let scenario = "i003002-repo-tool-flatten-tools-dir"
    prepare scenario

    paket "restore" scenario |> ignore

    let wrappersPath = Path.Combine(scenarioTempPath scenario, "paket-files", "bin")

    let toolNames =
        let toolsCmdPath =  ["FAKE.cmd"; "Fake.Deploy.cmd"]
        let toolsBashPath = ["FAKE"; "Fake.Deploy"]
        if Paket.Utils.isWindows then toolsCmdPath else toolsBashPath

    for toolName in toolNames do
        let toolPath = Path.Combine(wrappersPath, toolName)
        Assert.IsTrue(File.Exists(toolPath), (sprintf "file '%s' not found" toolPath))

[<Test>]
let ``#3003 repo tool with add to PATH``() =
    let scenario = "i003003-repo-tool-in-PATH"
    prepare scenario
    paket "restore" scenario |> ignore

    let wrappersPath = Path.Combine(scenarioTempPath scenario, "paket-files", "bin")

    let helperNames = if Paket.Utils.isWindows then [ "paketrt.cmd" ] else [ "paketrt" ]

    for name in helperNames do
        let cmdPath = Path.Combine(wrappersPath, name)
        Assert.IsTrue(File.Exists(cmdPath), (sprintf "file '%s' not found" cmdPath))

    let runitName = if Paket.Utils.isWindows then "runit.bat" else "runit"

    let scriptPath = (scenarioTempPath scenario) </> runitName

    let resultCmd = directExecScript scriptPath "" (scenarioTempPath scenario)
    CollectionAssert.Contains(resultCmd |> Seq.map PaketMsg.getMessage |> Array.ofSeq, """Hello World from F#! with args: ["a"; "3003"; "c"]""" )

[<Test>]
let ``#3004 repo tool multi tfm (net)``() =
    let scenario = "i003004-repo-tool-multi-tfm"
    prepare scenario
    paket "restore" scenario |> ignore

    let wrappersPath = Path.Combine(scenarioTempPath scenario, "paket-files", "bin")

    let helloPath = Path.Combine(wrappersPath, (if Paket.Utils.isWindows then "myhello.cmd" else "myhello"))

    Assert.IsTrue(File.Exists(helloPath), (sprintf "file '%s' not found" helloPath))
    StringAssert.DoesNotContain("dotnet", File.ReadAllText(helloPath))
    StringAssert.DoesNotContain("netcoreapp", File.ReadAllText(helloPath))

[<Test>]
let ``#3005 repo tool multi tfm (netcoreapp)``() =
    let scenario = "i003005-repo-tool-multi-tfm-dnc"
    prepare scenario

    try
        System.Environment.SetEnvironmentVariable("PAKET_REPOTOOL_PREFERRED_RUNTIME", "netcoreapp")
        paket "restore" scenario |> ignore
    finally
        System.Environment.SetEnvironmentVariable("PAKET_REPOTOOL_PREFER_RUNTIME", "")

    let wrappersPath = Path.Combine(scenarioTempPath scenario, "paket-files", "bin")

    let helloPath = Path.Combine(wrappersPath, (if Paket.Utils.isWindows then "myhello.cmd" else "myhello"))

    Assert.IsTrue(File.Exists(helloPath), (sprintf "file '%s' not found" helloPath))
    StringAssert.Contains("dotnet", File.ReadAllText(helloPath))
    StringAssert.Contains("netcoreapp", File.ReadAllText(helloPath))

    let resultCmd = directExecScript helloPath "" (scenarioTempPath scenario)
    CollectionAssert.AreEqual( [| "Hello World from F#! with args: []" |], (resultCmd |> Seq.map PaketMsg.getMessage |> Array.ofSeq) )

    let resultCmdWithArgs = directExecScript helloPath "1 2 3" (scenarioTempPath scenario)
    CollectionAssert.AreEqual( [| """Hello World from F#! with args: ["1"; "2"; "3"]""" |], (resultCmdWithArgs |> Seq.map PaketMsg.getMessage |> Array.ofSeq) )

[<Test>]
let ``#3006 repo tool should restore on specific bin dir based on repotools_bin_dir config``() =
    let scenario = "i003006-repo-tool-specific-bin-dir"
    prepare scenario
    paket "restore" scenario |> ignore

    let wrappersPath = Path.Combine(scenarioTempPath scenario, "use", "mybin")

    let helloPath = Path.Combine(wrappersPath, (if Paket.Utils.isWindows then "hello.cmd" else "hello"))

    Assert.IsTrue(File.Exists(helloPath), (sprintf "file '%s' not found" helloPath))

    let resultCmd = directExecScript helloPath "" (scenarioTempPath scenario)
    CollectionAssert.AreEqual( [| "Hello World from F#! with args: []" |], (resultCmd |> Seq.map PaketMsg.getMessage |> Array.ofSeq) )

    let resultCmdWithArgs = directExecScript helloPath "1 2 3" (scenarioTempPath scenario)
    CollectionAssert.AreEqual( [| """Hello World from F#! with args: ["1"; "2"; "3"]""" |], (resultCmdWithArgs |> Seq.map PaketMsg.getMessage |> Array.ofSeq) )

[<Test>]
let ``#3007 repo tool should consider alias on install``() =
    let scenario = "i003007-repo-tool-alias"
    prepare scenario
    paket "install" scenario |> ignore

    let wrappersPath = Path.Combine(scenarioTempPath scenario, "paket-files", "bin")

    let ciaoPath = Path.Combine(wrappersPath, (if Paket.Utils.isWindows then "ciao.cmd" else "ciao"))

    Assert.IsTrue(File.Exists(ciaoPath), (sprintf "file '%s' not found" ciaoPath))

    let resultCmd = directExecScript ciaoPath "" (scenarioTempPath scenario)
    CollectionAssert.AreEqual( [| "Hello World from F#! with args: []" |], (resultCmd |> Seq.map PaketMsg.getMessage |> Array.ofSeq) )

    let resultCmdWithArgs = directExecScript ciaoPath "1 2 3" (scenarioTempPath scenario)
    CollectionAssert.AreEqual( [| """Hello World from F#! with args: ["1"; "2"; "3"]""" |], (resultCmdWithArgs |> Seq.map PaketMsg.getMessage |> Array.ofSeq) )

[<Test>]
let ``#3008 repo tool write list of tools``() =
    let scenario = "i003008-repo-tool-csv"
    prepare scenario
    paket "restore" scenario |> ignore

    let repotoolsCsvPath = Path.Combine(scenarioTempPath scenario, "paket-files", "paket.repotools.csv")

    Assert.IsTrue(File.Exists(repotoolsCsvPath), (sprintf "file '%s' not found" repotoolsCsvPath))
