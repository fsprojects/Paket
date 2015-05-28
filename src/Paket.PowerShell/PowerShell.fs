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

[<Cmdlet("Paket", "Config")>]
type ConfigCmdlet() =   
    inherit Cmdlet()

[<Cmdlet("Paket", "ConvertFromNuGet")>]
type ConvertFromNuGetCmdlet() =   
    inherit Cmdlet()

[<Cmdlet("Paket", "FindRefs")>]
type FindRefsCmdlet() =   
    inherit Cmdlet()

[<Cmdlet("Paket", "FindPackages")>]
type FindPackagesCmdlet() =   
    inherit Cmdlet()

[<Cmdlet("Paket", "FindPackageVersions")>]
type FindPackageVersionsCmdlet() =   
    inherit Cmdlet()

[<Cmdlet("Paket", "Init")>]
type InitCmdlet() =   
    inherit Cmdlet()

[<Cmdlet("Paket", "Install")>]
type InstallCmdlet() =
    inherit Cmdlet()

[<Cmdlet("Paket", "Outdated")>]
type OutdatedCmdlet() =   
    inherit Cmdlet()

[<Cmdlet("Paket", "Push")>]
type PushCmdlet() =   
    inherit Cmdlet()

[<Cmdlet("Paket", "Remove")>]
type RemoveCmdlet() =   
    inherit Cmdlet()

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

[<Cmdlet("Paket", "ShowInstalledPackages")>]
type ShowInstalledPackagesCmdlet() =   
    inherit Cmdlet()

[<Cmdlet("Paket", "Update")>]
type UpdateCmdlet() =   
    inherit Cmdlet()