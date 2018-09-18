/// [omit]
module Paket.Program

open System
open System.Diagnostics
open System.IO

open Paket.Logging
open Paket.Commands

open Argu
open PackageSources


let waitForDebugger () =
    while not(System.Diagnostics.Debugger.IsAttached) do
        System.Threading.Thread.Sleep(100)

let main() =
    let waitDebuggerEnvVar = Environment.GetEnvironmentVariable ("PAKET_WAIT_DEBUGGER")
    if waitDebuggerEnvVar = "1" then
        waitForDebugger()

    let resolution = Environment.GetEnvironmentVariable ("PAKET_DISABLE_RUNTIME_RESOLUTION")
    Logging.verboseWarnings <- Environment.GetEnvironmentVariable "PAKET_DETAILED_WARNINGS" = "true"
    if System.String.IsNullOrEmpty resolution then
        Environment.SetEnvironmentVariable ("PAKET_DISABLE_RUNTIME_RESOLUTION", "true")
    use consoleTrace = Logging.event.Publish |> Observable.subscribe Logging.traceToConsole

    try
        let args = Environment.GetCommandLineArgs()
        Paket.CliRunner.runCli args
    with
    | exn when not (exn :? System.NullReferenceException) ->
        Environment.ExitCode <- 1
        traceErrorfn "Paket failed with"
        if Environment.GetEnvironmentVariable "PAKET_DETAILED_ERRORS" = "true" then
            printErrorExt true true true exn
        else printError exn

main()
