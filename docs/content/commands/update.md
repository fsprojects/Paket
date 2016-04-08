## Updating all packages

If you do not specify a package, then all packages from paket.dependencies are updated.

    [lang=batchfile]
    paket update [--force|-f] [--redirects] [--no-install]

First, the current [`paket.lock` file](lock-file.html) is deleted. `paket update` then recomputes the current dependency resolution,
as explained under [Package resolution algorithm](http://fsprojects.github.io/Paket/resolver.html), and writes it to paket.lock.
It then proceeds to download the packages and to install them into the projects.

Please see [`paket install`](paket-install.html) if you want to keep the current versions from your paket.lock file.

### Options:

  `--force [-f]`: Forces the download and reinstallation of all packages.

  `--redirects`: Creates binding redirects for the NuGet packages.

  `--no-install`: Skips paket install process afterward generation of [`paket.lock` file](lock-file.html).


## Updating a single package, or packages matching a pattern

It's also possible to update only specified packages and to keep all other dependencies fixed:

    [lang=batchfile]
    paket update nuget PACKAGENAME [version VERSION] [group GROUPNAME] [--force|-f] [--redirects] [--no-install]

### Options:

  `nuget <string>`: Nuget package id

  `version <string>`: Allows to specify version of the package.

  `group <string>`: Allows to specify the group where the package is located. If omitted then Paket defaults to the Main group.

  `--force [-f]`: Forces the download and reinstallation of all packages.

  `--redirects`: Creates binding redirects for the NuGet packages.

  `--no-install`: Skips paket install process afterward generation of [`paket.lock` file](lock-file.html).

  `--filter`: Treat PACKAGENAME as a regex pattern to match against, rather than a single package. Enforces a "total" match (i.e. an implicit ^ and $ at beginning and end of PACKAGENAME)

## Updating a single group

If you want to update a single group you can use the following command:

    [lang=batchfile]
    paket update group GROUPNAME [--force|-f] [--redirects] [--no-install]

### Options:

  `group <string>`: Group name

  `--force [-f]`: Forces the download and reinstallation of all packages.

  `--redirects`: Creates binding redirects for the NuGet packages.

  `--no-install`: Skips paket install process afterward generation of [`paket.lock` file](lock-file.html).

## Updating http dependencies

If you want to update a file you need to use the [`paket install` command](paket-install.html) or [`paket update` command](paket-update.html) with `--force [-f]` option.

Using [groups](groups.html) for http dependent files can be helpful in order to reduce the number of files that are re-installed.

