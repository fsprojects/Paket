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
        member __.Usage = ""

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
        member __.Usage = ""

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