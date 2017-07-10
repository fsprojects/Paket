# What is Paket?

Paket is a dependency manager for .NET and mono projects, which is designed to
work well with [NuGet](https://www.nuget.org/) packages and also enables
referencing files directly from [Git repositories](git-dependencies.html) or any
[HTTP resource](http-dependencies.html). It enables precise and predictable
control over what packages the projects within your application reference.

If you want to learn how to use Paket then read the
["Getting started" tutorial](getting-started.html) and take a look at the
[FAQ](faq.html).

If you are already using NuGet for package management in your solution then you
can learn about the upgrade process in the
[convert from NuGet](getting-started.html#Automatic-NuGet-conversion) section.

For information about Paket with .NET SDK, .NET Core and the `dotnet` CLI see
the
["Paket and the .NET SDK / .NET Core CLI tools" guide](paket-and-dotnet-cli.html).

[![Paket Overview](img/paket-overview.png)](img/paket-overview.png)

## How to get Paket

Paket is available as:

* [Download from GitHub.com](https://github.com/fsprojects/Paket/releases/latest)
* As a package [`Paket` on nuget.org](https://www.nuget.org/packages/Paket/)
* [Plugins for popular editors](editor-support.html)

[![Join the chat at https://gitter.im/fsprojects/Paket](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/fsprojects/Paket?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![NuGet Status](https://img.shields.io/nuget/v/Paket.svg?style=flat)](https://www.nuget.org/packages/Paket/)

## Contributing and copyright

The project is hosted on [GitHub][gh] where you can [report issues][issues],
fork the project and submit pull requests.

Please see the [Quick contributing guide in the README][readme] for contribution
guidelines.

The library is available under MIT license, which allows modification and
redistribution for both commercial and non-commercial purposes. For more
information see the [License file][license].

  [content]: https://github.com/fsprojects/Paket/tree/master/docs/content
  [gh]: https://github.com/fsprojects/Paket
  [issues]: https://github.com/fsprojects/Paket/issues
  [readme]: https://github.com/fsprojects/Paket/blob/master/README.md
  [license]: license.html
