paket install
=============

Ensures that all dependencies in your [Dependencies](Dependencies_file.html) file are available to your application.

    [lang=batchfile]
    $ paket install [--force] [--dependencies-file=FILE]

Options:

  `--force`:  Forces the download and reinstallation of all packages.

  `--dependencies-file`:  Use the specified file instead of `Dependencies`.