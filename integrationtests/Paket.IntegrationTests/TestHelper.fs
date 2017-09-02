[<AutoOpen>]
module Paket.IntegrationTests.TestHelpers

open Fake
open Paket
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Paket.Logging

let scenarios = System.Collections.Generic.List<_>()
let isLiveUnitTesting = AppDomain.CurrentDomain.GetAssemblies() |> Seq.exists (fun a -> a.GetName().Name = "Microsoft.CodeAnalysis.LiveUnitTesting.Runtime")

let partitionForTravis scenario =

    // travis executes tests in three stages:
    // * -1: build only
    // * 0: first half of the scenario tests
    // * 1: second half of the scenario tests
    //
    // use the hash of the scenario name to key between stage 0 and 1.
    let currentTravisStage =
        match Environment.GetEnvironmentVariable "TRAVIS_STAGE" with
        | null | "" -> None
        | sInt ->
            match Int32.TryParse sInt with
            | true, iState -> Some iState
            | _ -> None
    
    if currentTravisStage <> None &&
       currentTravisStage <> Some (scenario.GetHashCode() % 2)
    then Assert.Ignore("ignored in this part of the travis build")
    

let paketToolPath = FullName(__SOURCE_DIRECTORY__ + "../../../bin/paket.exe")
let integrationTestPath = FullName(__SOURCE_DIRECTORY__ + "../../../integrationtests/scenarios")
let scenarioTempPath scenario = Path.Combine(integrationTestPath,scenario,"temp")
let originalScenarioPath scenario = Path.Combine(integrationTestPath,scenario,"before")

let cleanup scenario =
    let scenarioPath = scenarioTempPath scenario
    try
        CleanDir scenarioPath
    with e ->
        traceWarnfn "Failed to clean dir '%s', trying again: %O" scenarioPath e
        CleanDir scenarioPath

let cleanupAllScenarios() =
    for scenario in scenarios do
        cleanup scenario
    scenarios.Clear()


let prepare scenario =
    partitionForTravis scenario

    if isLiveUnitTesting then Assert.Inconclusive("Integration tests are disabled when in a Live-Unit-Session")
    if scenarios.Count > 10 then
        cleanupAllScenarios()

    scenarios.Add scenario
    let originalScenarioPath = originalScenarioPath scenario
    let scenarioPath = scenarioTempPath scenario
    CleanDir scenarioPath
    CopyDir scenarioPath originalScenarioPath (fun _ -> true)

    for ext in ["fsproj";"csproj";"vcxproj";"template";"json"] do
        for file in Directory.GetFiles(scenarioPath, (sprintf "*.%stemplate" ext), SearchOption.AllDirectories) do
            File.Move(file, Path.ChangeExtension(file, ext))

type PaketMsg =
  { IsError : bool; Message : string }
    static member isError ({ IsError = e}:PaketMsg) = e
    static member getMessage ({ Message = msg }:PaketMsg) = msg

let directPaketInPathEx command scenarioPath =
    #if INTERACTIVE
    let result =
        ExecProcessWithLambdas (fun info ->
          info.FileName <- paketToolPath
          info.WorkingDirectory <- scenarioPath
          info.Arguments <- command) 
          (System.TimeSpan.FromMinutes 7.)
          false
          (printfn "%s")
          (printfn "%s")
    let res = new ResizeArray()
    res.Add (string result)
    res
    #else
    Environment.SetEnvironmentVariable("PAKET_DETAILED_ERRORS", "true")
    printfn "%s> paket %s" scenarioPath command
    let perfMessages = ResizeArray()
    let msgs = ResizeArray<PaketMsg>()
    let mutable perfMessagesStarted = false
    let addAndPrint isError msg =
        if not isError then
            if msg = "Performance:" then
                perfMessagesStarted <- true
            elif perfMessagesStarted then
                perfMessages.Add(msg)
                
        msgs.Add({ IsError = isError; Message = msg})
        
    let result =
        try
            ExecProcessWithLambdas (fun info ->
              info.FileName <- paketToolPath
              info.WorkingDirectory <- scenarioPath
              info.CreateNoWindow <- true
              info.Arguments <- command)
              (System.TimeSpan.FromMinutes 7.)
              true
              (addAndPrint true)
              (addAndPrint false)
        with exn ->
            if exn.Message.Contains "timed out" then
                printfn "PROCESS TIMED OUT, OUTPUT WAS: "
            else
                printfn "ExecProcessWithLambdas failed. Output was: "

            for { IsError = isError; Message = msg } in msgs do
                printfn "%s%s" (if isError then "ERR: " else "") msg
            reraise()
    // Only throw after the result <> 0 check because the current test might check the argument parsing
    // this is the only case where no performance is printed
    let isUsageError = result <> 0 && msgs |> Seq.filter PaketMsg.isError |> Seq.map PaketMsg.getMessage |> Seq.exists (fun msg -> msg.Contains "USAGE:")
    if not isUsageError then
        if perfMessages.Count = 0 then
            failwith "No Performance messages recieved in test!"
        printfn "Performance:"
        for msg in perfMessages do
            printfn "%s" msg

    // always print stderr
    for msg in msgs do
        if msg.IsError then
            printfn "ERR: %s" msg.Message

    if result <> 0 then 
        let errors = String.Join(Environment.NewLine,msgs |> Seq.filter PaketMsg.isError |> Seq.map PaketMsg.getMessage)
        if String.IsNullOrWhiteSpace errors then
            failwithf "The process exited with code %i" result
        else
            failwith errors

    msgs
    #endif
let private fromMessages msgs =
    String.Join(Environment.NewLine,msgs |> Seq.map PaketMsg.getMessage)

let directPaketInPath command scenarioPath = directPaketInPathEx command scenarioPath |> fromMessages

let directPaketEx command scenario =
    partitionForTravis scenario
    directPaketInPathEx command (scenarioTempPath scenario)

let directPaket command scenario = directPaketEx command scenario |> fromMessages

let paketEx checkZeroWarn command scenario =
    prepare scenario

    let msgs = directPaketEx command scenario
    if checkZeroWarn then
        msgs
        |> Seq.filter PaketMsg.isError
        |> Seq.toList
        |> shouldEqual []
    msgs

let paket command scenario =
    paketEx false command scenario |> fromMessages

let updateEx checkZeroWarn scenario =
    #if INTERACTIVE
    paket "update --verbose" scenario |> printfn "%s"
    #else
    paketEx checkZeroWarn "update" scenario |> ignore
    #endif
    LockFile.LoadFrom(Path.Combine(scenarioTempPath scenario,"paket.lock"))

let update scenario =
    updateEx false scenario

let installEx checkZeroWarn scenario =
    #if INTERACTIVE
    paket "install --verbose" scenario |> printfn "%s"
    #else
    paketEx checkZeroWarn  "install" scenario |> ignore
    #endif
    LockFile.LoadFrom(Path.Combine(scenarioTempPath scenario,"paket.lock"))

let install scenario = installEx false scenario

let restore scenario = paketEx false "restore" scenario |> ignore

let updateShouldFindPackageConflict packageName scenario =
    try
        update scenario |> ignore
        failwith "No conflict was found."
    with
    | exn when exn.Message.Contains("Conflict detected") && exn.Message.Contains(sprintf "requested package %s" packageName) -> 
        #if INTERACTIVE
        printfn "Ninject conflict test passed"
        #endif
        ()