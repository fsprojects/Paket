# paket restore

Ensures that all dependencies in your [`paket.dependencies` file](dependencies-file.html) are present in the `packages` directory .

    [lang=batchfile]
    $ paket restore [--force] [--references-files REFERENCESFILE1 REFERENCESFILE2 ...]

Options:

  `--force`: Forces the download of all packages.

  `--references-file`: Allows to restore all packages from the given `paket.references` files. If no `paket.references` file is given the all packages will be restored.