/// [omit]
module Paket.Program

open Microsoft.FSharp.Reflection
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

let (|Command|_|) args = 
    let results = 
        UnionArgParser.Create<Command>()
            .Parse(inputs = args,
                   ignoreMissing = true, 
                   ignoreUnrecognized = true, 
                   raiseOnUsage = false)

    match results.GetAllResults() with
    | [ command ] -> Some (command, args.[1..])
    | [] -> None
    | _ -> failwith "expected only one command"


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

let processCommand<'T when 'T :> IArgParserTemplate> (command : Command) args commandF =
    let uci,_ = FSharpValue.GetUnionFields(command, typeof<Command>)
    let commandName = 
        (uci.GetCustomAttributes(typeof<CustomCommandLineAttribute>) 
        |> Seq.head 
        :?> CustomCommandLineAttribute).Name

    let parser = UnionArgParser.Create<'T>()
    let results = 
        parser.Parse(inputs = args, raiseOnUsage = false, ignoreMissing = true, 
                        errorHandler = ProcessExiter())
            
    if results.IsUsageRequested then
        parser.Usage(
                        "Paket " + commandName +
                        Environment.NewLine + Environment.NewLine + 
                        (command :> IArgParserTemplate).Usage + 
                        Environment.NewLine + Environment.NewLine + 
                        HelpTexts.formatSyntax parser ("paket " + commandName)) |> trace
    else
        commandF results

let v, logFile, args = filterGlobalArgs (Environment.GetCommandLineArgs().[1..])

Logging.verbose <- v
Option.iter setLogFile logFile

