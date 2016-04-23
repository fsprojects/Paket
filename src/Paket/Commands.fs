module Paket.Commands

open System

open Argu

type Command =
    | [<First>][<CustomCommandLine("add")>]                      Add
    | [<First>][<CustomCommandLine("clear-cache")>]              ClearCache
    | [<First>][<CustomCommandLine("config")>]                   Config
    | [<First>][<CustomCommandLine("convert-from-nuget")>]       ConvertFromNuget
    | [<First>][<CustomCommandLine("find-refs")>]                FindRefs 
    | [<First>][<CustomCommandLine("init")>]                     Init
    | [<First>][<CustomCommandLine("auto-restore")>]             AutoRestore
    | [<First>][<CustomCommandLine("install")>]                  Install
    | [<First>][<CustomCommandLine("outdated")>]                 Outdated
    | [<First>][<CustomCommandLine("remove")>]                   Remove
    | [<First>][<CustomCommandLine("restore")>]                  Restore
    | [<First>][<CustomCommandLine("simplify")>]                 Simplify
    | [<First>][<CustomCommandLine("update")>]                   Update
    | [<First>][<CustomCommandLine("find-packages")>]            FindPackages
    | [<First>][<CustomCommandLine("find-package-versions")>]    FindPackageVersions
    | [<First>][<CustomCommandLine("show-installed-packages")>]  ShowInstalledPackages
    | [<First>][<CustomCommandLine("show-groups")>]              ShowGroups
    | [<First>][<CustomCommandLine("pack")>]                     Pack
    | [<First>][<CustomCommandLine("push")>]                     Push
    | [<First>][<CustomCommandLine("generate-include-scripts")>] GenerateIncludeScripts
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Add -> "Adds a new package to your paket.dependencies file."
            | ClearCache -> "Clears the NuGet and git cache folders."
            | Config -> "Allows to store global configuration values like NuGet credentials."
            | ConvertFromNuget -> "Converts from using NuGet to Paket."
            | FindRefs -> "Finds all project files that have the given NuGet packages installed."
            | Init -> "Creates an empty paket.dependencies file in the working directory."
            | AutoRestore -> "Enables or disables automatic Package Restore in Visual Studio during the build process."
            | Install -> "Download the dependencies specified by the paket.dependencies or paket.lock file into the `packages/` directory and update projects."
            | Outdated -> "Lists all dependencies that have newer versions available."
            | Remove -> "Removes a package from your paket.dependencies file and all paket.references files."
            | Restore -> "Download the dependencies specified by the paket.lock file into the `packages/` directory."
            | Simplify -> "Simplifies your paket.dependencies file by removing transitive dependencies."
            | Update -> "Update one or all dependencies to their latest version and update projects."
            | FindPackages -> "Allows to search for packages."
            | FindPackageVersions -> "Allows to search for package versions."
            | ShowInstalledPackages -> "Shows all installed top-level packages."
            | ShowGroups -> "Shows all groups."
            | Pack -> "Packs all paket.template files within this repository."
            | Push -> "Pushes the given `.nupkg` file."
            | GenerateIncludeScripts -> "Generate include scripts for installed packages."
    member this.Name =
        let uci,_ = Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(this, typeof<Command>)
        (uci.GetCustomAttributes(typeof<CustomCommandLineAttribute>)
        |> Seq.head
        :?> CustomCommandLineAttribute).Name

