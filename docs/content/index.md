What is Paket?
==============

First of all: Paket is work in progress!

Paket is a package manager for .NET and mono projects. It's inspired by [bundler][bundler], but designed to work well with [NuGet][nuget] packages. 
It enables precise and predictable control over what packages the projects within your application reference. More details are in the [FAQs](faq.html).

  [bundler]: http://bundler.io/
  [nuget]: https://www.nuget.org/ 

Getting Started
---------------

Specify the version-rules of all dependencies used in your application in a [Dependencies](Dependencies_file.html) file in your project's root:

    source "http://nuget.org/api/v2"

    nuget "Castle.Windsor-log4net" "~> 3.2"
    nuget "Rx-Main" "~> 2.0"

Install all of the required packages from your specified sources:

    [lang=batchfile]
    $ paket install

The [install command](paket_install.html) will analyze your [Dependencies](Dependencies_file.html) file and generate a [Lockfile (`Depedencies.lock`)](lockfile.html) file alongside it.
If the lockfile already exists, then it will not be regenerated.

You may have a [References.list](References_list_files.html) file next to your VS projects to have Paket automatically add references for the package IDs noted in that file.

All the involved files (`Dependencies`, `Dependencies.lock` and `References.list`) should be committed to your version control system. 

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
