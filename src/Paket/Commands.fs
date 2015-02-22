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
            | Hard -> "Replaces package references within project files even if they are not yet adhering to the Paket's conventions (and hence considered manually managed)."
            | No_Install -> "Skips paket install --hard process afterward generation of dependencies / references files."

type ConfigArgs = 
    | [<CustomCommandLine("add-credentials")>] AddCredentials of string
with 
    interface IArgParserTemplate with
        member this.Usage = 
            match this with
            | AddCredentials(_) -> "Add credentials for the specified Nuget feed"

type ConvertFromNugetArgs =
    | [<AltCommandLine("-f")>] Force
    | No_Install
    | No_Auto_Restore
    | Creds_Migration of string
with 
    interface IArgParserTemplate with
        member this.Usage = 
            match this with
            | Force -> "Forces the conversion, even if a paket.dependencies file or paket.references files are present."
            | No_Install -> "Skips paket install --hard process afterward generation of dependencies / references files."
            | No_Auto_Restore -> "Skips paket auto-restore process afterward generation of dependencies / references files."
            | Creds_Migration(_) -> "Specify mode for migrating NuGet source credentials. Possible values are [`encrypt`|`plaintext`|`selective`]. The default mode is `encrypt`."

type FindRefsArgs =
    | [<Rest>][<CustomCommandLine("nuget")>][<Mandatory>] Packages of string
with 
    interface IArgParserTemplate with
        member this.Usage = 
            match this with
            | Packages(_) -> "List of packages."

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
        member this.Usage = 
            match this with
            | On -> "Turns auto restore on"
            | Off -> "Turns auto restore off"

type InstallArgs =
    | [<AltCommandLine("-f")>] Force
    | Hard
    | Redirects
with 
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Force -> "Forces the download and reinstallation of all packages."
            | Hard -> "Replaces package references within project files even if they are not yet adhering to the Paket's conventions (and hence considered manually managed)."            
            | Redirects -> "Creates binding redirects for the NuGet packages."

type OutdatedArgs =
    | Ignore_Constraints
    | [<AltCommandLine("--pre")>] Include_Prereleases
with 
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Ignore_Constraints -> "Ignores the version requirement as in the paket.dependencies file."
            | Include_Prereleases -> "Includes prereleases."

type RemoveArgs =
    | [<CustomCommandLine("nuget")>][<Mandatory>] Nuget of string
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
            | Project(_) -> "Allows to add the package to a single project only."
            | Force -> "Forces the download and reinstallation of all packages."
            | Interactive -> "Asks the user for every project if he or she wants to remove the package from the projects's paket.references file. By default every installation of the package is removed."
            | Hard -> "Replaces package references within project files even if they are not yet adhering to the Paket's conventions (and hence considered manually managed)."
            | No_Install -> "Skips paket install --hard process afterward generation of dependencies / references files."

type RestoreArgs =
    | [<AltCommandLine("-f")>] Force
    | [<Rest>] References_Files of string
with 
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Force -> "Forces the download of all packages."
            | References_Files(_) -> "Allows to restore all packages from the given paket.references files. If no paket.references file is given then all packages will be restored."

(*

    [lang=batchfile]
    $ paket simplify [-v] [--interactive]

Options:

  `-v`: Verbose - output the difference in content before and after running simplify command.

  `--interactive`: Asks to confirm to delete every transitive dependency from each of the files. See [Interactive Mode](paket-simplify.html#Interactive-mode).

*)

type SimplifyArgs =
    | [<AltCommandLine("-i")>] Interactive
with 
    interface IArgParserTemplate with
        member __.Usage = ""

(*

    [lang=batchfile]
    $ paket update [--force] [--hard] [--redirects]	

Options:

  `--force`: Forces the download and reinstallation of all packages.

  `--hard`: Replaces package references within project files even if they are not yet adhering to the Paket's conventions (and hence considered manually managed). See [convert from NuGet](paket-convert-from-nuget.html).

  `--redirects`: Creates binding redirects for the NuGet packages.
*)

type UpdateArgs =
    | [<CustomCommandLine("nuget")>] Nuget of string
    | [<CustomCommandLine("version")>] Version of string
    | [<AltCommandLine("-f")>] Force
    | Hard
    | Redirects
with 
    interface IArgParserTemplate with
        member __.Usage = ""

(*

    [lang=batchfile]
    $ paket pack output outputDirectory [buildconfig Debug]

Options:

  `output`: Output directory to put nupkgs

  `buildconfig`: Optionally specify build configuration that should be packaged (defaults to Release).
*)

type PackArgs =
    | [<CustomCommandLine("output")>][<Mandatory>] Output of string
    | [<CustomCommandLine("buildconfig")>] BuildConfig of string
    | [<CustomCommandLine("version")>] Version of string
    | [<CustomCommandLine("releaseNotes")>] ReleaseNotes of string
with
    interface IArgParserTemplate with
        member __.Usage = ""


(*

    [lang=batchfile]
    $ paket push packagedir path/to/packages [apikey YourApiKey] [url NuGetFeed]

Options:

  `packagedir`: a directory; every `.nupkg` file in this directory or it's children will be pushed.

  `apikey`: Optionally specify your API key on the command line. Otherwise uses the value of the `nugetkey` environment variable.

  `url`: Optionally specify root url of the nuget repository you are pushing too. Defaults to [https://nuget.org](https://nuget.org).
*)

type PushArgs =
    | [<CustomCommandLine("url")>][<Mandatory>] Url of string
    | [<CustomCommandLine("file")>][<Mandatory>] FileName of string
    | [<CustomCommandLine("apikey")>] ApiKey of string
with
    interface IArgParserTemplate with
        member __.Usage = ""