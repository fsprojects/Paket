[<AutoOpen>]
module Paket.IntegrationTests.TestHelpers

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO

let paketToolPath = FullName(__SOURCE_DIRECTORY__ + "../../../bin/paket.exe")
let integrationTestPath = FullName(__SOURCE_DIRECTORY__ + "../../../integrationtests/scenarios")
let scenarioTempPath scenario = Path.Combine(integrationTestPath,scenario,"temp")

let paket command scenario =
    let originalScenarioPath = Path.Combine(integrationTestPath,scenario,"before")
    let scenarioPath = scenarioTempPath scenario
    CleanDir scenarioPath
    CopyDir scenarioPath originalScenarioPath (fun _ -> true)

    let result =
        ExecProcessAndReturnMessages (fun info ->
          info.FileName <- paketToolPath
          info.WorkingDirectory <- scenarioPath
          info.Arguments <- command) (System.TimeSpan.FromMinutes 1.)
    if result.ExitCode <> 0 then 
        let errors = String.Join(Environment.NewLine,result.Errors)
        failwith errors

let update scenario = paket "update" scenario

let updateShouldFindPackageConflict packageName scenario =
    try
        update scenario
        failwith "No conflict was found."
    with
    | exn when exn.Message.Contains(sprintf "Could not resolve package %s:" packageName) -> ()