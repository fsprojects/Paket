module Paket.IntegrationTests.RestoreSpec

open System
open System.IO
open Fake
open NUnit.Framework
open FsUnit

[<Test>]
let ``#2496 Paket fails on projects that target multiple frameworks``() = 
    let project = "EmptyTarget"
    let scenario = "i002496"
    prepareSdk scenario

    let wd = (scenarioTempPath scenario) @@ project
    directDotnet true (sprintf "restore %s.csproj" project) wd
        |> ignore