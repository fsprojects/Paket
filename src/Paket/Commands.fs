module Paket.Commands

open System

open Argu

type AddArgs =
    | [<Mandatory;MainCommandAttribute()>] NuGet of package_id:string
    | [<Hidden;CustomCommandLine("nuget")>] NuGet_Legacy of package_id:string

    | [<AltCommandLine("-V")>] Version of version:string
    | [<Hidden;CustomCommandLine("version")>] Version_Legacy of version:string

    | [<AltCommandLine("-p")>] Project of name:string
    | [<Hidden;CustomCommandLine("project")>] Project_Legacy of name:string

    | [<AltCommandLine("-g")>] Group of name:string
    | [<Hidden;CustomCommandLine("group")>] Group_Legacy of name:string

    | Create_New_Binding_Files
    | [<Hidden;CustomCommandLine("--createnewbindingfiles")>] Create_New_Binding_Files_Legacy

    | [<AltCommandLine("-f")>] Force
    | [<AltCommandLine("-i")>] Interactive
    | Redirects
    | Clean_Redirects
    | No_Install
    | Keep_Major
    | Keep_Minor
    | Keep_Patch
    | Touch_Affected_Refs
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | NuGet(_) -> "NuGet package ID"
            | NuGet_Legacy(_) -> "[obsolete]"

            | Group(_) -> "add the dependency to a group (default: Main group)"
            | Group_Legacy(_) -> "[obsolete]"

            | Version(_) -> "dependency version constraint"
            | Version_Legacy(_) -> "[obsolete]"

            | Project(_) -> "add the dependency to a single project only"
            | Project_Legacy(_) -> "[obsolete]"

            | Create_New_Binding_Files -> "create binding redirect files if needed"
            | Create_New_Binding_Files_Legacy -> "[obsolete]"

            | Force -> "force download and reinstallation of all dependencies"
            | Interactive -> "ask for every project whether to add the dependency"
            | Redirects -> "create binding redirects"
            | Clean_Redirects -> "remove binding redirects that were not created by Paket"
            | No_Install -> "do not add dependencies to projects"
            | Keep_Major -> "only allow updates that preserve the major version"
            | Keep_Minor -> "only allow updates that preserve the minor version"
            | Keep_Patch -> "only allow updates that preserve the patch version"
            | Touch_Affected_Refs -> "touch project files referencing affected dependencies to help incremental build tools detecting the change"

type ConfigArgs =
    | [<CustomCommandLine("add-credentials")>] AddCredentials of string
    | [<CustomCommandLine("add-token")>] AddToken of string * string
    | Username of string
    | Password of string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | AddCredentials(_) -> "add credentials for URL or credential key"
            | AddToken(_) -> "add token for URL or credential key"
            | Username(_) -> "provide username"
            | Password(_) -> "provide password"

type ConvertFromNugetArgs =
    | [<AltCommandLine("-f")>] Force
    | No_Install
    | No_Auto_Restore

    | Migrate_Credentials of mode:string
    | [<Hidden;CustomCommandLine("--creds-migrations")>] Migrate_Credentials_Legacy of mode:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Force -> "force the conversion even if paket.dependencies or paket.references files are present"
            | No_Install -> "do not add dependencies to projects"
            | No_Auto_Restore -> "do not enable Paket's auto-restore"

            | Migrate_Credentials(_) -> "specify mode for NuGet source credential migration: encrypt|plaintext|selective (default: encrypt)"
            | Migrate_Credentials_Legacy(_) -> "[obsolete]"

type FindRefsArgs =
    | [<Mandatory;MainCommandAttribute()>] NuGets of package_id:string list
    | [<Hidden;ExactlyOnce;CustomCommandLine("nuget")>] NuGets_Legacy of package_id:string list

    | [<AltCommandLine("-g")>] Group of name:string
    | [<Hidden;CustomCommandLine("group")>] Group_Legacy of name:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | NuGets(_) -> "list of NuGet package IDs"
            | NuGets_Legacy(_) -> "[obsolete]"

            | Group(_) -> "specify dependency group (default: Main group)"
            | Group_Legacy(_) -> "[obsolete]"

