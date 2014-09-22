# paket update

Recomputes the dependency resolution, updates the [`paket.lock` file](lock-file.html) and propagates any resulting package changes into all project files referencing updated packages.

    [lang=batchfile]
    $ paket update [--force] [--hard] [--dependencies-file=FILE]

Options:

  `--force`: Forces the download and reinstallation of all packages.

`--hard`: Replaces package references within project files even if they are not yet adhering to to Paket's conventions (and hence considered manually managed). See [convert from NuGet](convert-from-nuget.html).

  `--dependencies-file`: Use the specified file instead of [`paket.dependencies`](dependencies-file.html).
