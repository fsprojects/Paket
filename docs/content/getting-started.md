# Getting Started with Paket

This guide will show you

* [how to manually setup Paket](getting-started.html#Manual-setup) in your .NET
  and Mono solutions
* and
  [how to use the automatic NuGet conversion](convert-from-nuget-tutorial.html).

> The following guide is assuming you are using the `paket.exe` command line
> tool. For information on installing the command line tool follow the
> instructions for your operating system for [installation](installation.html).
> There are editor plugins for Visual Studio, Atom and other which can make this
> process easier and provide additional tooling like syntax highlighting. Check
> our [editor support page](editor-support.html) to see if your editor has a
> Paket plugin.

> If you are starting a new solution from scratch then take a look at
> [ProjectScaffold](http://fsprojects.github.io/ProjectScaffold/). This project
> helps you get started with a new .NET/Mono project solution with everything
> needed for successful organising of code, tools and publishing and includes
> Paket.

## Manual setup

### Downloading Paket's Bootstrapper

1. Create a `.paket` directory in the root of your solution.
1. Download the latest
   [`paket.bootstrapper.exe`](https://github.com/fsprojects/Paket/releases/latest)
   into that directory.
1. Rename `.paket/paket.bootstrapper.exe` to `.paket/paket.exe`.
   [Read more about "magic mode"](bootstrapper.html#Magic-mode).
1. Commit `.paket/paket.exe` to your repository.
1. After the first `.paket/paket.exe` invocation Paket will create a couple of
   files in `.paket` — commit those as well.
1. Configure your source control system to ignore the `paket-files` folder, for example
   in Git add a line to the `.gitignore` file containing `paket-files`.

### Specifying dependencies

Create a [`paket.dependencies` file](dependencies-file.html) in your solution's
root and specify all your dependencies in it. You can use
[NuGet packages](nuget-dependencies.html),
[Git repos](git-dependencies.html),
[GitHub files](github-dependencies.html) and
[HTTP dependencies](http-dependencies.html).

The file might look like this:

```paket
source https://api.nuget.org/v3/index.json

nuget Castle.Windsor-log4net >= 3.2
nuget NUnit

github forki/FsUnit FsUnit.fs
```

> If you use a [Paket plugin for your editor](editor-support.html), you may get
> autocompletion for [`paket.dependencies` file](dependencies-file.html).

[Read more about the importance and the structure of the `paket.dependencies` file](dependencies-file.html).
This file should be committed to your version control system.

### Installing dependencies

Install all required packages from the specified sources:

```sh
.paket/paket.exe install
```

The [`paket install` command](paket-install.html) will analyze your dependencies
and automatically generate a [`paket.lock` file](lock-file.html):

```paket
NUGET
  remote: https://nuget.org/api/v2
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
    FsUnit.fs (81d27fd09575a32c4ed52eadb2eeac5f365b8348)
```

This file shows all direct and [transitive dependencies](faq.html#transitive)
and pins every dependency to a concrete version. In most cases you want to
commit this file to your version control system ([read
why](faq.html#Why-should-I-commit-the-lock-file)).

[Read more about the the `paket.lock` file](lock-file.html). This file should be
committed to your version control system.

### Installing dependencies into projects

In the last paragraph you learned how to install packages into your repository,
but usually you want to use the dependencies in your C#, VB or F# projects. In
order to do so you need a [`paket.references` files](references-files.html)
alongside your Visual Studio project files. By listing the direct dependencies
in a [`paket.references` file](references-files.html), Paket will automatically
sync references to the corresponding projects whenever an
[`install`](paket-install.html) or [`update`](paket-update.html) takes place.

```paket
Castle.Windsor-log4net
NUnit

File:FsUnit.fs .
```

Don't forget to run [`install`](paket-install.html) again in order to let Paket
reference the dependencies in your projects:

```sh
.paket/paket.exe install
```

Like all of the files above, you should
[commit](faq.html#Why-should-I-commit-the-lock-file)
[`paket.references` file](references-files.html) to your version control system.

### Updating packages

If you want to check if your dependencies have updates you can run the
[`paket outdated` command](paket-outdated.html):

```sh
.paket/paket.exe outdated
```

If you want to update all packages you can use the
[`paket update` command](paket-update.html):

```sh
.paket/paket.exe update
```

This command will analyze your
[`paket.dependencies` file](dependencies-file.html) and update the
[`paket.lock` file](lock-file.html).

### Converting from NuGet

If you are already using NuGet and want to learn how to use the automatic NuGet
conversion, then read the next [tutorial](convert-from-nuget-tutorial.html).

### Working with Paket

The most frequently used Paket commands are:

* [`paket install` command](paket-install.html) which you should run after modifying the `paket.dependencies` file. 
  This command will update the `paket.lock` file, then update all of your Visual Studio
  projects to the new versions.

* [`paket restore` command](paket-restore.html) which you should run after cloning the repository or switching branches.
  This command will take the current `paket.lock` file and update your Visual Studio projects
  so that they are referencing the correct versions of NuGet packages. If you are using a
  continuous integration system like TeamCity or Jenkins then you should also run this
  command after getting the source code and before compiling it.
  
* [`paket update` command](paket-update.html) which you can run to update your project to the latest versions of all
  dependent packages. This command will update the `paket.lock` file to reference the most
  recent versions permitted by the restrictions in `paket.dependencies`, then apply these
  changes to your Visual Studio projects.