type InitArgs =
    | [<Hidden>] NoArg
with
    interface IArgParserTemplate with
        member __.Usage = ""

type AutoRestoreFlags = On | Off

type AutoRestoreArgs =
    | [<MainCommand;Mandatory>] Flags of AutoRestoreFlags
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Flags _ -> "enables or disables auto restore for the repo."

type InstallArgs =
    | [<AltCommandLine("-f")>] Force
    | Redirects
    | [<AltCommandLine("--createnewbindingfiles")>] Create_New_Binding_Files
    | Clean_Redirects
    | Keep_Major
    | Keep_Minor
    | Keep_Patch
    | [<CustomCommandLine("--generate-load-scripts")>] Generate_Load_Scripts
    | [<CustomCommandLine("--only-referenced")>] Install_Only_Referenced
    | [<CustomCommandLine("project-root")>] Project_Root of target:string
    | [<CustomCommandLine("load-script-framework")>] Load_Script_Framework of target:string
    | [<CustomCommandLine("load-script-type")>] Load_Script_Type of id:string
    | Touch_Affected_Refs
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Force -> "Forces the download and reinstallation of all packages."
            | Redirects -> "Creates binding redirects for the NuGet packages."
            | Create_New_Binding_Files -> "Creates binding redirect files if needed."
            | Clean_Redirects -> "Removes all binding redirects that are not specified by Paket."
            | Install_Only_Referenced -> "Only install packages that are referenced in paket.references files, instead of all packages in paket.dependencies."
            | Generate_Load_Scripts -> "Allows to generate C# and F# include scripts which references installed packages in a interactive environment like F# Interactive or ScriptCS."
            | Keep_Major -> "Allows only updates that are not changing the major version of the NuGet packages."
            | Keep_Minor -> "Allows only updates that are not changing the minor version of the NuGet packages."
            | Keep_Patch -> "Allows only updates that are not changing the patch version of the NuGet packages."
            | Touch_Affected_Refs -> "Touches project files referencing packages which are affected, to help incremental build tools detecting the change."
            | Project_Root _ -> "Alternative project root [only used for tooling]."
            | Load_Script_Framework _ -> "Framework identifier to generate scripts for, such as net45 or net4."
            | Load_Script_Type _ -> "Language to generate scripts for, must be one of 'fsx' or 'csx'."

type OutdatedArgs =
    | Ignore_Constraints
    | [<CustomCommandLine("group")>] Group of name:string
    | [<AltCommandLine("--pre")>] Include_Prereleases
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Ignore_Constraints -> "Ignores the version requirement as in the paket.dependencies file."
            | Group(_) -> "Just check for one group."
            | Include_Prereleases -> "Includes prereleases."

type RemoveArgs =
    | [<CustomCommandLine("nuget")>][<Mandatory>] Nuget of package_id:string
    | [<CustomCommandLine("project")>] Project of name:string
    | [<CustomCommandLine("group")>] Group of name:string
    | [<AltCommandLine("-f")>] Force
    | [<AltCommandLine("-i")>] Interactive
    | No_Install
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Nuget(_) -> "NuGet package id."
            | Group(_) -> "Removes the package from the given group. If omitted the Main group is used."
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
    | [<CustomCommandLine("--ignore-checks")>] Ignore_Checks
    | [<CustomCommandLine("--fail-on-checks")>] Fail_On_Checks
    | [<CustomCommandLine("group")>] Group of name:string
    | [<Unique>] Project of file_name:string
    | [<Unique>] References_Files of file_name:string list
    | [<Unique>] Target_Framework of target_framework:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Force -> "Forces the download of all packages."
            | Group(_) -> "Allows to restore a single group."
            | Install_Only_Referenced -> "Allows to restore packages that are referenced in paket.references files, instead of all packages in paket.dependencies."
            | Touch_Affected_Refs -> "Touches project files referencing packages which are being restored, to help incremental build tools detecting the change."
            | Ignore_Checks -> "Skips the test if paket.dependencies and paket.lock are in sync."
            | Fail_On_Checks -> "Causes the restore to fail if any of the checks fail."
            | Project(_) -> "Allows to restore dependencies for a project."
            | References_Files(_) -> "Allows to restore all packages from the given paket.references files."
            | Target_Framework(_) -> "Allows to restore only for a specified target framework."

