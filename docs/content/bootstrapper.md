# The Paket bootstrapper (paket.bootstrapper.exe)

The bootstrapper downloads the latest stable `paket.exe`. By default, the
bootstrapper caches downloaded versions of `paket.exe` for the current user
across all projects. If the requested version is not present in the cache, it is
downloaded from [github.com](https://github.com). If the download from GitHub
fails it tries to download the version from [nuget.org](https://www.nuget.org/)
instead.

Cached versions of `paket.exe` are removed when the NuGet cache directory is
[cleared](paket-clear-cache.html).

<kbd>Ctrl</kbd> + <kbd>C</kbd> interrupts the download process. The return value
of the bootstrapper is 0 when a fresh copy of `paket.exe` was found, so that any
subsequent scripts can continue.

```sh
paket.bootstrapper.exe [prerelease|<version>] [--prefer-nuget] [--self] [-s] [-f]
```

## Options

### On the command line

* `prerelease`: Download the latest `paket.exe` from GitHub, including
  prerelease versions.

* `<version>`: Download the given version of `paket.exe` from GitHub.

* `--prefer-nuget`: Download `paket.exe` from nuget.org instead of GitHub. Uses
  GitHub as a fallback, if the download from nuget.org fails.

* `--force-nuget`: Similar to `--prefer-nuget`, but *won't use* use GitHub.com
  as a fallback.

* `--max-file-age=120`: Skip download if `paket.exe` already exists and it is
  not older than `120` minutes.

* `--nuget-source=<URL>`: When specified as
  `--nuget-source=http://example.com/path/here`, the specified URL is used
  instead of the default nuget.org download URL when trying to fetch `paket.exe`
  as a NuGet package. Combine this with either `--prefer-nuget` or
  `--force-nuget` to get `paket.exe` from a custom source.

* `--self`: Self-update the `paket.bootstrapper.exe`. When this option is used
  the download of `paket.exe` will be skipped. Can be combined with
  `--prefer-nuget`.

* `-s`: Make the bootstrapper more silent. Use it once to display only errors or
  twice to supress all output.

* `-v`: Make the bootstrapper more verbose. Display more information about the
  boostrapper process, including operation timings.

* `-f`: Force the bootstrapper to ignore any cached `paket.exe` versions and go
  directly to GitHub or nuget.org, based on other flags.

* `--run`: Once downloaded run `paket.exe`. All arguments following this one are
  ignored and passed directly to `paket.exe`.

* `--config-file=<config-file-path>`: Forces the bootstrapper to use another
  config file instead of the default `paket.bootstrapper.exe.config` file.

* `--help`: Show help detailing all possible options.

### In application settings

If present, the `paket.bootstrapper.exe.config` file can be used to configure
default options. When an option is passed on the command line, the corresponding
application setting is ignored.

Example:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <appSettings>
    <add key="PreferNuget" value="True"/>
    <add key="ForceNuget" value="True"/>
    <add key="Prerelease" value="True"/>
    <add key="PaketVersion" value="1.5"/>
    <add key="BoostrapperOutputDir" value="_workDir" />
  </appSettings>
</configuration>
```

* `PreferNuget`: Same as `--prefer-nuget` option.
* `ForceNuget`: Same as `--force-nuget` option.
* `Prerelease`: Same as `prerelease` option.
* `PaketVersion`: Same as `<version>` option.
* `BootstrapperOutputDir`: Same as `--output-dir=` option.

### With environment variables

`PAKET_VERSION`: The requested version can also be set using this environment
variable.

* powershell
    ```
    $env:PAKET_VERSION = "5.119.7"
    ```
* cmd
    ```
    set PAKET_VERSION=5.119.7
    ```
* bash
    ```
    export PAKET_VERSION="5.119.7"
    ```


### In paket.dependencies

If a [`paket.dependencies` file](dependencies-file.html) is found in the current
directory it may contain a special line containing options for the bootstrapper.

The line must start with `version` followed by a requested `paket.exe` version
and optionally other bootstrapper command line arguments:

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

When `paket.bootstrapper.exe` is renamed to `paket.exe`, the real `paket.exe` is
downloaded to a temporary location and executed with all arguments passed
directly.

```sh
paket.exe add nuget FAKE
```

Executes the same thing as:

```sh
paket.bootstrapper.exe -s --max-file-age=720 --run add nuget FAKE
```

Using this feature Paket usage may be simplified by committing a ~50KB
`paket.exe` to source control and using it directly. The fact that a
bootstrapper exists is completely hidden and becomes an implementation detail
that contributors to your repository won't have to know — or care — about.

While bootstrapper command line options can't be used, the other sources
(application settings, environment variables and paket.dependencies) may still
be used to configure the bootstrapper. The only difference is that the
application settings file should be named `paket.exe.config` rather than
`paket.bootstrapper.exe.config`.

To self update the bootstrapper in magic mode you can run:

```sh
paket.exe --self --run --version
```

A few default settings are applied:

* The bootstrapper is silenced and only errors are displayed. `-v` can be used
  once to restore normal output or twice to show more details.
* If no version is specified a default `--max-file-age` of `12` hours is used.

**Note about anti virus and magic mode:**

If your anti virus is too aggressive, and must have paket excluded, it will not
be fully excluded unless you change the `BoostrapperOutputDir` to a folder that
is excluded from the anti virus scanner, e.g. a sub folder inside the repository
of the project utilizing magic mode.
