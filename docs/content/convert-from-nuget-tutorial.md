# Converting from NuGet

## Automatic NuGet conversion

Paket comes with a command that helps to convert existing solution from NuGet's
`packages.config` format to Paket's format.

1. Please start by making a **backup of your repository**
1. Download Paket and it's bootstrapper as described in the
   ["Get started" tutorial](get-started.html)
1. Run the [`convert-from-nuget`](paket-convert-from-nuget.html) command:

```sh
$ dotnet paket convert-from-nuget
```

Or if you're not using .NET Core:

```sh
$ .paket/paket.exe convert-from-nuget
```

Read more about the details and parameters for
[`convert-from-nuget`](paket-convert-from-nuget.html).

### Preparation

Choose a directory to run the conversion from that is parent to **all** the
projects to be converted.

When NuGet package restore is enabled, the `packages` directory is located next
to the solution. It is also possible that the parent directory of `packages` is
**not** also parent to all the projects in the solution.

A solution is effectively acting as a symlink, but this indirection via the
solution is not possible with Paket because Paket manages projects and not
solutions. In the example below, it would not be possible to run the
`paket convert-from-nuget` command from the `Build` directory but it would be
from the root directory.

```text
.
├── Build
│   ├── Numbers.sln
│   ├── .nuget
│   │   ├── NuGet.Config
│   │   ├── NuGet.exe
│   │   └── NuGet.targets
│   └── packages
└── Encoding
    ├── Encoding.fsproj
    └── packages.config
```

After running the conversion from the root directory:

```text
.
├── .paket
│   ├── paket.bootstrapper.exe
│   ├── paket.exe
│   └── paket.targets
├── packages
├── Build
│   └── Numbers.sln
└── Encoding
    ├── Encoding.fsproj
    └── paket.references
```

### Command steps

The `paket convert-from-nuget` command:

1. Finds all `packages.config` files, generates a paket.dependencies file in the
   solution root and replaces each `packages.config` with an equivalent
   [`paket.references` file](references-files.html).
1. If there is a solution-level `packages.config`, then it will be removed and
   its dependencies will be included in the
   [`paket.dependencies` file](dependencies-file.html).
1. If you use NuGet Package Restore
   ([MSBuild-Integrated or Automatic Visual Studio Package Restore](http://docs.nuget.org/docs/workflows/migrating-to-automatic-package-restore)),
   then the [`paket auto-restore on`](paket-auto-restore.html) command will be
   invoked.
1. Unless `--no-install` is specified, the
   [`paket install`](paket-install.html) process will be executed. This will

   * analyze the dependencies,
   * generate a [`paket.lock` file](lock-file.html),
   * remove all the old package references from your project files and install
     new references in Paket's syntax.

1. If you specify `--force`, the conversion will attempt to infer additional
   dependencies from newly added or previously unprocessed `packages.config`
   files and

   * add any newly discovered dependencies to the end of an existing
     [`paket.dependencies` file](dependencies-file.html),
   * add references from the `packages.config` files to
     [`paket.references` files](references-files.html).

### Migrating NuGet source credentials

If you are using authorized NuGet feeds, `convert-from-nuget` will automatically
migrate the credentials for you. Following are valid modes for the
`--migrate-credentials` option:

* `encrypt`: Encrypt credentials and save them in the
  [Paket configuration file](paket-config.html).
* `plaintext`: Include credentials in plaintext in the
  [`paket.dependencies` file](dependencies-file.html).
  See [example](nuget-dependencies.html#plaintext-credentials).
* `selective`: Use this option if have more than one authorized NuGet
  feed and you want to apply different modes for each of them.

## Simplify to direct dependencies

After converting your solution from NuGet, you may end up with many
[transitive dependencies](faq.html#transitive) in your Paket files. Consider
using [`paket simplify`](paket-simplify.html) to remove unnecessary
[transitive dependencies](faq.html#transitive) from your
[`paket.dependencies`](dependencies-file.html) and
[`paket.references` files](references-files.html).

## Partial NuGet conversion

[`convert-from-nuget`](paket-convert-from-nuget.html) will not work if it
discovers that the codebase already utilizes Paket (i.e.
[`paket.dependencies` file](dependencies-file.html) is found). However, if for
some reason you happen to have a mixture of projects already migrated to Paket
and projects still using NuGet, you can pass the `--force` flag to
`convert-from-nuget` for the remaining projects.
