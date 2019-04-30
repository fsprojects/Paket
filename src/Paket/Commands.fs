module Paket.Commands

open System

open Argu

type AddArgs =
    | [<ExactlyOnce;MainCommand>] NuGet of package_ID:string
    | [<Hidden;ExactlyOnce;CustomCommandLine("nuget")>] NuGet_Legacy of package_ID:string

    | [<Unique;AltCommandLine("-V")>] Version of version_constraint:string
    | [<Hidden;Unique;CustomCommandLine("version")>] Version_Legacy of version_constraint:string

    | [<Unique;AltCommandLine("-p")>] Project of path:string
    | [<Hidden;Unique;CustomCommandLine("project")>] Project_Legacy of path:string

    | [<Unique;AltCommandLine("-g")>] Group of name:string
    | [<Hidden;Unique;CustomCommandLine("group")>] Group_Legacy of name:string

    | [<Unique>] Create_New_Binding_Files
    | [<Hidden;Unique;CustomCommandLine("--createnewbindingfiles")>] Create_New_Binding_Files_Legacy

    | [<Unique;AltCommandLine("-f")>] Force
    | [<Unique;AltCommandLine("-i")>] Interactive
    | [<Unique>] Redirects
    | [<Unique>] Clean_Redirects
    | [<Unique>] No_Install
    | [<Unique>] No_Resolve
    | [<Unique>] Keep_Major
    | [<Unique>] Keep_Minor
    | [<Unique>] Keep_Patch
    | [<Unique>] Touch_Affected_Refs
    | [<Unique;AltCommandLine("-t")>] Type of packageType:AddArgsDependencyType
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
            | No_Resolve -> "do not resolve"
            | No_Install -> "do not modify projects"
            | Keep_Major -> "only allow updates that preserve the major version"
            | Keep_Minor -> "only allow updates that preserve the minor version"
            | Keep_Patch -> "only allow updates that preserve the patch version"
            | Touch_Affected_Refs -> "touch project files referencing affected dependencies to help incremental build tools detecting the change"
            | Type _ -> "the type of dependency: nuget|clitool (default: nuget)"
and [<RequireQualifiedAccess>] AddArgsDependencyType =
    | Nuget
    | Clitool

type AddGithubArgs =
    | [<ExactlyOnce;MainCommand>] Repository of repository_name:string
    | [<Unique;AltCommandLine("-V")>] Version of version_constraint:string
    | [<Unique;AltCommandLine("-g")>] Group of group_name:string
    | [<Unique>] File of file_name:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Repository(_) -> "repository name <author>/<repo> on github"
            | Version(_) -> "dependency version constraint"
            | Group(_) -> "add the dependency to a group (default: Main group)"
            | File(_) -> "only add specified file"
and GithubArgs =
    | [<CliPrefix(CliPrefix.None)>] Add of ParseResults<AddGithubArgs>
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Add(_) -> "add github repository"

type AddGitArgs =
    | [<ExactlyOnce;MainCommand>] Repository of repository_name:string
    | [<Unique;AltCommandLine("-V")>] Version of version_constraint:string
    | [<Unique;AltCommandLine("-g")>] Group of group_name:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Repository(_) -> "repository path or url"
            | Version(_) -> "dependency version, can be branch, commit-hash or tag"
            | Group(_) -> "add the dependency to a group (default: Main group)"
and GitArgs =
    | [<CliPrefix(CliPrefix.None)>] Add of ParseResults<AddGitArgs>
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Add(_) -> "add git repository"

type ConfigArgs =
    | [<Unique;CustomCommandLine("add-credentials")>] AddCredentials of key_or_URL:string
    | [<Unique;CustomCommandLine("add-token")>] AddToken of key_or_URL:string * token:string
    | [<Unique>] Username of username:string
    | [<Unique>] Password of password:string
    | [<Unique>] AuthType of authType:string
    | [<Unique;AltCommandLine>] Verify
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | AddCredentials(_) -> "add credentials for URL or credential key"
            | AddToken(_) -> "add token for URL or credential key"
            | Username(_) -> "provide username"
            | Password(_) -> "provide password"
            | AuthType (_) -> "specify authentication type: basic|ntlm (default: basic)"
            | Verify (_) -> "specify in case you want to verify the credentials"

