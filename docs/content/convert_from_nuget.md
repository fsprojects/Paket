# Convert your solution from NuGet

## Manual process

If you are already using `NuGet.exe` for package restore then it should be easy to convert to Paket.

1. Analyse your `packages.config` files and extract the referenced packages into a [paket.dependencies](dependencies_file.html) file.
2. Convert every `packages.config` file to [paket.references](references_files.html) syntax. This is very easy - you just have to remove all the XML and keep the package names.
3. Run [paket install](paket_install.html) with the `--hard` flag. This will analyze the dependencies, generate a [paket.lock](lock_file.html) file and remove all the old package references from your project files and install new references in Paket's syntax.

<div id="automatic"></div>
## Automatic process

** Only in [0.2.0 alpha versions](https://www.nuget.org/packages/Paket/0.2.0-alpha001) **

Paket can assist you with the conversion. The `convert-from-nuget` command finds all `packages.config` files and converts them to [paket.references](references_files.html) and generates a [paket.dependencies](dependencies_file.html) file. 
If the `packages.config` is solution-level, then its dependencies will be written to [paket.dependencies](dependencies_file.html) and it will be removed.
Afterwards it will run the [paket install](paket_install.html) process with the `--hard` flag. This will analyze the dependencies, generate a [paket.lock](lock_file.html) file and remove all the old package references from your project files and install new references in Paket's syntax.

    [lang=batchfile]
    $ paket convert-from-nuget