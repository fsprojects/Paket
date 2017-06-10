[<AutoOpen>]
module Paket.IntegrationTests.TestHelpers

open Fake
open Paket
open System
open NUnit.Framework
open FsUnit
open System
open System.IO

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
    Directory.GetFiles(scenarioPath, "*.fsprojtemplate", SearchOption.AllDirectories)
    |> Seq.iter (fun f -> File.Move(f, Path.ChangeExtension(f, "fsproj")))
    Directory.GetFiles(scenarioPath, "*.csprojtemplate", SearchOption.AllDirectories)
    |> Seq.iter (fun f -> File.Move(f, Path.ChangeExtension(f, "csproj")))
    Directory.GetFiles(scenarioPath, "*.vcxprojtemplate", SearchOption.AllDirectories)
    |> Seq.iter (fun f -> File.Move(f, Path.ChangeExtension(f, "vcxproj")))
    Directory.GetFiles(scenarioPath, "*.templatetemplate", SearchOption.AllDirectories)
    |> Seq.iter (fun f -> File.Move(f, Path.ChangeExtension(f, "template")))
    Directory.GetFiles(scenarioPath, "*.jsontemplate", SearchOption.AllDirectories)
    |> Seq.iter (fun f -> File.Move(f, Path.ChangeExtension(f, "json")))

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
        if isError then
            printfn "ERR: %s" msg
        else printfn "%s" msg
        
    let result =
        ExecProcessWithLambdas (fun info ->
          info.FileName <- paketToolPath
          info.WorkingDirectory <- scenarioPath
          info.Arguments <- command) 
          (System.TimeSpan.FromMinutes 7.)
          true
          (addAndPrint true)
          (addAndPrint false)
    if perfMessages.Count = 0 then
        failwith "No Performance messages recieved in test!"
    printfn "Performance:"
    for msg in perfMessages do
        printfn "%s" msg
    //let result =
    //    ExecProcessAndReturnMessages (fun info ->
    //      info.FileName <- paketToolPath
    //      info.WorkingDirectory <- scenarioPath
    //      info.Arguments <- command) (System.TimeSpan.FromMinutes 5.)
    if result <> 0 then 
        let errors = String.Join(Environment.NewLine,msgs |> Seq.filter fst |> Seq.map snd)
        //printfn "%s" <| String.Join(Environment.NewLine,msgs)
        failwith errors      
    String.Join(Environment.NewLine,msgs |> Seq.filter (fst >> not) |> Seq.map snd)
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