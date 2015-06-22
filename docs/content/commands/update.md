## Updating all packages

If you do not specify a package, then all packages from paket.dependencies are updated.

    [lang=batchfile]
    paket update [--force|-f] [--hard] [--redirects] [--no-install]

First, the current [`paket.lock` file](lock-file.html) is deleted. `paket update` then recomputes the current dependency resolution,
as explained under [Package resolution algorithm](http://fsprojects.github.io/Paket/resolver.html), and writes it to paket.lock.
It then proceeds to download the packages and to install them into the projects.

Please see [`paket install`](paket-install.html) if you want to keep the current versions from your paket.lock file.

### Options:

  `--force [-f]`: Forces the download and reinstallation of all packages.

  `--hard`: Replaces package references within project files even if they are not yet adhering to the Paket's conventions (and hence considered manually managed). See [convert from NuGet](paket-convert-from-nuget.html).

  `--redirects`: Creates binding redirects for the NuGet packages.

  `--no-install`: Skips paket install --hard process afterward generation of [`paket.lock` file](lock-file.html).


## Updating a single package

It's also possible to update only a single package and to keep all other dependencies fixed:

    [lang=batchfile]
    paket update nuget PACKAGENAME [version VERSION] [--force|-f] [--hard] [--redirects] [--no-install]

### Options:

  `nuget <string>`: Nuget package id

  `version <string>`: Allows to specify version of the package.

  `--force [-f]`: Forces the download and reinstallation of all packages.

  `--hard`: Replaces package references within project files even if they are not yet adhering to the Paket's conventions (and hence considered manually managed). See [convert from NuGet](paket-convert-from-nuget.html).

  `--redirects`: Creates binding redirects for the NuGet packages.

  `--no-install`: Skips paket install --hard process afterward generation of [`paket.lock` file](lock-file.html).
