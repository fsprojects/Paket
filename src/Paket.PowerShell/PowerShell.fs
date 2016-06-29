namespace Paket.PowerShell

open System.Management.Automation
open System.Diagnostics
open Paket
open Paket.Commands
open Argu
open System

// types exposed publicly by sending to output
type Package = { GroupName: string; Name: string; Version: string }

[<AutoOpen>]
module PaketPs =
    
    type PaketOutput =
        | Trace of Logging.Trace
        | Package of Package

    /// runs the async computation and then a continuation on the result
    let runWithLogging (cmdlet: PSCmdlet) (computation: Async<'T>) (cont: EventSink<PaketOutput> -> 'T -> unit) =
        Environment.CurrentDirectory <- cmdlet.SessionState.Path.CurrentFileSystemLocation.Path
        Logging.verbose <- cmdlet.Verbose
        use sink = new EventSink<PaketOutput>()

        Logging.event.Publish |> sink.ConvertAndFill PaketOutput.Trace (fun output ->
            match output with
            | Trace trace ->
                match trace.Level with
                | TraceLevel.Error -> cmdlet.WriteWarning trace.Text
                | TraceLevel.Warning -> cmdlet.WriteWarning trace.Text
                | TraceLevel.Verbose -> cmdlet.WriteVerbose trace.Text
                | _ -> cmdlet.WriteObject trace.Text
            | _ -> () )
        
        let run =
            async {
                try
                    do! Async.SwitchToNewThread()
                    // prevent an exception from killing the thread, PS thread, and PS exe
                    try
                        let! v = computation
                        cont sink v
                    with
                        | ex -> Logging.traceWarn ex.Message
                finally
                    sink.StopFill()
            } |> Async.Start

        sink.Drain()

    /// runs the async computation, but no continuation is needed
    let processWithLogging (cmdlet:PSCmdlet) (computation:Async<unit>) =
        runWithLogging cmdlet computation (fun _ _ -> ())

[<Cmdlet("Paket", "Add")>]
type Add() =   
    inherit PSCmdlet()

    [<Parameter(Position=1)>][<ValidateNotNullOrEmpty>] member val NuGet = "" with get, set
    [<Parameter(Position=2)>] member val Version = "" with get, set
    [<Parameter>] member val Project = "" with get, set
    [<Parameter>] member val Force = SwitchParameter() with get, set
    [<Parameter>] member val Interactive = SwitchParameter() with get, set
    [<Parameter>] member val NoInstall = SwitchParameter() with get, set

    override x.ProcessRecord() =
        async {
            let parser = ArgumentParser.Create<AddArgs>()
            [
                if String.IsNullOrEmpty x.NuGet = false then
                    yield AddArgs.Nuget x.NuGet
                if String.IsNullOrEmpty x.Version = false then
                    yield AddArgs.Version x.Version
                if String.IsNullOrEmpty x.Project = false then
                    yield AddArgs.Project x.Project
                if x.Force.IsPresent then
                    yield AddArgs.Force
                if x.Interactive.IsPresent then
                    yield AddArgs.Interactive
                if x.NoInstall.IsPresent then
                    yield AddArgs.No_Install
            ]
            |> parser.CreateParseResultsOfList
            |> Program.add
        } |> processWithLogging x 

[<Cmdlet("Paket", "AutoRestore")>]
type AutoRestoreCmdlet() =   
    inherit PSCmdlet()

    [<Parameter>] member val On = SwitchParameter() with get, set
    [<Parameter>] member val Off = SwitchParameter() with get, set

    override x.ProcessRecord() =
        async {
            let parser = ArgumentParser.Create<AutoRestoreArgs>()
            [
                if x.On.IsPresent then
                    yield AutoRestoreArgs.On
                if x.Off.IsPresent then
                    yield AutoRestoreArgs.Off
            ]
            |> parser.CreateParseResultsOfList
            |> Program.autoRestore
        } |> processWithLogging x