type ConvertFromNugetArgs =
    | [<Unique;AltCommandLine("-f")>] Force
    | [<Unique>] No_Install
    | [<Unique>] No_Auto_Restore

    | [<Unique>] Migrate_Credentials of mode:string
    | [<Hidden;Unique;CustomCommandLine("--creds-migrations")>] Migrate_Credentials_Legacy of mode:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Force -> "force the conversion even if paket.dependencies or paket.references files are present"
            | No_Install -> "do not modify projects"
            | No_Auto_Restore -> "do not enable automatic package restore"

            | Migrate_Credentials(_) -> "specify mode for NuGet source credential migration: encrypt|plaintext|selective (default: encrypt)"
            | Migrate_Credentials_Legacy(_) -> "[obsolete]"

type FindRefsArgs =
    | [<ExactlyOnce;MainCommand>] NuGets of package_ID:string list
    | [<Hidden;ExactlyOnce;CustomCommandLine("nuget")>] NuGets_Legacy of package_ID:string list

    | [<Unique;AltCommandLine("-g")>] Group of name:string
    | [<Hidden;Unique;CustomCommandLine("group")>] Group_Legacy of name:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | NuGets(_) -> "list of NuGet package IDs"
            | NuGets_Legacy(_) -> "[obsolete]"

            | Group(_) -> "specify dependency group (default: Main group)"
            | Group_Legacy(_) -> "[obsolete]"

type InitArgs =
    | [<Hidden;NoCommandLine>] NoArgs
with
    interface IArgParserTemplate with
        member __.Usage = ""

type AutoRestoreFlags = On | Off

type AutoRestoreArgs =
    | [<MainCommand;ExactlyOnce>] Flags of AutoRestoreFlags
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Flags(_) -> "enable or disable automatic package restore"

type LanguageFlags = Csx | Fsx

type InstallArgs =
    | [<Unique;AltCommandLine("-f")>] Force
    | [<Unique>] Redirects

    | [<Unique>] Create_New_Binding_Files
    | [<Hidden;Unique;CustomCommandLine("--createnewbindingfiles")>] Create_New_Binding_Files_Legacy

    | [<Unique>] Clean_Redirects
    | [<Unique>] Keep_Major
    | [<Unique>] Keep_Minor
    | [<Unique>] Keep_Patch
    | [<Unique;CustomCommandLine("--only-referenced")>] Install_Only_Referenced
    | [<Unique>] Touch_Affected_Refs
    | [<Hidden;Unique;CustomCommandLine("project-root")>] Project_Root of path:string

    | [<Unique>] Generate_Load_Scripts
    | Load_Script_Framework of framework:string
    | [<Hidden;CustomCommandLine("load-script-framework")>] Load_Script_Framework_Legacy of framework:string

    | Load_Script_Type of LanguageFlags
    | [<Hidden;CustomCommandLine("load-script-type")>] Load_Script_Type_Legacy of LanguageFlags
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Force -> "force download and reinstallation of all dependencies"
            | Redirects -> "create binding redirects"

            | Create_New_Binding_Files -> "create binding redirect files if needed"
            | Create_New_Binding_Files_Legacy -> "[obsolete]"

            | Clean_Redirects -> "remove binding redirects that were not created by Paket"
            | Install_Only_Referenced -> "only install dependencies listed in paket.references files, instead of all packages in paket.dependencies"
            | Keep_Major -> "only allow updates that preserve the major version"
            | Keep_Minor -> "only allow updates that preserve the minor version"
            | Keep_Patch -> "only allow updates that preserve the patch version"
            | Touch_Affected_Refs -> "touch project files referencing affected dependencies to help incremental build tools detecting the change"
            | Project_Root(_) -> "alternative project root (only used for tooling)"

            | Generate_Load_Scripts -> "generate F# and C# include scripts that reference installed packages in a interactive environment like F# Interactive or ScriptCS"
            | Load_Script_Framework(_) -> "framework identifier to generate scripts for, such as net45 or netstandard1.6; may be repeated"
            | Load_Script_Framework_Legacy(_) -> "[obsolete]"

            | Load_Script_Type(_) -> "language to generate scripts for; may be repeated; may be repeated"
            | Load_Script_Type_Legacy(_) -> "[obsolete]"

