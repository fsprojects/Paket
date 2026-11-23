# F# Interactive Integration

F# Interactive v5 and above ships with [extensions mechanism](https://github.com/fsharp/fslang-design/blob/dcc45b557f713a9aee75d85eae7555d41cd1cb0b/tooling/FST-1027-fsi-references.md).

FSharp.DependencyManager.Paket implements this extension mechanism to hook same power as [paket.dependencies](dependencies-file.html) file right inside .fsx scripts.

## Making sure Paket is found

The extension runs the Paket .NET tool. It looks for, in order:

* the local tool of the first `.config/dotnet-tools.json` or `dotnet-tools.json`
  up from the script that has Paket, without looking above an `isRoot` one (run
  `dotnet tool restore` first, in the folder that has `.config/dotnet-tools.json`
  or `dotnet-tools.json`),
* Paket installed with `dotnet tool install paket --tool-path`, in the folder
  hierarchy containing the script, directly or in a `.paket` folder; it is
  skipped with a warning when such a manifest declares Paket,
* a global tool, in `~/.dotnet/tools` or `~/.paket`,
* the most recent Paket .NET tool restored in the NuGet packages folder.

As dotnet, it doesn't look for a local tool in or above a tool manifest that isn't
JSON, which allows no comment or trailing comma, that isn't a JSON object, that has
no `isRoot` set to `true` or `false`, whose `tools` isn't an object, or whose Paket
tool has no version or one that NuGet refuses. It warns and runs the Paket that it
runs without a manifest that declares Paket, which may be another version, and fails
with the reason only when it finds none. As when the local tool isn't restored, the
error of that Paket repeats the reason, and its result isn't kept. It doesn't check
the rest: it still reads a manifest that dotnet refuses for another tool.

When the local tool isn't restored, a Paket installed with `--tool-path` is still
skipped: `#r "paket:"` warns and runs a global tool instead, or else the most recent
Paket .NET tool restored, either of which may be a version other than the one in the
manifest. When it finds neither, it fails and asks to run `dotnet tool restore`.
When the Paket that runs instead fails, the error of `#r "paket:"` ends by repeating
that the local tool isn't restored. When it succeeds, its result isn't kept: the
next run of the script runs Paket again, with the local tool once it is restored,
which installs the package versions written in `paket.lock` by the Paket that ran
instead, as `paket install` does.

The local tool is the `paket.dll` that `dotnet tool restore` recorded for its
version in `~/.dotnet/toolResolverCache`, or under `DOTNET_CLI_HOME` when it is set,
which is the one that `dotnet tool run` runs. When none is recorded, or that
`paket.dll` is gone, the local tool is looked for in the NuGet packages folder, and
then in `~/.nuget/packages`. The most recent Paket .NET tool is the most recent one
of these two folders.

The NuGet packages folder is `NUGET_PACKAGES` when it is set, without running
`dotnet`; a relative path, which NuGet refuses, is left out. When it isn't set,
`#r "paket:"` runs `dotnet nuget locals global-packages --list`, which gives the
`globalPackagesFolder` of the `nuget.config` files, or else `~/.nuget/packages`. It
runs it in the folder that has `.config/dotnet-tools.json` or `dotnet-tools.json`
for the local tool, as `dotnet tool restore` restores the tool there when run in
that folder, and in the folder of the script for the most recent Paket .NET tool.
It runs it with the first `dotnet` that has an SDK, looked for in the folders listed
below. When no `dotnet` has an SDK, or when the command fails, as with a
`global.json` that asks for an SDK that isn't installed, or gives no folder, only
`~/.nuget/packages` is looked at. A `dotnet` that doesn't exit within 30 seconds is
stopped, and the folder that it gave until then, if any, is taken. Each time the
script resolves its packages, the command runs at most once in each folder, and in
no other folder after a `dotnet` that didn't exit within 30 seconds: an SDK that
hangs delays the script by 30 seconds, not by 30 seconds for each folder.

