namespace Paket.PowerShell

open System.Management.Automation

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
        x.WritefWarning "need this implement Add-Paket, nuget: %s" x.NuGet
        ()

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

[<Cmdlet("Paket", "Simplify")>]
type SimplifyCmdlet() =   
    inherit Cmdlet()

[<Cmdlet("Paket", "ShowInstalledPackages")>]
type ShowInstalledPackagesCmdlet() =   
    inherit Cmdlet()

[<Cmdlet("Paket", "Update")>]
type UpdateCmdlet() =   
    inherit Cmdlet()