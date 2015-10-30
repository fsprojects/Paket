[<AutoOpen>]
module Paket.IntegrationTests.TestHelpers

open Fake
open Paket
open System
open NUnit.Framework
open FsUnit
open System
open System.IO

let paketToolPath = FullName(__SOURCE_DIRECTORY__ + "../../../bin/paket.exe")
let integrationTestPath = FullName(__SOURCE_DIRECTORY__ + "../../../integrationtests/scenarios")
let scenarioTempPath scenario = Path.Combine(integrationTestPath,scenario,"temp")
let originalScenarioPath scenario = Path.Combine(integrationTestPath,scenario,"before")

let paket command scenario =
    let originalScenarioPath = originalScenarioPath scenario
    let scenarioPath = scenarioTempPath scenario
    CleanDir scenarioPath
    CopyDir scenarioPath originalScenarioPath (fun _ -> true)
    Directory.GetFiles(scenarioPath, "*.fsprojtemplate", SearchOption.AllDirectories)
    |> Seq.iter (fun f -> File.Move(f, Path.ChangeExtension(f, "fsproj")))

    let result =
        ExecProcessAndReturnMessages (fun info ->
          info.FileName <- paketToolPath
          info.WorkingDirectory <- scenarioPath
          info.Arguments <- command) (System.TimeSpan.FromMinutes 5.)
    if result.ExitCode <> 0 then 
        let errors = String.Join(Environment.NewLine,result.Errors)
        printfn "%s" <| String.Join(Environment.NewLine,result.Messages)
        failwith errors
    String.Join(Environment.NewLine,result.Messages)

let update scenario =
    paket "update -v" scenario |> ignore
    LockFile.LoadFrom(Path.Combine(scenarioTempPath scenario,"paket.lock"))

let install scenario =
    paket "install" scenario |> ignore
    LockFile.LoadFrom(Path.Combine(scenarioTempPath scenario,"paket.lock"))

let updateShouldFindPackageConflict packageName scenario =
    try
        update scenario |> ignore
        failwith "No conflict was found."
    with
    | exn when exn.Message.Contains(sprintf "Could not resolve package %s:" packageName) -> ()