/// [omit]
module Paket.Program

open System
open Nessos.UnionArgParser

type CLIArguments =
    | Package_File of string
    | Force
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Package_File _ -> "specify a dependency definition."
            | Force -> "specify a dependency definition."


let parser = UnionArgParser.Create<CLIArguments>()
 
let cmdArgs = System.Environment.GetCommandLineArgs()

let command,results =
    try
        cmdArgs.[1],parser.Parse(cmdArgs.[2..])
    with
    | _ -> 
         failwithf "Paket.exe%s%s" Environment.NewLine (parser.Usage() )

let packageFile = 
    match results.TryGetResult <@ CLIArguments.Package_File @> with
    | Some x -> x
    | _ -> "packages.fsx"

let force = 
    match results.TryGetResult <@ CLIArguments.Force @> with
    | Some _ -> true
    | None -> false

match command with
| "install" -> Process.Install(false,force,packageFile)
| "update" ->  Process.Install(true,force,packageFile)
| "outdated" ->  Process.ListOutdated(packageFile)
| _ -> failwith "no command given"
|> ignore