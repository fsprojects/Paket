/// [omit]
module Paket.Program

open System
open System.Diagnostics
open System.Reflection
open System.IO

open Paket.Logging
open Paket.Commands

open Nessos.UnionArgParser

let private stopWatch = new Stopwatch()
stopWatch.Start()

let assembly = Assembly.GetExecutingAssembly()
let fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
tracefn "Paket version %s" fvi.FileVersion

let filterGlobalArgs args = 
    let globalResults = 
        UnionArgParser.Create<GlobalArgs>()
            .Parse(ignoreMissing = true, 
                   ignoreUnrecognized = true, 
                   raiseOnUsage = false)
    let verbose = globalResults.Contains <@ GlobalArgs.Verbose @>
    let logFile = globalResults.TryGetResult <@ GlobalArgs.Log_File @>
    
    let rest = 
        match logFile with
        | Some file -> 
            args |> Array.filter (fun a -> a <> "--log-file" && a <> file)
        | None -> args
    
    let rest = 
        if verbose then 
            rest |> Array.filter (fun a -> a <> "-v" && a <> "--verbose")
        else rest
    
    verbose, logFile, rest

let processWithValidation<'T when 'T :> IArgParserTemplate> validateF commandF command 
    args = 
    let parser = UnionArgParser.Create<'T>()
    let results = 
        parser.Parse
            (inputs = args, raiseOnUsage = false, ignoreMissing = true, 
             errorHandler = ProcessExiter())
    let resultsValid = validateF (results)
    if results.IsUsageRequested || not resultsValid then 
        parser.Usage(Commands.cmdLineUsageMessage command parser) |> trace
    else 
        commandF results
        let elapsedTime = Utils.TimeSpanToReadableString stopWatch.Elapsed
        tracefn "%s - ready." elapsedTime

