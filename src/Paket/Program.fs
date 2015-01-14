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
    | Init
    | Add
    | Remove
    | Install
    | Restore
    | Update
    | Outdated
    | ConvertFromNuget
    | InitAutoRestore
    | Simplify
    | FindRefs
    | Config
    | Unknown

type CLIArguments =
    | [<First>][<NoAppSettings>][<CustomCommandLine("init")>] Init
    | [<First>][<NoAppSettings>][<CustomCommandLine("add")>] Add
    | [<First>][<NoAppSettings>][<CustomCommandLine("remove")>] Remove
    | [<First>][<NoAppSettings>][<CustomCommandLine("install")>] Install
    | [<First>][<NoAppSettings>][<CustomCommandLine("restore")>] Restore
    | [<First>][<NoAppSettings>][<CustomCommandLine("update")>] Update
    | [<First>][<NoAppSettings>][<CustomCommandLine("outdated")>] Outdated
    | [<First>][<NoAppSettings>][<CustomCommandLine("convert-from-nuget")>] ConvertFromNuget
    | [<First>][<NoAppSettings>][<CustomCommandLine("init-auto-restore")>] InitAutoRestore
    | [<First>][<NoAppSettings>][<CustomCommandLine("simplify")>] Simplify
    | [<First>][<NoAppSettings>][<CustomCommandLine("config")>] Config
    | [<First>][<NoAppSettings>][<Rest>][<CustomCommandLine("find-refs")>] FindRefs of string
    | [<AltCommandLine("-v")>] Verbose
    | [<AltCommandLine("-i")>] Interactive
    | Redirects
    | [<AltCommandLine("-f")>] Force
    | Hard
    | [<CustomCommandLine("nuget")>] Nuget of string
    | [<CustomCommandLine("version")>] Version of string
    | [<CustomCommandLine("add-credentials")>] AddCredentials of string
    | [<Rest>]References_Files of string
    | No_Install
    | Ignore_Constraints
    | [<AltCommandLine("--pre")>] Include_Prereleases
    | No_Auto_Restore
    | Creds_Migration of string
    | Log_File of string
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Init -> "creates dependencies file."
            | Add -> "adds a package to the dependencies."
            | Remove -> "removes a package from the dependencies."
            | Install -> "installs all packages."
            | Restore -> "restores all packages."
            | Update -> "updates the paket.lock file and installs all packages."
            | References_Files _ -> "allows to specify a list of references file names."
            | Outdated -> "displays information about new packages."
            | ConvertFromNuget -> "converts all projects from NuGet to Paket."
            | InitAutoRestore -> "enables automatic restore for Visual Studio."
            | Simplify -> "analyzes dependencies and removes unnecessary transitive dependencies."
            | Config -> "sets config values."
            | Verbose -> "displays verbose output."
            | Force -> "forces the download of all packages."
            | Redirects -> "creates assembly binding redirects."
            | Interactive -> "interactive process."
            | Hard -> "overwrites manual package references."
            | No_Install -> "omits install --hard after convert-from-nuget."
            | Ignore_Constraints -> "ignores the version requirements when searching for outdated packages."
            | Include_Prereleases -> "includes prereleases when searching for outdated packages."
            | No_Auto_Restore -> "omits init-auto-restore after convert-from-nuget."
            | Nuget _ -> "allows to specify a nuget package."
            | Version _ -> "allows to specify a package version."
            | Creds_Migration _ -> "allows to specify credentials migration mode for convert-from-nuget."
            | Log_File _ -> "allows to specify a log file."
            | FindRefs _ -> "finds all references to the given packages."
            | AddCredentials _ -> "add credentials to config file for the specified source."


let parser = UnionArgParser.Create<CLIArguments>("USAGE: paket [add|remove|install|update|outdated|convert-from-nuget|init-auto-restore|simplify|find-refs|config] ... options")
 

type GlobalArgs =
    | [<AltCommandLine("-v")>] Verbose
    | Log_File of string
with
    interface IArgParserTemplate with
        member __.Usage = ""