type SimplifyArgs =
    | [<AltCommandLine("-i")>] Interactive
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Interactive -> "confirm deletion of every transitive dependency"

type UpdateArgs =
    | [<CustomCommandLine("nuget")>] Nuget of package_id:string
    | [<CustomCommandLine("version")>] Version of version:string
    | [<CustomCommandLine("group")>] Group of name:string
    | [<AltCommandLine("-f")>] Force
    | Redirects
    | [<AltCommandLine("--createnewbindingfiles")>] Create_New_Binding_Files
    | Clean_Redirects
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
            | Create_New_Binding_Files -> "Creates binding redirect files if needed."
            | Clean_Redirects -> "Removes all binding redirects that are not specified by Paket."
            | No_Install -> "Skips paket install process (patching of csproj, fsproj, ... files) after the generation of paket.lock file."
            | Keep_Major -> "Allows only updates that are not changing the major version of the NuGet packages."
            | Keep_Minor -> "Allows only updates that are not changing the minor version of the NuGet packages."
            | Keep_Patch -> "Allows only updates that are not changing the patch version of the NuGet packages."
            | Filter -> "Treat the nuget parameter as a regex to filter packages rather than an exact match."
            | Touch_Affected_Refs -> "Touches project files referencing packages which are affected, to help incremental build tools detecting the change."

type FindPackagesArgs =
    | [<MainCommandAttribute()>] Search of package_id:string
    | [<Hidden;CustomCommandLine("searchtext")>] Search_Legacy of package_id:string

    | Source of source_feed:string
    | [<Hidden;CustomCommandLine("source")>] Source_Legacy of source_feed:string

    | [<CustomCommandLine("--max")>] Max_Results of int
    | [<Hidden;CustomCommandLine("max")>] Max_Results_Legacy of int
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Search(_) -> "search for NuGet package ID"
            | Search_Legacy(_) -> "[obsolete]"

            | Source(_) -> "specify source feed"
            | Source_Legacy(_) -> "[obsolete]"

            | Max_Results(_) -> "limit maximum number of results"
            | Max_Results_Legacy(_) -> "[obsolete]"

type FixNuspecArgs =
    | [<Mandatory>][<CustomCommandLine("file")>] File of text:string
    | [<Mandatory>][<CustomCommandLine("references-file")>] ReferencesFile of text:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | File _ -> "FileName of the nuspec file."
            | ReferencesFile _ -> "FileName of the nuspec file."

type FixNuspecsArgs =
    | [<Mandatory>][<CustomCommandLine("files")>] Files of nuspecPaths:string list
    | [<Mandatory>][<CustomCommandLine("references-file")>] ReferencesFile of referencePath:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Files _ -> "List of .nuspec files to fix transitive dependencies within."
            | ReferencesFile _ -> "FileName of the nuspec file."

type GenerateNuspecArgs =
    | [<CustomCommandLine "project">][<Mandatory>] Project of project:string
    | [<CustomCommandLine "dependencies">][<Mandatory>] DependenciesFile of dependenciesPath:string
    | [<CustomCommandLine "output">][<Mandatory>] Output of output:string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Project _ -> "Project to generate a nuspec file for."
            | DependenciesFile _ -> "'paket.dependencies' file used to populate the generated nuspec file."
            | Output _ -> "Output directory to save generated nuspec to"

