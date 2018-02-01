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

    let helloCmdPath = Path.Combine(wrappersPath, "hello.cmd")
    Assert.IsTrue(File.Exists(helloCmdPath), (sprintf "file '%s' not found" helloCmdPath))
    
    let helloBashPath = Path.Combine(wrappersPath, "hello")
    Assert.IsTrue(File.Exists(helloBashPath), (sprintf "file '%s' not found" helloBashPath))

    let helloPath = if Paket.Utils.isWindows then helloCmdPath else helloBashPath

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

    let helloCmdPath = Path.Combine(wrappersPath, "hello.cmd")
    Assert.IsTrue(File.Exists(helloCmdPath), (sprintf "file '%s' not found" helloCmdPath))
    
    let helloBashPath = Path.Combine(wrappersPath, "hello")
    Assert.IsTrue(File.Exists(helloBashPath), (sprintf "file '%s' not found" helloBashPath))

    let helloPath = if Paket.Utils.isWindows then helloCmdPath else helloBashPath

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

    for toolName in ["FAKE.cmd"; "FAKE"; "Fake.Deploy.cmd"; "Fake.Deploy"] do
        let toolPath = Path.Combine(wrappersPath, toolName)
        Assert.IsTrue(File.Exists(toolPath), (sprintf "file '%s' not found" toolPath))

[<Test>]
let ``#3003 repo tool with add to PATH``() =
    let scenario = "i003003-repo-tool-in-PATH"
    prepare scenario
    paket "restore" scenario |> ignore

    let wrappersPath = Path.Combine(scenarioTempPath scenario, "paket-files", "bin")

    for name in ["repotools.cmd"; "repotools"] do
        let cmdPath = Path.Combine(wrappersPath, name)
        Assert.IsTrue(File.Exists(cmdPath), (sprintf "file '%s' not found" cmdPath))

    if Paket.Utils.isWindows then
        let scriptPath = (scenarioTempPath scenario) </> "runit.bat"
        File.WriteAllLines(scriptPath,
            [| @"@ECHO OFF"
               @"SETLOCAL"
               @"CALL paket-files\bin\repotools enable"
               @"CALL hello" |] )

        let resultCmd = directExecScript scriptPath "" (scenarioTempPath scenario)
        CollectionAssert.Contains(resultCmd |> Seq.map PaketMsg.getMessage |> Array.ofSeq, "Hello World from F#! with args: []" )
    else
        let scriptPath = (scenarioTempPath scenario) </> "runit"
        File.WriteAllLines(scriptPath,
            [| "#!/bin/sh"
               "source <(./paket-files/bin/repotools e)"
               "hello"
               "" |] )

        let resultCmd = directExecScript scriptPath "" (scenarioTempPath scenario)
        CollectionAssert.Contains(resultCmd |> Seq.map PaketMsg.getMessage |> Array.ofSeq, "Hello World from F#! with args: []" )

[<Test>]
let ``#3004 repo tool multi tfm (net)``() =
    let scenario = "i003004-repo-tool-multi-tfm"
    prepare scenario
    paket "restore" scenario |> ignore

    let wrappersPath = Path.Combine(scenarioTempPath scenario, "paket-files", "bin")

    let helloCmdPath = Path.Combine(wrappersPath, "myhello.cmd")
    Assert.IsTrue(File.Exists(helloCmdPath), (sprintf "file '%s' not found" helloCmdPath))
    StringAssert.DoesNotContain("dotnet", File.ReadAllText(helloCmdPath))
    StringAssert.DoesNotContain("netcoreapp", File.ReadAllText(helloCmdPath))
    
    let helloBashPath = Path.Combine(wrappersPath, "myhello")
    Assert.IsTrue(File.Exists(helloBashPath), (sprintf "file '%s' not found" helloBashPath))
    StringAssert.DoesNotContain("dotnet", File.ReadAllText(helloBashPath))
    StringAssert.DoesNotContain("netcoreapp", File.ReadAllText(helloBashPath))

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

    let helloCmdPath = Path.Combine(wrappersPath, "myhello.cmd")
    Assert.IsTrue(File.Exists(helloCmdPath), (sprintf "file '%s' not found" helloCmdPath))
    StringAssert.Contains("dotnet", File.ReadAllText(helloCmdPath))
    StringAssert.Contains("netcoreapp", File.ReadAllText(helloCmdPath))
    
    let helloBashPath = Path.Combine(wrappersPath, "myhello")
    Assert.IsTrue(File.Exists(helloBashPath), (sprintf "file '%s' not found" helloBashPath))
    StringAssert.Contains("dotnet", File.ReadAllText(helloBashPath))
    StringAssert.Contains("netcoreapp", File.ReadAllText(helloBashPath))

    let helloPath = if Paket.Utils.isWindows then helloCmdPath else helloBashPath

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

    let helloCmdPath = Path.Combine(wrappersPath, "hello.cmd")
    Assert.IsTrue(File.Exists(helloCmdPath), (sprintf "file '%s' not found" helloCmdPath))
    
    let helloBashPath = Path.Combine(wrappersPath, "hello")
    Assert.IsTrue(File.Exists(helloBashPath), (sprintf "file '%s' not found" helloBashPath))

    let helloPath = if Paket.Utils.isWindows then helloCmdPath else helloBashPath

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

    let ciaoCmdPath = Path.Combine(wrappersPath, "ciao.cmd")
    Assert.IsTrue(File.Exists(ciaoCmdPath), (sprintf "file '%s' not found" ciaoCmdPath))
    
    let ciaoBashPath = Path.Combine(wrappersPath, "ciao")
    Assert.IsTrue(File.Exists(ciaoBashPath), (sprintf "file '%s' not found" ciaoBashPath))

    let ciaoPath = if Paket.Utils.isWindows then ciaoCmdPath else ciaoBashPath

    let resultCmd = directExecScript ciaoPath "" (scenarioTempPath scenario)
    CollectionAssert.AreEqual( [| "Hello World from F#! with args: []" |], (resultCmd |> Seq.map PaketMsg.getMessage |> Array.ofSeq) )

    let resultCmdWithArgs = directExecScript ciaoPath "1 2 3" (scenarioTempPath scenario)
    CollectionAssert.AreEqual( [| """Hello World from F#! with args: ["1"; "2"; "3"]""" |], (resultCmdWithArgs |> Seq.map PaketMsg.getMessage |> Array.ofSeq) )
