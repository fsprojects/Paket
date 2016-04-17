# paket.bootstrapper.exe

Downloads the latest stable paket.exe. By default, the bootstrapper caches downloaded versions of paket.exe for the current user across all projects. If the requested version is not present in the cache, it is downloaded from github.com. If the download from github.com fails it tries to download the version from nuget.org instead. 

Cached paket.exe versions are removed when the NuGet cache folder is [cleared](paket-clear-cache.html).

`Ctrl+C` interrupts the download process. The return value of the bootstrapper is 0 when a paket.exe already exists, so that any subsequent scripts can continue. 

    [lang=batchfile]
    $ paket.bootstrapper.exe [prerelease|version] [--prefer-nuget] [--self] [-s] [-f]

Options:

  `prerelease`: Downloads the latest paket.exe from github.com and includes prerelease versions.

  `version`: Downloads the given version of paket.exe from github.com.

  `--prefer-nuget`: Downloads the given version of paket.exe from nuget.org instead of github.com. Uses github.com as fallback, when nuget.org fails

  `--force-nuget`: As with the `--prefer-nuget` option, downloads paket.exe from nuget.org instead of github.com, but does *not* use github.com as a fallback.

  `--nuget-source`: When specified as `--nuget-source=http://local.site/path/here`, the specified path is used instead of nuget.org when trying to fetch paket.exe as a nuget package. Combine this with either `--prefer-nuget` or `--force-nuget` to get paket.exe from a custom source.

  `--self`: Self updating the paket.bootstrapper.exe. When this option is used the download of paket.exe will be skipped. (Can be combined with `--prefer-nuget`)

  `-s`: If this flag is set the bootstrapper will not perform any output.

  `-f`: Forces the bootstrapper to ignore any cached paket.exe versions and go directly to github.com or nuget.org based on other flags.

  `--help`: Shows a help page with all possible options.

Environment Variables:

  `PAKET.VERSION`: The requested version can also be set using this environment variable. Above options take precedence over the environment variable 