type AddArgs =
    | [<CustomCommandLine("nuget")>][<Mandatory>] Nuget of string
    | [<CustomCommandLine("version")>] Version of string
    | [<CustomCommandLine("project")>] Project of string
    | [<CustomCommandLine("group")>] Group of string
    | [<AltCommandLine("-f")>] Force
    | [<AltCommandLine("-i")>] Interactive
    | Redirects
    | CreateNewBindingFiles
    | No_Install
    | Keep_Major
    | Keep_Minor
    | Keep_Patch
    | Touch_Affected_Refs
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Nuget(_) -> "NuGet package id."
            | Group(_) -> "Add the package to the given group. If omited the Main group is used."
            | Version(_) -> "Allows to specify version of the package."
            | Project(_) -> "Allows to add the package to a single project only."
            | Force -> "Forces the download and reinstallation of all packages."
            | Interactive -> "Asks the user for every project if he or she wants to add the package to the projects's paket.references file."
            | Redirects -> "Creates binding redirects for the NuGet packages."
            | CreateNewBindingFiles -> "Creates binding redirect files if needed."
            | No_Install -> "Skips paket install process (patching of csproj, fsproj, ... files) after the generation of paket.lock file."
            | Keep_Major -> "Allows only updates that are not changing the major version of the NuGet packages."
            | Keep_Minor -> "Allows only updates that are not changing the minor version of the NuGet packages."
            | Keep_Patch -> "Allows only updates that are not changing the patch version of the NuGet packages."
            | Touch_Affected_Refs -> "Touches project files referencing packages which are affected, to help incremental build tools detecting the change."

type ConfigArgs =
    | [<CustomCommandLine("add-credentials")>] AddCredentials of string
    | [<CustomCommandLine("add-token")>] AddToken of string * string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | AddCredentials(_) -> "Add credentials for the specified NuGet feed."
            | AddToken(_) -> "Add token for the specified source."

type ConvertFromNugetArgs =
    | [<AltCommandLine("-f")>] Force
    | No_Install
    | No_Auto_Restore
    | Creds_Migration of string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Force -> "Forces the conversion, even if paket.dependencies or paket.references files are present."
            | No_Install -> "Skips paket install process (patching of csproj, fsproj, ... files) after the generation of paket.lock file."
            | No_Auto_Restore -> "Skips paket auto-restore process afterward generation of dependencies / references files."
            | Creds_Migration(_) -> "Specify a mode for migrating NuGet source credentials. Possible values are [`encrypt`|`plaintext`|`selective`]. The default mode is `encrypt`."

type FindRefsArgs =
    | [<CustomCommandLine("group")>] Group of string
    | [<Rest>][<CustomCommandLine("nuget")>][<Mandatory>] Packages of string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Group(_) -> "Allows to specify a group. If omited the Main group is used."
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
            | On -> "Turns auto restore on."
            | Off -> "Turns auto restore off."

type InstallArgs =
    | [<AltCommandLine("-f")>] Force
    | Redirects
    | CreateNewBindingFiles
    | Keep_Major
    | Keep_Minor
    | Keep_Patch
    | [<CustomCommandLine("--only-referenced")>] Install_Only_Referenced
    | Touch_Affected_Refs
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Force -> "Forces the download and reinstallation of all packages."
            | Redirects -> "Creates binding redirects for the NuGet packages."
            | CreateNewBindingFiles -> "Creates binding redirect files if needed."
            | Install_Only_Referenced -> "Only install packages that are referenced in paket.references files, instead of all packages in paket.dependencies."
            | Keep_Major -> "Allows only updates that are not changing the major version of the NuGet packages."
            | Keep_Minor -> "Allows only updates that are not changing the minor version of the NuGet packages."
            | Keep_Patch -> "Allows only updates that are not changing the patch version of the NuGet packages."
            | Touch_Affected_Refs -> "Touches project files referencing packages which are affected, to help incremental build tools detecting the change."

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
    | [<CustomCommandLine("group")>] Group of string
    | [<AltCommandLine("-f")>] Force
    | [<AltCommandLine("-i")>] Interactive
    | No_Install
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Nuget(_) -> "NuGet package id."
            | Group(_) -> "Removes the package from the given group. If omited the Main group is used."
            | Project(_) -> "Allows to remove the package from a single project only."
            | Force -> "Forces the download and reinstallation of all packages."
            | Interactive -> "Asks the user for every project if he or she wants to remove the package from the projects's paket.references file. By default every installation of the package is removed."
            | No_Install -> "Skips paket install process (patching of csproj, fsproj, ... files) after the generation of paket.lock file."


