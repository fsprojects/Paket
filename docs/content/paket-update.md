# paket update

Recomputes the dependency resolution and updates the [`paket.lock` file](lock-file.html) if updates are warranted.

    [lang=batchfile]
    $ paket update [--force] [--hard] [--dependencies-file=FILE]

Options:

  `--force`: Forces the download and reinstallation of all packages.

  `--hard`: Replaces the package references from the project files even if they are not installed by Paket. See [convert from NuGet](convert-from-nuget.html).

  `--dependencies-file`: Use the specified file instead of [`paket.dependencies`](dependencies-file.html).