type OutdatedArgs =
    | [<Unique;AltCommandLine("-f")>] Force
    | [<Unique>] Ignore_Constraints

    | [<Unique;AltCommandLine("-g")>] Group of name:string
    | [<Hidden;Unique;CustomCommandLine("group")>] Group_Legacy of name:string

    | [<Unique;AltCommandLine("--pre")>] Include_Prereleases
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Force -> "force download and reinstallation of all dependencies"
            | Ignore_Constraints -> "ignore version constraints in the paket.dependencies file"

            | Group(_) -> "specify dependency group (default: all groups)"
            | Group_Legacy(_) -> "[obsolete]"

            | Include_Prereleases -> "consider prerelease versions as updates"

type RemoveArgs =
    | [<ExactlyOnce;MainCommand>] NuGet of package_ID:string
    | [<Hidden;ExactlyOnce;CustomCommandLine("nuget")>] NuGet_Legacy of package_ID:string

    | [<Unique;AltCommandLine("-p")>] Project of path:string
    | [<Hidden;Unique;CustomCommandLine("project")>] Project_Legacy of path:string

    | [<Unique;AltCommandLine("-g")>] Group of name:string
    | [<Hidden;Unique;CustomCommandLine("group")>] Group_Legacy of name:string

    | [<Unique;AltCommandLine("-f")>] Force
    | [<Unique;AltCommandLine("-i")>] Interactive
    | [<Unique>] No_Install
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | NuGet(_) -> "NuGet package ID"
            | NuGet_Legacy(_) -> "[obsolete]"

            | Group(_) -> "remove the dependency from a group (default: Main group)"
            | Group_Legacy(_) -> "[obsolete]"

            | Project(_) -> "remove the dependency from a single project only"
            | Project_Legacy(_) -> "[obsolete]"

            | Force -> "force download and reinstallation of all dependencies"
            | Interactive -> "ask for every project whether to remove the dependency"
            | No_Install -> "do not modify projects"

type ClearCacheArgs =
    | [<Unique;CustomCommandLine("--clear-local")>] ClearLocal
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | ClearLocal -> "also clear local packages and paket-files directory"

type RestoreArgs =
    | [<Unique;AltCommandLine("-f")>] Force
    | [<Unique;CustomCommandLine("--only-referenced")>] Install_Only_Referenced
    | [<Unique>] Touch_Affected_Refs
    | [<Unique>] Ignore_Checks
    | [<Unique>] Fail_On_Checks

    | [<Unique;AltCommandLine("-g")>] Group of name:string
    | [<Hidden;Unique;CustomCommandLine("group")>] Group_Legacy of name:string

    | [<Unique;AltCommandLine("-p")>] Project of path:string
    | [<Hidden;Unique;CustomCommandLine("project")>] Project_Legacy of path:string

    | References_File of path:string
    | [<Hidden;CustomCommandLine("--references-files")>] References_File_Legacy of path:string list

    | [<Unique>] Target_Framework of framework:string
    | [<Unique>] Output_Path of path:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Force -> "force download and reinstallation of all dependencies"

            | Group(_) -> "restore dependencies of a single group"
            | Group_Legacy(_) -> "[obsolete]"

            | Install_Only_Referenced -> "only restore packages that are referenced by paket.references files"
            | Touch_Affected_Refs -> "touch project files referencing affected dependencies to help incremental build tools detecting the change"
            | Ignore_Checks -> "do not check if paket.dependencies and paket.lock are in sync"
            | Fail_On_Checks -> "abort if any checks fail"

            | Project(_) -> "restore dependencies of a single project"
            | Project_Legacy(_) -> "[obsolete]"

            | References_File(_) -> "restore packages from a paket.references file; may be repeated"
            | References_File_Legacy(_) -> "[obsolete]"

            | Target_Framework(_) -> "restore only for the specified target framework"
            | Output_Path(_) -> "Output path directory of MSBuild. When used in combination with the new dotnet cli based sdk, paket will write supporting files (nuget.config, paket.resolved) there"

