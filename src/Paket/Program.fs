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

    
let commandArgs<'T when 'T :> IArgParserTemplate> args = 
    UnionArgParser.Create<'T>()
        .Parse(inputs = args, raiseOnUsage = false, ignoreMissing = true, 
               errorHandler = ProcessExiter())


let showHelp (helpTopic:HelpTexts.CommandHelpTopic) = 
                        tracefn "%s" helpTopic.Title
                        tracefn "%s" helpTopic.Text

let v, logFile, args = filterGlobalArgs (Environment.GetCommandLineArgs().[1..])

Logging.verbose <- v
Option.iter setLogFile logFile

try
    match args with
    | Command(Add, args) ->
        let results = commandArgs<AddArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["add"]
        else
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
        
    | Command(Config, args) ->
        let results = commandArgs<ConfigArgs> args

        if results.IsUsageRequested then
            trace <| results.Usage("paket config")
        else
            let args = results.GetResults <@ ConfigArgs.AddCredentials @> 
            if args.Length = 0 then
                trace <| results.Usage("paket config")
            else
                let source = args.Item 0
                let username = 
                    if(args.Length > 1) then
                        args.Item 1
                    else
                        ""
                Dependencies.Locate().AddCredentials(source, username)

    | Command(ConvertFromNuget, args) ->
        let results = commandArgs<ConvertFromNugetArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["convert-from-nuget"]
        else
            let force = results.Contains <@ ConvertFromNugetArgs.Force @>
            let noInstall = results.Contains <@ ConvertFromNugetArgs.No_Install @>
            let noAutoRestore = results.Contains <@ ConvertFromNugetArgs.No_Auto_Restore @>
            let credsMigrationMode = results.TryGetResult <@ ConvertFromNugetArgs.Creds_Migration @>
            Dependencies.ConvertFromNuget(force, noInstall |> not, noAutoRestore |> not, credsMigrationMode)
    
    | Command(FindRefs, args) ->
        let results = commandArgs<FindRefsArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["find-refs"]
        else
            let packages = results.GetResults <@ FindRefsArgs.Packages @>
            Dependencies.Locate().ShowReferencesFor(packages)
        
    | Command(Init, args) ->
        let results = commandArgs<InitArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["init"]
        else
            Dependencies.Init()

    | Command(AutoRestore, args) ->
        let results = commandArgs<AutoRestoreArgs> args
        
        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["auto-restore"]
        else
            match results.GetAllResults() with
            | [On] -> Dependencies.Locate().TurnOnAutoRestore()
            | [Off] -> Dependencies.Locate().TurnOffAutoRestore()
            | _ -> showHelp HelpTexts.commands.["auto-restore"]

    | Command(Install, args) ->
        let results = commandArgs<InstallArgs> args
            
        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["install"]
        else
            let force = results.Contains <@ InstallArgs.Force @>
            let hard = results.Contains <@ InstallArgs.Hard @>
            let withBindingRedirects = results.Contains <@ InstallArgs.Redirects @>
            Dependencies.Locate().Install(force,hard,withBindingRedirects)

    | Command(Outdated, args) ->
        let results = commandArgs<OutdatedArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["outdated"]
        else
            let strict = results.Contains <@ OutdatedArgs.Ignore_Constraints @> |> not
            let includePrereleases = results.Contains <@ OutdatedArgs.Include_Prereleases @>
            Dependencies.Locate().ShowOutdated(strict,includePrereleases)

    | Command(Remove, args) ->
        let results = commandArgs<RemoveArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["remove"]
        else 
            let packageName = results.GetResult <@ RemoveArgs.Nuget @>
            let force = results.Contains <@ RemoveArgs.Force @>
            let hard = results.Contains <@ RemoveArgs.Hard @>
            let noInstall = results.Contains <@ RemoveArgs.No_Install @>
            match results.TryGetResult <@ RemoveArgs.Project @> with
            | Some projectName ->
                Dependencies.Locate().RemoveFromProject(packageName, force, hard, projectName, noInstall |> not)
            | None ->
                let interactive = results.Contains <@ RemoveArgs.Interactive @>
                Dependencies.Locate().Remove(packageName, force, hard, interactive, noInstall |> not)

    | Command(Restore, args) ->
        let results = commandArgs<RestoreArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["restore"]
        else 
            let force = results.Contains <@ RestoreArgs.Force @>
            let files = results.GetResults <@ RestoreArgs.References_Files @> 
            Dependencies.Locate().Restore(force,files)

    | Command(Simplify, args) ->
        let results = commandArgs<SimplifyArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["simplify"]
        else 
            let interactive = results.Contains <@ SimplifyArgs.Interactive @>
            Dependencies.Simplify(interactive)

    | Command(Update, args) ->
        let results = commandArgs<UpdateArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["update"]
        else 
            let hard = results.Contains <@ UpdateArgs.Hard @>
            let force = results.Contains <@ UpdateArgs.Force @>
            match results.TryGetResult <@ UpdateArgs.Nuget @> with
            | Some packageName -> 
                let version = results.TryGetResult <@ UpdateArgs.Version @>
                Dependencies.Locate().UpdatePackage(packageName, version, force, hard)
            | _ -> 
                let withBindingRedirects = results.Contains <@ UpdateArgs.Redirects @>
                Dependencies.Locate().Update(force,hard,withBindingRedirects)
    | Command(Pack, args) ->
        let results = commandArgs<PackArgs> args
        
        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["pack"]
        else
            let outputPath = results.GetResult <@ PackArgs.Output @>
            let buildConfig =
                match results.TryGetResult <@ PackArgs.BuildConfig @> with
                | Some c -> c
                | None -> "Release"
            Dependencies.Locate().Pack(buildConfig, outputPath)
    | Command(Push, args) ->
        let results = commandArgs<PushArgs> args
        
        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["push"]
        else
            let url = results.GetResult <@ PushArgs.Url @>
            let fileName = results.GetResult <@ PushArgs.FileName @>
            match results.TryGetResult <@ PushArgs.ApiKey @> with
            | Some apikey -> Dependencies.Locate().Push(fileName, url, apiKey = apikey)
            | None -> Dependencies.Locate().Push(fileName, url)
    | _ ->
        let allCommands = 
            Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(typeof<Command>)
            |> Array.map (fun command -> 
                   let attr = 
                       command.GetCustomAttributes(typeof<CustomCommandLineAttribute>)
                       |> Seq.cast<CustomCommandLineAttribute>
                       |> Seq.head
                   attr.Name)
            |> String.concat Environment.NewLine

        tracefn "available commands: %s%s%s%s" 
            Environment.NewLine
            Environment.NewLine
            allCommands
            Environment.NewLine

    let elapsedTime = Utils.TimeSpanToReadableString stopWatch.Elapsed
    tracefn "%s - ready." elapsedTime
with
| exn when not (exn :? System.NullReferenceException) -> 
    Environment.ExitCode <- 1
    traceErrorfn "Paket failed with:%s\t%s" Environment.NewLine exn.Message

    if verbose then
        traceErrorfn "StackTrace:%s  %s" Environment.NewLine exn.StackTrace
