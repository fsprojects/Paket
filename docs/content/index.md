What is Paket?
==============

Paket is a package manager for .NET and mono projects. It's inspired by [bundler][bundler], but designed to work well with [NuGet][nuget] packages. 
It enables precise and predictable control over what packages the projects within your application reference. More details are in the [FAQs](faq.html).

  [bundler]: http://bundler.io/
  [nuget]: https://www.nuget.org/ 

How to get Paket
----------------

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

* [Release Notes](RELEASE_NOTES.html)
* [![NuGet Status](http://img.shields.io/nuget/v/Paket.svg?style=flat)](https://www.nuget.org/packages/Paket/)

Getting Started
---------------

Specify the version-rules of all dependencies used in your application in a [Paket.dependencies](Dependencies_file.html) file in your project's root:

    source "http://nuget.org/api/v2"

    nuget "Castle.Windsor-log4net" "~> 3.2"
    nuget "Rx-Main" "~> 2.0"

Install all of the required packages from your specified sources:

    [lang=batchfile]
    $ paket install

The [install command](paket_install.html) will analyze your [Paket.dependencies](Dependencies_file.html) file and generate a [Paket.lock](lock_file.html) file alongside it.
If the lock file already exists, it will not be regenerated.

You may have a [Paket.references](References_files.html) file next to your VS projects to have Paket automatically add references for the package IDs noted in that file.

All of the files involved (`Paket.dependencies`, `Paket.lock` and `Paket.references`) should be committed to your version control system. 

Determine if there are package updates available:

    [lang=batchfile]
    $ paket outdated

Download updated packages; update lock:

    [lang=batchfile]
    $ paket update

The [update command](paket_update.html) will analyze your [Paket.dependencies](Dependencies_file.html) file, and (iff the rules dictate that any direct or indirect dependencies have updates that should be applied) updates the [Paket.lock](lock_file.html) file.

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
