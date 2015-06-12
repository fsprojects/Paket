namespace Paket.PowerShell

open System.Management.Automation
open Paket
open Paket.Commands
open Nessos.UnionArgParser
open System

[<Cmdlet("Paket", "Add")>]
type Add() =   
    inherit Cmdlet()

    [<Parameter>] member val NuGet = "" with get, set
    [<Parameter>] member val Version = "" with get, set
    [<Parameter>] member val Project = "" with get, set
    [<Parameter>] member val Force = SwitchParameter() with get, set
    [<Parameter>] member val Interactive = SwitchParameter() with get, set
    [<Parameter>] member val Hard = SwitchParameter() with get, set
    [<Parameter>] member val NoInstall = SwitchParameter() with get, set

    override x.ProcessRecord() = 
        let parser = UnionArgParser.Create<AddArgs>()
        seq {
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
            if x.Hard.IsPresent then
                yield AddArgs.Hard
            if x.NoInstall.IsPresent then
                yield AddArgs.No_Install
        }
        |> List.ofSeq
        |> parser.CreateParseResultsOfList
        |> Program.add

[<Cmdlet("Paket", "AutoRestore")>]
type AutoRestoreCmdlet() =   
    inherit Cmdlet()

    [<Parameter>] member val On = SwitchParameter() with get, set
    [<Parameter>] member val Off = SwitchParameter() with get, set

    override x.ProcessRecord() = 
        let parser = UnionArgParser.Create<AutoRestoreArgs>()
        seq {
            if x.On.IsPresent then
                yield AutoRestoreArgs.On
            if x.Off.IsPresent then
                yield AutoRestoreArgs.Off
        }
        |> List.ofSeq
        |> parser.CreateParseResultsOfList
        |> Program.autoRestore

[<Cmdlet("Paket", "Config")>]
type ConfigCmdlet() =   
    inherit Cmdlet()

    [<Parameter>] member val AddCredentials = "" with get, set

    override x.ProcessRecord() = 
        let parser = UnionArgParser.Create<ConfigArgs>()
        seq {
            if String.IsNullOrEmpty x.AddCredentials = false then
                yield ConfigArgs.AddCredentials x.AddCredentials
        }
        |> List.ofSeq
        |> parser.CreateParseResultsOfList
        |> Program.config

[<Cmdlet("Paket", "ConvertFromNuGet")>]
type ConvertFromNuGetCmdlet() =   
    inherit Cmdlet()

    [<Parameter>] member val Force = SwitchParameter() with get, set
    [<Parameter>] member val NoInstall = SwitchParameter() with get, set
    [<Parameter>] member val NoAutoRestore = SwitchParameter() with get, set
    [<ValidateSet("encrypt", "plaintext", "selective")>]
    [<Parameter>] member val CredsMigration = "encrypt" with get, set

    override x.ProcessRecord() = 
        let parser = UnionArgParser.Create<ConvertFromNugetArgs>()
        seq {
            if x.Force.IsPresent then
                yield ConvertFromNugetArgs.Force
            if x.NoInstall.IsPresent then
                yield ConvertFromNugetArgs.No_Install
            if x.NoAutoRestore.IsPresent then
                yield ConvertFromNugetArgs.No_Auto_Restore
            if String.IsNullOrEmpty x.CredsMigration = false then
                yield ConvertFromNugetArgs.Creds_Migration x.CredsMigration
        }
        |> List.ofSeq
        |> parser.CreateParseResultsOfList
        |> Program.convert

[<Cmdlet("Paket", "FindRefs")>]
type FindRefsCmdlet() =   
    inherit Cmdlet()

    [<Parameter>] member val NuGet : string[] = Array.empty with get, set

    override x.ProcessRecord() = 
        let parser = UnionArgParser.Create<FindRefsArgs>()
        seq {
            for p in x.NuGet do
                yield FindRefsArgs.Packages p
        }
        |> List.ofSeq
        |> parser.CreateParseResultsOfList
        |> Program.findRefs