[<Cmdlet("Paket", "Config")>]
type ConfigCmdlet() =   
    inherit PSCmdlet()

    [<Parameter>] member val AddCredentials = "" with get, set

    override x.ProcessRecord() =
        async {
            let parser = ArgumentParser.Create<ConfigArgs>()
            [
                if String.IsNullOrEmpty x.AddCredentials = false then
                    yield ConfigArgs.AddCredentials x.AddCredentials
            ]
            |> parser.CreateParseResultsOfList
            |> Program.config
        } |> processWithLogging x

[<Cmdlet("Paket", "ConvertFromNuGet")>]
type ConvertFromNuGetCmdlet() =   
    inherit PSCmdlet()

    [<Parameter>] member val Force = SwitchParameter() with get, set
    [<Parameter>] member val NoInstall = SwitchParameter() with get, set
    [<Parameter>] member val NoAutoRestore = SwitchParameter() with get, set
    [<ValidateSet("encrypt", "plaintext", "selective")>]
    [<Parameter>] member val CredsMigration = "encrypt" with get, set

    override x.ProcessRecord() =
        async {
            let parser = ArgumentParser.Create<ConvertFromNugetArgs>()
            [
                if x.Force.IsPresent then
                    yield ConvertFromNugetArgs.Force
                if x.NoInstall.IsPresent then
                    yield ConvertFromNugetArgs.No_Install
                if x.NoAutoRestore.IsPresent then
                    yield ConvertFromNugetArgs.No_Auto_Restore
                if String.IsNullOrEmpty x.CredsMigration = false then
                    yield ConvertFromNugetArgs.Creds_Migration x.CredsMigration
            ]
            |> parser.CreateParseResultsOfList
            |> Program.convert
        } |> processWithLogging x

[<Cmdlet("Paket", "FindRefs")>]
type FindRefsCmdlet() =
    inherit PSCmdlet()

    [<Parameter(Position=1)>] member val NuGet : string[] = Array.empty with get, set

    override x.ProcessRecord() =
        async {
            let parser = ArgumentParser.Create<FindRefsArgs>()
            [
                if x.NuGet.Length > 0 then
                    yield FindRefsArgs.Packages (Array.toList x.NuGet)
            ]
            |> parser.CreateParseResultsOfList
            |> Program.findRefs
        } |> processWithLogging x

[<Cmdlet("Paket", "FindPackages")>]
type FindPackagesCmdlet() =   
    inherit PSCmdlet()

    [<Parameter(Position=1)>][<ValidateNotNullOrEmpty>] member val SearchText = "" with get, set
    [<Parameter>] member val Source = "" with get, set
    [<Parameter>] member val Max = Int32.MinValue with get, set
    [<Parameter>] member val Silent = SwitchParameter() with get, set

    override x.ProcessRecord() =
        async {
            let parser = ArgumentParser.Create<FindPackagesArgs>()
            [
                if String.IsNullOrEmpty x.SearchText = false then
                    yield FindPackagesArgs.SearchText x.SearchText
                if String.IsNullOrEmpty x.Source = false then
                    yield FindPackagesArgs.Source x.Source
                if x.Max <> Int32.MinValue then
                    yield FindPackagesArgs.MaxResults x.Max
                if x.Silent.IsPresent then
                    yield FindPackagesArgs.Silent
            ]
            |> parser.CreateParseResultsOfList
            |> Program.findPackages
        } |> processWithLogging x

[<Cmdlet("Paket", "FindPackageVersions")>]
type FindPackageVersionsCmdlet() =   
    inherit PSCmdlet()

    [<Parameter(Position=1)>][<ValidateNotNullOrEmpty>]  member val Name = "" with get, set
    [<Parameter>] member val Source = "" with get, set
    [<Parameter>] member val Max = Int32.MinValue with get, set
    [<Parameter>] member val Silent = SwitchParameter() with get, set

    override x.ProcessRecord() =
        async {
            let parser = ArgumentParser.Create<FindPackageVersionsArgs>()
            [
                if String.IsNullOrEmpty x.Name = false then
                    yield FindPackageVersionsArgs.Name x.Name
                if String.IsNullOrEmpty x.Source = false then
                    yield FindPackageVersionsArgs.Source x.Source
                if x.Max <> Int32.MinValue then
                    yield FindPackageVersionsArgs.MaxResults x.Max
                if x.Silent.IsPresent then
                    yield FindPackageVersionsArgs.Silent
            ]
            |> parser.CreateParseResultsOfList
            |> Program.findPackageVersions
        } |> processWithLogging x

