# paket.bootstrapper.exe

Downloads the latest stable paket.exe from github.com. If the download from github.com fails it tries to download the version from nuget.org instead. 

`Ctrl+C` interrupts the download process. The return value of the bootstrapper is 0 when a paket.exe already exists, so that any subsequent scripts can continue. 

    [lang=batchfile]
    $ paket.bootstrapper.exe [prerelease|version] [--prefer-nuget] [--self] [-s]

Options:

  `prerelease`: Downloads the latest paket.exe from github.com and includes prerelease versions.

  `version`: Downloads the given version of paket.exe from github.com.

  `--prefer-nuget`: Downloads the given version of paket.exe from nuget.org instead of github.com. Uses github.com as fallback, when nuget.org fails

  `--force-nuget`: As with the `--prefer-nuget` option, downloads paket.exe from nuget.org instead of github.com, but does *not* use github.com as a fallback.

  `--nuget-source`: When specified as `--nuget-source=http://local.site/path/here`, the specified path is used instead of nuget.org when trying to fetch paket.exe as a nuget package. Combine this with either `--prefer-nuget` or `--force-nuget` to get paket.exe from a custom source.

  `--self`: Self updating the paket.bootstrapper.exe. When this option is used the download of paket.exe will be skipped. (Can be combined with `--prefer-nuget`)

  `-s`: If this flag is set the bootstrapper will not perform any output.

Environment Variables:

  `PAKET.VERSION`: The requested version can also be set using this environment variable. Above options take precedence over the environment variable 


