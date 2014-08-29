open System
open System.IO
open Nessos.UnionArgParser
open Paket

type CLIArguments =
    | [<AltCommandLine("-s")>] Source of string
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Source _ -> "specify a dependency definition."


let parser = UnionArgParser.Create<CLIArguments>()
 
let cmdArgs = System.Environment.GetCommandLineArgs()

let command,results =
    try
        cmdArgs.[1],parser.Parse(cmdArgs.[2..])
    with
    | _ -> 
         failwithf "Paket.exe%s%s" Environment.NewLine (parser.Usage() )

match command with
| "install" ->         
    let source = 
        match results.TryGetResult <@ CLIArguments.Source @> with
        | Some x -> x
        | _ -> "packages.fsx"

    let lockfile =
        let fi = FileInfo(source)
        fi.Directory.FullName + Path.DirectorySeparatorChar.ToString() + fi.Name.Replace(fi.Extension,".lock")

    let cfg = Config.ReadFromFile source

    cfg.Resolve(Nuget.NugetDiscovery).DirectDependencies
    |> LockFile.CreateLockFile lockfile

    printfn "Lockfile written to %s" lockfile
| _ -> failwith "no command given"