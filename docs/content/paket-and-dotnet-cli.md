# Paket and the .NET SDK / .NET Core CLI tools (dotnet CLI and MSBuild 15)

Paket provides support for [.NET SDK](https://github.com/dotnet/sdk)-based
projects that are used with [the `dotnet` CLI](https://github.com/dotnet/cli)
(running with [.NET Core](https://github.com/dotnet/core)) or with MSBuild 15
(Visual Studio 2017 and Mono 5).

The general workflow is not very different from using Paket with traditional
.NET projects which it is described in the
["Getting started" tutorial](getting-started.html).

## Setup

### Downloading Paket's Bootstrapper

For `dotnet` CLI to work properly Paket needs to be used in
["magic mode"](bootstrapper.html#Magic-mode).

1. Create a `.paket` directory in the root of your solution.
1. Download the latest
   [`paket.bootstrapper.exe`](https://github.com/fsprojects/Paket/releases/latest)
   into that directory.
1. Rename `.paket/paket.bootstrapper.exe` to `.paket/paket.exe`.
   [Read more about "magic mode"](bootstrapper.html#Magic-mode).
1. Commit `.paket/paket.exe` to your repository.
1. After the first `.paket/paket.exe` invocation Paket will create a couple of
   files in `.paket` — commit those as well.

There are already a couple of `dotnet` [templates available](https://github.com/dotnet/templating/wiki/Available-templates-for-dotnet-new#f-templates) that ship with Paket
support. In that case you don't need to setup the bootstrapper manually.

### Specifying dependencies

Create a [`paket.dependencies` file](dependencies-file.html) in your project's
root and specify all your dependencies in it.

To create an empty `paket.dependencies` file, just run:

```sh
.paket/paket.exe init
```

This step is the same as with traditional .NET projects.

### Specifying dependencies for `dotnet` CLI tools

Paket 5.5 and later supports a new keyword for the
[`paket.dependencies` file](dependencies-file.html): The
[`clitool` reference](nuget-dependencies.html#Special-case-CLI-tools)
allows you to use specialized NuGet packages that provide `dotnet` CLI tools.

CLI tools are only available for .NET SDK-based projects.

> Consider setting `storage: none` in your dependencies file for the relevant groups to mirror NuGet behavior and
> not copy all dependencies to the packages folder. This will keep your repository folder small and clean.
> Please read the relevant section of the [`paket.dependencies` file](dependencies-file.html) documentation.

### Installing dependencies

Install all required packages with:

```sh
.paket/paket.exe install
```

This step is the same as with traditional .NET projects.

### Installing dependencies into projects

Like with traditional .NET projects you also need to put a
[`paket.references` files](references-files.html) alongside your MSBuild project
files.

After [`paket.references` files](references-files.html) files have been created,
run `dotnet restore` (see
[restoring packages](paket-and-dotnet-cli.html#Restoring-packages)) to update
your projects.

In contrast to traditional .NET projects Paket will not add assembly references
to your project files. Instead it will only generate a single line:

```xml
<Import Project="..\..\.paket\Paket.Restore.targets" />
```

This hook tells the .NET SDK to restore packages via
[Paket's `restore` mechanism](paket-restore.html). A nice benefit is that your
project files are now much cleaner and don't contain many assembly references.

### Restoring packages

**Note:** This is changed from the traditional .NET behavior.

In traditional .NET projects you were used to invoke the
[`restore` command](paket-restore.html) from the root of your repository.

* With `dotnet` CLI you can now run:

  ```sh
  dotnet restore
  ```

* With MSBuild 15 (Developer Command Prompt for VS2017 or Mono 5) you can now
  run:

  ```sh
  msbuild /t:Restore
  ```

Both commands will call [`paket restore`](paket-restore.html) under the hood.

This step integrates well into the new .NET SDK philosophy. It also works
automatically in situations where [`auto-restore`](paket-auto-restore.html) is
enabled. For example, if you open a Paket-enabled solution in Visual Studio 2017
then Visual Studio's background build will restore Paket dependencies
automatically.

#### Global restore

For performance reasons, Paket by default will call initial restore for all projects (global).
Because global restore doesn't work in context of a single project, it's not possible to distinguish project-specific MSBuild variables, like [BaseIntermediateOutputPath](https://docs.microsoft.com/pl-pl/visualstudio/msbuild/common-msbuild-project-properties?view=vs-2017).
If you want to use such variables, (e.g. specified in `Directory.Build.props`), you might want consider disabling global restore by adding following to either `Directory.Build.props`, or project file:

```xml
    <PropertyGroup>
        <PaketDisableGlobalRestore>true</PaketDisableGlobalRestore>
    </PropertyGroup>
```

Be aware though that this might slow Paket restore process for solutions with many projects.
To find out more check out [this issue](https://github.com/fsprojects/Paket/pull/3527).

### Updating packages

If you want to update packages you can use the
[`paket update` command](paket-update.html):

```sh
.paket/paket.exe update
```

This step is the same as with traditional .NET projects.

### Creating packages

If you want to create NuGet packages you can continue to use the
[`pack` command](paket-pack.html) and
[`paket.template` files](template-files.html).

Alternatively, you can use the .NET SDK's
[`dotnet pack` support](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-pack)
that will use package metadata information from your MSBuild project and
dependency information from the
[`paket.references` file](references-files.html).

* With `dotnet` CLI you can now run:

  ```sh
  dotnet pack
  ```

* With MSBuild 15 (Developer Command Prompt for VS2017 or Mono 5) you can now
  run:

  ```sh
  msbuild /t:Pack
  ```

For .NET SDK-based projects, all usual NuGet metadata (e.g. `Author`) for
`dotnet pack` can be customized for automation as follows:

* MSBuild property in the project file: `<Author>Nigel Sheldon</Author>` or in
  imported files,
* property passed as a command line argument: `/p:Author="Nigel Sheldon"`,
* environment variable: `Author=Nigel Sheldon`.

### Converting from NuGet

The NuGet conversion process is identical to traditional .NET projects. You can
read about it in the
["Converting from NuGet" tutorial](convert-from-nuget-tutorial.html).
