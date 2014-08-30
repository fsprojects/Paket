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

let updateLockFile() =
    let cfg = Config.ReadFromFile source

    cfg.Resolve(Nuget.NugetDiscovery)    
    |> LockFile.CreateLockFile lockfile.FullName

    printfn "Lockfile written to %s" lockfile.FullName

match command with
| "install" ->
    if not lockfile.Exists then
        updateLockFile()
| "update" -> updateLockFile()
| _ -> failwith "no command given"