/// [omit]
module Paket.Program

open System
open Nessos.UnionArgParser
open Paket.Logging
open System.Diagnostics
open System.Reflection

let private stopWatch = new Stopwatch()
stopWatch.Start()

let assembly = Assembly.GetExecutingAssembly()
let fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
tracefn "Paket version %s" fvi.FileVersion

type Command =
    | Install
    | Update
    | Outdated
    | ConvertFromNuget
    | InitAutoRestore
    | Unknown

type CLIArguments =
    | [<First>][<NoAppSettings>][<CustomCommandLine("install")>] Install
    | [<First>][<NoAppSettings>][<CustomCommandLine("update")>] Update
    | [<First>][<NoAppSettings>][<CustomCommandLine("outdated")>] Outdated
    | [<First>][<NoAppSettings>][<CustomCommandLine("convert-from-nuget")>] ConvertFromNuget
    | [<First>][<NoAppSettings>][<CustomCommandLine("init-auto-restore")>] InitAutoRestore
    | [<AltCommandLine("-v")>] Verbose
    | Dependencies_file of string
    | [<AltCommandLine("-f")>] Force
    | Hard
    | No_install
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Install -> "installs all packages."
            | Update -> "updates the packet.lock ile and installs all packages."
            | Outdated -> "displays information about new packages."
            | ConvertFromNuget -> "converts all projects from NuGet to Paket."
            | InitAutoRestore -> "enables automatic restore for VS"
            | Verbose -> "displays verbose output."
            | Dependencies_file _ -> "specify a file containing dependency definitions."
            | Force -> "forces the download of all packages."
            | Hard -> "overwrites manual package references."
            | No_install -> "omits install --hard after convert-from-nuget"


let parser = UnionArgParser.Create<CLIArguments>("USAGE: paket [install|update|outdated|convert-from-nuget] ... options")
 
let results =
    try
        let results = parser.Parse()
        let command =
            if results.Contains <@ CLIArguments.Install @> then Command.Install
            elif results.Contains <@ CLIArguments.Update @> then Command.Update
            elif results.Contains <@ CLIArguments.Outdated @> then Command.Outdated
            elif results.Contains <@ CLIArguments.ConvertFromNuget @> then Command.ConvertFromNuget
            elif results.Contains <@ CLIArguments.InitAutoRestore @> then Command.InitAutoRestore
            else Command.Unknown
        if results.Contains <@ CLIArguments.Verbose @> then
            verbose <- true

        Some(command,results)
    with
    | _ ->
        tracefn "%s %s%s" (String.Join(" ",Environment.GetCommandLineArgs())) Environment.NewLine (parser.Usage())
        None

try
    match results with
    | Some(command,results) ->
        let dependenciesFile = 
            match results.TryGetResult <@ CLIArguments.Dependencies_file @> with
            | Some x -> x
            | _ -> "paket.dependencies"

        let force = 
            match results.TryGetResult <@ CLIArguments.Force @> with
            | Some _ -> true
            | None -> false

        let hard = 
            match results.TryGetResult <@ CLIArguments.Hard @> with
            | Some _ -> true
            | None -> false

        let installAfterConvert =
            match results.TryGetResult <@ CLIArguments.No_install @> with
            | Some _ -> false
            | None -> true

        match command with
        | Command.Install -> InstallProcess.Install(false,force,hard,dependenciesFile)
        | Command.Update -> InstallProcess.Install(true,force,hard,dependenciesFile)
        | Command.Outdated -> FindOutdated.ListOutdated(dependenciesFile)
        | Command.InitAutoRestore -> VSIntegration.InitAutoRestore()
        | Command.ConvertFromNuget -> NuGetConvert.ConvertFromNuget(force,installAfterConvert)
        | _ -> traceErrorfn "no command given.%s" (parser.Usage())
        
        let ts = stopWatch.Elapsed
        let elapsedTime = String.Format("{0:00}.{1:00}s", ts.Seconds, ts.Milliseconds / 10)

        tracefn "%s - ready." elapsedTime
    | None -> ()
with
| exn -> 
    Environment.ExitCode <- 1
    traceErrorfn "Paket failed with:%s   %s" Environment.NewLine exn.Message

    if verbose then
        traceErrorfn "StackTrace:%s  %s" Environment.NewLine exn.StackTrace
