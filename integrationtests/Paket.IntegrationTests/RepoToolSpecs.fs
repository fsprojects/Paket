module Paket.IntegrationTests.RepoToolSpecs

open NUnit.Framework
open Fake
open FsUnit
open System.IO

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

    let resultCmd = directToolEx false helloCmdPath "" (scenarioTempPath scenario) 
    CollectionAssert.AreEqual( [| "Hello World from F#!" |], (resultCmd |> Seq.map PaketMsg.getMessage |> Array.ofSeq) )


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

    let resultCmd = directToolEx false helloCmdPath "" (scenarioTempPath scenario) 
    CollectionAssert.AreEqual( [| "Hello World from F#!" |], (resultCmd |> Seq.map PaketMsg.getMessage |> Array.ofSeq) )