[<Cmdlet("Paket", "Init")>]
type InitCmdlet() =
    inherit PSCmdlet()

    override x.ProcessRecord() =
        async {
            let parser = ArgumentParser.Create<InitArgs>()
            List.empty
            |> parser.CreateParseResultsOfList
            |> Program.init
        } |> processWithLogging x

[<Cmdlet("Paket", "Install")>]
type InstallCmdlet() =
    inherit PSCmdlet()

    [<Parameter>] member val Force = SwitchParameter() with get, set
    [<Parameter>] member val Redirects = SwitchParameter() with get, set

    override x.ProcessRecord() =
        async {
            let parser = ArgumentParser.Create<InstallArgs>()
            [
                if x.Force.IsPresent then
                    yield InstallArgs.Force       
                if x.Redirects.IsPresent then
                    yield InstallArgs.Redirects
            ]
            |> parser.CreateParseResultsOfList
            |> Program.install
        } |> processWithLogging x

[<Cmdlet("Paket", "Outdated")>]
type OutdatedCmdlet() =   
    inherit PSCmdlet()

    [<Parameter>] member val IgnoreConstraints = SwitchParameter() with get, set
    [<Parameter>] member val IncludePrereleases = SwitchParameter() with get, set

    override x.ProcessRecord() =
        async {
            let parser = ArgumentParser.Create<OutdatedArgs>()
            [
                if x.IgnoreConstraints.IsPresent then
                    yield OutdatedArgs.Ignore_Constraints
                if x.IncludePrereleases.IsPresent then
                    yield OutdatedArgs.Include_Prereleases             
            ]
            |> parser.CreateParseResultsOfList
            |> Program.outdated
        } |> processWithLogging x

[<Cmdlet("Paket", "Push")>]
type PushCmdlet() =   
    inherit PSCmdlet()

    [<Parameter>] member val Url = "" with get, set
    [<Parameter>] member val File = "" with get, set
    [<Parameter>] member val ApiKey = "" with get, set
    [<Parameter>] member val Endpoint = "" with get, set

    override x.ProcessRecord() =
        async {
            let parser = ArgumentParser.Create<PushArgs>()
            [
                if String.IsNullOrEmpty x.Url = false then
                    yield PushArgs.Url x.Url
                if String.IsNullOrEmpty x.File = false then
                    yield PushArgs.FileName x.File
                if String.IsNullOrEmpty x.ApiKey = false then
                    yield PushArgs.ApiKey x.ApiKey
                if String.IsNullOrEmpty x.Endpoint = false then
                    yield PushArgs.EndPoint x.Endpoint
            ]
            |> parser.CreateParseResultsOfList
            |> Program.push
        } |> processWithLogging x

[<Cmdlet("Paket", "Remove")>]
type RemoveCmdlet() =   
    inherit PSCmdlet()

    [<Parameter(Position=1)>][<ValidateNotNullOrEmpty>] member val NuGet = "" with get, set
    [<Parameter>] member val Project = "" with get, set
    [<Parameter>] member val Force = SwitchParameter() with get, set
    [<Parameter>] member val Interactive = SwitchParameter() with get, set
    [<Parameter>] member val NoInstall = SwitchParameter() with get, set

    override x.ProcessRecord() =
        async {
            let parser = ArgumentParser.Create<RemoveArgs>()
            [
                if String.IsNullOrEmpty x.NuGet = false then
                    yield RemoveArgs.Nuget x.NuGet
                if String.IsNullOrEmpty x.Project = false then
                    yield RemoveArgs.Project x.Project
                if x.Force.IsPresent then
                    yield RemoveArgs.Force
                if x.Interactive.IsPresent then
                    yield RemoveArgs.Interactive
                if x.NoInstall.IsPresent then
                    yield RemoveArgs.No_Install
            ]
            |> parser.CreateParseResultsOfList
            |> Program.remove
        } |> processWithLogging x

