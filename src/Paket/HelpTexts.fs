module Paket.HelpTexts

open Paket.Commands

type CommandHelpTopic = 
    { Command : Command
      Text : string }

let commands = 
    lazy
    [   { Command = ConvertFromNuget
          Text = """"""}

        { Command = AutoRestore
          Text = """Auto-restore on:

  - creates a `.paket` directory in your root directory,
  - downloads `paket.targets` and `paket.bootstrapper.exe` into the `.paket` directory,
  - adds an `<Import>` statement for `paket.targets` to projects that have the [references file](references-files.html).
  
Auto-restore off:

  - removes `paket.targets` from the `.paket` directory,
  - removes the `<Import>` statement for `paket.targets` from projects that have the [references file](references-files.html)."""}

        { Command = Restore
          Text = """"""}

        { Command = Simplify
          Text = """Simplify will also affect paket.references files, unless [strict](dependencies-file.html#Strict-references) mode is used.

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

        { Command = Init
          Text = """"""}

        { Command = Add
          Text = """## Adding to a single project

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

        { Command = FindRefs
          Text = """## Sample

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

        { Command = Update
          Text = """## Updating a single package

It's also possible to update only a single package and to keep all other dependencies fixed:

    [lang=batchfile]
    $ paket update nuget PACKAGENAME [version VERSION] [--force] [--hard]	

Options:

  `--force`: Forces the download and reinstallation of all packages.

  `--hard`: Replaces package references within project files even if they are not yet adhering to to Paket's conventions (and hence considered manually managed). See [convert from NuGet](paket-convert-from-nuget.html)."""}
  
        { Command = Outdated
          Text = """## Sample

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

        { Command = Remove
          Text = """## Removing from a single project

It's also possible to remove a package from a specified project only: 

    [lang=batchfile]
    $ paket remove nuget PACKAGENAME [project PROJECT] [--force] [--hard]

See also [paket add](paket-add.html)."""}

        { Command = Install
          Text = """"""}
        { Command = Pack
          Text = """"""}
        { Command = Push
          Text = """"""}
        { Command = Config
          Text = """Paket will then ask for username and password.

This credentials will be used if no username and password for the source are configured in the [`paket.dependencies` file](nuget-dependencies.html).

The configuration file can be found in:

	let AppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
	let PaketConfigFolder = Path.Combine(AppDataFolder, "Paket")
	let PaketConfigFile = Path.Combine(PaketConfigFolder, "paket.config")
"""}]