[<Cmdlet("Paket", "FindPackages")>]
type FindPackagesCmdlet() =   
    inherit Cmdlet()

    [<Parameter>] member val SearchText = "" with get, set
    [<Parameter>] member val Source = "" with get, set
    [<Parameter>] member val Max = Int32.MinValue with get, set
    [<Parameter>] member val Silent = SwitchParameter() with get, set

    override x.ProcessRecord() = 
        let parser = UnionArgParser.Create<FindPackagesArgs>()
        seq {
            if String.IsNullOrEmpty x.SearchText = false then
                yield FindPackagesArgs.SearchText x.SearchText
            if String.IsNullOrEmpty x.Source = false then
                yield FindPackagesArgs.Source x.Source
            if x.Max <> Int32.MinValue then
                yield FindPackagesArgs.MaxResults x.Max
            if x.Silent.IsPresent then
                yield FindPackagesArgs.Silent
        }
        |> List.ofSeq
        |> parser.CreateParseResultsOfList
        |> Program.findPackages

[<Cmdlet("Paket", "FindPackageVersions")>]
type FindPackageVersionsCmdlet() =   
    inherit Cmdlet()

    [<Parameter>] member val Name = "" with get, set
    [<Parameter>] member val Source = "" with get, set
    [<Parameter>] member val Max = Int32.MinValue with get, set
    [<Parameter>] member val Silent = SwitchParameter() with get, set

    override x.ProcessRecord() = 
        let parser = UnionArgParser.Create<FindPackageVersionsArgs>()
        seq {
            if String.IsNullOrEmpty x.Name = false then
                yield FindPackageVersionsArgs.Name x.Name
            if String.IsNullOrEmpty x.Source = false then
                yield FindPackageVersionsArgs.Source x.Source
            if x.Max <> Int32.MinValue then
                yield FindPackageVersionsArgs.MaxResults x.Max
            if x.Silent.IsPresent then
                yield FindPackageVersionsArgs.Silent
        }
        |> List.ofSeq
        |> parser.CreateParseResultsOfList
        |> Program.findPackageVersions

[<Cmdlet("Paket", "Init")>]
type InitCmdlet() =   
    inherit Cmdlet()

    override x.ProcessRecord() = 
        let parser = UnionArgParser.Create<InitArgs>()
        List.empty
        |> parser.CreateParseResultsOfList
        |> Program.init

[<Cmdlet("Paket", "Install")>]
type InstallCmdlet() =
    inherit Cmdlet()

    [<Parameter>] member val Force = SwitchParameter() with get, set
    [<Parameter>] member val Hard = SwitchParameter() with get, set
    [<Parameter>] member val Redirects = SwitchParameter() with get, set

    override x.ProcessRecord() =
        let parser = UnionArgParser.Create<InstallArgs>()
        seq {
            if x.Force.IsPresent then
                yield InstallArgs.Force
            if x.Hard.IsPresent then
                yield InstallArgs.Hard             
            if x.Redirects.IsPresent then
                yield InstallArgs.Redirects
        }
        |> List.ofSeq
        |> parser.CreateParseResultsOfList
        |> Program.install

[<Cmdlet("Paket", "Outdated")>]
type OutdatedCmdlet() =   
    inherit Cmdlet()

    [<Parameter>] member val IgnoreConstraints = SwitchParameter() with get, set
    [<Parameter>] member val IncludePrereleases = SwitchParameter() with get, set

    override x.ProcessRecord() =
        let parser = UnionArgParser.Create<OutdatedArgs>()
        seq {
            if x.IgnoreConstraints.IsPresent then
                yield OutdatedArgs.Ignore_Constraints
            if x.IncludePrereleases.IsPresent then
                yield OutdatedArgs.Include_Prereleases             
        }
        |> List.ofSeq
        |> parser.CreateParseResultsOfList
        |> Program.outdated

[<Cmdlet("Paket", "Push")>]
type PushCmdlet() =   
    inherit Cmdlet()

    [<Parameter>] member val Url = "" with get, set
    [<Parameter>] member val File = "" with get, set
    [<Parameter>] member val ApiKey = "" with get, set
    [<Parameter>] member val Endpoint = "" with get, set

    override x.ProcessRecord() = 
        let parser = UnionArgParser.Create<PushArgs>()
        seq {
            if String.IsNullOrEmpty x.Url = false then
                yield PushArgs.Url x.Url
            if String.IsNullOrEmpty x.File = false then
                yield PushArgs.FileName x.File
            if String.IsNullOrEmpty x.ApiKey = false then
                yield PushArgs.ApiKey x.ApiKey
            if String.IsNullOrEmpty x.Endpoint = false then
                yield PushArgs.EndPoint x.Endpoint
        }
        |> List.ofSeq
        |> parser.CreateParseResultsOfList
        |> Program.push

