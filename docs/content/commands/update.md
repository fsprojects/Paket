## Updating a single package

It's also possible to update only a single package and to keep all other dependencies fixed:

    [lang=batchfile]
    $ paket update nuget PACKAGENAME [version VERSION] [--force] [--hard]	

Options:

  `--force`: Forces the download and reinstallation of all packages.

  `--hard`: Replaces package references within project files even if they are not yet adhering to to Paket's conventions (and hence considered manually managed). See [convert from NuGet](paket-convert-from-nuget.html).