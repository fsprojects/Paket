module Paket.HelpTexts

open Paket.Commands

open Nessos.UnionArgParser

let formatSyntax (parser:UnionArgParser<'TArgs>) commandName = 
    "$ " + commandName + " " + parser.PrintCommandLineSyntax()

let formatUsage (parser:UnionArgParser<'TArgs>)  commandName =
    parser.Usage(formatSyntax parser commandName)
    
let replace (pattern : string) (replacement : string) input = 
    System.Text.RegularExpressions.Regex.Replace(input, pattern, replacement)

let formatCommandSyntax (parser : UnionArgParser<_>) title = 
    let options =
        parser.Usage() 
        |> replace @"\s\t--help.*" ""
        |> replace @"\t([-\w \[\]|\/\?]+):" (System.Environment.NewLine + @"  `$1`:")
    sprintf """    [lang=batchfile]
    %s

Options:
%s"""
        (formatSyntax parser title)
        options

type CommandHelpTopic = 
    { Title : string
      Syntax : string -> string
      Text : string }
    member this.ToMarkDown() =
        let text =
            this.Text
                .Replace("<<syntax goes here>>", this.Syntax this.Title)
                .Replace("paket.dependencies file","[`paket.dependencies` file](dependencies-file.html)")
                .Replace("paket.lock file","[`paket.lock` file](lock-file.html)")
                .Replace("paket.template files","[`paket.template` files](template-files.html)")
                .Replace("paket.references files","[`paket.references` files](references-files.html)")
                .Replace("paket.references file","[`paket.references` file](references-files.html)")
                
        sprintf "# %s%s%s" this.Title System.Environment.NewLine text

let commands =
    ["convert-from-nuget", 
        { Title = "Convert your solution from NuGet"
          Syntax = formatCommandSyntax (UnionArgParser.Create<InstallArgs>())
          Text = """## Manual process

If you are already using `NuGet.exe` for package restore then it should be easy to convert to Paket.

1. Analyse your `packages.config` files and extract the referenced packages into a paket.dependencies file.
2. Convert each `packages.config` file to a paket.references file. This is very easy - you just have to remove all the XML and keep the package names.
3. Run [paket install](paket-install.html) with the `--hard` flag. This will analyze the dependencies, generate a paket.lock file, remove all the old package references from your project files and replace them with equivalent `Reference`s in a syntax that can be managed automatically by Paket.

<div id="automatic"></div>
## Automated process

Paket can assist you with the conversion. The `paket convert-from-nuget` command:

1. Finds all `packages.config` files, generates a paket.dependencies file in the solution root and replaces each `packages.config` with an equivalent paket.references file. 
2. If there is a solution-level `packages.config`, then it will be removed and its dependencies will be included into the paket.dependencies file.
3. If you use NuGet Package Restore ([MSBuild-Integrated or Automatic Visual Studio Package Restore](http://docs.nuget.org/docs/workflows/migrating-to-automatic-package-restore)), then the [`paket auto-restore`](paket-auto-restore.html) command will be invoked.
4. Next (unless `--no-install` is specified), the [paket install](paket-install.html) process with the `--hard` flag will be executed. This will:

  - analyze the dependencies.
  - generate a paket.lock file.
  - remove all the old package references from your project files and install new references in Paket's syntax.

5. If you specify `--force`, the conversion will attempt to infer additional dependencies from newly added / previously unprocessed `packages.config` files and 

  - add any newly discovered dependencies to the end of an existing `paket.dependencies` file.
  - transfer/append references from the `packages.config` files into `paket.references` files alongside.

<div id="syntax"></div>

    [lang=batchfile]
    $ paket convert-from-nuget [--force] [--no-install] [--no-auto-restore] [--creds-migration MODE]

Options:

  `--force`: Forces the conversion, even if a paket.dependencies file or paket.references files are present.

  `--no-install`: Skips [`paket install --hard`](paket-install.html) process afterward generation of dependencies / references files.

  `--no-auto-restore`: Skips [`paket auto-restore`](paket-auto-restore.html) process afterward generation of dependencies / references files.

  `--creds-migration`: Specify mode for migrating NuGet source credentials. Possible values for `MODE` are [`encrypt`|`plaintext`|`selective`]. The default `MODE` is `encrypt`.

## Migrating NuGet source credentials

If you are using authorized NuGet feeds, convert-from-nuget command will automatically migrate the credentials for you.
Following are valid modes for `--creds-migration` option:

1. `encrypt` -  Encrypt your credentials and save in [paket configuration file](paket-config-file.html).
2. `plaintext` - Include your credentials in plaintext in paket.dependencies file. See [example](nuget-dependencies.html#plaintext-credentials)
3. `selective` - Use this switch, if you're using more than one authorized NuGet feed, and want to apply different mode for each of them.

## Simplify direct dependencies

After converting your solution from NuGet, you may end up with many transitive dependencies in your Paket files.
Consider using [`paket simplify`](paket-simplify.html) to remove unnecessary transitive dependencies from your paket.dependencies file and paket.references files."""}

     "auto-restore",
        { Title = "paket auto-restore"
          Syntax = formatCommandSyntax (UnionArgParser.Create<InstallArgs>())
          Text = """Enables or disables automatic Package Restore in Visual Studio during the build process. 

    [lang=batchfile]
    $ paket auto-restore [on|off]

Auto-restore on:

  - creates a `.paket` directory in your root directory,
  - downloads `paket.targets` and `paket.bootstrapper.exe` into the `.paket` directory,
  - adds an `<Import>` statement for `paket.targets` to projects that have the [references file](references-files.html).
  
Auto-restore off:

  - removes `paket.targets` from the `.paket` directory,
  - removes the `<Import>` statement for `paket.targets` from projects that have the [references file](references-files.html)."""}

     "restore",
        { Title = "paket restore"
          Syntax = formatCommandSyntax (UnionArgParser.Create<InstallArgs>())
          Text = """Ensures that all dependencies in your paket.dependencies file are present in the `packages` directory .

    [lang=batchfile]
    $ paket restore [--force] [--references-files REFERENCESFILE1 REFERENCESFILE2 ...]

Options:

  `--force`: Forces the download of all packages.

  `--references-files`: Allows to restore all packages from the given paket.references files. If no paket.references file is given then all packages will be restored."""}

     "simplify",
        { Title = "paket simplify"
          Syntax = formatCommandSyntax (UnionArgParser.Create<InstallArgs>())
          Text = """Simplifies your paket.dependencies file by removing transitive dependencies.
Does also simplify paket.references files, unless [strict](dependencies-file.html#Strict-references) mode is used.

    [lang=batchfile]
    $ paket simplify [-v] [--interactive]

Options:

  `-v`: Verbose - output the difference in content before and after running simplify command.

  `--interactive`: Asks to confirm to delete every transitive dependency from each of the files. See [Interactive Mode](paket-simplify.html#Interactive-mode).

## Sample

When you install `Castle.Windsor` package in NuGet to a project, it will generate a following `packages.config` file in the project location:

    [lang=xml]
    <?xml version="1.0" encoding="utf-8"?>
    <packages>
      <package id="Castle.Core" version="3.3.1" targetFramework="net451" />
      <package id="Castle.Windsor" version="3.3.0" targetFramework="net451" />
    </packages>

After converting to Paket with [`paket convert-from-nuget command`](paket-convert-from-nuget.html), you should get a following paket.dependencies file:

    source https://nuget.org/api/v2
    
    nuget Castle.Core 3.3.1
    nuget Castle.Windsor 3.3.0

and the NuGet `packages.config` should be converted to following paket.references file:

    Castle.Core
    Castle.Windsor

As you have already probably guessed, the `Castle.Windsor` package happens to have a dependency on the `Castle.Core` package.
Paket by default (without [strict](dependencies-file.html#Strict-references) mode) adds references to all required dependencies of a package that you define for a specific project in paket.references file.
In other words, you still get the same result if you remove `Castle.Core` from your paket.references file.
And this is exactly what happens after executing `paket simplify` command:

    source https://nuget.org/api/v2
    
    nuget Castle.Windsor 3.3.0

will be the content of your paket.dependencies file, and:

    Castle.Windsor

will be the content of your paket.references file.

Unless you are relying heavily on components from `Castle.Core`, you would not care about controlling the required version of `Castle.Core` package. Paket will do the job.

The simplify command will help you maintain your direct dependencies.

## Interactive mode

Sometimes, you may still want to have control over some of the transitive dependencies. In this case you can use the `--interactive` flag,
which will ask you to confirm before deleting a dependency from a file."""}

     "init",
        { Title = "paket init"
          Syntax = formatCommandSyntax (UnionArgParser.Create<InstallArgs>())
          Text = """Creates empty paket.dependencies file in the working directory.

    [lang=batchfile]
    $ paket init"""}

     "add",
        { Title = "paket add"
          Syntax = formatCommandSyntax (UnionArgParser.Create<InstallArgs>())
          Text = """Adds a new package to your paket.dependencies file.

    [lang=batchfile]
    $ paket add nuget PACKAGENAME [version VERSION] [--interactive] [--force] [--hard]

Options:

  `--interactive`: Asks the user for every project if he or she wants to add the package to the projects's paket.references file.

  `--force`: Forces the download and reinstallation of all packages.

  `--hard`: Replaces package references within project files even if they are not yet adhering to to Paket's conventions (and hence considered manually managed). See [convert from NuGet](paket-convert-from-nuget.html).

## Adding to a single project

It's also possible to add a package to a specified project only: 

    [lang=batchfile]
    $ paket add nuget PACKAGENAME [version VERSION] [project PROJECT] [--force] [--hard]

See also [paket remove](paket-remove.html).

## Sample

Consider the following paket.dependencies file:

	source https://nuget.org/api/v2

	nuget FAKE

Now we run `paket add nuget xunit --interactive` install the package:

![alt text](img/interactive-add.png "Interactive paket add")

This will add the package to the selected paket.references files and also to the paket.dependencies file:

	source https://nuget.org/api/v2

	nuget FAKE
	nuget xunit"""}

     "find-refs",
        { Title = "paket find-refs"
          Syntax = formatCommandSyntax (UnionArgParser.Create<InstallArgs>())
          Text = """Finds all project files that have the given NuGet packages installed.

    [lang=batchfile]
    $ paket find-refs nuget PACKAGENAME1 PACKAGENAME1 ...

## Sample

*.src/Paket/paket.references* contains:

	UnionArgParser
	FSharp.Core

*.src/Paket.Core/paket.references* contains:

	Newtonsoft.Json
	DotNetZip
	FSharp.Core

Now we run
	
	paket find-refs DotNetZip FSharp.Core

and paket gives the following output:
	
	DotNetZip
	.src/Paket.Core/Paket.Core.fsproj

	FSharp.Core
	.src/Paket.Core/Paket.Core.fsproj
	.src/Paket/Paket.fsproj"""}

     "update",
        { Title = "paket update"
          Syntax = formatCommandSyntax (UnionArgParser.Create<InstallArgs>())
          Text = """Recomputes the dependency resolution, updates the paket.lock file and propagates any resulting package changes into all project files referencing updated packages.

    [lang=batchfile]
    $ paket update [--force] [--hard] [--redirects]	

Options:

  `--force`: Forces the download and reinstallation of all packages.

  `--hard`: Replaces package references within project files even if they are not yet adhering to to Paket's conventions (and hence considered manually managed). See [convert from NuGet](paket-convert-from-nuget.html).

  `--redirects`: Creates binding redirects for the NuGet packages.

## Updating a single package

It's also possible to update only a single package and to keep all other dependencies fixed:

    [lang=batchfile]
    $ paket update nuget PACKAGENAME [version VERSION] [--force] [--hard]	

Options:

  `--force`: Forces the download and reinstallation of all packages.

  `--hard`: Replaces package references within project files even if they are not yet adhering to to Paket's conventions (and hence considered manually managed). See [convert from NuGet](paket-convert-from-nuget.html)."""}
  
     "outdated",
        { Title = "paket outdated"
          Syntax = formatCommandSyntax (UnionArgParser.Create<InstallArgs>())
          Text = """Lists all dependencies that have newer versions available.

    [lang=batchfile]
    $ paket outdated [--pre] [--ignore-constraints]

Options:

  `--pre`: Includes prereleases.

  `--ignore-constraints`: Ignores the version requirement as in the paket.dependencies file.

## Sample

Consider the following paket.dependencies file:

    source https://nuget.org/api/v2
    
    nuget Castle.Core
    nuget Castle.Windsor

and the following paket.lock file: 

    NUGET
      remote: https://nuget.org/api/v2
      specs:
        Castle.Core (2.0.0)
        Castle.Windsor (2.0.0)
          Castle.Core (>= 2.0.0)

Now we run `paket outdated`:

![alt text](img/paket-outdated.png "paket outdated command")"""}

     "remove",
        { Title = "paket remove"
          Syntax = formatCommandSyntax (UnionArgParser.Create<InstallArgs>())
          Text = """Removes a package from your paket.dependencies file and all paket.references files.

    [lang=batchfile]
    $ paket remove nuget PACKAGENAME [--interactive] [--force] [--hard]

Options:

  `--interactive`: Asks the user for every project if he or she wants to remove the package from the projects's paket.references file. By default every installation of the package is removed.

  `--force`: Forces the download and reinstallation of all packages.

  `--hard`: Replaces package references within project files even if they are not yet adhering to to Paket's conventions (and hence considered manually managed). See [convert from NuGet](paket-convert-from-nuget.html).

## Removing from a single project

It's also possible to remove a package from a specified project only: 

    [lang=batchfile]
    $ paket remove nuget PACKAGENAME [project PROJECT] [--force] [--hard]

See also [paket add](paket-add.html)."""}

     "install",
        { Title = "paket install"
          Syntax = formatCommandSyntax (UnionArgParser.Create<InstallArgs>())
          Text = """Ensures that all dependencies in your paket.dependencies file are present in the `packages` directory and referenced correctly in all projects.

<<syntax goes here>>"""}
     "pack",
        { Title = "paket pack"
          Syntax = formatCommandSyntax (UnionArgParser.Create<InstallArgs>())
          Text = """Packs all paket.template files within this repository

    [lang=batchfile]
    $ paket pack output outputDirectory [buildconfig Debug]

Options:

  `output`: Output directory to put nupkgs

  `buildconfig`: Optionally specify build configuration that should be packaged (defaults to Release)."""}
     "push",
        { Title = "paket push"
          Syntax = formatCommandSyntax (UnionArgParser.Create<InstallArgs>())
          Text = """Pushes all `.nupkg` files from the given directory.

    [lang=batchfile]
    $ paket push packagedir path/to/packages [apikey YourApiKey] [url NuGetFeed]

Options:

  `packagedir`: a directory; every `.nupkg` file in this directory or it's children will be pushed.

  `apikey`: Optionally specify your API key on the command line. Otherwise uses the value of the `nugetkey` environment variable.

  `url`: Optionally specify root url of the nuget repository you are pushing too. Defaults to [https://nuget.org](https://nuget.org)."""}
     "config",
        { Title = "paket config"
          Syntax = formatCommandSyntax (UnionArgParser.Create<InstallArgs>())
          Text = """# paket.config file

Allows to store global configuration values like NuGet credentials. It can be found in:

	let AppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
	let PaketConfigFolder = Path.Combine(AppDataFolder, "Paket")
	let PaketConfigFile = Path.Combine(PaketConfigFolder, "paket.config")

## Add credentials

Credentials for a specific source can be added with the following command:

	paket config add-credentials http://myserver.com/myfeed

Paket will then ask for username and password.

This credentials will be used if no username and password for the source are configured in the [`paket.dependencies` file](nuget-dependencies.html)."""}]

  |> dict
