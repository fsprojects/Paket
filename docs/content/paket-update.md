# paket update

Recomputes the dependency resolution, updates the [`paket.lock` file](lock-file.html) and propagates any resulting package changes into all project files referencing updated packages.

    [lang=batchfile]
    $ paket update [--force] [--hard] [--redirects]

Options:

  `--force`: Forces the download and reinstallation of all packages.

  `--hard`: Replaces package references within project files even if they are not yet adhering to to Paket's conventions (and hence considered manually managed). See [convert from NuGet](convert-from-nuget.html).

  `--redirects`: Creates binding redirects for the NuGet packages.

## Updating a single package

It's also possible to update only a single package and to keep all other dependencies fixed:

    [lang=batchfile]
    $ paket update nuget PACKAGENAME [version VERSION] [--force] [--hard]	

Options:

  `--force`: Forces the download and reinstallation of all packages.

  `--hard`: Replaces package references within project files even if they are not yet adhering to to Paket's conventions (and hence considered manually managed). See [convert from NuGet](convert-from-nuget.html).