type ShowInstalledPackagesArgs =
    | All
    | [<CustomCommandLine("project")>] Project of string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | All -> "Shows all installed packages (incl. transitive dependencies)."
            | Project(_) -> "Show only packages that are installed in the given project."

type ShowGroupsArgs =
    | [<Hidden;NoCommandLine>] PlaceHolder
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | PlaceHolder -> "Doesn't trace other output than installed packages."

type FindPackageVersionsArgs =
    | [<Mandatory;MainCommandAttribute()>] NuGet of package_id:string
    | [<Hidden;CustomCommandLine("nuget", "name")>] NuGet_Legacy of package_id:string

    | Source of source_feed:string
    | [<Hidden;CustomCommandLine("source")>] Source_Legacy of source_feed:string

    | [<CustomCommandLine("--max")>] Max_Results of int
    | [<Hidden;CustomCommandLine("max")>] Max_Results_Legacy of int
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | NuGet(_) -> "NuGet package ID"
            | NuGet_Legacy(_) -> "[obsolete]"

            | Source(_) -> "specify source feed"
            | Source_Legacy(_) -> "[obsolete]"

            | Max_Results(_) -> "limit maximum number of results"
            | Max_Results_Legacy(_) -> "[obsolete]"

type PackArgs =
    | [<CustomCommandLine("output")>][<Mandatory>] Output of path:string
    | [<CustomCommandLine("buildconfig")>] BuildConfig of config_name:string
    | [<CustomCommandLine("buildplatform")>] BuildPlatform of target:string
    | [<CustomCommandLine("version")>] Version of version:string
    | [<CustomCommandLine("templatefile")>] TemplateFile of path:string
    | [<CustomCommandLine("exclude")>] ExcludedTemplate of templateId:string
    | [<CustomCommandLine("specific-version")>] SpecificVersion of templateId:string * version:string
    | [<CustomCommandLine("releaseNotes")>] ReleaseNotes of text:string
    | [<CustomCommandLine("lock-dependencies")>] LockDependencies
    | [<CustomCommandLine("minimum-from-lock-file")>] LockDependenciesToMinimum
    | [<CustomCommandLine("pin-project-references")>] PinProjectReferences
    | [<CustomCommandLine("symbols")>] Symbols
    | [<CustomCommandLine("include-referenced-projects")>] IncludeReferencedProjects
    | [<CustomCommandLine("project-url")>] ProjectUrl of url:string
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
            | PinProjectReferences -> "Pin dependencies generated from project references (=) instead of using minimum (>=) for version specification.  If `lock-dependencies` is specified, project references will be pinned even if this option is not specified."
            | Symbols -> "Build symbol/source packages in addition to library/content packages."
            | IncludeReferencedProjects -> "Include symbol/source from referenced projects."
            | ProjectUrl(_) -> "Url to the projects home page."

type PushArgs =
    | [<CustomCommandLine("url")>][<Mandatory>] Url of url:string
    | [<CustomCommandLine("file")>][<Mandatory>] FileName of path:string
    | [<CustomCommandLine("apikey")>] ApiKey of key:string
    | [<CustomCommandLine("endpoint")>] EndPoint of path:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Url(_) -> "Url of the NuGet feed."
            | FileName(_) -> "Path to the package."
            | ApiKey(_) -> "Optionally specify your API key on the command line. Otherwise uses the value of the `nugetkey` environment variable."
            | EndPoint(_) -> "Optionally specify a custom api endpoint to push to. Defaults to `/api/v2/package`."

type GenerateLoadScriptsArgs =
    | [<AltCommandLine("-g")>] Groups of group:string list
    | [<Hidden;CustomCommandLine("groups")>] Groups_Legacy of group:string list

    | [<AltCommandLine("-f")>] Framework of framework:string
    | [<Hidden;CustomCommandLine("framework")>] Framework_Legacy of framework:string

    | [<AltCommandLine("-t")>] Type of script_type:string
    | [<Hidden;CustomCommandLine("type")>] Type_Legacy of script_type:string
