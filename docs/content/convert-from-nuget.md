# Convert your solution from NuGet

## Manual process

If you are already using `NuGet.exe` for package restore then it should be easy to convert to Paket.

1. Analyse your `packages.config` files and extract the referenced packages into a [`paket.dependencies` file](dependencies-file.html).
2. Convert every `packages.config` file to [`paket.references`](references-files.html) syntax. This is very easy - you just have to remove all the XML and keep the package names.
3. Run [paket install](paket-install.html) with the `--hard` flag. This will analyze the dependencies, generate a [`paket.lock` file](lock-file.html) and remove all the old package references from your project files and install new references in Paket's syntax.

<div id="automatic"></div>
## Automatic process

Paket can assist you with the conversion. The `convert-from-nuget` command:

1. Finds all `packages.config` files and converts them to [`paket.references`](references-files.html) and generates a [`paket.dependencies` file](dependencies-file.html). 
2. If there is a solution-level `packages.config`, then its dependencies will be written to [`paket.dependencies`](dependencies-file.html) and the file will be removed.
3. If you use NuGet automatic package restore and have directory `.nuget` with `nuget.targets` inside, then appropriate entries in all project files will be removed ([read more](http://docs.nuget.org/docs/workflows/migrating-to-automatic-package-restore#If_you_are_not_using_TFS)), 
`nuget.targets` file will be removed and the [paket init-auto-restore](paket-init-auto-restore.html) command will be invoked.
4. Unless `--no-install` option is given, the [paket install](paket-install.html) process with the `--hard` flag will be executed. This will analyze the dependencies, generate a [`paket.lock` file](lock-file.html) and remove all the old package references from your project files and install new references in Paket's syntax.
5. If you specify `--force`, the conversion will attempt to infer additional dependencies from newly added / previously unprocessed `packages.config` files and add them to the end of an existing `paket.dependencies` and `paket.references` files.

<div id="syntax"></div>

    [lang=batchfile]
    $ paket convert-from-nuget [--force] [--no-install]

Options:

  `--force`: Forces the convertion, even if there already exist any [`paket.dependencies` file](dependencies-file.html) or [`paket.references`](references-files.html) files.

  `--no-install`: Does not run [paket install](paket-install.html) process afterwards.