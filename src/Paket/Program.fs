open System
open Nessos.UnionArgParser
open Paket.Process

type CLIArguments =
    | Package_File of string
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Package_File _ -> "specify a dependency definition."


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

match command with
| "install" -> Install false packageFile
| "update" ->  Install true packageFile
| _ -> failwith "no command given"