with
  interface IArgParserTemplate with
      member this.Usage =
        match this with
        | Groups(_) -> "groups to generate scripts for (default: all groups)"
        | Groups_Legacy(_) -> "[obsolete]"

        | Framework(_) -> "framework identifier to generate scripts for, such as net45 or netstandard1.6"
        | Framework_Legacy(_) -> "[obsolete]"

        | Type(_) -> "language to generate scripts for, must be one of 'fsx' or 'csx'"
        | Type_Legacy(_) -> "[obsolete]"

type WhyArgs =
    | [<Mandatory;MainCommandAttribute()>] NuGet of package_id:string
    | [<Hidden;CustomCommandLine("nuget")>] NuGet_Legacy of package_id:string

    | [<AltCommandLine("-g")>] Group of name:string
    | [<Hidden;CustomCommandLine("group")>] Group_Legacy of name:string

    | Details
with
  interface IArgParserTemplate with
      member this.Usage =
        match this with
        | NuGet(_) -> "NuGet package ID"
        | NuGet_Legacy(_) -> "[obsolete]"

        | Group(_) -> "specify dependency group (default: Main group)"
        | Group_Legacy(_) -> "[obsolete]"

        | Details -> "display detailed information with all paths, versions and framework restrictions"

type Command =
    // global options
    |                                                   Version
    | [<AltCommandLine("-s");Inherit>]                  Silent
    | [<AltCommandLine("-v");Inherit>]                  Verbose
    | [<Inherit>]                                       Log_File of path:string
    | [<Inherit;Hidden>]                                From_Bootstrapper
    // subcommands
    | [<CustomCommandLine("add")>]                      Add of ParseResults<AddArgs>
    | [<CustomCommandLine("clear-cache")>]              ClearCache of ParseResults<ClearCacheArgs>
    | [<CustomCommandLine("config")>]                   Config of ParseResults<ConfigArgs>
    | [<CustomCommandLine("convert-from-nuget")>]       ConvertFromNuget of ParseResults<ConvertFromNugetArgs>
    | [<CustomCommandLine("find-refs")>]                FindRefs of ParseResults<FindRefsArgs>
    | [<CustomCommandLine("init")>]                     Init of ParseResults<InitArgs>
    | [<CustomCommandLine("auto-restore")>]             AutoRestore of ParseResults<AutoRestoreArgs>
    | [<CustomCommandLine("install")>]                  Install of ParseResults<InstallArgs>
    | [<CustomCommandLine("outdated")>]                 Outdated of ParseResults<OutdatedArgs>
    | [<CustomCommandLine("remove")>]                   Remove of ParseResults<RemoveArgs>
    | [<CustomCommandLine("restore")>]                  Restore of ParseResults<RestoreArgs>
    | [<CustomCommandLine("simplify")>]                 Simplify of ParseResults<SimplifyArgs>
    | [<CustomCommandLine("update")>]                   Update of ParseResults<UpdateArgs>
    | [<CustomCommandLine("find-packages")>]            FindPackages of ParseResults<FindPackagesArgs>
    | [<CustomCommandLine("find-package-versions")>]    FindPackageVersions of ParseResults<FindPackageVersionsArgs>
    | [<Hidden;CustomCommandLine("fix-nuspec")>]        FixNuspec of ParseResults<FixNuspecArgs>
    | [<CustomCommandLine("fix-nuspecs")>]              FixNuspecs of ParseResults<FixNuspecsArgs>
    | [<CustomCommandLine("generate-nuspec")>]          GenerateNuspec of ParseResults<GenerateNuspecArgs>
    | [<CustomCommandLine("show-installed-packages")>]  ShowInstalledPackages of ParseResults<ShowInstalledPackagesArgs>
    | [<CustomCommandLine("show-groups")>]              ShowGroups of ParseResults<ShowGroupsArgs>
    | [<CustomCommandLine("pack")>]                     Pack of ParseResults<PackArgs>
    | [<CustomCommandLine("push")>]                     Push of ParseResults<PushArgs>
    | [<Hidden;CustomCommandLine("generate-include-scripts")>] GenerateIncludeScripts of ParseResults<GenerateLoadScriptsArgs>
    | [<CustomCommandLine("generate-load-scripts")>]    GenerateLoadScripts of ParseResults<GenerateLoadScriptsArgs>
    | [<CustomCommandLine("why")>]                      Why of ParseResults<WhyArgs>
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Add _ -> "add a new dependency"
            | ClearCache _ -> "clear the NuGet and git cache directories"
            | Config _ -> "store global configuration values like NuGet credentials"
            | ConvertFromNuget _ -> "convert projects from NuGet to Paket"
            | FindRefs _ -> "find all project files that have a dependency installed"
            | Init _ -> "create an empty paket.dependencies file in the current working directory"
            | AutoRestore _ -> "manage automatic package restore during the build process inside Visual Studio"
            | Install _ -> "download dependencies and update projects"
            | Outdated _ -> "find dependencies that have newer versions available"
            | Remove _ -> "remove a dependency"
            | Restore _ -> "download the computed dependency graph"
            | Simplify _ -> "simplify declared dependencies by removing transitive dependencies"
            | Update _ -> "update dependencies to their latest version"
            | FindPackages _ -> "search for NuGet packages"
            | FindPackageVersions _ -> "search for dependency versions"
            | FixNuspec _ -> "[obsolete]"
            | FixNuspecs _ -> "patch a list of .nuspec files to correct transitive dependencies"
            | GenerateNuspec _ -> "generate a default nuspec for a project including its direct dependencies"
            | ShowInstalledPackages _ -> "show installed top-level packages"
            | ShowGroups _ -> "show groups"
            | Pack _ -> "create NuGet packages from paket.template files"
            | Push _ -> "push a NuGet package"
            | GenerateIncludeScripts _ -> "[obsolete]"
            | GenerateLoadScripts _ -> "generate F# and C# include scripts that reference installed packages in a interactive environment like F# Interactive or ScriptCS"
            | Why _ -> "determine why a dependency is required"
            | Log_File _ -> "print output to a file"
            | Silent -> "suppress console output"
            | Verbose -> "print detailed information to the console"
            | Version -> "show Paket version"
            | From_Bootstrapper -> "call coming from the '--run' feature of the bootstrapper"

