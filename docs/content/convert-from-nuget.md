# Convert your solution from NuGet

## Manual process

If you are already using `NuGet.exe` for package restore then it should be easy to convert to Paket.

1. Analyse your `packages.config` files and extract the referenced packages into a [`paket.dependencies` file](dependencies-file.html).
2. Convert each `packages.config` file to [`paket.references`](references-files.html) syntax. This is very easy - you just have to remove all the XML and keep the package names.
3. Run [paket install](paket-install.html) with the `--hard` flag. This will analyze the dependencies, generate a [`paket.lock` file](lock-file.html), remove all the old package references from your project files and replace them with equivalent `Reference`s in a syntax that can be managed automatically by Paket.

<div id="automatic"></div>
## Automated process

Paket can assist you with the conversion. The `paket convert-from-nuget` command:

1. Finds all `packages.config` files, generates a [`paket.dependencies` file](dependencies-file.html) in the solution root and replaces each `packages.config` with an equivalent [`paket.references` file](references-files.html). 
2. If there is a solution-level `packages.config`, then it will be removed and its dependencies will be included into the [`paket.dependencies`](dependencies-file.html).
3. If you use NuGet Package Restore ([MSBuild-Integrated or Automatic Visual Studio Package Restore](http://docs.nuget.org/docs/workflows/migrating-to-automatic-package-restore)), then the [`paket init-auto-restore`](paket-init-auto-restore.html) command will be invoked.
4. Next (unless `--no-install` is specified), the [paket install](paket-install.html) process with the `--hard` flag will be executed. This will:

  - analyze the dependencies.
  - generate a [`paket.lock` file](lock-file.html).
  - remove all the old package references from your project files and install new references in Paket's syntax.

5. If you specify `--force`, the conversion will attempt to infer additional dependencies from newly added / previously unprocessed `packages.config` files and 

  - add any newly discovered dependencies to the end of an existing `paket.dependencies` file.
  - transfer/append references from the `packages.config` files into `paket.references` files alongside.

<div id="syntax"></div>

    [lang=batchfile]
    $ paket convert-from-nuget [--force] [--no-install] [--no-auto-restore] [--creds-migration MODE]

Options:

  `--force`: Forces the conversion, even if a [`paket.dependencies` file](dependencies-file.html) or [`paket.references`](references-files.html) files are present.

  `--no-install`: Skips [`paket install --hard`](paket-install.html) process afterward generation of dependencies / references files.

  `--no-auto-restore`: Skips [`paket init-auto-restore`](paket-init-auto-restore.html) process afterward generation of dependencies / references files.

  `--creds-migration`: Specify mode for migrating NuGet source credentials. Possible values for `MODE` are [`encrypt`|`plaintext`|`selective`]. The default `MODE` is `encrypt`.

## Migrating NuGet source credentials

If you are using authorized NuGet feeds, convert-from-nuget command will automatically migrate the credentials for you.
Following are valid modes for `--creds-migration` option:

1. `encrypt` -  Encrypt your credentials and save in [Paket configuration file](paket-config-file.html).
2. `plaintext` - Include your credentials in plaintext in [`paket.dependencies`](dependencies-file.html) file. See [example](nuget-dependencies.html#plaintext-credentials)
3. `selective` - Use this switch, if you're using more than one authorized NuGet feed, and want to apply different mode for each of them.

## Simplify direct dependencies

After converting your solution from NuGet, you may end up with many indirect dependencies in your Paket files.
Consider using [`paket simplify`](paket-simplify.html) to remove unnecessary indirect dependencies from your [`paket.dependencies`](dependencies-file.html) and [`paket.references`](references-files.html) files.