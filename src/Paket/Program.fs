open System
open System.IO
open Nessos.UnionArgParser
open Paket

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

let source = 
    match results.TryGetResult <@ CLIArguments.Package_File @> with
    | Some x -> x
    | _ -> "packages.fsx"

let lockfile =
    let fi = FileInfo(source)
    FileInfo(fi.Directory.FullName + Path.DirectorySeparatorChar.ToString() + fi.Name.Replace(fi.Extension,".lock"))



match command with
| "install" ->
    if not lockfile.Exists then
        LockFile.Update source lockfile.FullName
| "update" -> LockFile.Update source lockfile.FullName
| _ -> failwith "no command given"