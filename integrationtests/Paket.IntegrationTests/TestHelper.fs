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

let disableScenarioCleanup = false // change to true to debug a single test temporarily.

let isLiveUnitTesting = AppDomain.CurrentDomain.GetAssemblies() |> Seq.exists (fun a -> a.GetName().Name = "Microsoft.CodeAnalysis.LiveUnitTesting.Runtime")

let dotnetToolPath =
    match Environment.GetEnvironmentVariable "DOTNET_EXE_PATH" with
    | null | "" -> "dotnet"
    | s -> s

let paketToolPath =
#if PAKET_NETCORE
    dotnetToolPath, FullName(__SOURCE_DIRECTORY__ + "../../../bin/netcoreapp2.1/paket.dll")
#else
    "", FullName(__SOURCE_DIRECTORY__ + "../../../bin/net461/paket.exe")
#endif

let paketBootstrapperToolPath =
#if PAKET_NETCORE
    dotnetToolPath, FullName(__SOURCE_DIRECTORY__ + "../../../bin_bootstrapper/netcoreapp2.1/paket.bootstrapper.dll")
#else
    "", FullName(__SOURCE_DIRECTORY__ + "../../../bin_bootstrapper/net461/paket.bootstrapper.exe")
#endif

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

//let cleanupAllScenarios() =
//    for scenario in scenarios |> Seq.toList do
//        try
//            cleanup scenario
//            scenarios.Remove(scenario) |> ignore<bool>
//        with e ->
//            traceWarnfn "Failed to cleanup a particular scenario %s, %O" scenario e

let createScenarioDir scenario =
    let scenarioPath = scenarioTempPath scenario
    CleanDir scenarioPath
    scenarioPath

let prepare scenario =
    if isLiveUnitTesting then Assert.Inconclusive("Integration tests are disabled when in a Live-Unit-Session")
    //if scenarios.Count > 10 then
    //    cleanupAllScenarios()

    //scenarios.Add scenario
    let cleanup =
        { new System.IDisposable with
            member x.Dispose() =
                if not disableScenarioCleanup then
                    try
                        cleanup scenario
                    with e -> eprintfn "Error in test cleanup (Dispose): %O" e }
    let mutable shouldDispose = cleanup
    try
        let originalScenarioPath = originalScenarioPath scenario
        let scenarioPath = createScenarioDir scenario
        CopyDir scenarioPath originalScenarioPath (fun _ -> true)

        for ext in ["fsproj";"csproj";"vcxproj";"template";"json"] do
            for file in Directory.GetFiles(scenarioPath, (sprintf "*.%stemplate" ext), SearchOption.AllDirectories) do
                File.Move(file, Path.ChangeExtension(file, ext))
        shouldDispose <- null
        cleanup
    finally
        if not (isNull shouldDispose) then shouldDispose.Dispose()


let prepareSdk scenario =
    let tmpPaketFolder = (scenarioTempPath scenario) @@ ".paket"
    let targetsFile = FullName(__SOURCE_DIRECTORY__ + "../../../src/Paket.Core/embedded/Paket.Restore.targets")
    let paketExe = snd paketToolPath

    setEnvironVar "PaketExePath" paketExe
    let cleanup = prepare scenario
    if (not (Directory.Exists tmpPaketFolder)) then
        Directory.CreateDirectory tmpPaketFolder |> ignore

    FileHelper.CopyFile tmpPaketFolder targetsFile
    cleanup

type OutputMsg =
  { IsError : bool; Message : string }
    static member isError ({ IsError = e}:OutputMsg) = e
    static member getMessage ({ Message = msg }:OutputMsg) = msg
type OutputData =
    internal { _Messages : ResizeArray<OutputMsg> }
    member x.Messages : seq<OutputMsg> = x._Messages :> _
    member x.Errors = x._Messages |> Seq.filter OutputMsg.isError |> Seq.map OutputMsg.getMessage
    member x.Outputs = x._Messages |> Seq.filter (not << OutputMsg.isError) |> Seq.map OutputMsg.getMessage

exception ProcessFailedWithExitCode of exitCode:int * fileName:string * msgs:OutputData
    with
    override x.Message =
        let output = String.Join(Environment.NewLine,x.msgs.Messages |> Seq.map (fun m -> (if m.IsError then "ERR:" else "OUT:") + m.Message ))
        sprintf "The process '%s' exited with code %i, output: \n%s" x.fileName x.exitCode output

