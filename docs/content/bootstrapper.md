# paket.bootstrapper.exe

Downloads the latest stable paket.exe from github.com.

    [lang=batchfile]
    $ paket.bootstrapper.exe [prerelease|version] [--prefer-nuget]

Options:

  `prerelease`: Downloads the latest paket.exe from github.com and includes prerelease versions.

  `version`: Downloads the given version of paket.exe from github.com.

  `--prefer-nuget`: Downloads the given version of paket.exe from nuget.org instead of github.com.
