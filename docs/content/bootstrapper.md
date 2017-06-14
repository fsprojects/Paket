# paket.bootstrapper.exe

Downloads the latest stable paket.exe. By default, the bootstrapper caches downloaded versions of paket.exe for the current user across all projects. If the requested version is not present in the cache, it is downloaded from github.com. If the download from github.com fails it tries to download the version from nuget.org instead.

Cached paket.exe versions are removed when the NuGet cache folder is [cleared](paket-clear-cache.html).

`Ctrl+C` interrupts the download process. The return value of the bootstrapper is 0 when a paket.exe already exists, so that any subsequent scripts can continue.

    [lang=batchfile]
    $ paket.bootstrapper.exe [prerelease|<version>] [--prefer-nuget] [--self] [-s] [-f]

## Options

### On the command line

  `prerelease`: Downloads the latest paket.exe from github.com and includes prerelease versions.

  `<version>`: Downloads the given version of paket.exe from github.com.

  `--prefer-nuget`: Downloads the given version of paket.exe from nuget.org instead of github.com. Uses github.com as fallback, when nuget.org fails

  `--force-nuget`: As with the `--prefer-nuget` option, downloads paket.exe from nuget.org instead of github.com, but does *not* use github.com as a fallback.

  `--max-file-age=120`: if paket.exe already exists, and it is not older than `120` minutes, all checks will be skipped.

  `--nuget-source`: When specified as `--nuget-source=http://local.site/path/here`, the specified path is used instead of nuget.org when trying to fetch paket.exe as a nuget package. Combine this with either `--prefer-nuget` or `--force-nuget` to get paket.exe from a custom source.

  `--self`: Self updating the paket.bootstrapper.exe. When this option is used the download of paket.exe will be skipped. (Can be combined with `--prefer-nuget`)

  `-s`: Make the bootstrapper more silent. Use it once to display display only errors or twice to supress all output.

  `-v`: Make the bootstrapper more verbose. Display more information about the boostrapper process, including operation timings.

  `-f`: Forces the bootstrapper to ignore any cached paket.exe versions and go directly to github.com or nuget.org based on other flags.

  `--run`: Once downloaded run `paket.exe`. All arguments following this one are ignored and passed directly to `paket.exe`.

  `--help`: Shows a help page with all possible options.

### In Application settings

If present, the `paket.bootstrapper.exe.config` file can be used to set AppSettings. When an option is passed on the command line, the corresponding application setting is ignored.

Example file:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <appSettings>
    <add key="PreferNuget" value="True"/>
    <add key="ForceNuget" value="True"/>
    <add key="Prerelease" value="True"/>
    <add key="PaketVersion" value="1.5"/>
  </appSettings>
</configuration>
```

  `PreferNuget`: Same as `--prefer-nuget` option. Downloads the given version of paket.exe from nuget.org instead of
  github.com. Uses github.com as fallback, when nuget.org fails

  `ForceNuget`: Same as `--force-nuget` option. Downloads paket.exe from nuget.org instead of github.com, but does
  *not* use github.com as a fallback.

  `PaketVersion`: Same as `version` option. Downloads the given version of paket.exe from github.com.

  `Prerelease`: Same as `prerelease` option. Ignored if a version number is specified in `PaketVersion` or via
  another way.

### In Environment Variables

  `PAKET.VERSION`: The requested version can also be set using this environment variable.

### In paket.dependencies

If a [`paket.dependencies` file](dependencies-file.html) can be found in the current directory it can contain a
special line containing options for the bootstrapper.

The line must start with `version` followed by a requested `paket.exe` version and optionally bootstrapper command line arguments:

```paket
version 3.24.1

source https://api.nuget.org/v3/index.json
nuget FAKE
nuget FSharp.Core ~> 4
```

or

```paket
version 3.24.1 --prefer-nuget

source https://api.nuget.org/v3/index.json
nuget FAKE
nuget FSharp.Core ~> 4
```

## Magic mode

When `paket.bootstrapper.exe` is renamed to `paket.exe`, the real `paket.exe` is downloaded to a temporary location and
executed with all arguments passed directly.

```batch
paket.exe add nuget FAKE
```

Would do the same thing as :

```batch
paket.bootstrapper.exe -s --max-file-age=720 --run add nuget FAKE
```

Using this feature paket can be used simply by committing a ~50KB `paket.exe` to source control and using it directly.
The fact that a bootstrapper exists is completely hidden and becomes an implementation detail that contributors to your
repository won't have to know — or care — about.

While command line bootstrapper options can't be used the other sources (AppSettings, Environment Variables
& paket.dependencies) can still be used to configure the bootstrapper.

A few default settings are applied:
* The bootstrapper is silent and only errors are displayed. `-v` can be used once to restore normal output or twice
  to show more details.
* If no version is specified a default `--max-file-age` of 12 Hours is used.