let directToolEx env isPaket toolInfo commands workingDir =
    let processFilename, processArgs =
        match fst toolInfo, snd toolInfo with
        | "", path ->
            path, commands
        | host, path ->
            host, (sprintf "%s %s" path commands)

    #if INTERACTIVE
    let result =
        ExecProcessWithLambdas (fun info ->
          info.FileName <- processFilename
          info.WorkingDirectory <- workingDir
          info.Arguments <- processArgs)
          (System.TimeSpan.FromMinutes 7.)
          false
          (printfn "%s")
          (printfn "%s")
    let res = new ResizeArray()
    res.Add (string result)
    res
    #else
    Environment.SetEnvironmentVariable("PAKET_DETAILED_ERRORS", "true")
    Environment.SetEnvironmentVariable("PAKET_DETAILED_WARNINGS", "true")
    printfn "%s> %s %s" workingDir (if isPaket then "paket" else processFilename) processArgs
    let perfMessages = ResizeArray()
    let msgs = ResizeArray<OutputMsg>()
    let mutable perfMessagesStarted = false
    let addAndPrint isError msg =
        if not isError then
            if isPaket && msg = "Performance:" then
                perfMessagesStarted <- true
            elif isPaket && perfMessagesStarted then
                perfMessages.Add(msg)

        msgs.Add({ IsError = isError; Message = msg})

    let result =
        try
            ExecProcessWithLambdas (fun info ->
              info.FileName <- processFilename
              for key, value in env do
                info.EnvironmentVariables.[key] <- value
              info.WorkingDirectory <- workingDir
              info.CreateNoWindow <- true
              info.Arguments <- processArgs)
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

    // always print stderr
    for msg in msgs do
        if msg.IsError then
            printfn "ERR: %s" msg.Message

    if isPaket then
        // Only throw after the result <> 0 check because the current test might check the argument parsing
        // this is the only case where no performance is printed
        let isUsageError = result <> 0 && msgs |> Seq.filter OutputMsg.isError |> Seq.map OutputMsg.getMessage |> Seq.exists (fun msg -> msg.Contains "USAGE:")
        if not isUsageError then
            printfn "Performance:"
            for msg in perfMessages do
                printfn "%s" msg

    if result <> 0 then
        raise <| ProcessFailedWithExitCode(result, processFilename,  { _Messages = msgs })

    msgs
    #endif

let directPaketInPathEx command scenarioPath =
    directToolEx [] true paketToolPath command scenarioPath

let directPaketInPathExWithEnv command scenarioPath env =
    directToolEx env true paketToolPath command scenarioPath


let checkResults msgs =
    msgs
    |> Seq.filter OutputMsg.isError
    |> Seq.toList
    |> shouldEqual []

let directDotnetEx env checkZeroWarn command workingDir =
    let msgs = directToolEx env false ("", dotnetToolPath) command workingDir
    if checkZeroWarn then checkResults msgs
    msgs


let directDotnet checkZeroWarn command workingDir =
    directDotnetEx [] checkZeroWarn command workingDir

let private fromMessages msgs =
    String.Join(Environment.NewLine,msgs |> Seq.map OutputMsg.getMessage)

let directPaketInPath command scenarioPath = directPaketInPathEx command scenarioPath |> fromMessages

let directPaketEx command scenario =
    directPaketInPathEx command (scenarioTempPath scenario)

let directPaket command scenario = directPaketEx command scenario |> fromMessages

let paketEx checkZeroWarn command scenario =
    let cleanup = prepare scenario
    let mutable shouldDispose = cleanup
    try
        let msgs = directPaketEx command scenario
        if checkZeroWarn then checkResults msgs
        shouldDispose <- null
        cleanup, msgs
    finally
        if not (isNull shouldDispose) then shouldDispose.Dispose()

let paket command scenario =
    let mutable shouldDispose = null
    try
        let cleanup, msgs = paketEx false command scenario
        shouldDispose <- cleanup
        let newMsgs = msgs |> fromMessages
        shouldDispose <- null
        cleanup, newMsgs
    finally
        if not (isNull shouldDispose) then shouldDispose.Dispose()

