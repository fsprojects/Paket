# Convert your solution from NuGet

## Manual process

If you are already using `NuGet.exe` for package restore then it should be easy to convert to Paket.

1. Analyse your `packages.config` files and extract the referenced packages into a [`paket.dependencies` file](dependencies_file.html).
2. Convert every `packages.config` file to [`paket.references`](references_files.html) syntax. This is very easy - you just have to remove all the XML and keep the package names.
3. Run [paket install](paket_install.html) with the `--hard` flag. This will analyze the dependencies, generate a [`paket.lock` file](lock_file.html) and remove all the old package references from your project files and install new references in Paket's syntax.

<div id="automatic"></div>
## Automatic process

Paket can assist you with the conversion. The `convert-from-nuget` command finds all `packages.config` files and converts them to [`paket.references`](references_files.html) and generates a [`paket.dependencies` file](dependencies_file.html). 
If the `packages.config` is solution-level, then its dependencies will be written to [`paket.dependencies`](dependencies_file.html) and it will be removed.
Afterwards it will run the [paket install](paket_install.html) process with the `--hard` flag. This will analyze the dependencies, generate a [`paket.lock` file](lock_file.html) and remove all the old package references from your project files and install new references in Paket's syntax.

    [lang=batchfile]
    $ paket convert-from-nuget