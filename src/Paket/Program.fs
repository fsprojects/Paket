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
    | Add
    | Install
    | Update
    | Outdated
    | ConvertFromNuget
    | InitAutoRestore
    | Simplify
    | Unknown

type CLIArguments =
    | [<First>][<NoAppSettings>][<CustomCommandLine("add")>] Add
    | [<First>][<NoAppSettings>][<CustomCommandLine("install")>] Install
    | [<First>][<NoAppSettings>][<CustomCommandLine("update")>] Update
    | [<First>][<NoAppSettings>][<CustomCommandLine("outdated")>] Outdated
    | [<First>][<NoAppSettings>][<CustomCommandLine("convert-from-nuget")>] ConvertFromNuget
    | [<First>][<NoAppSettings>][<CustomCommandLine("init-auto-restore")>] InitAutoRestore
    | [<First>][<NoAppSettings>][<CustomCommandLine("simplify")>] Simplify
    | [<AltCommandLine("-v")>] Verbose
    | Dependencies_file of string
    | [<AltCommandLine("-i")>] Interactive
    | [<AltCommandLine("-f")>] Force
    | Hard
    | [<CustomCommandLine("nuget")>] Nuget of string
    | [<CustomCommandLine("version")>] Version of string
    | No_install
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Add -> "adds a package to the dependencies."
            | Install -> "installs all packages."
            | Update -> "updates the packet.lock file and installs all packages."
            | Outdated -> "displays information about new packages."
            | ConvertFromNuget -> "converts all projects from NuGet to Paket."
            | InitAutoRestore -> "enables automatic restore for Visual Studio."
            | Simplify -> "analyzes dependencies and removes unnecessary indirect dependencies."
            | Verbose -> "displays verbose output."
            | Dependencies_file _ -> "specify a file containing dependency definitions."
            | Force -> "forces the download of all packages."
            | Interactive -> "interactive process."
            | Hard -> "overwrites manual package references."
            | No_install -> "omits install --hard after convert-from-nuget."
            | Nuget _ -> "allows to specify a nuget package."
            | Version _ -> "allows to specify a package version."

let parser = UnionArgParser.Create<CLIArguments>("USAGE: paket [add|install|update|outdated|convert-from-nuget|init-auto-restore|simplify] ... options")
 
let results =
    try
        let results = parser.Parse()
        let command = 
            if results.Contains <@ CLIArguments.Add @> then Command.Add
            elif results.Contains <@ CLIArguments.Install @> then Command.Install
            elif results.Contains <@ CLIArguments.Update @> then Command.Update
            elif results.Contains <@ CLIArguments.Outdated @> then Command.Outdated
            elif results.Contains <@ CLIArguments.ConvertFromNuget @> then Command.ConvertFromNuget
            elif results.Contains <@ CLIArguments.InitAutoRestore @> then Command.InitAutoRestore
            elif results.Contains <@ CLIArguments.Simplify @> then Command.Simplify
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
        let dependenciesFileName = 
            match results.TryGetResult <@ CLIArguments.Dependencies_file @> with
            | Some x -> x
            | _ -> Constants.DependenciesFile

        let force = results.Contains <@ CLIArguments.Force @> 
        let interactive = results.Contains <@ CLIArguments.Interactive @> 
        let hard = results.Contains <@ CLIArguments.Hard @> 
        let noInstall = results.Contains <@ CLIArguments.No_install @>

        match command with
        | Command.Add -> 
            let packageName =  results.GetResult <@ CLIArguments.Nuget @>
            let version = 
                match results.TryGetResult <@ CLIArguments.Version @> with
                | Some x -> x
                | _ -> ""
            AddProcess.Add(packageName,version,force,hard,interactive,noInstall |> not,dependenciesFileName)
        | Command.Install -> UpdateProcess.Update(dependenciesFileName,false,force,hard) 
        | Command.Update -> UpdateProcess.Update(dependenciesFileName,true,force,hard)
        | Command.Outdated -> FindOutdated.ListOutdated(dependenciesFileName)
        | Command.InitAutoRestore -> VSIntegration.InitAutoRestore()
        | Command.ConvertFromNuget -> NuGetConvert.ConvertFromNuget(force,noInstall |> not,dependenciesFileName)
        | Command.Simplify -> Simplifier.Simplify(interactive,dependenciesFileName)
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
