The paket.lock file
===================

Consider the following [`paket.dependencies` file](dependencies-file.html):

    source https://nuget.org/api/v2

    nuget Castle.Windsor-log4net !~> 3.2
    nuget Rx-Main !~> 2.0

Here we [specify dependencies](dependencies-file.html) on the default NuGet feed's [`Castle.Windsor-log4net`](https://www.nuget.org/packages/Castle.Windsor-log4net/) and [`Rx-Main`](https://www.nuget.org/packages/Rx-Main/) packages; both these packages have dependencies on further NuGet packages.

The [`paket.lock` file](lock-file.html) records the concrete dependency resolution of all direct *and transitive* dependencies of your project:

    NUGET
      remote: https://nuget.org/api/v2
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

If the [`paket.lock` file](lock-file.html) is not present when [`paket install`](paket-install.html) is run, it will be generated. Subsequent runs of [`paket install`](paket-install.html) will only perform updates according to the latest changes in the [`paket.dependencies` file](dependencies-file.html).

Committing the [`paket.lock` file](lock-file.html) to your version control system guarantees that other developers and/or build servers will always end up with a reliable and consistent set of packages regardless of where or when [`paket restore`](paket-restore.html) is executed.

Performing updates
------------------

If you make changes to [`paket.dependencies`](dependencies-file.html) or you want Paket to check for newer versions of the direct and transitive dependencies as specified in [`paket.dependencies`](dependencies-file.html), run:

  - [`paket outdated`](paket-outdated.html) to check for new versions, and report what's available.
  - [`paket install`](paket-install.html) to analyze the modifications in the [`paket.dependencies` file](dependencies-file.html) and perform a selective update (only changed dependencies are updated).
  - [`paket update`](paket-update.html) to check for new versions, download any that fit the criteria, and update the references within the project files as specified by their associated [`paket.references`](references-files.html).
