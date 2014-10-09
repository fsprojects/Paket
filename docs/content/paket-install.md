# paket install

Ensures that all dependencies in your [`paket.dependencies` file](dependencies-file.html) are present in the `packages` directory and referenced correctly in all projects.

    [lang=batchfile]
    $ paket install [--force] [--hard]

Options:

  `--force`: Forces the download and reinstallation of all packages.

  `--hard`: Replaces package references within project files even if they are not yet adhering to Paket's conventions (and hence considered manually managed). See [convert from NuGet](convert-from-nuget.html).
