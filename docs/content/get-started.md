# Get started

This guide shows how to get started with Paket in various ways, depending on your scenario:

* [Get started with the .NET tool](#Get-started-with-the-NET-tool)
* [Convert from legacy NuGet](#convert-from-nuget)

## Get started with the .NET tool

Paket is a .NET tool, and it's simple to get started.

1. Install the .NET SDK

   If you don't have it already, you'll need to [download and install the latest .NET SDK](https://dotnet.microsoft.com/download).

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

This will ensure Paket works in any .NET build environment. For a solution with projects that
don't use the .NET SDK, such as .NET Framework projects built by Visual Studio, also run
`dotnet paket auto-restore on`, see [installation](installation.html#Solutions-without-the-NET-SDK).

Make sure to add the following entry to your `.gitignore`:

```
# Paket dependency manager
paket-files/
```

Next, [learn how to use Paket](learn-how-to-use-paket.html)

### Convert from NuGet

If you are using legacy NuGet (`packages.config`-style), then check out the tutorial on how to automatically [convert from legacy nuget](convert-from-nuget-tutorial.html).
