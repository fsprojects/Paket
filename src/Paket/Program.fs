open System
open Nessos.UnionArgParser
open Paket

type CLIArguments =
    | [<AltCommandLine("-s")>] Source of string

with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Source _ -> "specify a dependency definition"


let parser = UnionArgParser.Create<CLIArguments>()
 
let cmdArgs = System.Environment.GetCommandLineArgs()

let results =
    try
         parser.Parse(cmdArgs.[1..])
    with
    | _ -> 
         failwithf "Paket.exe%s%s" Environment.NewLine (parser.Usage() )
         
let source = results.GetResult <@ CLIArguments.Source @> 
let target = "packages.lock"
let cfg = Config.ReadFromFile source

cfg.Resolve(Nuget.NugetDiscovery).DirectDependencies
|> LockFile.CreateLockFile target