type ClearCacheArgs =
    | [<Hidden>] NoArg
with
    interface IArgParserTemplate with
        member __.Usage = ""

type RestoreArgs =
    | [<AltCommandLine("-f")>] Force
    | [<CustomCommandLine("--only-referenced")>] Install_Only_Referenced
    | [<CustomCommandLine("--touch-affected-refs")>] Touch_Affected_Refs
    | [<CustomCommandLine("group")>] Group of string
    | [<Rest>] References_Files of string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Force -> "Forces the download of all packages."
            | Group(_) -> "Allows to restore a single group."
            | Install_Only_Referenced -> "Allows to restore packages that are referenced in paket.references files, instead of all packages in paket.dependencies."
            | Touch_Affected_Refs -> "Touches project files referencing packages which are being restored, to help incremental build tools detecting the change."
            | References_Files(_) -> "Allows to restore all packages from the given paket.references files. This implies --only-referenced."

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
    | [<CustomCommandLine("group")>] Group of string
    | [<AltCommandLine("-f")>] Force
    | Redirects
    | CreateNewBindingFiles
    | No_Install
    | Keep_Major
    | Keep_Minor
    | Keep_Patch
    | Filter
    | Touch_Affected_Refs
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Nuget(_) -> "NuGet package id."
            | Group(_) -> "Allows to specify the dependency group."
            | Version(_) -> "Allows to specify version of the package."
            | Force -> "Forces the download and reinstallation of all packages."
            | Redirects -> "Creates binding redirects for the NuGet packages."
            | CreateNewBindingFiles -> "Creates binding redirect files if needed."
            | No_Install -> "Skips paket install process (patching of csproj, fsproj, ... files) after the generation of paket.lock file."
            | Keep_Major -> "Allows only updates that are not changing the major version of the NuGet packages."
            | Keep_Minor -> "Allows only updates that are not changing the minor version of the NuGet packages."
            | Keep_Patch -> "Allows only updates that are not changing the patch version of the NuGet packages."
            | Filter -> "Treat the nuget parameter as a regex to filter packages rather than an exact match."
            | Touch_Affected_Refs -> "Touches project files referencing packages which are affected, to help incremental build tools detecting the change."

type FindPackagesArgs =
    | [<CustomCommandLine("searchtext")>] SearchText of string
    | [<CustomCommandLine("source")>] Source of string
    | [<CustomCommandLine("max")>] MaxResults of int
    | [<AltCommandLine("-s")>] Silent
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | SearchText(_) -> "Search text of a Package."
            | Source(_) -> "Allows to specify the package source feed."
            | MaxResults(_) -> "Maximum number of results."
            | Silent -> "Doesn't trace other output than the search result."

type ShowInstalledPackagesArgs =
    | All
    | [<CustomCommandLine("project")>] Project of string
    | [<AltCommandLine("-s")>] Silent
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | All -> "Shows all installed packages (incl. transitive dependencies)."
            | Project(_) -> "Show only packages that are installed in the given project."
            | Silent -> "Doesn't trace other output than installed packages."

type ShowGroupsArgs =
    | [<AltCommandLine("-s")>] Silent
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Silent -> "Doesn't trace other output than installed packages."

type FindPackageVersionsArgs =
    | [<CustomCommandLine("name")>] [<Hidden>] Name of string
    | [<CustomCommandLine("nuget")>] NuGet of string
    | [<CustomCommandLine("source")>] Source of string
    | [<CustomCommandLine("max")>] MaxResults of int
    | [<AltCommandLine("-s")>] Silent
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Name(_) -> "Name of the package. [DEPRECATED]"
            | NuGet(_) -> "Name of the NuGet package."
            | Source(_) -> "Allows to specify the package source feed."
            | MaxResults(_) -> "Maximum number of results."
            | Silent -> "Doesn't trace other output than the search result."