let updateEx checkZeroWarn scenario =
    let mutable shouldDispose = null
    try
        let cleanup = paketEx checkZeroWarn "update" scenario |> fst
        shouldDispose <- cleanup
        let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath scenario,"paket.lock"))
        shouldDispose <- null
        cleanup, lockFile
    finally
        if not (isNull shouldDispose) then shouldDispose.Dispose()

let update scenario =
    updateEx false scenario

let installEx checkZeroWarn scenario =
    #if INTERACTIVE
    paket "install --verbose" scenario |> printfn "%s"
    #else
    paketEx checkZeroWarn  "install" scenario |> fst,
    #endif
    LockFile.LoadFrom(Path.Combine(scenarioTempPath scenario,"paket.lock"))

let install scenario = installEx false scenario

let restore scenario = paketEx false "restore" scenario |> fst

let updateShouldFindPackageConflict packageName scenario =
    try
        use __ = update scenario |> fst
        failwith "No conflict was found."
    with
    | exn when exn.Message.Contains("Conflict detected") && exn.Message.Contains(sprintf "requested package %s" packageName) ->
        #if INTERACTIVE
        printfn "Ninject conflict test passed"
        #endif
        ()

let clearPackage name =
    // ~/.nuget/packages
    let userPackageFolder = Paket.Constants.UserNuGetPackagesFolder

    // %APPDATA%/NuGet/Cache
    let nugetCache = Paket.Constants.NuGetCacheFolder

    for cacheDir in [ nugetCache; userPackageFolder ] do
        if Directory.Exists cacheDir then
            Directory.EnumerateDirectories(cacheDir)
            |> Seq.filter (fun n -> Path.GetFileName n |> String.startsWithIgnoreCase name)
            |> Seq.iter (fun n -> Directory.Delete(n, true))
            Directory.EnumerateFiles(cacheDir)
            |> Seq.filter (fun n -> Path.GetFileName n |> String.startsWithIgnoreCase name)
            |> Seq.iter (fun n -> File.Delete(n))

let isPackageCached name version =
    // ~/.nuget/packages
    let userPackageFolder = Paket.Constants.UserNuGetPackagesFolder

    // %APPDATA%/NuGet/Cache
    let nugetCache = Paket.Constants.NuGetCacheFolder

    [ for cacheDir in [ nugetCache; userPackageFolder ] do
        if Directory.Exists cacheDir then
            yield!
                Directory.EnumerateDirectories(cacheDir)
                |> Seq.filter (fun n -> Path.GetFileName n |> String.equalsIgnoreCase name)
                |> Seq.collect (fun n -> Directory.EnumerateDirectories(n))
                |> Seq.filter (fun n -> Path.GetFileName n |> String.equalsIgnoreCase version)
                |> Seq.toList ]

let clearPackageAtVersion name version =
    isPackageCached name version
    |> List.iter (fun n -> Directory.Delete(n, true))

// Checks if a given package is present in cache ONLY with lowercase naming (see issue #2812)
let isPackageCachedWithOnlyLowercaseNames (name: string) =
    // // ~/.nuget/packages
    let userPackageFolder = Paket.Constants.UserNuGetPackagesFolder

    // // %APPDATA%/NuGet/Cache
    let nugetCache = Paket.Constants.NuGetCacheFolder

    let lowercaseName = name.ToLowerInvariant()

    let packageFolders =
        [ nugetCache; userPackageFolder ]
        |> List.collect (Directory.GetDirectories >> List.ofArray)
        |> List.filter (fun x -> Path.GetFileName x |> String.equalsIgnoreCase name)

    let packageFolderNames = packageFolders |> List.map Path.GetFileName |> List.distinct

    // ensure that names of package directories are lowercase only
    match packageFolderNames with
    | [ x ] when x = lowercaseName ->
        // ensure thet names o package files that start with package name are lowercase only
        let packageFiles =
            packageFolders
            |> Seq.collect Directory.GetDirectories
            |> Seq.collect Directory.GetFiles
        let packageFileNames = packageFiles |> Seq.map Path.GetFileName
        let packageNameSegments =
            packageFileNames
            |> Seq.filter (String.startsWithIgnoreCase <| sprintf "%s." name)
            |> Seq.map (fun x -> x.Substring(0, name.Length))
            |> Seq.distinct
            |> List.ofSeq
        packageNameSegments = [ lowercaseName ]
    | _ -> false

[<AttributeUsage(AttributeTargets.Method, AllowMultiple=false)>]
type FlakyAttribute() = inherit CategoryAttribute()
