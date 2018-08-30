## Generate load scripts for all NuGet packages

It is possible to generate load scripts for all registered NuGet packages
defined in the [`paket.dependencies` file](dependencies-file.html).

```sh
paket generate-load-scripts --framework net45
```

This will create `.csx` and `.fsx` scripts under `.paket/load/net45/`. Those
files can now be loaded in your scripts without having to bother with the list
and order of all dependencies for given package.

The generated load scripts will reference DLLs from the packages using `#r`.
Additionally, all scripts in a `loadscripts` directory in the package will be referenced by `#load`,
as will any script `PackageName.fsx` or `PackageName.csx` in the root of the package.

Notes:

* This command only works after packages have been restored. Please run
  [`paket restore`](paket-restore.html) before using `paket generate-load-scripts` or
  [`paket install`](paket-install.html) if you just changed your
  [`paket.dependencies` file](dependencies-file.html).
* This command was called `generate-include-scripts` in Paket 3.x and used to
  put files under `paket-files/include-scripts` instead of `.paket/load`.

## Generate load scripts while installing packages

Alternatively, load scripts can be generated automatically while running the
[`paket install` command](paket-install.html).

To enable this feature, add the `generate_load_scripts` option to the
[`paket.dependencies` file](dependencies-file.html)

```paket
generate_load_scripts: true
source https://nuget.org/api/v2

nuget Suave
```

## Example

Consider the following [`paket.dependencies` file](dependencies-file.html):

```paket
source https://nuget.org/api/v2

nuget FsLab
```

Now we run [`paket install`](paket-install.html) to install the package.

Then we run `paket generate-load-scripts --framework net45` to generate include
scripts.

In a `.fsx` script file you can now use

```fsharp
#load @".paket/load/net45/fslab.fsx"

// Now you are ready to use FsLab and any of its dependencies.
```