let filterGlobalArgs args = 
    let globalResults = 
        UnionArgParser.Create<GlobalArgs>().Parse(ignoreMissing = true, ignoreUnrecognized = true, raiseOnUsage = false)
    let verbose = globalResults.Contains <@ GlobalArgs.Verbose @>
    let logFile = globalResults.TryGetResult <@ GlobalArgs.Log_File @> 

    let rest = 
        match logFile with
        | Some file -> args |> List.filter (fun a -> a <> "--log-file" && a <> file)
        | None -> args

    let rest = 
        if verbose then rest |> List.filter (fun a -> a <> "-v" && a <> "--verbose")
        else rest

    verbose, logFile, rest

type AddArgs =
    | [<First>][<CustomCommandLine("nuget")>][<Mandatory>] Nuget of string
    | [<CustomCommandLine("version")>] Version of string
    | [<AltCommandLine("-f")>] Force
    | [<AltCommandLine("-i")>] Interactive
    | Hard
    | No_Install
with 
    interface IArgParserTemplate with
        member __.Usage = ""

type ConvertFromNugetArgs =
    | [<AltCommandLine("-f")>] Force
    | No_Install
    | No_Auto_Restore
    | Creds_Migration of string
with 
    interface IArgParserTemplate with
        member __.Usage = ""

type FindRefsArgs =
    | [<Rest>][<CustomCommandLine("nuget")>][<Mandatory>] Packages of string
with 
    interface IArgParserTemplate with
        member __.Usage = ""

type InitArgs =
    | [<Hidden>] NoArg
with 
    interface IArgParserTemplate with
        member __.Usage = ""

type InstallArgs =
    | [<AltCommandLine("-f")>] Force
    | Hard
    | Redirects
with 
    interface IArgParserTemplate with
        member __.Usage = ""

type OutdatedArgs =
    | Ignore_Constraints
    | [<AltCommandLine("--pre")>] Include_Prereleases
with 
    interface IArgParserTemplate with
        member __.Usage = ""

type RemoveArgs =
    | [<First>][<CustomCommandLine("nuget")>][<Mandatory>] Nuget of string
    | [<AltCommandLine("-f")>] Force
    | [<AltCommandLine("-i")>] Interactive
    | Hard
    | No_Install
with 
    interface IArgParserTemplate with
        member __.Usage = ""

type RestoreArgs =
    | [<AltCommandLine("-f")>] Force
    | [<Rest>] References_Files of string
with 
    interface IArgParserTemplate with
        member __.Usage = ""

type SimplifyArgs =
    | [<AltCommandLine("-i")>] Interactive
with 
    interface IArgParserTemplate with
        member __.Usage = ""

type UpdateArgs =
    | [<First>][<CustomCommandLine("nuget")>] Nuget of string
    | [<CustomCommandLine("version")>] Version of string
    | [<AltCommandLine("-f")>] Force
    | Hard
    | Redirects
with 
    interface IArgParserTemplate with
        member __.Usage = ""


let showHelp (helpTopic:HelpTexts.CommandHelpTopic) = 
                        tracefn "%s" helpTopic.Title
                        tracefn "%s" helpTopic.Text

let v, logFile, args = filterGlobalArgs (Environment.GetCommandLineArgs() |> Seq.skip 1 |> Seq.toList)

let commandArgs<'T when 'T :> IArgParserTemplate> args = 
    UnionArgParser
        .Create<'T>()
        .Parse(inputs = Array.ofList args, raiseOnUsage = false, errorHandler = ProcessExiter())

Logging.verbose <- v
Option.iter setLogFile logFile

