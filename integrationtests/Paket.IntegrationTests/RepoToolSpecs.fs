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



