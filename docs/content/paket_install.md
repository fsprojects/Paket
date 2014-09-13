paket install
=============

Ensures that all dependencies in your [`paket.dependencies` file](dependencies_file.html) are available to your application.

    [lang=batchfile]
    $ paket install [--force] [--dependencies-file=FILE]

Options:

  `--force`:  Forces the download and reinstallation of all packages.

  `--dependencies-file`:  Use the specified file instead of `paket.dependencies`.