type SimplifyArgs =
    | [<Unique;AltCommandLine("-i")>] Interactive
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Interactive -> "confirm deletion of every transitive dependency"

type UpdateArgs =
    | [<ExactlyOnce;MainCommand>] NuGet of package_id:string
    | [<Hidden;ExactlyOnce;CustomCommandLine("nuget")>] NuGet_Legacy of package_id:string

    | [<Unique;AltCommandLine("-V")>] Version of version_constraint:string
    | [<Hidden;Unique;CustomCommandLine("version")>] Version_Legacy of version_constraint:string

    | [<Unique;AltCommandLine("-g")>] Group of name:string
    | [<Hidden;Unique;CustomCommandLine("group")>] Group_Legacy of name:string

    | [<Unique>] Create_New_Binding_Files
    | [<Hidden;Unique;CustomCommandLine("--createnewbindingfiles")>] Create_New_Binding_Files_Legacy

    | [<Unique;AltCommandLine("-f")>] Force
    | [<Unique>] Redirects
    | [<Unique>] Clean_Redirects
    | [<Unique>] No_Install
    | [<Unique>] Keep_Major
    | [<Unique>] Keep_Minor
    | [<Unique>] Keep_Patch
    | [<Unique>] Filter
    | [<Unique>] Touch_Affected_Refs
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | NuGet(_) -> "NuGet package ID"
            | NuGet_Legacy(_) -> "[obsolete]"

            | Group(_) -> "specify dependency group to update (default: all groups)"
            | Group_Legacy(_) -> "[obsolete]"

            | Version(_) -> "dependency version constraint"
            | Version_Legacy(_) -> "[obsolete]"

            | Create_New_Binding_Files -> "create binding redirect files if needed"
            | Create_New_Binding_Files_Legacy -> "[obsolete]"

            | Force -> "force download and reinstallation of all dependencies"
            | Redirects -> "create binding redirects"
            | Clean_Redirects -> "remove binding redirects that were not created by Paket"
            | No_Install -> "do not modify projects"
            | Keep_Major -> "only allow updates that preserve the major version"
            | Keep_Minor -> "only allow updates that preserve the minor version"
            | Keep_Patch -> "only allow updates that preserve the patch version"
            | Touch_Affected_Refs -> "touch project files referencing affected dependencies to help incremental build tools detecting the change"
            | Filter -> "treat the NuGet package ID as a regex to filter packages"

type FindPackagesArgs =
    | [<ExactlyOnce;MainCommand>] Search of package_ID:string
    | [<Hidden;ExactlyOnce;CustomCommandLine("searchtext")>] Search_Legacy of package_ID:string

    | [<Unique>] Source of source_URL:string
    | [<Hidden;Unique;CustomCommandLine("source")>] Source_Legacy of source_URL:string

    | [<Unique;CustomCommandLine("--max")>] Max_Results of int
    | [<Hidden;Unique;CustomCommandLine("max")>] Max_Results_Legacy of int
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Search(_) -> "search for NuGet package ID"
            | Search_Legacy(_) -> "[obsolete]"

            | Source(_) -> "specify source URL"
            | Source_Legacy(_) -> "[obsolete]"

            | Max_Results(_) -> "limit maximum number of results"
            | Max_Results_Legacy(_) -> "[obsolete]"

type FixNuspecArgs =
    | [<ExactlyOnce;CustomCommandLine("file")>] File of text:string
    | [<ExactlyOnce;CustomCommandLine("references-file")>] ReferencesFile of text:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | File _ -> ".nuspec file to fix transitive dependencies within"
            | ReferencesFile _ -> "paket.references to use"

type FixNuspecsArgs =
    | [<ExactlyOnce;CustomCommandLine("files")>] Files of nuspecPaths:string list
    | [<CustomCommandLine("references-file")>] ReferencesFile of referencePath:string
    | [<CustomCommandLine("project-file")>] ProjectFile of referencePath:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Files _ -> ".nuspec files to fix transitive dependencies within"
            | ReferencesFile _ -> "paket.references to use"
            | ProjectFile _ -> "the project file to use"