try
    match args with
    | Command(Add, args) ->
        processCommand<AddArgs> Add args
            (fun results -> 
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
                Dependencies.Locate().Add(packageName, version, force, hard, interactive, noInstall |> not))
        
    | Command(Config, args) ->
        processCommand<ConfigArgs> Config args
            (fun results ->
            let args = results.GetResults <@ ConfigArgs.AddCredentials @> 
            if args.Length = 0 then
                let parser = UnionArgParser.Create<ConfigArgs>()
                parser.Usage(HelpTexts.formatSyntax parser "paket config") |> trace
            else
                let source = args.Item 0
                let username = 
                    if(args.Length > 1) then
                        args.Item 1
                    else
                        ""
                Dependencies.Locate().AddCredentials(source, username))

    | Command(ConvertFromNuget, args) ->
        processCommand<ConvertFromNugetArgs> ConvertFromNuget args
            (fun results ->
            let force = results.Contains <@ ConvertFromNugetArgs.Force @>
            let noInstall = results.Contains <@ ConvertFromNugetArgs.No_Install @>
            let noAutoRestore = results.Contains <@ ConvertFromNugetArgs.No_Auto_Restore @>
            let credsMigrationMode = results.TryGetResult <@ ConvertFromNugetArgs.Creds_Migration @>
            Dependencies.ConvertFromNuget(force, noInstall |> not, noAutoRestore |> not, credsMigrationMode))
    
    | Command(FindRefs, args) ->
         processCommand<FindRefsArgs> FindRefs args
            (fun results ->
            let packages = results.GetResults <@ FindRefsArgs.Packages @>
            Dependencies.Locate().ShowReferencesFor(packages))
        
    | Command(Init, args) ->
        processCommand<InitArgs> Init args
            (fun results ->
            Dependencies.Init())

    | Command(AutoRestore, args) ->
        processCommand<AutoRestoreArgs> AutoRestore args
            (fun results -> 
            match results.GetAllResults() with
            | [On] -> Dependencies.Locate().TurnOnAutoRestore()
            | [Off] -> Dependencies.Locate().TurnOffAutoRestore()
            | _ ->
                let parser = UnionArgParser.Create<AutoRestoreArgs>()
                parser.Usage(HelpTexts.formatSyntax parser "paket auto-restore") |> trace)

    | Command(Install, args) ->
        processCommand<InstallArgs> Install args
            (fun results -> 
                let force = results.Contains <@ InstallArgs.Force @>
                let hard = results.Contains <@ InstallArgs.Hard @>
                let withBindingRedirects = results.Contains <@ InstallArgs.Redirects @>
                Dependencies.Locate().Install(force,hard,withBindingRedirects))

    | Command(Outdated, args) ->
        processCommand<OutdatedArgs> Outdated args
            (fun results -> 
            let strict = results.Contains <@ OutdatedArgs.Ignore_Constraints @> |> not
            let includePrereleases = results.Contains <@ OutdatedArgs.Include_Prereleases @>
            Dependencies.Locate().ShowOutdated(strict,includePrereleases))

    | Command(Remove, args) ->
        processCommand<RemoveArgs> Remove args
            (fun results -> 
            let packageName = results.GetResult <@ RemoveArgs.Nuget @>
            let force = results.Contains <@ RemoveArgs.Force @>
            let hard = results.Contains <@ RemoveArgs.Hard @>
            let noInstall = results.Contains <@ RemoveArgs.No_Install @>
            match results.TryGetResult <@ RemoveArgs.Project @> with
            | Some projectName ->
                Dependencies.Locate().RemoveFromProject(packageName, force, hard, projectName, noInstall |> not)
            | None ->
                let interactive = results.Contains <@ RemoveArgs.Interactive @>
                Dependencies.Locate().Remove(packageName, force, hard, interactive, noInstall |> not))

    | Command(Restore, args) ->
        processCommand<RestoreArgs> Restore args
            (fun results -> 
            let force = results.Contains <@ RestoreArgs.Force @>
            let files = results.GetResults <@ RestoreArgs.References_Files @> 
            Dependencies.Locate().Restore(force,files))

    | Command(Simplify, args) ->
        processCommand<SimplifyArgs> Simplify args
            (fun results -> 
            let interactive = results.Contains <@ SimplifyArgs.Interactive @>
            Dependencies.Simplify(interactive))

    | Command(Update, args) ->
        processCommand<UpdateArgs> Update args
            (fun results -> 
            let hard = results.Contains <@ UpdateArgs.Hard @>
            let force = results.Contains <@ UpdateArgs.Force @>
            match results.TryGetResult <@ UpdateArgs.Nuget @> with
            | Some packageName -> 
                let version = results.TryGetResult <@ UpdateArgs.Version @>
                Dependencies.Locate().UpdatePackage(packageName, version, force, hard)
            | _ -> 
                let withBindingRedirects = results.Contains <@ UpdateArgs.Redirects @>
                Dependencies.Locate().Update(force,hard,withBindingRedirects))
    | Command(Pack, args) ->
        processCommand<PackArgs> Pack args
            (fun results -> 
            let outputPath = results.GetResult <@ PackArgs.Output @>            
            Dependencies.Locate().Pack(
                outputPath, 
                ?buildConfig = results.TryGetResult <@ PackArgs.BuildConfig @>,
                ?version = results.TryGetResult <@ PackArgs.Version @>,
                ?releaseNotes = results.TryGetResult <@ PackArgs.ReleaseNotes @>))
    | Command(Push, args) ->
        processCommand<PushArgs> Push args
            (fun results -> 
            let fileName = results.GetResult <@ PushArgs.FileName @>
            Dependencies.Locate().Push(
                fileName, 
                ?url = results.TryGetResult <@ PushArgs.Url @>, 
                ?apiKey = results.TryGetResult <@ PushArgs.ApiKey @>))
    | _ ->
        let parser = UnionArgParser.Create<Command>()
        parser.Usage("available commands:") |> trace

    let elapsedTime = Utils.TimeSpanToReadableString stopWatch.Elapsed
    tracefn "%s - ready." elapsedTime
with
| exn when not (exn :? System.NullReferenceException) -> 
    Environment.ExitCode <- 1
    traceErrorfn "Paket failed with:%s\t%s" Environment.NewLine exn.Message

    if verbose then
        traceErrorfn "StackTrace:%s  %s" Environment.NewLine exn.StackTrace
