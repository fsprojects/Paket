/// [omit]
module Paket.Program

open System
open Nessos.UnionArgParser

type Command =
    | Install
    | Update
    | Outdated
    | Unkown

type CLIArguments =
    | [<First>][<NoAppSettings>][<CustomCommandLine("install")>] Install
    | [<First>][<NoAppSettings>][<CustomCommandLine("update")>] Update
    | [<First>][<NoAppSettings>][<CustomCommandLine("outdated")>] Outdated
    | Dependencies_File of string
    | [<AltCommandLine("-f")>] Force
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Install -> "installs all packages."
            | Update -> "updates the lockfile and installs all packages."
            | Outdated -> "displays information about new packages."
            | Dependencies_File _ -> "specify a dependency definition."
            | Force -> "specify a dependency definition."


let parser = UnionArgParser.Create<CLIArguments>("USAGE: paket [install|update|outdated] ... options")
 
let cmdArgs = Environment.GetCommandLineArgs()

let results =
    try
        let results = parser.Parse(cmdArgs.[1..])
        let command =
            if results.Contains <@ CLIArguments.Install @> then Command.Install
            elif results.Contains <@ CLIArguments.Update @> then Command.Update
            elif results.Contains <@ CLIArguments.Outdated @> then Command.Outdated
            else Command.Unkown
        Some(command,results)
    with
    | _ ->
        tracefn "Paket.exe%s%s" Environment.NewLine (parser.Usage())
        None

try
    match results with
    | Some(command,results) ->
        let packageFile = 
            match results.TryGetResult <@ CLIArguments.Dependencies_File @> with
            | Some x -> x
            | _ -> "Dependencies"

        let force = 
            match results.TryGetResult <@ CLIArguments.Force @> with
            | Some _ -> true
            | None -> false

        match command with
        | Command.Install -> Process.Install(false,force,packageFile)
        | Command.Update -> Process.Install(true,force,packageFile)
        | Command.Outdated -> Process.ListOutdated(packageFile)
        | _ -> failwithf "no command given.%s" (parser.Usage())
        |> ignore
    | None -> ()
with
| exn -> 
    Environment.ExitCode <- 1
    traceError exn.Message