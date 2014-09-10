paket update
============

Recomputes the package dependency resolution and updates the [paket.lock](lock_file.html) if updates are warranted.

    [lang=batchfile]
    $ paket update [--force] [--dependencies-file=FILE]

Options:

  `--force`:  Forces the download and reinstallation of all packages.

  `--dependencies-file`:  Use the specified file instead of `paket.dependencies`.
