# paket.bootstrapper.exe

Downloads the latest stable paket.exe from github.com.

    [lang=batchfile]
    $ paket.bootstrapper.exe [prerelease|version] [--prefer-nuget]

Options:

  `prerelease`: Downloads the latest paket.exe from github.com and includes prerelease versions.

  `version`: Downloads the given version of paket.exe from github.com.

  `--prefer-nuget`: Downloads the given version of paket.exe from nuget.org instead of github.com.
  
  `--self`: Self updating the paket.bootstrapper.exe. When this option is used the download of paket.exe will be skipped.

Environment Variables:

  `PAKET.VERSION`: The requested version can also be set using this environment variable. Above options take precedence over the environment variable 