[<Cmdlet("Paket", "Remove")>]
type RemoveCmdlet() =   
    inherit Cmdlet()

    [<Parameter>] member val NuGet = "" with get, set
    [<Parameter>] member val Project = "" with get, set
    [<Parameter>] member val Force = SwitchParameter() with get, set
    [<Parameter>] member val Interactive = SwitchParameter() with get, set
    [<Parameter>] member val Hard = SwitchParameter() with get, set
    [<Parameter>] member val NoInstall = SwitchParameter() with get, set

    override x.ProcessRecord() = 
        let parser = UnionArgParser.Create<RemoveArgs>()
        seq {
            if String.IsNullOrEmpty x.NuGet = false then
                yield RemoveArgs.Nuget x.NuGet
            if String.IsNullOrEmpty x.Project = false then
                yield RemoveArgs.Project x.Project
            if x.Force.IsPresent then
                yield RemoveArgs.Force
            if x.Interactive.IsPresent then
                yield RemoveArgs.Interactive
            if x.Hard.IsPresent then
                yield RemoveArgs.Hard
            if x.NoInstall.IsPresent then
                yield RemoveArgs.No_Install
        }
        |> List.ofSeq
        |> parser.CreateParseResultsOfList
        |> Program.remove

[<Cmdlet("Paket", "Restore")>]
type RestoreCmdlet() =   
    inherit Cmdlet()

    [<Parameter>] member val Force = SwitchParameter() with get, set
    [<Parameter>] member val ReferencesFiles = Array.empty<string> with get, set

    override x.ProcessRecord() =
        let parser = UnionArgParser.Create<RestoreArgs>()
        seq {
            if x.Force.IsPresent then
                yield RestoreArgs.Force
            for rf in x.ReferencesFiles do
                yield RestoreArgs.References_Files rf
        }
        |> List.ofSeq
        |> parser.CreateParseResultsOfList
        |> Program.restore

[<Cmdlet("Paket", "Simplify")>]
type SimplifyCmdlet() =
    inherit Cmdlet()

    [<Parameter>] member val Interactive = SwitchParameter() with get, set

    override x.ProcessRecord() =
        let parser = UnionArgParser.Create<SimplifyArgs>()
        seq {
            if x.Interactive.IsPresent then
                yield SimplifyArgs.Interactive
        }
        |> List.ofSeq
        |> parser.CreateParseResultsOfList
        |> Program.simplify

[<Cmdlet("Paket", "ShowInstalledPackages")>]
type ShowInstalledPackagesCmdlet() =   
    inherit Cmdlet()

    [<Parameter>] member val All = SwitchParameter() with get, set
    [<Parameter>] member val Project = "" with get, set
    [<Parameter>] member val Silent = SwitchParameter() with get, set

    override x.ProcessRecord() = 
        let parser = UnionArgParser.Create<ShowInstalledPackagesArgs>()
        seq {
            if x.All.IsPresent then
                yield ShowInstalledPackagesArgs.All
            if String.IsNullOrEmpty x.Project = false then
                yield ShowInstalledPackagesArgs.Project x.Project
            if x.Silent.IsPresent then
                yield ShowInstalledPackagesArgs.Silent
        }
        |> List.ofSeq
        |> parser.CreateParseResultsOfList
        |> Program.showInstalledPackages

[<Cmdlet("Paket", "Update")>]
type UpdateCmdlet() =   
    inherit Cmdlet()

    [<Parameter>] member val NuGet = "" with get, set
    [<Parameter>] member val Version = "" with get, set
    [<Parameter>] member val Force = SwitchParameter() with get, set
    [<Parameter>] member val Hard = SwitchParameter() with get, set
    [<Parameter>] member val Redirects = SwitchParameter() with get, set

    override x.ProcessRecord() = 
        let parser = UnionArgParser.Create<UpdateArgs>()
        seq {
            if String.IsNullOrEmpty x.NuGet = false then
                yield UpdateArgs.Nuget x.NuGet
            if String.IsNullOrEmpty x.Version = false then
                yield UpdateArgs.Version x.Version
            if x.Force.IsPresent then
                yield UpdateArgs.Force
            if x.Hard.IsPresent then
                yield UpdateArgs.Hard
            if x.Redirects.IsPresent then
                yield UpdateArgs.Redirects
        }
        |> List.ofSeq
        |> parser.CreateParseResultsOfList
        |> Program.update