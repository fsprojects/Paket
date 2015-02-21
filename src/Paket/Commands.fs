module Paket.Commands

open Nessos.UnionArgParser

type Command =
    | [<First>][<CustomCommandLine("add")>]                 Add
    | [<First>][<CustomCommandLine("config")>]              Config
    | [<First>][<CustomCommandLine("convert-from-nuget")>]  ConvertFromNuget
    | [<First>][<CustomCommandLine("find-refs")>]           FindRefs 
    | [<First>][<CustomCommandLine("init")>]                Init
    | [<First>][<CustomCommandLine("auto-restore")>]        AutoRestore
    | [<First>][<CustomCommandLine("install")>]             Install
    | [<First>][<CustomCommandLine("outdated")>]            Outdated
    | [<First>][<CustomCommandLine("remove")>]              Remove
    | [<First>][<CustomCommandLine("restore")>]             Restore
    | [<First>][<CustomCommandLine("simplify")>]            Simplify
    | [<First>][<CustomCommandLine("update")>]              Update
    | [<First>][<CustomCommandLine("pack")>]                Pack
    | [<First>][<CustomCommandLine("push")>]                Push
with 
    interface IArgParserTemplate with
        member __.Usage = ""
 
type GlobalArgs =
    | [<AltCommandLine("-v")>] Verbose
    | Log_File of string
with
    interface IArgParserTemplate with
        member __.Usage = ""

type AddArgs =
    | [<CustomCommandLine("nuget")>][<Mandatory>] Nuget of string
    | [<CustomCommandLine("version")>] Version of string
    | [<CustomCommandLine("project")>] Project of string
    | [<AltCommandLine("-f")>] Force
    | [<AltCommandLine("-i")>] Interactive
    | Hard
    | No_Install
with 
    interface IArgParserTemplate with
        member this.Usage = 
            match this with
            | Nuget(_) -> "Nuget package id."
            | Version(_) -> "Allows to specify version of the package."
            | Project(_) -> "Allows to add the package to a single project only."
            | Force -> "Forces the download and reinstallation of all packages."
            | Interactive -> "Asks the user for every project if he or she wants to add the package to the projects's paket.references file."
            | Hard -> "Replaces package references within project files even if they are not yet adhering to to Paket's conventions (and hence considered manually managed)."
            | No_Install -> "Skips paket install --hard process afterward generation of dependencies / references files."

type ConfigArgs = 
    | [<CustomCommandLine("add-credentials")>] AddCredentials of string
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

type AutoRestoreArgs =
    | [<First>][<CustomCommandLine("on")>] On
    | [<First>][<CustomCommandLine("off")>] Off
with 
    interface IArgParserTemplate with
        member __.Usage = ""

type InstallArgs =
    | [<AltCommandLine("-f")>] Force
    | Hard
    | Redirects
with 
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Force -> "Forces the download and reinstallation of all packages."
            | Hard -> "Replaces package references within project files even if they are not yet adhering to Paket's conventions (and hence considered manually managed)."            
            | Redirects -> "Creates binding redirects for the NuGet packages."

type OutdatedArgs =
    | Ignore_Constraints
    | [<AltCommandLine("--pre")>] Include_Prereleases
with 
    interface IArgParserTemplate with
        member __.Usage = ""

type RemoveArgs =
    | [<CustomCommandLine("nuget")>][<Mandatory>] Nuget of string
    | [<CustomCommandLine("project")>] Project of string
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
    | [<CustomCommandLine("nuget")>] Nuget of string
    | [<CustomCommandLine("version")>] Version of string
    | [<AltCommandLine("-f")>] Force
    | Hard
    | Redirects
with 
    interface IArgParserTemplate with
        member __.Usage = ""

type PackArgs =
    | [<CustomCommandLine("output")>][<Mandatory>] Output of string
    | [<CustomCommandLine("buildconfig")>] BuildConfig of string
    | [<CustomCommandLine("version")>] Version of string
    | [<CustomCommandLine("releaseNotes")>] ReleaseNotes of string
with
    interface IArgParserTemplate with
        member __.Usage = ""

type PushArgs =
    | [<CustomCommandLine("url")>][<Mandatory>] Url of string
    | [<CustomCommandLine("file")>][<Mandatory>] FileName of string
    | [<CustomCommandLine("apikey")>] ApiKey of string
with
    interface IArgParserTemplate with
        member __.Usage = ""