type PackArgs =
    | [<CustomCommandLine("output")>][<Mandatory>] Output of string
    | [<CustomCommandLine("buildconfig")>] BuildConfig of string
    | [<CustomCommandLine("buildplatform")>] BuildPlatform of string
    | [<CustomCommandLine("version")>] Version of string
    | [<CustomCommandLine("templatefile")>] TemplateFile of string
    | [<CustomCommandLine("exclude")>] ExcludedTemplate of string
    | [<CustomCommandLine("specific-version")>] SpecificVersion of templateId:string * version:string
    | [<CustomCommandLine("releaseNotes")>] ReleaseNotes of string
    | [<CustomCommandLine("lock-dependencies")>] LockDependencies
    | [<CustomCommandLine("minimum-from-lock-file")>] LockDependenciesToMinimum
    | [<CustomCommandLine("symbols")>] Symbols
    | [<CustomCommandLine("include-referenced-projects")>] IncludeReferencedProjects
    | [<CustomCommandLine("project-url")>] ProjectUrl of string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Output(_) -> "Output directory to put .nupkg files."
            | BuildConfig(_) -> "Optionally specify build configuration that should be packaged (defaults to Release)."
            | BuildPlatform(_) -> "Optionally specify build platform that should be packaged (if not provided or empty, checks all known platform targets)."
            | Version(_) -> "Specify version of the package."
            | TemplateFile(_) -> "Allows to specify a single template file."
            | ExcludedTemplate(_) -> "Exclude template file by id."
            | SpecificVersion(_) -> "Specifies a version number for template with given id."
            | ReleaseNotes(_) -> "Specify relase notes for the package."
            | LockDependencies -> "Get the version requirements from paket.lock instead of paket.dependencies."
            | LockDependenciesToMinimum -> "Get the version requirements from paket.lock instead of paket.dependencies, and add them as a minimum version.  `lock-dependencies` will over-ride this option."
            | Symbols -> "Build symbol/source packages in addition to library/content packages."
            | IncludeReferencedProjects -> "Include symbol/source from referenced projects."
            | ProjectUrl(_) -> "Url to the projects home page."

type PushArgs =
    | [<CustomCommandLine("url")>][<Mandatory>] Url of string
    | [<CustomCommandLine("file")>][<Mandatory>] FileName of string
    | [<CustomCommandLine("apikey")>] ApiKey of string
    | [<CustomCommandLine("endpoint")>] EndPoint of string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Url(_) -> "Url of the NuGet feed."
            | FileName(_) -> "Path to the package."
            | ApiKey(_) -> "Optionally specify your API key on the command line. Otherwise uses the value of the `nugetkey` environment variable."
            | EndPoint(_) -> "Optionally specify a custom api endpoint to push to. Defaults to `/api/v2/package`."

type GenerateIncludeScriptsArgs = 
    | [<CustomCommandLine("framework")>] Framework of string
    | [<CustomCommandLine("type")>] ScriptType of string
with
  interface IArgParserTemplate with
      member this.Usage = 
        match this with
        | Framework _ -> "Framework identifier to generate scripts for, such as net4 or netcore or xamarinios."
        | ScriptType _ -> "Language to generate scripts for, must be one of 'fsx' or 'csx'. Can be provided multiple times."
      

  