let commandParser = ArgumentParser.Create<Command>(programName = "paket", errorHandler = new ProcessExiter())

let markdown (subParser : ArgumentParser) (width : int) (additionalText : string) =
    let ensureLineBreak (text : string) = if String.IsNullOrEmpty(text) then text else text + Environment.NewLine + Environment.NewLine
    let cleanUp (text : string) = text.Trim('\r', '\n') |> ensureLineBreak

    let parentMetadata = subParser.ParentInfo |> Option.get

    let syntax =
        subParser.PrintCommandLineSyntax(usageStringCharacterWidth = width)

    let options = subParser.PrintUsage(hideSyntax=true, usageStringCharacterWidth = width)

    let makeSentence t =
        let upcase (s:string) =
            s.Substring(0,1).ToUpper() + s.Substring(1)

        sprintf "%s." (upcase t)

    System.Text.StringBuilder()
        .Append("# paket ")
        .AppendLine(parentMetadata.Name)
        .AppendLine()
        .AppendLine(parentMetadata.Description |> makeSentence)
        .AppendLine()
        .AppendLine("```sh")
        .AppendLine(syntax)
        .AppendLine()
        .Append(options)
        .AppendLine("```")
        .AppendLine()
        .Append(additionalText |> cleanUp)
        .ToString()

let getAllCommands () = commandParser.GetSubCommandParsers()
