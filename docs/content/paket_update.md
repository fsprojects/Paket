paket update
============

Recomputes the package dependency resolution and updates the [Lockfile](lockfile.html).

    [lang=batchfile]
    $ paket update [--force] [--package-file=FILE]

Options:

  `--force`:  Forces the download and reinstallation of all packages.
  `--package-file`:  Use the specified packages file instead of `packages.fsx`.