type GenerateNuspecArgs =
    | [<ExactlyOnce;CustomCommandLine "project">] Project of project:string
    | [<ExactlyOnce;CustomCommandLine "dependencies">] DependenciesFile of dependenciesPath:string
    | [<ExactlyOnce;CustomCommandLine "output">] Output of output:string
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Project _ -> "generate .nuspec for project"
            | DependenciesFile _ -> "paket.dependencies file used to populate .nuspec file"
            | Output _ -> "output directory of the .nuspec file"

type ShowInstalledPackagesArgs =
    | [<Unique;AltCommandLine("-a")>] All

    | [<Unique;AltCommandLine("-p")>] Project of path:string
    | [<Hidden;Unique;CustomCommandLine("project")>] Project_Legacy of path:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | All -> "include transitive dependencies"

            | Project(_) -> "specify project to show dependencies for"
            | Project_Legacy(_) -> "[obsolete]"

type ShowGroupsArgs =
    | [<Hidden;NoCommandLine>] NoArgs
with
    interface IArgParserTemplate with
        member __.Usage = ""

type FindPackageVersionsArgs =
    | [<ExactlyOnce;MainCommand>] NuGet of package_ID:string
    | [<Hidden;ExactlyOnce;CustomCommandLine("nuget", "name")>] NuGet_Legacy of package_ID:string

    | [<Unique>] Source of source_URL:string
    | [<Hidden;Unique;CustomCommandLine("source")>] Source_Legacy of source_URL:string

    | [<Unique;CustomCommandLine("--max")>] Max_Results of int
    | [<Hidden;Unique;CustomCommandLine("max")>] Max_Results_Legacy of int
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | NuGet(_) -> "NuGet package ID"
            | NuGet_Legacy(_) -> "[obsolete]"

            | Source(_) -> "specify source URL"
            | Source_Legacy(_) -> "[obsolete]"

            | Max_Results(_) -> "limit maximum number of results"
            | Max_Results_Legacy(_) -> "[obsolete]"

type InfoArgs =
    | [<Unique>] Paket_Dependencies_Dir
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Paket_Dependencies_Dir -> "absolute path of paket.dependencies directory, if exists"

type InterprojectReferencesConstraintArgs =
    | Min
    | Fix
    | Keep_Major
    | Keep_Minor
    | Keep_Patch