let processCommand<'T when 'T :> IArgParserTemplate> (commandF : ArgParseResults<'T> -> unit) = 
    processWithValidation (fun _ -> true) commandF 

let v, logFile, args = filterGlobalArgs (Environment.GetCommandLineArgs().[1..])

Logging.verbose <- v
Option.iter setLogFile logFile

let add (results : ArgParseResults<_>) =
    let packageName = results.GetResult <@ AddArgs.Nuget @>
    let version = defaultArg (results.TryGetResult <@ AddArgs.Version @>) ""
    let force = results.Contains <@ AddArgs.Force @>
    let hard = results.Contains <@ AddArgs.Hard @>
    let noInstall = results.Contains <@ AddArgs.No_Install @>
    match results.TryGetResult <@ AddArgs.Project @> with
    | Some projectName ->
        Dependencies.Locate().AddToProject(packageName, version, force, hard, projectName, noInstall |> not)
    | None ->
        let interactive = results.Contains <@ AddArgs.Interactive @>
        Dependencies.Locate().Add(packageName, version, force, hard, interactive, noInstall |> not)

let validateConfig (results : ArgParseResults<_>) =
    let args = results.GetResults <@ ConfigArgs.AddCredentials @> 
    args.Length > 0

let config (results : ArgParseResults<_>) =
    let args = results.GetResults <@ ConfigArgs.AddCredentials @> 
    let source = args.Item 0
    let username = 
        if(args.Length > 1) then
            args.Item 1
        else
            ""
    Dependencies.Locate().AddCredentials(source, username)

let validateAutoRestore (results : ArgParseResults<_>) =
    results.GetAllResults().Length = 1

let autoRestore (results : ArgParseResults<_>) =
    match results.GetAllResults() with
    | [On] -> Dependencies.Locate().TurnOnAutoRestore()
    | [Off] -> Dependencies.Locate().TurnOffAutoRestore()
    | _ -> failwith "expected only one argument"

let convert (results : ArgParseResults<_>) =
    let force = results.Contains <@ ConvertFromNugetArgs.Force @>
    let noInstall = results.Contains <@ ConvertFromNugetArgs.No_Install @>
    let noAutoRestore = results.Contains <@ ConvertFromNugetArgs.No_Auto_Restore @>
    let credsMigrationMode = results.TryGetResult <@ ConvertFromNugetArgs.Creds_Migration @>
    Dependencies.ConvertFromNuget(force, noInstall |> not, noAutoRestore |> not, credsMigrationMode)
    
let findRefs (results : ArgParseResults<_>) =
    let packages = results.GetResults <@ FindRefsArgs.Packages @>
    Dependencies.Locate().ShowReferencesFor(packages)
        
let init (results : ArgParseResults<InitArgs>) =
    Dependencies.Init()

let install (results : ArgParseResults<_>) = 
    let force = results.Contains <@ InstallArgs.Force @>
    let hard = results.Contains <@ InstallArgs.Hard @>
    let withBindingRedirects = results.Contains <@ InstallArgs.Redirects @>
    Dependencies.Locate().Install(force, hard, withBindingRedirects)

let outdated (results : ArgParseResults<_>) = 
    let strict = results.Contains <@ OutdatedArgs.Ignore_Constraints @> |> not
    let includePrereleases = results.Contains <@ OutdatedArgs.Include_Prereleases @>
    Dependencies.Locate().ShowOutdated(strict, includePrereleases)

let remove (results : ArgParseResults<_>) = 
    let packageName = results.GetResult <@ RemoveArgs.Nuget @>
    let force = results.Contains <@ RemoveArgs.Force @>
    let hard = results.Contains <@ RemoveArgs.Hard @>
    let noInstall = results.Contains <@ RemoveArgs.No_Install @>
    match results.TryGetResult <@ RemoveArgs.Project @> with
    | Some projectName -> 
        Dependencies.Locate()
                    .RemoveFromProject(packageName, force, hard, projectName, noInstall |> not)
    | None -> 
        let interactive = results.Contains <@ RemoveArgs.Interactive @>
        Dependencies.Locate().Remove(packageName, force, hard, interactive, noInstall |> not)

let restore (results : ArgParseResults<_>) = 
    let force = results.Contains <@ RestoreArgs.Force @>
    let files = results.GetResults <@ RestoreArgs.References_Files @>
    Dependencies.Locate().Restore(force, files)

let simplify (results : ArgParseResults<_>) = 
    let interactive = results.Contains <@ SimplifyArgs.Interactive @>
    Dependencies.Simplify(interactive)

let update (results : ArgParseResults<_>) = 
    let hard = results.Contains <@ UpdateArgs.Hard @>
    let force = results.Contains <@ UpdateArgs.Force @>
    match results.TryGetResult <@ UpdateArgs.Nuget @> with
    | Some packageName -> 
        let version = results.TryGetResult <@ UpdateArgs.Version @>
        Dependencies.Locate().UpdatePackage(packageName, version, force, hard)
    | _ -> 
        let withBindingRedirects = results.Contains <@ UpdateArgs.Redirects @>
        Dependencies.Locate().Update(force, hard, withBindingRedirects)

let pack (results : ArgParseResults<_>) = 
    let outputPath = results.GetResult <@ PackArgs.Output @>
    Dependencies.Locate()
                .Pack(outputPath, ?buildConfig = results.TryGetResult <@ PackArgs.BuildConfig @>, 
                      ?version = results.TryGetResult <@ PackArgs.Version @>, 
                      ?releaseNotes = results.TryGetResult <@ PackArgs.ReleaseNotes @>)

let push (results : ArgParseResults<_>) = 
    let fileName = results.GetResult <@ PushArgs.FileName @>
    Dependencies.Locate()
                .Push(fileName, ?url = results.TryGetResult <@ PushArgs.Url @>, 
                      ?endPoint = results.TryGetResult <@ PushArgs.EndPoint @>,
                      ?apiKey = results.TryGetResult <@ PushArgs.ApiKey @>)

try
    let parser = UnionArgParser.Create<Command>()
    let results = 
        parser.Parse(inputs = args,
                   ignoreMissing = true, 
                   ignoreUnrecognized = true, 
                   raiseOnUsage = false)

    match results.GetAllResults() with
    | [ command ] -> 
        let handler =
            match command with
            | Add -> processCommand add
            | Config -> processWithValidation validateConfig config
            | ConvertFromNuget -> processCommand convert
            | FindRefs -> processCommand findRefs
            | Init -> processCommand init
            | AutoRestore -> processWithValidation validateAutoRestore autoRestore
            | Install -> processCommand install
            | Outdated -> processCommand outdated
            | Remove -> processCommand remove
            | Restore -> processCommand restore
            | Simplify -> processCommand simplify
            | Update -> processCommand update
            | Pack -> processCommand pack
            | Push -> processCommand push

        let args = args.[1..]
        handler command args
    | [] -> 
        parser.Usage("available commands:") |> trace
    | _ -> failwith "expected only one command"
with
| exn when not (exn :? System.NullReferenceException) -> 
    Environment.ExitCode <- 1
    traceErrorfn "Paket failed with:%s\t%s" Environment.NewLine exn.Message

    if verbose then
        traceErrorfn "StackTrace:%s  %s" Environment.NewLine exn.StackTrace