# Getting Started

## Creating new solutions

If you are starting a new solution from scratch then take a look at [ProjectScaffold](http://fsprojects.github.io/ProjectScaffold/). 
This project helps you get started with a new .NET/Mono project solution with everything needed for successful organising of code, tools and publishing and includes Paket.

If you don't want to use ProjectScaffold you can set up Paket easily with the following steps:

### Downloading Paket and it's BootStrapper

  * Create a `.paket` folder in the root of your solution
  * Download the latest [paket.bootstrapper.exe](https://github.com/fsprojects/Paket/releases/latest) into that folder
  * Run `.paket/paket.bootstrapper.exe`. This will download the latest `paket.exe`
  * Commit `.paket/paket.bootstrapper.exe` into your repo and add `.paket/paket.exe` to your `.gitignore` file

### Specifying dependencies

Create a [`paket.dependencies` file](dependencies-file.html) in your project's root and specify all your dependencies in it.
You can use [NuGet packages](nuget-dependencies.html), [GitHub files](github-dependencies.html) and [HTTP dependencies](http-dependencies.html). 
The file might look like this:

    source https://nuget.org/api/v2

    nuget Castle.Windsor-log4net >= 3.2
    nuget NUnit
	
	github forki/FsUnit FsUnit.fs

You can read more about the importance and the structure of the `paket.dependencies` file in the [docs](dependencies-file.html).

### Installing dependencies

Install all of the required packages from the specified sources:

    [lang=batchfile]
    $ .paket/paket.exe install

The [`paket install` command](paket-install.html) will analyze your dependencies and automatically generate a [`paket.lock` file](lock-file.html) like:

	NUGET
	  remote: https://nuget.org/api/v2
	  specs:
		Castle.Core (3.3.3)
		Castle.Core-log4net (3.3.3)
		  Castle.Core (>= 3.3.3)
		  log4net (1.2.10)
		Castle.LoggingFacility (3.3.0)
		  Castle.Core (>= 3.3.0)
		  Castle.Windsor (>= 3.3.0)
		Castle.Windsor (3.3.0)
		  Castle.Core (>= 3.3.0)
		Castle.Windsor-log4net (3.3.0)
		  Castle.Core-log4net (>= 3.3.0)
		  Castle.LoggingFacility (>= 3.3.0)
		log4net (1.2.10)
		NUnit (2.6.4)
	GITHUB
	  remote: forki/FsUnit
	  specs:
		FsUnit.fs (81d27fd09575a32c4ed52eadb2eeac5f365b8348)

This file shows all direct and transitive dependencies and pins every dependency to a concrete version. In most cases you want to commit this file to your version control system.

You can read more about the `paket.lock` file in the [docs](lock-file.html).

TBC