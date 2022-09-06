# Get started

This guide shows how to get started with Paket in various ways, depending on your scenario:

* [Get started with .NET Core (preferred)](#net-core-preferred)
* [Get started with the paket bootstrapper (legacy)](#install-the-paket-bootstrapper-legacy)
* [Convert from legacy NuGet](#convert-from-nuget)

## .NET Core (preferred)

Paket works entirely out of the box on .NET Core, and it's simple to get started.

1. Install .NET Core 3.0 or higher

   If you don't have it already, you'll need to [download and install the latest .NET Core](https://dotnet.microsoft.com/download).

2. Install and restore Paket as a local tool in the root of your codebase:

   ```sh
   dotnet new tool-manifest
   dotnet tool install paket
   dotnet tool restore
   ```

   This will create a `.config/dotnet-tools.json` file in the root of your codebase. It must be checked into source control.

3. Initialize Paket by creating a dependencies file.

   ```sh
   dotnet paket init
   ```

If you have a `build.sh`/`build.cmd` build script, also make sure you add the last two commands before you execute your build:

```sh
dotnet tool restore
dotnet paket restore
# Your call to build comes after the restore calls, possibly with FAKE: https://fake.build/
```

This will ensure Paket works in any .NET Core build environment.

Make sure to add the following entry to your `.gitignore`:

```
# Paket dependency manager
paket-files/
```

Next, [learn how to use Paket](learn-how-to-use-paket.html)

## Install the Paket bootstrapper (legacy)

If you're not using .NET Core, or you're stuck on .NET Core 2.2 or lower, you can use the paket bootstrapper.

1. Create a `.paket` directory in the root of your solution.
1. Download the latest [`paket.bootstrapper.exe`](https://github.com/fsprojects/Paket/releases/latest) into that directory.
1. Rename `.paket/paket.bootstrapper.exe` to `.paket/paket.exe`. [Read more about "magic mode"](bootstrapper.html#Magic-mode).
1. Commit `.paket/paket.exe` to your repository.
1. After the first `.paket/paket.exe` invocation Paket will create a couple of
   files in `.paket` — commit those as well.

Make sure to add the following entries to your `.gitignore`:

```
# Paket dependency manager
.paket/
paket-files/
```

Next, [learn how to use Paket](learn-how-to-use-paket.html)

### Convert from NuGet

If you are using legacy NuGet (`packages.config`-style), then check out the tutorial on how to automatically [convert from legacy nuget](convert-from-nuget-tutorial.html).