let cmdLineSyntax (parser:ArgumentParser<_>) commandName =
    "paket " + commandName + " " + parser.PrintCommandLineSyntax()

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
    let (afterCommandText, afterOptionsText) =
        let ensureLineBreak (text : string) = if String.IsNullOrEmpty(text) then text else text + Environment.NewLine + Environment.NewLine
        let cleanUp (text : string) = text.Replace("# [after-command]", "")
                                          .Replace("# [after-options]", "")
                                          .Trim('\r', '\n') |> ensureLineBreak
        let afterCommandIndex = additionalText.IndexOf("# [after-command]")
        let afterOptionsIndex = additionalText.IndexOf("# [after-options]")
        
        if afterCommandIndex = -1 then "", additionalText |> cleanUp
        else if afterOptionsIndex = -1 then additionalText |> cleanUp, ""
        else (additionalText.Substring(0, afterCommandIndex) |> cleanUp, additionalText.Substring(afterOptionsIndex) |> cleanUp)

    let replace (pattern : string) (replacement : string) input =
        System.Text.RegularExpressions.Regex.Replace(input, pattern, replacement)

    let syntaxAndOptions (parser : ArgumentParser<_>) =
        let options =
            parser.Usage()
            |> replace @"\s\t--help.*" ""
            |> replace @"\t([-\w \[\]|\/\?<>\.]+):" (System.Environment.NewLine + @"  `$1`:")

        let syntax = cmdLineSyntax parser command.Name
        syntax, options

    let syntax, options = 
        match command with
        | Add -> syntaxAndOptions (ArgumentParser.Create<AddArgs>())
        | ClearCache -> syntaxAndOptions (ArgumentParser.Create<ClearCacheArgs>())
        | Config -> syntaxAndOptions (ArgumentParser.Create<ConfigArgs>())
        | ConvertFromNuget -> syntaxAndOptions (ArgumentParser.Create<ConvertFromNugetArgs>())
        | FindRefs -> syntaxAndOptions (ArgumentParser.Create<FindRefsArgs>())
        | Init -> syntaxAndOptions (ArgumentParser.Create<InitArgs>())
        | AutoRestore -> syntaxAndOptions (ArgumentParser.Create<AutoRestoreArgs>())
        | Install -> syntaxAndOptions (ArgumentParser.Create<InstallArgs>())
        | Outdated -> syntaxAndOptions (ArgumentParser.Create<OutdatedArgs>())
        | Remove -> syntaxAndOptions (ArgumentParser.Create<RemoveArgs>())
        | Restore -> syntaxAndOptions (ArgumentParser.Create<RestoreArgs>())
        | Simplify -> syntaxAndOptions (ArgumentParser.Create<SimplifyArgs>())
        | Update -> syntaxAndOptions (ArgumentParser.Create<UpdateArgs>())
        | FindPackages -> syntaxAndOptions (ArgumentParser.Create<FindPackagesArgs>())
        | FindPackageVersions -> syntaxAndOptions (ArgumentParser.Create<FindPackageVersionsArgs>())
        | ShowInstalledPackages -> syntaxAndOptions (ArgumentParser.Create<ShowInstalledPackagesArgs>())
        | ShowGroups -> syntaxAndOptions (ArgumentParser.Create<ShowGroupsArgs>())
        | Pack -> syntaxAndOptions (ArgumentParser.Create<PackArgs>())
        | Push -> syntaxAndOptions (ArgumentParser.Create<PushArgs>())
        | GenerateIncludeScripts -> syntaxAndOptions (ArgumentParser.Create<GenerateIncludeScriptsArgs>())
    let replaceLinks (text : string) =
        text
        |> replace "(?<=\s)paket.dependencies( file(s)?)?" "[`paket.dependencies`$1](dependencies-file.html)"
        |> replace "(?<=\s)paket.lock( file(s)?)?" "[`paket.lock`$1](lock-file.html)"
        |> replace "(?<=\s)paket.template( file(s)?)?" "[`paket.template`$1](template-files.html)"
        |> replace "(?<=\s)paket.references( file(s)?)?" "[`paket.references`$1](references-files.html)"

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
        .Append(afterCommandText)
        .Append("### Options:")
        .AppendLine(options)
        .Append(afterOptionsText)
        .ToString()
    |> replaceLinks

let getAllCommands () =
    Microsoft.FSharp.Reflection.FSharpType.GetUnionCases(typeof<Command>)
    |> Array.map (fun uci ->
        Microsoft.FSharp.Reflection.FSharpValue.MakeUnion(uci, [||]) :?> Command)
