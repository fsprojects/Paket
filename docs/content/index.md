What is Paket?
==============

First of all: Paket is work in progress!

Paket is a package manager for .NET and mono projects. It's inspired by [bundler][bundler], but designed to work well with [NuGet][nuget] packages. 
It allows you to track and install the exact package versions that are needed.

  [bundler]: http://bundler.io/
  [nuget]: https://www.nuget.org/ 

Getting Started
---------------

Specify your dependencies in a `packages.fsx` file in your project's root:

    source "http://nuget.org/api/v2"

    nuget "Castle.Windsor-log4net" "~> 3.2"
    nuget "Rx-Main" "~> 2.0"

[More details about the packages file.](packages_file.html)

Install all of the required packages from your specified sources:

    [lang=batchfile]
    $ paket install

The [install command](paket_install.html) will analyze your package definitions and generate a `package.lock` file. 
You should commit `packages.fsx` and `package.lock` to your version control system.
[More details about the Lockfile.](lockfile.html)

Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork 
the project and submit pull requests. If you're adding new public API, please also 
consider adding [samples][content] that can be turned into a documentation. You might
also want to read [library design notes][readme] to understand how it works.

The library is available under Public Domain license, which allows modification and 
redistribution for both commercial and non-commercial purposes. For more information see the 
[License file][license] in the GitHub repository. 

  [content]: https://github.com/fsprojects/Paket/tree/master/docs/content
  [gh]: https://github.com/fsprojects/Paket
  [issues]: https://github.com/fsprojects/Paket/issues
  [readme]: https://github.com/fsprojects/Paket/blob/master/README.md
  [license]: https://github.com/fsprojects/Paket/blob/master/LICENSE.txt