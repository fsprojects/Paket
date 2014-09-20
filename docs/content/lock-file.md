The paket.lock file
====================

Consider the following [`paket.dependencies`](dependencies-file.html) file:

    source http://nuget.org/api/v2

    nuget Castle.Windsor-log4net !~> 3.2
    nuget Rx-Main !~> 2.0

Here we [specify dependencies](dependencies-file.html) on the default NuGet feed's [`Castle.Windsor-log4net`](https://www.nuget.org/packages/Castle.Windsor-log4net/) and [`Rx-Main`](https://www.nuget.org/packages/Rx-Main/) packages; both these packages have dependencies on further NuGet packages.

The `paket.lock` file records the concrete dependency resolution of all direct *and indirect* dependencies of your project:

    [lang=textfile]
    NUGET
      remote: http://nuget.org/api/v2
      specs:
        Castle.Core (3.3.0)
        Castle.Core-log4net (3.3.0)
          Castle.Core (>= 3.3.0)
          log4net (1.2.10)
        Castle.LoggingFacility (3.3.0)
          Castle.Core (>= 3.3.0)
          Castle.Windsor (>= 3.3.0)
        Castle.Windsor (3.3.0)
          Castle.Core (>= 3.3.0)
        Castle.Windsor-log4net (3.3.0)
          Castle.Core-log4net (>= 3.3.0)
          Castle.LoggingFacility (>= 3.3.0)
        Rx-Core (2.2.5)
          Rx-Interfaces (>= 2.2.5)
        Rx-Interfaces (2.2.5)
        Rx-Linq (2.2.5)
          Rx-Interfaces (>= 2.2.5)
          Rx-Core (>= 2.2.5)
        Rx-Main (2.2.5)
          Rx-Interfaces (>= 2.2.5)
          Rx-Core (>= 2.2.5)
          Rx-Linq (>= 2.2.5)
          Rx-PlatformServices (>= 2.2.5)
        Rx-PlatformServices (2.2.5)
          Rx-Interfaces (>= 2.2.5)
          Rx-Core (>= 2.2.5)
        log4net (1.2.10)

If the `paket.lock` file is not present when [paket install](paket-install.html) is requested, it will be generated. Subsequent runs of [paket install](paket-install.html) will not reanalyze the `paket.dependencies` file or touch `paket.lock`.

All changes after the initial generation will be as a result of [`paket install`](paket-install.html) or [paket update](paket-update.html) commands.

As a result, committing the `paket.lock` file to your version control system guarantees that other developers and/or build servers will always end up with a reliable and consistent set of packages regardless of where or when a [paket install](paket-install.html) occurs.
When you run [`paket install`](paket-install.html) and the `paket.lock` file is not present, it will be generated. Subsequent runs of [`paket install`](paket-install.html) will not reanalyze the `paket.dependencies` file nor touch `paket.lock`.

If you make changes to [`paket.dependencies`](dependencies-file.html) or you want Paket to check for newer versions of the direct and indirect dependencies as specified in [`paket.dependencies`](dependencies-file.html), run [`paket update`](paket-update.html).

As a result, committing the `paket.lock` file to your version control system guarantees that other developers and/or build servers will always end up with a reliable and consistent set of packages regardless of where or when [`paket install`](paket-install.html) is executed.