match args with
    | "add" :: args ->
        let results = commandArgs<AddArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["add"]
        else
            let packageName = results.GetResult <@ AddArgs.Nuget @>
            let version = defaultArg (results.TryGetResult <@ AddArgs.Version @>) ""
            let force = results.Contains <@ AddArgs.Force @>
            let hard = results.Contains <@ AddArgs.Hard @>
            let interactive = results.Contains <@ AddArgs.Interactive @>
            let noInstall = results.Contains <@ AddArgs.No_Install @>
            Dependencies.Locate().Add(packageName, version, force, hard, interactive, noInstall |> not)
        
    | "convert-from-nuget" :: args ->
        let results = commandArgs<ConvertFromNugetArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["convert-from-nuget"]
        else
            let force = results.Contains <@ ConvertFromNugetArgs.Force @>
            let noInstall = results.Contains <@ ConvertFromNugetArgs.No_Install @>
            let noAutoRestore = results.Contains <@ ConvertFromNugetArgs.No_Install @>
            let credsMigrationMode = results.TryGetResult <@ ConvertFromNugetArgs.Creds_Migration @>
            Dependencies.ConvertFromNuget(force, noInstall |> not, noAutoRestore |> not, credsMigrationMode)
    
    | "find-refs" :: args ->
        let results = commandArgs<FindRefsArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["find-refs"]
        else
            let packages = results.GetResults <@ FindRefsArgs.Packages @>
            Dependencies.Locate().ShowReferencesFor(packages)
        
    | "init" :: args ->
        let results = commandArgs<InitArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["init"]
        else
            Dependencies.Init()

    | "install" :: args ->
        let results = commandArgs<InstallArgs> args
            
        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["install"]
        else
            let force = results.Contains <@ InstallArgs.Force @>
            let hard = results.Contains <@ InstallArgs.Hard @>
            let withBindingRedirects = results.Contains <@ InstallArgs.Redirects @>
            Dependencies.Locate().Install(force,hard,withBindingRedirects)

    | "outdated" :: args ->
        let results = commandArgs<OutdatedArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["outdated"]
        else
            let strict = results.Contains <@ OutdatedArgs.Ignore_Constraints @> |> not
            let includePrereleases = results.Contains <@ OutdatedArgs.Include_Prereleases @>
            Dependencies.Locate().ShowOutdated(strict,includePrereleases)

    | "remove" :: args ->
        let results = commandArgs<RemoveArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["remove"]
        else 
            let packageName = results.GetResult <@ RemoveArgs.Nuget @>
            let force = results.Contains <@ RemoveArgs.Force @>
            let hard = results.Contains <@ RemoveArgs.Hard @>
            let interactive = results.Contains <@ RemoveArgs.Interactive @>
            let noInstall = results.Contains <@ RemoveArgs.No_Install @>
            Dependencies.Locate().Remove(packageName, force, hard, interactive, noInstall |> not)

    | "restore" :: args ->
        let results = commandArgs<RestoreArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["restore"]
        else 
            let force = results.Contains <@ RestoreArgs.Force @>
            let files = results.GetResults <@ RestoreArgs.References_Files @> 
            Dependencies.Locate().Restore(force,files)

    | "simplify" :: args ->
        let results = commandArgs<SimplifyArgs> args

        if results.IsUsageRequested then
            showHelp HelpTexts.commands.["simplify"]
        else 
            let interactive = results.Contains <@ SimplifyArgs.Interactive @>
            Dependencies.Simplify(interactive)

    | "update" :: args ->
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

    | _ ->

        let results =
            try
        
                    let results = parser.Parse(raiseOnUsage=false)
                    let command = 
                        if results.Contains <@ CLIArguments.Init @> then Command.Init
                        elif results.Contains <@ CLIArguments.Add @> then Command.Add
                        elif results.Contains <@ CLIArguments.Remove @> then Command.Remove
                        elif results.Contains <@ CLIArguments.Install @> then Command.Install
                        elif results.Contains <@ CLIArguments.Restore @> then Command.Restore
                        elif results.Contains <@ CLIArguments.Update @> then Command.Update
                        elif results.Contains <@ CLIArguments.Outdated @> then Command.Outdated
                        elif results.Contains <@ CLIArguments.ConvertFromNuget @> then Command.ConvertFromNuget
                        elif results.Contains <@ CLIArguments.InitAutoRestore @> then Command.InitAutoRestore
                        elif results.Contains <@ CLIArguments.Simplify @> then Command.Simplify
                        elif results.Contains <@ CLIArguments.Config @> then Command.Config
                        elif results.Contains <@ CLIArguments.FindRefs @> then Command.FindRefs
                        else Command.Unknown
                    if results.Contains <@ CLIArguments.Verbose @> then
                        verbose <- true

                    if results.Contains <@ CLIArguments.Log_File @> then
                        setLogFile <| results.GetResult <@ CLIArguments.Log_File @> 

                    Some(command,results)
            with
            | _ ->
                tracefn "%s %s%s" (String.Join(" ",Environment.GetCommandLineArgs())) Environment.NewLine (parser.Usage())
                None

        try
            match results with
            | Some(command,results) ->
                let force = results.Contains <@ CLIArguments.Force @> 
                let interactive = results.Contains <@ CLIArguments.Interactive @> 
                let hard = results.Contains <@ CLIArguments.Hard @> 
                let noInstall = results.Contains <@ CLIArguments.No_Install @>
                let noAutoRestore = results.Contains <@ CLIArguments.No_Auto_Restore @>
                let includePrereleases = results.Contains <@ CLIArguments.Include_Prereleases @> 
                let withBindingRedirects = results.Contains <@ CLIArguments.Redirects @>

                if results.IsUsageRequested then 
                    match command with
                    | Command.Init -> showHelp HelpTexts.commands.["init"]
                    | Command.Add -> showHelp HelpTexts.commands.["add"]
                    | Command.Remove -> showHelp HelpTexts.commands.["remove"]
                    | Command.Install -> showHelp HelpTexts.commands.["install"]
                    | Command.Restore -> showHelp HelpTexts.commands.["restore"]
                    | Command.Update -> showHelp HelpTexts.commands.["update"]
                    | Command.Outdated -> showHelp HelpTexts.commands.["outdated"]
                    | Command.InitAutoRestore -> showHelp HelpTexts.commands.["init-auto-restore"]
                    | Command.ConvertFromNuget -> showHelp HelpTexts.commands.["convert-from-nuget"]
                    | Command.Simplify -> showHelp HelpTexts.commands.["simplify"]
                    | Command.FindRefs -> showHelp HelpTexts.commands.["find-refs"]
                    | Command.Config -> showHelp HelpTexts.commands.["config"]
                    | Command.Unknown -> traceErrorfn "no command given.%s" (parser.Usage())

                else
                    match command with
                    | Command.Init -> Dependencies.Init()
                    | Command.Add -> 
                        let packageName = results.GetResult <@ CLIArguments.Nuget @>
                        let version = 
                            match results.TryGetResult <@ CLIArguments.Version @> with
                            | Some x -> x
                            | _ -> ""

                        Dependencies.Locate().Add(packageName, version, force, hard, interactive, noInstall |> not)
                    | Command.Remove -> 
                        let packageName = results.GetResult <@ CLIArguments.Nuget @>            
                        Dependencies.Locate().Remove(packageName,force,hard,interactive,noInstall |> not)
                    | Command.Install -> Dependencies.Locate().Install(force,hard,withBindingRedirects)
                    | Command.Restore -> 
                        let files = results.GetResults <@ CLIArguments.References_Files @> 
                        Dependencies.Locate().Restore(force,files)
                    | Command.Update -> 
                        match results.TryGetResult <@ CLIArguments.Nuget @> with
                        | Some packageName -> 
                            let version = results.TryGetResult <@ CLIArguments.Version @>
                            Dependencies.Locate().UpdatePackage(packageName, version, force, hard)
                        | _ -> Dependencies.Locate().Update(force,hard,withBindingRedirects)
                    | Command.Outdated ->         
                        let strict = results.Contains <@ CLIArguments.Ignore_Constraints @> |> not
                        Dependencies.Locate().ShowOutdated(strict,includePrereleases)
                    | Command.InitAutoRestore -> Dependencies.Locate().InitAutoRestore()
                    | Command.ConvertFromNuget -> 
                        let credsMigrationMode = results.TryGetResult <@ CLIArguments.Creds_Migration @>
                        Dependencies.ConvertFromNuget(force, noInstall |> not, noAutoRestore |> not, credsMigrationMode)
                    | Command.Simplify -> Dependencies.Simplify(interactive)
                    | Command.FindRefs ->
                        let packages = results.GetResults <@ CLIArguments.FindRefs @>
                        Dependencies.Locate().ShowReferencesFor(packages)
                    | Command.Config -> 
                        let args = results.GetResults <@ CLIArguments.AddCredentials @> 
                        let source = args.Item 0
                        let username = 
                            if(args.Length > 1) then
                                args.Item 1
                            else
                                ""
                        Paket.ConfigFile.askAndAddAuth(source)(username)

                    | Command.Unknown -> traceErrorfn "no command given.%s" (parser.Usage())
        
                    let elapsedTime = Utils.TimeSpanToReadableString stopWatch.Elapsed

                    tracefn "%s - ready." elapsedTime
            | None -> ()
        with
        | exn when not (exn :? System.NullReferenceException) -> 
            Environment.ExitCode <- 1
            traceErrorfn "Paket failed with:%s\t%s" Environment.NewLine exn.Message

            if verbose then
                traceErrorfn "StackTrace:%s  %s" Environment.NewLine exn.StackTrace