# paket remove

Removes a package from your [`paket.dependencies` file](dependencies-file.html) and all [`paket.references` files](references-file.html).

    [lang=batchfile]
    $ paket remove nuget PACKAGENAME [--interactive] [--force] [--hard]

Options:

  `--interactive`: Asks the user for every project if he or she wants to remove the package from the projects's [`paket.references` file](references-file.html). By default every installation of the package is removed.

  `--force`: Forces the download and reinstallation of all packages.

  `--hard`: Replaces package references within project files even if they are not yet adhering to to Paket's conventions (and hence considered manually managed). See [convert from NuGet](convert-from-nuget.html).

See also [paket add](paket-add.html).