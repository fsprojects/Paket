module Paket.Commands

open System

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
        member this.Usage = 
            match this with
            | Add -> "Adds a new package to your paket.dependencies file."
            | Config -> "Allows to store global configuration values like NuGet credentials."
            | ConvertFromNuget -> "Converts from using NuGet to Paket."
            | FindRefs -> "Finds all project files that have the given NuGet packages installed."
            | Init -> "Creates empty paket.dependencies file in the working directory."
            | AutoRestore -> "Enables or disables automatic Package Restore in Visual Studio during the build process."
            | Install -> "Ensures that all dependencies in your paket.dependencies file are present in the `packages` directory and referenced correctly in all projects."
            | Outdated -> "Lists all dependencies that have newer versions available."
            | Remove -> "Removes a package from your paket.dependencies file and all paket.references files."
            | Restore -> "Ensures that all dependencies in your paket.dependencies file are present in the `packages` directory."
            | Simplify -> "Simplifies your paket.dependencies file by removing transitive dependencies."
            | Update -> "Recomputes the dependency resolution, updates the paket.lock file and propagates any resulting package changes into all project files referencing updated packages."
            | Pack -> "Packs all paket.template files within this repository"
            | Push -> "Pushes all `.nupkg` files from the given directory."
    
    member this.Name = 
        let uci,_ = Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(this, typeof<Command>)
        (uci.GetCustomAttributes(typeof<CustomCommandLineAttribute>) 
        |> Seq.head 
        :?> CustomCommandLineAttribute).Name

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

type SimplifyArgs =
    | [<AltCommandLine("-i")>] Interactive
with 
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Interactive -> "Asks to confirm to delete every transitive dependency from each of the files."

type UpdateArgs =
    | [<CustomCommandLine("nuget")>] Nuget of string
    | [<CustomCommandLine("version")>] Version of string
    | [<AltCommandLine("-f")>] Force
    | Hard
    | Redirects
with 
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Nuget(_) -> "Nuget package id"
            | Version(_) -> "Allows to specify version of the package."
            | Force -> "Forces the download and reinstallation of all packages."
            | Hard -> "Replaces package references within project files even if they are not yet adhering to the Paket's conventions (and hence considered manually managed)."
            | Redirects -> "Creates binding redirects for the NuGet packages."

type PackArgs =
    | [<CustomCommandLine("output")>][<Mandatory>] Output of string
    | [<CustomCommandLine("buildconfig")>] BuildConfig of string
    | [<CustomCommandLine("version")>] Version of string
    | [<CustomCommandLine("releaseNotes")>] ReleaseNotes of string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Output(_) -> "Output directory to put nupkgs."
            | BuildConfig(_) -> "Optionally specify build configuration that should be packaged (defaults to Release)."
            | Version(_) -> "Specify version of the package."
            | ReleaseNotes(_) -> "Specify relase notes for the package."

type PushArgs =
    | [<CustomCommandLine("url")>][<Mandatory>] Url of string
    | [<CustomCommandLine("file")>][<Mandatory>] FileName of string
    | [<CustomCommandLine("apikey")>] ApiKey of string
    | [<CustomCommandLine("endpoint")>] EndPoint of string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Url(_) -> "Url of the Nuget feed."
            | FileName(_) -> "Path to the package."
            | ApiKey(_) -> "Optionally specify your API key on the command line. Otherwise uses the value of the `nugetkey` environment variable."
            | EndPoint(_) -> "Optionally specify a custom api endpoint to push to. Defaults to `/api/v2/package`"

let cmdLineSyntax (parser:UnionArgParser<_>) commandName = 
    "$ paket " + commandName + " " + parser.PrintCommandLineSyntax()

let cmdLineUsageMessage (command : Command) parser =
    System.Text.StringBuilder()
        .Append("Paket ")
        .AppendLine(command.Name)
        .AppendLine()
        .AppendLine((command :> IArgParserTemplate).Usage)
        .AppendLine()
        .Append(cmdLineSyntax parser command.Name)
        .ToString()
    
let markdown (command : Command) (additionalText : string) =
    let replace (pattern : string) (replacement : string) input = 
        System.Text.RegularExpressions.Regex.Replace(input, pattern, replacement)
    
    let syntaxAndOptions (parser : UnionArgParser<_>) = 
        let options =
            parser.Usage() 
            |> replace @"\s\t--help.*" ""
            |> replace @"\t([-\w \[\]|\/\?<>\.]+):" (System.Environment.NewLine + @"  `$1`:")

        let syntax = cmdLineSyntax parser command.Name
        syntax, options
    
    let getSyntax = function
        | Add -> syntaxAndOptions (UnionArgParser.Create<AddArgs>())
        | Config -> syntaxAndOptions (UnionArgParser.Create<ConfigArgs>())
        | ConvertFromNuget -> syntaxAndOptions (UnionArgParser.Create<ConvertFromNugetArgs>())
        | FindRefs -> syntaxAndOptions (UnionArgParser.Create<FindRefsArgs>())
        | Init -> syntaxAndOptions (UnionArgParser.Create<InitArgs>())
        | AutoRestore -> syntaxAndOptions (UnionArgParser.Create<AutoRestoreArgs>())
        | Install -> syntaxAndOptions (UnionArgParser.Create<InstallArgs>())
        | Outdated -> syntaxAndOptions (UnionArgParser.Create<OutdatedArgs>())
        | Remove -> syntaxAndOptions (UnionArgParser.Create<RemoveArgs>())
        | Restore -> syntaxAndOptions (UnionArgParser.Create<RestoreArgs>())
        | Simplify -> syntaxAndOptions (UnionArgParser.Create<SimplifyArgs>())
        | Update -> syntaxAndOptions (UnionArgParser.Create<UpdateArgs>())
        | Pack -> syntaxAndOptions (UnionArgParser.Create<PackArgs>())
        | Push -> syntaxAndOptions (UnionArgParser.Create<PushArgs>())

    
    let replaceLinks (text : string) =
        text
            .Replace("paket.dependencies file","[`paket.dependencies` file](dependencies-file.html)")
            .Replace("paket.lock file","[`paket.lock` file](lock-file.html)")
            .Replace("paket.template files","[`paket.template` files](template-files.html)")
            .Replace("paket.references files","[`paket.references` files](references-files.html)")
            .Replace("paket.references file","[`paket.references` file](references-files.html)")
    
    let syntax, options = getSyntax command

    System.Text.StringBuilder()
        .Append("# paket ")
        .AppendLine(command.Name)       
        .AppendLine()
        .AppendLine((command :> IArgParserTemplate).Usage)
        .AppendLine()
        .AppendLine("    [lang=batchfile]")
        .Append("    ")
        .AppendLine(syntax)
        .AppendLine()
        .AppendLine("Options:")
        .AppendLine(options)
        .Append(additionalText)
        .ToString()
    |> replaceLinks

let getAllCommands () =
    Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(typeof<Command>)
    |> Array.map (fun uci -> 
        Microsoft.FSharp.Reflection.FSharpValue.MakeUnion(uci, [||]) :?> Command)