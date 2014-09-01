paket install
=============

Ensures that all dependencies in your `packages.fsx` are available to your application.

    [lang=batchfile]
    $ paket install [--force] [--package-file=FILE]

Options:

  `--force`:  Forces the download and reinstallation of all packages.
  `--package-file`:  Use the specified packages file instead of `packages.fsx`.