type PackArgs =
    | [<ExactlyOnce;MainCommand>] Output of path:string
    | [<Hidden;ExactlyOnce;CustomCommandLine("output")>] Output_Legacy of path:string

    | [<Unique>] Build_Config of configuration:string
    | [<Hidden;Unique;CustomCommandLine("buildconfig")>] Build_Config_Legacy of configuration:string

    | [<Unique>] Build_Platform of platform:string
    | [<Hidden;Unique;CustomCommandLine("buildplatform")>] Build_Platform_Legacy of platform:string

    | [<Unique>] Version of version:string
    | [<Hidden;Unique;CustomCommandLine("version")>] Version_Legacy of version:string

    | [<Unique;CustomCommandLine("--template")>] Template_File of path:string
    | [<Hidden;Unique;CustomCommandLine("templatefile")>] Template_File_Legacy of path:string

    | [<CustomCommandLine("--exclude")>] Exclude_Template of package_ID:string
    | [<Hidden;CustomCommandLine("exclude")>] Exclude_Template_Legacy of package_ID:string

    |  Specific_Version of package_ID:string * version:string
    | [<Hidden;CustomCommandLine("specific-version")>] Specific_Version_Legacy of package_ID:string * version:string

    | [<Unique>] Release_Notes of text:string
    | [<Hidden;Unique;CustomCommandLine("releaseNotes")>] Release_Notes_Legacy of text:string

    | [<Unique>] Lock_Dependencies
    | [<Hidden;Unique;CustomCommandLine("lock-dependencies")>] Lock_Dependencies_Legacy

    | [<Unique;CustomCommandLine("--minimum-from-lock-file")>] Lock_Dependencies_To_Minimum
    | [<Hidden;Unique;CustomCommandLine("minimum-from-lock-file")>] Lock_Dependencies_To_Minimum_Legacy

    | [<Unique>] Pin_Project_References
    | [<Hidden;Unique;CustomCommandLine("pin-project-references")>] Pin_Project_References_Legacy

    | [<Unique>] Interproject_References of InterprojectReferencesConstraintArgs

    | [<Unique>] Symbols
    | [<Hidden;Unique;CustomCommandLine("symbols")>] Symbols_Legacy

    | [<Unique>] Include_Referenced_Projects
    | [<Hidden;Unique;CustomCommandLine("include-referenced-projects")>] Include_Referenced_Projects_Legacy

    | [<Unique>] Project_Url of URL:string
    | [<Hidden;Unique;CustomCommandLine("project-url")>] Project_Url_Legacy of URL:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Output(_) -> "output directory for .nupkg files"
            | Output_Legacy(_) -> "[obsolete]"

            | Build_Config(_) -> "build configuration that should be packaged (default: Release)"
            | Build_Config_Legacy(_) -> "[obsolete]"

            | Build_Platform(_) -> "build platform that should be packaged (default: check all known platform targets)"
            | Build_Platform_Legacy(_) -> "[obsolete]"

            | Version(_) -> "version of the package"
            | Version_Legacy(_) -> "[obsolete]"

            | Template_File(_) -> "pack a single paket.template file"
            | Template_File_Legacy(_) -> "[obsolete]"

            | Exclude_Template(_) -> "exclude paket.template file by package ID; may be repeated"
            | Exclude_Template_Legacy(_) -> "[obsolete]"

            | Specific_Version(_) -> "version number to use for package ID; may be repeated"
            | Specific_Version_Legacy(_) -> "[obsolete]"

            | Release_Notes(_) -> "release notes"
            | Release_Notes_Legacy(_) -> "[obsolete]"

            | Lock_Dependencies -> "use version constraints from paket.lock instead of paket.dependencies"
            | Lock_Dependencies_Legacy(_) -> "[obsolete]"

            | Lock_Dependencies_To_Minimum -> "use version constraints from paket.lock instead of paket.dependencies and add them as a minimum version; --lock-dependencies overrides this option"
            | Lock_Dependencies_To_Minimum_Legacy(_) -> "[obsolete]"

            | Pin_Project_References -> "pin dependencies generated from project references to exact versions (=) instead of using minimum versions (>=); with --lock-dependencies project references will be pinned even if this option is not specified"
            | Pin_Project_References_Legacy(_) -> "[obsolete]"

            | Interproject_References(_) -> "set constraints for referenced project versions"

            | Symbols -> "create symbol and source packages in addition to library and content packages"
            | Symbols_Legacy(_) -> "[obsolete]"

            | Include_Referenced_Projects -> "include symbols and source from referenced projects"
            | Include_Referenced_Projects_Legacy(_) -> "[obsolete]"

            | Project_Url(_) -> "homepage URL for the package"
            | Project_Url_Legacy(_) -> "[obsolete]"

type PushArgs =
    | [<ExactlyOnce;MainCommand>] Package of path:string
    | [<Hidden;ExactlyOnce;CustomCommandLine("file")>] Package_Legacy of path:string

    | [<Unique>] Url of url:string
    | [<Hidden;Unique;CustomCommandLine("url")>] Url_Legacy of url:string

    | [<Unique>] Api_Key of key:string
    | [<Hidden;Unique;CustomCommandLine("apikey")>] Api_Key_Legacy of key:string

    | [<Unique>] Endpoint of path:string
    | [<Hidden;Unique;CustomCommandLine("endpoint")>] Endpoint_Legacy of path:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Package(_) -> "path to the .nupkg file"
            | Package_Legacy(_) -> "[obsolete]"

            | Url(_) -> "URL of the NuGet feed"
            | Url_Legacy(_) -> "[obsolete]"

            | Api_Key(_) -> "API key for the URL (default: value of the NUGET_KEY environment variable)"
            | Api_Key_Legacy(_) -> "[obsolete]"

            | Endpoint(_) -> "API endpoint to push to (default: /api/v2/package)"
            | Endpoint_Legacy(_) -> "[obsolete]"

type GenerateLoadScriptsArgs =
    | [<AltCommandLine("-g")>] Group of name:string
    | [<Hidden;CustomCommandLine("groups")>] Group_Legacy of name:string list

    | [<AltCommandLine("-f")>] Framework of framework:string
    | [<Hidden;CustomCommandLine("framework")>] Framework_Legacy of framework:string

    | [<AltCommandLine("-t")>] Type of LanguageFlags
    | [<Hidden;CustomCommandLine("type")>] Type_Legacy of LanguageFlags
