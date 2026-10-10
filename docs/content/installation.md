# Paket installation

This guide will show you

* How to install Paket as a .NET tool, for a repository or for your machine.
* How to restore packages before the build of a solution without the .NET SDK.
* How to move from `paket.exe` and the bootstrapper.
* Install [editor support](editor-support.html).
* Set up [shell completion](shell-completion.html) for Paket commands.

Paket is a .NET tool: it needs the [.NET SDK](https://dotnet.microsoft.com/download) and runs on
Windows, Linux and macOS. It manages the dependencies of any project, including the projects that
target the .NET Framework.

## Local tool

A local tool pins the version of Paket for the repository, so that everyone and every build
server uses the same one. In the root of the repository:

```sh
dotnet new tool-manifest

dotnet tool install paket
```

Don't forget to commit `.config/dotnet-tools.json` to your source control. After a clone, run
`dotnet tool restore` to install the tools it lists, then run Paket with:

```sh
dotnet paket --help
```

## Global tool

A global tool puts the `paket` command on your `PATH`, for every repository of your machine:

```sh
dotnet tool install --global paket
```

You can also install Paket into a folder of the repository, and run it from there:

```sh
dotnet tool install paket --tool-path .paket
```

`Paket.Restore.targets` and `paket.targets` find Paket when it is installed with `--tool-path` in
`.paket` or in the root of the repository, as a local tool, or on the `PATH`, in that order.

## Solutions without the .NET SDK

The projects that use the .NET SDK restore their packages through `Paket.Restore.targets`, which
`paket install` adds to them. For the other projects, such as the projects of a .NET Framework
solution built by Visual Studio, run:

```sh
dotnet paket auto-restore on
```

It writes `.paket/paket.targets` and imports it into those projects, so that they run
`paket restore` before their build. Commit `.paket/paket.targets` as well. Paket can be a local
tool, a global tool or installed with `--tool-path`: `paket.targets` finds it the same way.

## Moving from paket.exe and the bootstrapper

From Paket 12.0, `paket.exe` and `paket.bootstrapper.exe` are no longer published. A bootstrapper
older than 11.0 downloads a `paket.exe` that only explains how to move on, and fails the build.
The bootstrapper of Paket 11 never goes past the 11.x releases.

To move to the .NET tool:

1. Install Paket as a [local tool](#Local-tool) in the root of the repository.
1. Delete `.paket/paket.exe`, `.paket/paket.exe.config` and `.paket/paket.bootstrapper.exe`, and
   remove the `version` line from `paket.dependencies` if there is one: only the bootstrapper read
   it.
1. In your build scripts, run `dotnet tool restore` before Paket, and replace `.paket/paket.exe`
   with `dotnet paket` (without `mono` in front of it).
1. If you use `paket auto-restore`, run `dotnet paket auto-restore on` again to update
   `.paket/paket.targets`.

To keep `paket.exe` for now, pin Paket 11 with a `version 11.0.0` line in `paket.dependencies`, or
use the bootstrapper of Paket 11.

### Post installation

Once the basic installation is complete it is often very useful to add some tools to your
shell/IDE/Text Editor of choice.

* [Editor support](editor-support.html)
* [Shell completion](shell-completion.html)

For next steps check out the [Getting Started](get-started.html) section.
