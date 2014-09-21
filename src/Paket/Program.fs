/// [omit]
module Paket.Program

open System
open Nessos.UnionArgParser
open Paket.Logging
open System.Diagnostics
open System.Reflection
open System.IO

let private stopWatch = new Stopwatch()
stopWatch.Start()

let assembly = Assembly.GetExecutingAssembly()
let fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
tracefn "Paket version %s" fvi.FileVersion

type Command =
    | Add
    | Install
    | Update
    | Outdated
    | ConvertFromNuget
    | InitAutoRestore
    | Unknown

type CLIArguments =
    | [<First>][<NoAppSettings>][<CustomCommandLine("add")>] Add
    | [<First>][<NoAppSettings>][<CustomCommandLine("install")>] Install
    | [<First>][<NoAppSettings>][<CustomCommandLine("update")>] Update
    | [<First>][<NoAppSettings>][<CustomCommandLine("outdated")>] Outdated
    | [<First>][<NoAppSettings>][<CustomCommandLine("convert-from-nuget")>] ConvertFromNuget
    | [<First>][<NoAppSettings>][<CustomCommandLine("init-auto-restore")>] InitAutoRestore
    | [<AltCommandLine("-v")>] Verbose
    | Dependencies_file of string
    | [<AltCommandLine("-f")>] Force
    | Hard
    | No_install
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Add -> "adds a package to the depedencies."
            | Install -> "installs all packages."
            | Update -> "updates the packet.lock ile and installs all packages."
            | Outdated -> "displays information about new packages."
            | ConvertFromNuget -> "converts all projects from NuGet to Paket."
            | InitAutoRestore -> "enables automatic restore for VS"
            | Verbose -> "displays verbose output."
            | Dependencies_file _ -> "specify a file containing dependency definitions."
            | Force -> "forces the download of all packages."
            | Hard -> "overwrites manual package references."
            | No_install -> "omits install --hard after convert-from-nuget"


let parser = UnionArgParser.Create<CLIArguments>("USAGE: paket [add|install|update|outdated|convert-from-nuget] ... options")
 
let results =
    try
        let results = parser.Parse()
        let command =
            if results.Contains <@ CLIArguments.Install @> then Command.Install
            elif results.Contains <@ CLIArguments.Update @> then Command.Update
            elif results.Contains <@ CLIArguments.Outdated @> then Command.Outdated
            elif results.Contains <@ CLIArguments.ConvertFromNuget @> then Command.ConvertFromNuget
            elif results.Contains <@ CLIArguments.InitAutoRestore @> then Command.InitAutoRestore
            else Command.Unknown
        if results.Contains <@ CLIArguments.Verbose @> then
            verbose <- true

        Some(command,results)
    with
    | _ ->
        tracefn "%s %s%s" (String.Join(" ",Environment.GetCommandLineArgs())) Environment.NewLine (parser.Usage())
        None

try
    match results with
    | Some(command,results) ->
        let dependenciesFileName = 
            match results.TryGetResult <@ CLIArguments.Dependencies_file @> with
            | Some x -> x
            | _ -> "paket.dependencies"

        let force = results.Contains <@ CLIArguments.Force @> 
        let hard = results.Contains <@ CLIArguments.Hard @> 
        let installAfterConvert = results.Contains <@ CLIArguments.No_install @> 

        match command with
        | Command.Add -> AddProcess.Add(force,hard,dependenciesFileName)
        | Command.Install -> 
            let resolution = DependencyResolution.Analyze(dependenciesFileName,force)            
            let lockFileName = resolution.DependenciesFile.FindLockfile()
            
            let lockFile = 
                if not lockFileName.Exists then 
                    let lockFile = LockFile(lockFileName.FullName,resolution.DependenciesFile.Strict,resolution)
                    lockFile.Save()
                    lockFile
                else
                    LockFile.LoadFrom resolution.DependenciesFile

            InstallProcess.Install(force,hard,lockFile)
        | Command.Update -> 
            let resolution = DependencyResolution.Analyze(dependenciesFileName,force)
            let lockFileName = resolution.DependenciesFile.FindLockfile()
            let lockFile =             
                let lockFile = LockFile(lockFileName.FullName,resolution.DependenciesFile.Strict,resolution)
                lockFile.Save()
                lockFile
            InstallProcess.Install(force,hard,lockFile)
        | Command.Outdated -> FindOutdated.ListOutdated(dependenciesFileName)
        | Command.InitAutoRestore -> VSIntegration.InitAutoRestore()
        | Command.ConvertFromNuget -> NuGetConvert.ConvertFromNuget(force,installAfterConvert)
        | _ -> traceErrorfn "no command given.%s" (parser.Usage())
        
        let ts = stopWatch.Elapsed
        let elapsedTime = String.Format("{0:00}.{1:00}s", ts.Seconds, ts.Milliseconds / 10)

        tracefn "%s - ready." elapsedTime
    | None -> ()
with
| exn -> 
    Environment.ExitCode <- 1
    traceErrorfn "Paket failed with:%s   %s" Environment.NewLine exn.Message

    if verbose then
        traceErrorfn "StackTrace:%s  %s" Environment.NewLine exn.StackTrace
