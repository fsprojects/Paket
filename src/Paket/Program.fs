/// [omit]
module Paket.Program

open System
open Nessos.UnionArgParser
open System.IO

type Command =
    | Install
    | Update
    | Outdated
    | Unkown

type CLIArguments =
    | [<First>][<NoAppSettings>][<CustomCommandLine("install")>] Install
    | [<First>][<NoAppSettings>][<CustomCommandLine("update")>] Update
    | [<First>][<NoAppSettings>][<CustomCommandLine("outdated")>] Outdated
    | [<AltCommandLine("-v")>] Verbose
    | dependencies_file of string
    | [<AltCommandLine("-f")>] Force
    | Hard
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Install -> "installs all packages."
            | Update -> "updates the packet.lock ile and installs all packages."
            | Outdated -> "displays information about new packages."
            | Verbose -> "displays verbose output."
            | dependencies_file _ -> "specify a file containing dependency definitions."
            | Force -> "forces the download of all packages."
            | Hard -> "overwrites manual package references."


let parser = UnionArgParser.Create<CLIArguments>("USAGE: paket [install|update|outdated] ... options")
 
let results,verbose =
    try
        let results = parser.Parse()
        let command =
            if results.Contains <@ CLIArguments.Install @> then Command.Install
            elif results.Contains <@ CLIArguments.Update @> then Command.Update
            elif results.Contains <@ CLIArguments.Outdated @> then Command.Outdated
            else Command.Unkown
        Some(command,results),results.Contains <@ CLIArguments.Verbose @>
    with
    | _ ->
        tracefn "%s %s%s" (String.Join(" ",Environment.GetCommandLineArgs())) Environment.NewLine (parser.Usage())
        None,false

try
    match results with
    | Some(command,results) ->
        let dependenciesFile = 
            match results.TryGetResult <@ CLIArguments.dependencies_file @> with
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

        match command with
        | Command.Install -> Process.Install(false,force,hard,dependenciesFile)
        | Command.Update -> Process.Install(true,force,hard,dependenciesFile)
        | Command.Outdated -> Process.ListOutdated(dependenciesFile)
        | _ -> failwithf "no command given.%s" (parser.Usage())
        
        tracefn "Ready."
    | None -> ()
with
| exn -> 
    Environment.ExitCode <- 1
    traceErrorfn "Paket failed with:%s   %s" Environment.NewLine exn.Message

    if verbose then
        traceErrorfn "StackTrace:%s  %s" Environment.NewLine exn.StackTrace
