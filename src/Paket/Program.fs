/// [omit]
module Paket.Program

open System
open Nessos.UnionArgParser

type Command =
    | Install
    | Update
    | Outdated
    | ConvertFromNuget
    | Unknown

type CLIArguments =
    | [<First>][<NoAppSettings>][<CustomCommandLine("install")>] Install
    | [<First>][<NoAppSettings>][<CustomCommandLine("update")>] Update
    | [<First>][<NoAppSettings>][<CustomCommandLine("outdated")>] Outdated
    | [<First>][<NoAppSettings>][<CustomCommandLine("convert-from-nuget")>] ConvertFromNuget
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
            | Verbose -> "displays verbose output."
            | Dependencies_file _ -> "specify a file containing dependency definitions."
            | Force -> "forces the download of all packages."
            | Hard -> "overwrites manual package references."
            | No_install -> "omits install --hard after convert-from-nuget"


let parser = UnionArgParser.Create<CLIArguments>("USAGE: paket [install|update|outdated|convert-from-nuget] ... options")
 
let results,verbose =
    try
        let results = parser.Parse()
        let command =
            if results.Contains <@ CLIArguments.Install @> then Command.Install
            elif results.Contains <@ CLIArguments.Update @> then Command.Update
            elif results.Contains <@ CLIArguments.Outdated @> then Command.Outdated
            elif results.Contains <@ CLIArguments.ConvertFromNuget @> then Command.ConvertFromNuget
            else Command.Unknown
        Some(command,results),results.Contains <@ CLIArguments.Verbose @>
    with
    | _ ->
        tracefn "%s %s%s" (String.Join(" ",Environment.GetCommandLineArgs())) Environment.NewLine (parser.Usage())
        None,false

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
        | Command.Install -> Process.Install(false,force,hard,dependenciesFile)
        | Command.Update -> Process.Install(true,force,hard,dependenciesFile)
        | Command.Outdated -> Process.ListOutdated(dependenciesFile)
        | Command.ConvertFromNuget -> Process.ConvertFromNuget(force,installAfterConvert)
        | _ -> failwithf "no command given.%s" (parser.Usage())
        
        tracefn "Ready."
    | None -> ()
with
| exn -> 
    Environment.ExitCode <- 1
    traceErrorfn "Paket failed with:%s   %s" Environment.NewLine exn.Message

    if verbose then
        traceErrorfn "StackTrace:%s  %s" Environment.NewLine exn.StackTrace
