/// [omit]
module Paket.Program

open System
open Nessos.UnionArgParser

type CLIArguments =
    | [<First>] Command of string
    | Package_File of string
    | Force
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Command _ -> "specify a command."
            | Package_File _ -> "specify a dependency definition."
            | Force -> "specify a dependency definition."


let parser = UnionArgParser.Create<CLIArguments>()
 
let cmdArgs = System.Environment.GetCommandLineArgs()

let results =
    try
        Some(parser.Parse(cmdArgs.[1..]))
    with
    | exn ->
        traceErrorfn "%s" exn.Message
        None

match results with
| Some(results) ->
    let packageFile = 
        match results.TryGetResult <@ CLIArguments.Package_File @> with
        | Some x -> x
        | _ -> "packages.fsx"

    let force = 
        match results.TryGetResult <@ CLIArguments.Force @> with
        | Some _ -> true
        | None -> false

    match results.GetResult <@ CLIArguments.Command @> with
    | "install" -> Process.Install(false,force,packageFile)
    | "update" ->  Process.Install(true,force,packageFile)
    | "outdated" ->  Process.ListOutdated(packageFile)
    | x -> 
        traceErrorfn "%s is not valid command" x
        tracefn "Paket.exe%s%s" Environment.NewLine (parser.Usage())
    |> ignore
| None -> ()