The local tool and the one restored in the NuGet packages folder are each a
`paket.dll`, which runs with the first `dotnet` that has a .NET runtime the tool
runs on, looking at the one that runs the script, then in `DOTNET_ROOT`, the `PATH`
and the default install folders: `C:\Program Files\dotnet` (and then
`C:\Program Files (x86)\dotnet` for a 32-bit app) and
`%LocalAppData%\Microsoft\dotnet` on Windows, `/usr/local/share/dotnet`,
`/usr/share/dotnet`, `/usr/lib/dotnet` and `/usr/lib64/dotnet` on macOS and Linux,
and `~/.dotnet`. Paket 12 runs on .NET 10, or on a later version with a `dotnet`
that has no .NET 10, unless `DOTNET_ROLL_FORWARD` says otherwise, as `Minor` and
`Disable` do. When the manifest says `"rollForward": true` for Paket, as
`dotnet tool install --allow-roll-forward` writes it, the local tool runs with
`dotnet --roll-forward Major`, as `dotnet tool run` does, whatever
`DOTNET_ROLL_FORWARD` says, even `LatestMajor`: on the major version it targets
with a `dotnet` that has it, and on a later one with a `dotnet` that doesn't. The
most recent Paket .NET tool restored, which runs instead when the local tool isn't
restored, runs without `--roll-forward Major`. A `dotnet` whose runtimes aren't
known, such as the link `/usr/bin/dotnet`, comes after the ones known to have a
runtime that the tool runs on, and a `dotnet` known to have none is only taken when
no other one may have such a runtime: its error then names the runtime that is
missing, which you can install, or set `DOTNET_ROOT` to the folder of a `dotnet`
that has it. When no `dotnet` is found, set `DOTNET_ROOT` to the folder of `dotnet`
in the environment of the IDE or app that runs F# Interactive, or add that folder
to its `PATH`.

A Paket installed with `--tool-path` or as a global tool is an executable that finds
.NET by itself, as any .NET app does: through `DOTNET_ROOT` or the location where
the .NET installer put it, but not through the `PATH`, so an install by the
dotnet-install scripts, in `~/.dotnet` or `%LocalAppData%\Microsoft\dotnet`, needs
`DOTNET_ROOT` in the environment of the IDE or app that runs F# Interactive.

## Install the extension

You have two choices to deploy the assembly:

### Copying the assembly next to the host process

Install the FSharp.DependencyManager.Paket.dll aside of your F# Interactive binaries or the binary of the host process loading the extensions.

In general, the process will explicitly list the folders it is currently checking for extensions:

```text
error FS3216: Package manager key 'paket' was not registered in [C:\Program Files\dotnet\sdk\5.0.100\FSharp; C:\Program Files\dotnet\sdk\5.0.100\FSharp\], []. Currently registered: nuget
```

The locations to copy `FSharp.DependencyManager.Paket.dll`

Dotnet SDK:
 - Windows  
   `C:\Program Files\dotnet\sdk\<version number>\FSharp`
 - macOS  
   `/usr/local/share/dotnet/sdk/<version number>/FSharp`
 - Linux  
   `/home/user/dotnet/sdk/<version number>/FSharp`

Visual Studio:
 - For Editor Support  
   `C:\Program Files\Microsoft Visual Studio\<version number>\Community\Common7\IDE\CommonExtensions\Microsoft\FSharp`
 - For Fsi Window Support  
   `C:\Program Files\Microsoft Visual Studio\<version number>\Community\Common7\IDE\CommonExtensions\Microsoft\FSharp\Tools`


### Passing the folder of the extension as --compilertool flag

Use the `--compilertool` flag when invoking F# Interactive or refer to the documentation of the host process.

For example on windows:

```text
--compilertool:"c:\users\username\.nuget\packages\fsharp.dependencymanager.paket\6.0.0-alpha055\lib\netstandard2.0"
```

For example on unix:

```text
--compilertool:"~/.nuget/packages/fsharp.dependencymanager.paket/6.0.0-alpha055/lib/netstandard2.0"
```

## Usage in scripts

Using it involves the `#r` directive with `paket` extension.

Here is an example importing FSharp.Data library:

```fsharp
#r "paket: nuget FSharp.Data"

open FSharp.Data
// do cool stuff
```

It is also possible to use github dependencies to import individual files:

```fsharp
#r "paket: github fsharp/FAKE src/legacy/FakeLib/Globbing/Globbing.fs"
#load @"fsharp\FAKE\src\legacy\FakeLib\Globbing\Globbing.fs"
 
let f = Fake.Globbing.search
```

## Usage in FSharp.Compiler.Service

Same remarks as for installation section above, you'd either make sure the assembly is deployed aside the host process binary, or you'd pass `--compilertool` flag when building the project options (through `GetProjectOptionsFromScript`).