with
  interface IArgParserTemplate with
      member this.Usage =
        match this with
        | Group(_) -> "groups to generate scripts for (default: all groups); may be repeated"
        | Group_Legacy(_) -> "[obsolete]"

        | Framework(_) -> "framework identifier to generate scripts for, such as net45 or netstandard1.6; may be repeated"
        | Framework_Legacy(_) -> "[obsolete]"

        | Type(_) -> "language to generate scripts for; may be repeated"
        | Type_Legacy(_) -> "[obsolete]"

type WhyArgs =
    | [<ExactlyOnce;MainCommand>] NuGet of package_ID:string
    | [<Hidden;ExactlyOnce;CustomCommandLine("nuget")>] NuGet_Legacy of package_ID:string

    | [<Unique;AltCommandLine("-g")>] Group of name:string
    | [<Hidden;Unique;CustomCommandLine("group")>] Group_Legacy of name:string

    | [<Unique>] Details
with
  interface IArgParserTemplate with
      member this.Usage =
        match this with
        | NuGet(_) -> "NuGet package ID"
        | NuGet_Legacy(_) -> "[obsolete]"

        | Group(_) -> "specify dependency group (default: Main group)"
        | Group_Legacy(_) -> "[obsolete]"

        | Details -> "display detailed information with all paths, versions and framework restrictions"

type RestrictionArgs =
    | [<ExactlyOnce;MainCommand>] Restriction of restrictionRaw:string
with
  interface IArgParserTemplate with
      member this.Usage =
        match this with
        | Restriction(_) -> "The restriction to resolve"

type Command =
    // global options
    |                                                   Version
    | [<AltCommandLine("-s");Inherit>]                  Silent
    | [<AltCommandLine("-v");Inherit>]                  Verbose
    | [<Inherit>]                                       Log_File of path:string
    | [<Hidden;Inherit>]                                From_Bootstrapper
    | [<Hidden;Inherit;Unique;CustomCommandLine("--enablenetfx461netstandard2support")>] EnableNetFx461NetStandard2Support
    // subcommands
    | [<CustomCommandLine("add")>]                      Add of ParseResults<AddArgs>
    | [<CustomCommandLine("github")>]                   Github of ParseResults<GithubArgs>
    | [<CustomCommandLine("git")>]                      Git of ParseResults<GitArgs>
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
    | [<CustomCommandLine("restriction")>]              Restriction of ParseResults<RestrictionArgs>
    | [<CustomCommandLine("info")>]                     Info of ParseResults<InfoArgs>
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Add _ -> "add a new dependency"
            | Github _ -> "commands to manipulate GitHub repository references"
            | Git _ -> "commands to manipulate git repository references"
            | ClearCache _ -> "clear the global and optionally local NuGet and cache directories"
            | Config _ -> "store global configuration values like NuGet credentials"
            | ConvertFromNuget _ -> "convert projects from NuGet to Paket"
            | FindRefs _ -> "find all project files that have a dependency installed"
            | Init _ -> "create an empty paket.dependencies file in the current working directory"
            | AutoRestore _ -> "manage automatic package restore during the build process inside Visual Studio"
            | Install _ -> "compute dependency graph, download dependencies and update projects"
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
            | Restriction _ -> "resolve a framework restriction and show details"
            | Info _ -> "info"
            | Log_File _ -> "print output to a file"
            | Silent -> "suppress console output"
            | Verbose -> "print detailed information to the console"
            | Version -> "show Paket version"
            | From_Bootstrapper -> "call coming from the '--run' feature of the bootstrapper"
            | EnableNetFx461NetStandard2Support -> "enable mapping when called from the bootstrapper. do not use manually."

let commandParser = ArgumentParser.Create<Command>(programName = "paket", errorHandler = new ProcessExiter(), checkStructure = false)

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
        .AppendLine(parentMetadata.Name.Value)
        .AppendLine()
        .AppendLine(parentMetadata.Description.Value |> makeSentence)
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