[<Cmdlet("Paket", "Restore")>]
type RestoreCmdlet() =   
    inherit PSCmdlet()

    [<Parameter>] member val Force = SwitchParameter() with get, set
    [<Parameter>] member val ReferencesFiles = Array.empty<string> with get, set

    override x.ProcessRecord() =
        async {
            let parser = ArgumentParser.Create<RestoreArgs>()
            [
                if x.Force.IsPresent then
                    yield RestoreArgs.Force

                if x.ReferencesFiles.Length > 0 then
                    yield RestoreArgs.References_Files (Array.toList x.ReferencesFiles)
            ]
            |> parser.CreateParseResultsOfList
            |> Program.restore
        } |> processWithLogging x

[<Cmdlet("Paket", "Simplify")>]
type SimplifyCmdlet() =
    inherit PSCmdlet()

    [<Parameter>] member val Interactive = SwitchParameter() with get, set

    override x.ProcessRecord() =
        async {
            let parser = ArgumentParser.Create<SimplifyArgs>()
            [
                if x.Interactive.IsPresent then
                    yield SimplifyArgs.Interactive
            ]
            |> parser.CreateParseResultsOfList
            |> Program.simplify
        } |> processWithLogging x

[<Cmdlet("Paket", "ShowInstalledPackages")>]
type ShowInstalledPackagesCmdlet() =   
    inherit PSCmdlet()

    [<Parameter>] member val All = SwitchParameter() with get, set
    [<Parameter>] member val Project = "" with get, set
    [<Parameter>] member val Silent = SwitchParameter() with get, set

    override x.ProcessRecord() =
        let computation =
            async {
                let packages =
                    let parser = ArgumentParser.Create<ShowInstalledPackagesArgs>()
                    [
                        if x.All.IsPresent then
                            yield ShowInstalledPackagesArgs.All
                        if String.IsNullOrEmpty x.Project = false then
                            yield ShowInstalledPackagesArgs.Project x.Project
                        if x.Silent.IsPresent then
                            yield ShowInstalledPackagesArgs.Silent
                    ]
                    |> parser.CreateParseResultsOfList
                    |> Program.getInstalledPackages
                return packages
            }
            
        runWithLogging x computation
            (fun sink packages ->
                 for groupName,name,version in packages do
                    sink.Add
                        (PaketOutput.Package {GroupName=groupName ;Name=name; Version=version})
                        (fun po -> 
                            match po with
                            | Package p -> x.WriteObject p
                            | _ -> () ) )

[<Cmdlet("Paket", "Update")>]
type UpdateCmdlet() =   
    inherit PSCmdlet()

    [<Parameter(Position=1)>][<ValidateNotNullOrEmpty>] member val NuGet = "" with get, set
    [<Parameter(Position=2)>] member val Version = "" with get, set
    [<Parameter>] member val Force = SwitchParameter() with get, set
    [<Parameter>] member val Redirects = SwitchParameter() with get, set

    override x.ProcessRecord() =
        async {
            let parser = ArgumentParser.Create<UpdateArgs>()
            [
                if String.IsNullOrEmpty x.NuGet = false then
                    yield UpdateArgs.Nuget x.NuGet
                if String.IsNullOrEmpty x.Version = false then
                    yield UpdateArgs.Version x.Version
                if x.Force.IsPresent then
                    yield UpdateArgs.Force
                if x.Redirects.IsPresent then
                    yield UpdateArgs.Redirects
            ]
            |> parser.CreateParseResultsOfList
            |> Program.update
        } |> processWithLogging x