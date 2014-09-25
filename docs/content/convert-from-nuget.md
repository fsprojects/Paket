# Convert your solution from NuGet

## Manual process

If you are already using `NuGet.exe` for package restore then it should be easy to convert to Paket.

1. Analyse your `packages.config` files and extract the referenced packages into a [`paket.dependencies` file](dependencies-file.html).
2. Convert each `packages.config` file to [`paket.references`](references-files.html) syntax. This is very easy - you just have to remove all the XML and keep the package names.
3. Run [paket install](paket-install.html) with the `--hard` flag. This will analyze the dependencies, generate a [`paket.lock` file](lock-file.html), remove all the old package references from your project files and replace them with equivalent `<Reference`s in a syntax that can be managed automatically by Paket.

<div id="automatic"></div>
## Automated process

Paket can assist you with the conversion. The `paket convert-from-nuget` command:

1. Finds all `packages.config` files, generates a [`paket.dependencies` file](dependencies-file.html) in the solution root and replaces each `packages.config` with an equivalent [`paket.references` file](references-files.html). 
2. If there is a solution-level `packages.config`, then it will be removed and its dependencies will be included into the [`paket.dependencies`](dependencies-file.html).
3. If you use NuGet Package Restore (and have a `.nuget` directory with `nuget.targets` inside), then 

  - associated elements in all project files will be removed - ([read more](http://docs.nuget.org/docs/workflows/migrating-to-automatic-package-restore#If_you_are_not_using_TFS)).
  - the `nuget.targets` file will be removed =.
  - the [`paket init-auto-restore`](paket-init-auto-restore.html) command will be invoked.

4. Next (unless `--no-install` is specified), the [paket install](paket-install.html) process with the `--hard` flag will be executed. This will:

  - analyze the dependencies.
  - generate a [`paket.lock` file](lock-file.html).
  - remove all the old package references from your project files and install new references in Paket's syntax.

5. If you specify `--force`, the conversion will attempt to infer additional dependencies from newly added / previously unprocessed `packages.config` files and 

  - add any newly discovered dependencies to the end of an existing `paket.dependencies` file.
  - transfer/append references from the `packages.config` files into `paket.references` files alongside.

<div id="syntax"></div>

    [lang=batchfile]
    $ paket convert-from-nuget [--force] [--no-install]

Options:

  `--force`: Forces the conversion, even if a [`paket.dependencies` file](dependencies-file.html) or [`paket.references`](references-files.html) files are present.

  `--no-install`: Skips [`paket install --hard`](paket-install.html) process afterward generation of dependencies / references files.

  `--dependencies-file`: Use the specified file instead of [`paket.dependencies`](dependencies-file.html).
