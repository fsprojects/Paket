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

    nuget Castle.Windsor-log4net ~> 3.2
    nuget NUnit
	
	github forki/FsUnit FsUnit.fs

You can read more about the importance and the structure of the `paket.dependencies` file in the [docs](dependencies-file.html).

### Installing dependencies

Install all of the required packages from the specified sources:

    [lang=batchfile]
    $ paket install

TBC