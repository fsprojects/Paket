# paket outdated

Lists all dependencies that have newer versions available.

    [lang=batchfile]
    $ paket outdated [--pre] [--strict] [--dependencies-file FILE]

Options:

  `--pre`: Includes prereleases.

  `--strict`: Keeps the version requirement as in the [`paket.dependencies`](dependencies-file.html).

  `--dependencies-file`: Use the specified file instead of [`paket.dependencies`](dependencies-file.html).

## Sample

Consider the following [`paket.dependencies` file](dependencies-file.html):

    source http://nuget.org/api/v2
    
    nuget Castle.Core
    nuget Castle.Windsor

and the following [`paket.lock` file](lock-file.html): 

    NUGET
      remote: http://nuget.org/api/v2
      specs:
        Castle.Core (2.0.0)
        Castle.Windsor (2.0.0)
          Castle.Core (>= 2.0.0)

Now we run `paket outdated`:

![alt text](img/paket-outdated.png "paket outdated command")