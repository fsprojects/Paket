[<AutoOpen>]
module Paket.IntegrationTests.TestHelpers

open Fake
open Paket
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Pri.LongPath

let scenarios = System.Collections.Generic.List<_>()
let isLiveUnitTesting = AppDomain.CurrentDomain.GetAssemblies() |> Seq.exists (fun a -> a.GetName().Name = "Microsoft.CodeAnalysis.LiveUnitTesting.Runtime")

let paketToolPath = FullName(__SOURCE_DIRECTORY__ + "../../../bin/paket.exe")
let integrationTestPath = FullName(__SOURCE_DIRECTORY__ + "../../../integrationtests/scenarios")
let scenarioTempPath scenario = Path.Combine(integrationTestPath,scenario,"temp")
let originalScenarioPath scenario = Path.Combine(integrationTestPath,scenario,"before")

let cleanup scenario =
    let scenarioPath = scenarioTempPath scenario
    CleanDir scenarioPath

let cleanupAllScenarios() =
    for scenario in scenarios do
        cleanup scenario
    scenarios.Clear()

let prepare scenario =
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

let directPaketInPath command scenarioPath =
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
    string result
    #else
    Environment.SetEnvironmentVariable("PAKET_DETAILED_ERRORS", "true")
    printfn "%s> paket %s" scenarioPath command
    let perfMessages = ResizeArray()
    let msgs = ResizeArray()
    let mutable perfMessagesStarted = false
    let addAndPrint isError msg =
        if not isError then
            if msg = "Performance:" then
                perfMessagesStarted <- true
            elif perfMessagesStarted then
                perfMessages.Add(msg)
                
        msgs.Add((isError, msg))
        
    let result =
        try
            ExecProcessWithLambdas (fun info ->
              info.FileName <- paketToolPath
              info.WorkingDirectory <- scenarioPath
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

            for isError, msg in msgs do
                printfn "%s%s" (if isError then "ERR: " else "") msg
            reraise()
    // Only throw after the result <> 0 check because the current test might check the argument parsing
    // this is the only case where no performance is printed
    let isUsageError = result <> 0 && msgs |> Seq.filter fst |> Seq.map snd |> Seq.exists (fun msg -> msg.Contains "USAGE:")
    if not isUsageError then
        if perfMessages.Count = 0 then
            failwith "No Performance messages recieved in test!"
        printfn "Performance:"
        for msg in perfMessages do
            printfn "%s" msg

    // always print stderr
    for isError, msg in msgs do
        if isError then
            printfn "ERR: %s" msg

    if result <> 0 then 
        let errors = String.Join(Environment.NewLine,msgs |> Seq.filter fst |> Seq.map snd)
        failwith errors      


    String.Join(Environment.NewLine,msgs |> Seq.map snd)
    #endif

let directPaket command scenario =
    directPaketInPath command (scenarioTempPath scenario)

let paket command scenario =
    prepare scenario

    directPaket command scenario

let update scenario =
    #if INTERACTIVE
    paket "update --verbose" scenario |> printfn "%s"
    #else
    paket "update" scenario |> ignore
    #endif
    LockFile.LoadFrom(Path.Combine(scenarioTempPath scenario,"paket.lock"))

let install scenario =
    #if INTERACTIVE
    paket "install --verbose" scenario |> printfn "%s"
    #else
    paket "install" scenario |> ignore
    #endif
    LockFile.LoadFrom(Path.Combine(scenarioTempPath scenario,"paket.lock"))

let restore scenario = paket "restore" scenario |> ignore

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