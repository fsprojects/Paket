# What is Paket?

Paket is a dependency manager for .NET and [Mono][mono] projects, which is designed to work well with [NuGet][nuget] packages and also enables [referencing files directly from GitHub repositories](github-dependencies.html).
It enables precise and predictable control over what packages the projects within your application reference. More details are in the [FAQ](faq.html).
If you are already using NuGet for package restore in your solution then you can learn about the upgrade process in the [convert from NuGet](convert-from-nuget.html) section.

  [mono]: http://www.mono-project.com/
  [nuget]: https://www.nuget.org/

## How to get Paket

Paket is available as:

  * [download from GitHub.com](https://github.com/fsprojects/Paket/releases/latest)
  * as a package [`Paket` on nuget.org](https://www.nuget.org/packages/Paket/)
  
[![NuGet Status](http://img.shields.io/nuget/v/Paket.svg?style=flat)](https://www.nuget.org/packages/Paket/)

## Getting Started

Specify the version rules of all dependencies used in your application in a [`paket.dependencies` file](dependencies-file.html) in your project's root:

    source http://nuget.org/api/v2
    
    nuget Castle.Windsor-log4net ~> 3.2
    nuget NUnit

Install all of the required packages from the specified sources:

    [lang=batchfile]
    $ paket install

The [`paket install` command](paket-install.html) will analyze your dependencies and generate a [`paket.lock` file](lock-file.html) if it doesn't exist yet:

    NUGET
      remote: http://nuget.org/api/v2
      specs:
        Castle.Core (3.3.1)
		Castle.Core-log4net (3.3.1)
		  Castle.Core (>= 3.3.1)
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
		NUnit (2.6.3)

You can place [`paket.references` files](references-files.html) alongside your Visual Studio projects to have Paket automatically sync references for the packages noted in that file whenever an `install` or `update` takes place.

All of the [files involved should be committed](faq.html#Why-should-I-commit-the-lock-file) to your version control system.

Determine if there are package updates available:

    [lang=batchfile]
    $ paket outdated

Download updated packages; update [`paket.lock` file](lock-file.html) and re-install to reflect and changes:

    [lang=batchfile]
    $ paket update

The [`paket update` command](paket-update.html) will analyze your [`paket.dependencies` file](dependencies-file.html), and update the [`paket.lock` file](lock-file.html).

Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork the project and submit pull requests.

Please see the [Quick contributing guide in the README][readme] for contribution gudelines.

The library is available under MIT license, which allows modification and redistribution for both commercial and non-commercial purposes. 
For more information see the [License file][license] in the GitHub repository.

  [content]: https://github.com/fsprojects/Paket/tree/master/docs/content
  [gh]: https://github.com/fsprojects/Paket
  [issues]: https://github.com/fsprojects/Paket/issues
  [readme]: https://github.com/fsprojects/Paket/blob/master/README.md
  [license]: https://github.com/fsprojects/Paket/blob/master/LICENSE.txt
