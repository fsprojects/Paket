# Getting Started

This guide will show you

  * [how to manually setup Paket](getting-started.html#Manual-setup) in your .NET / mono solutions
  * and [how to use the automatic NuGet conversion](convert-from-nuget-tutorial.html).

<blockquote>The following guide is assuming you are using the paket.exe command line tool. For information on installing the command line tool follow the instructions for your operating system for <a href="installation.html">installation</a>.
There are editor plugins for Visual Studio, Atom and other which can make this process easier and provide additional tooling like syntax highlighting.
Check our <a href="editor-support.html">editor support page</a> to see if your editor has a Paket plugin.</blockquote>

<blockquote>If you are starting a new solution from scratch then take a look at <a href="http://fsprojects.github.io/ProjectScaffold/">ProjectScaffold</a>.
This project helps you get started with a new .NET/Mono project solution with everything needed for successful organising of code, tools and publishing and includes Paket.</blockquote>

## Manual setup

### Downloading Paket and its Bootstrapper

  * Create a `.paket` folder in the root of your solution.
  * Download the latest [`paket.bootstrapper.exe`](https://github.com/fsprojects/Paket/releases/latest) into that folder.
  * Run `.paket/paket.bootstrapper.exe`. This will download the latest `paket.exe`.
  * Commit `.paket/paket.bootstrapper.exe` into your repo and add `.paket/paket.exe` to your `.gitignore` file.

### Specifying dependencies

Create a [`paket.dependencies` file](dependencies-file.html) in your project's root and specify all your dependencies in it.
You can use [NuGet packages](nuget-dependencies.html), [GitHub files](github-dependencies.html) and [HTTP dependencies](http-dependencies.html).
The file might look like this:

    [lang=paket]
    source https://nuget.org/api/v2

    nuget Castle.Windsor-log4net >= 3.2
    nuget NUnit

    github forki/FsUnit FsUnit.fs

<blockquote>If you use a <a href="editor-support.html">Paket plugin for your editor</a>, you may get autocompletion for paket.dependencies files.</blockquote>

You can read more about the importance and the structure of the `paket.dependencies` file in the [docs](dependencies-file.html). This file should be committed to your version control system.

### Installing dependencies

Install all of the required packages from the specified sources:

    [lang=batchfile]
    $ .paket/paket.exe install

The [`paket install` command](paket-install.html) will analyze your dependencies and automatically generate a [`paket.lock` file](lock-file.html) like:

    [lang=paket]
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

This file shows all direct and [transitive dependencies](faq.html#transitive) and pins every dependency to a concrete version. In most cases you want to commit this file to your version control system ([read why](faq.html#Why-should-I-commit-the-lock-file)).

You can read more about the `paket.lock` file in the [docs](lock-file.html).

### Installing dependencies into projects

In the last paragraph you learned how to install packages into your repository, but usually you want to use the dependencies in your C#, VB or F# projects.
In order to do so you need a [`paket.references` files](references-files.html) alongside your Visual Studio project files.
By listing the direct dependencies in a `paket.references` file, Paket will automatically sync references to the corresponding projects whenever an `install` or `update` takes place.

    [lang=paket]
    Castle.Windsor-log4net
    NUnit

    File:FsUnit.fs .

Don't forget to run `install` again in order to let Paket reference the dependencies in your projects:

    [lang=batchfile]
    $ .paket/paket.exe install


Like all of the files above, you should [commit](faq.html#Why-should-I-commit-the-lock-file) `paket.references` files to your version control system.

### Updating packages

If you want to check if your dependencies have updates you can run the [`paket outdated` command](paket-outdated.html):

    [lang=batchfile]
    $ .paket/paket.exe outdated

If you want to update all packages you can use the [`paket update` command](paket-update.html):

    [lang=batchfile]
    $ .paket/paket.exe update

This command will analyze your [`paket.dependencies` file](dependencies-file.html) and update the [`paket.lock` file](lock-file.html).

### Converting from NuGet

If you are already using NuGet and want to learn how to use the automatic NuGet conversion, then read the next [tutorial](convert-from-nuget-tutorial.html).
