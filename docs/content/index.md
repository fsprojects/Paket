# What is Paket?

Paket is a dependency manager for .NET and [Mono][mono] projects, which is designed to work well with [NuGet][nuget] packages and also allows to [reference files directly from GitHub repositories](github_dependencies.html).
It enables precise and predictable control over what packages the projects within your application reference. More details are in the [FAQ](faq.html).

  [mono]: http://www.mono-project.com/
  [bundler]: http://bundler.io/
  [nuget]: https://www.nuget.org/

## How to get Paket

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      Paket is available <a href="https://nuget.org/packages/Paket">on NuGet</a>.
      To install the tool, run the following command in the <a href="http://docs.nuget.org/docs/start-here/using-the-package-manager-console">Package Manager Console</a>:
      <pre>PM> Install-Package Paket</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

If you are already using NuGet for package restore in your solution then you can learn about the upgrade process in the [convert from NuGet](convert_from_nuget.html) section.

[![NuGet Status](http://img.shields.io/nuget/v/Paket.svg?style=flat)](https://www.nuget.org/packages/Paket/)

## Getting Started

Specify the version rules of all dependencies used in your application in a [`paket.dependencies` file](dependencies_file.html) in your project's root:

    source http://nuget.org/api/v2

    nuget Castle.Windsor-log4net ~> 3.2
    nuget Rx-Main ~> 2.0

Install all of the required packages from the specified sources:

    [lang=batchfile]
    $ paket install

The [`paket install` command](paket_install.html) will analyze your [`paket.dependencies` file](dependencies_file.html) and generate a [`paket.lock` file](lock_file.html) alongside it.
If the lock file already exists, it will not be regenerated.

You may have [`paket.references` files](references_files.html) next to your Visual Studio projects to have Paket automatically add references for the packages noted in that file.

All of the files involved ([`paket.dependencies`](dependencies_file.html), [`paket.lock`](lock_file.html) and [`paket.references`](references_files.html)) should be committed to your version control system.

Determine if there are package updates available:

    [lang=batchfile]
    $ paket outdated

Download updated packages; update lock:

    [lang=batchfile]
    $ paket update

The [`paket update` command](paket_update.html) will analyze your [`paket.dependencies` file](dependencies_file.html), and update the [`paket.lock` file](lock_file.html).

Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork
the project and submit pull requests. If you're adding a new public API, please also
consider adding [samples][content] that can be turned into documentation. You might
also want to read [library design notes][readme] to understand how it works.

The library is available under MIT license, which allows modification and
redistribution for both commercial and non-commercial purposes. For more information see the
[License file][license] in the GitHub repository.

  [content]: https://github.com/fsprojects/Paket/tree/master/docs/content
  [gh]: https://github.com/fsprojects/Paket
  [issues]: https://github.com/fsprojects/Paket/issues
  [readme]: https://github.com/fsprojects/Paket/blob/master/README.md
  [license]: https://github.com/fsprojects/Paket/blob/master/LICENSE.txt
