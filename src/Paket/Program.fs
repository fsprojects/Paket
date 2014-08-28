open System
open System.IO
open Nessos.UnionArgParser
open Paket

type CLIArguments =
    | [<AltCommandLine("-s")>] Source of string
    | [<AltCommandLine("-lf")>] LockFile of string

with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Source _ -> "specify a dependency definition."
            | LockFile _ -> "specify a lockfile name."


let parser = UnionArgParser.Create<CLIArguments>()
 
let cmdArgs = System.Environment.GetCommandLineArgs()

let results =
    try
         parser.Parse(cmdArgs.[1..])
    with
    | _ -> 
         failwithf "Paket.exe%s%s" Environment.NewLine (parser.Usage() )
         
let source = results.GetResult <@ CLIArguments.Source @> 
let lockfile =
    match results.TryGetResult <@ CLIArguments.LockFile @> with
    | Some x -> x
    | _ -> 
        let fi = FileInfo(source)
        fi.Directory.FullName + Path.DirectorySeparatorChar.ToString() + fi.Name.Replace(fi.Extension,".lock")

let cfg = Config.ReadFromFile source

cfg.Resolve(Nuget.NugetDiscovery).DirectDependencies
|> LockFile.CreateLockFile lockfile
