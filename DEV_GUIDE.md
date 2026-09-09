# Notes about development in this repository

Please contribute any notes that made your contributions easier here.

Note that historically, the bulk of the development occured on Windows before dotnet got cross platform, for now, the tooling to target .NET Framework is still required for some areas.

# Notes about the build script

`build.cmd` and `build.sh` run `build.fsx` with `dotnet fsi`. FAKE is used as a library only: there
is no FAKE runner to install, and its version no longer has to match the .NET SDK. The FAKE modules
come from the `BuildScript` group of `paket.dependencies` and are loaded through the scripts that
the `paket generate-load-scripts` call in `build.cmd` / `build.sh` writes to
`.paket/load/net10.0/BuildScript/`, so running `dotnet fsi build.fsx` directly assumes one of those
entry points has been run at least once.

Targets and parameters are unchanged, so `./build.sh <Target> [key=value ...]` keeps working as
before: the script turns the `key=value` arguments into environment variables itself, which is what
the FAKE runner used to do, and passes the target name to FAKE as `--target` through the execution
context it creates. Only one target name is accepted.

Mono is still needed on Linux for the targets that execute .NET Framework binaries: the `net461`
test passes and `PublishNuGet`, which pushes with the merged `net461` `paket.exe`. `MergePaketTool`
no longer needs it, it repacks through the `dotnet-ilrepack` tool of `.config/dotnet-tools.json`.
Removing the remaining Mono dependency is tracked separately in
[#4348](https://github.com/fsprojects/Paket/issues/4348).

# Notes about the Paket F# Interactive extension

Some pointers to help efforts to foster the F# Interactive extension ecosystem:

* integrations in IDEs:
  * Rider: take inspiration from [how Rider does autocomplete for nuget package in `#r "nuget: "`](https://github.com/JetBrains/resharper-fsharp/pull/539)
  * Ionide setting to [use FSAC path to pass as an extra `--compilertool:` argument to FSI](https://github.com/ionide/ionide-vscode-fsharp/pull/1959)
  * Proposal to [ship the paket extension with FSAC](https://github.com/fsharp/FsAutoComplete/pull/1198)
* keeping the documentation refering to the extensions mechanism in shape:
  * [F# Interactive options](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-interactive-options)
  * [F# Interactive options](https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-interactive-options)
  * [Paket extension documentation](https://github.com/fsprojects/Paket/blob/master/docs/content/fsi-integration.md)
  * [Documentation about extensions (https://aka.ms/dotnetdepmanager)](https://github.com/dotnet/fsharp/blob/main/src/FSharp.DependencyManager.Nuget/README.md)
* Discussion about a [stable location / mechanism so extensions are loaded in a single place by all tooling and variants of F# Interactive](https://github.com/dotnet/fsharp/issues/8880)

# Updates to support new major SDK releases

This is overall a stress free process, you can take example on previous PRs:

* [dotnet 8](https://github.com/fsprojects/Paket/pull/4219)
* [dotnet 7](https://github.com/fsprojects/Paket/pull/4219](https://github.com/fsprojects/Paket/commit/c6e1bd1b67f26ce29417cd853425ccf33fc067c0)
* [dotnet 6](https://github.com/fsprojects/Paket/pull/4013)
* [dotnet 4.8.1](https://github.com/fsprojects/Paket/pull/4227)
