# paket update

Recomputes the dependency resolution and updates the [`paket.lock` file](lock_file.html) if updates are warranted.

    [lang=batchfile]
    $ paket update [--force] [--hard] [--dependencies-file=FILE]

Options:

  `--force`: Forces the download and reinstallation of all packages.

  `--hard`: Replaces the package references from the project files even if they are not installed by Paket. See [convert from NuGet](convert_from_nuget.html).

  `--dependencies-file`: Use the specified file instead of [`paket.dependencies`](dependencies_file.html).
