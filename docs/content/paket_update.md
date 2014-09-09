paket update
============

Recomputes the package dependency resolution and updates the [Paket.Lock](lock_file.html).

    [lang=batchfile]
    $ paket update [--force] [--dependencies-file=FILE]

Options:

  `--force`:  Forces the download and reinstallation of all packages.

  `--dependencies-file`:  Use the specified file instead of `Paket.dependencies`.