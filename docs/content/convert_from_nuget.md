# Convert your solution from NuGet

If you are already using `NuGet.exe` for package restore then it should be easy to convert to Paket.

1. Analyse your `packages.config` files and extract the referenced packages into a [paket.dependencies](dependencies_file.html) file.
2. Convert every `packages.config` file to [paket.references](references_files.html) syntax. This is very easy - you just have to remove all the XML and keep the package names.
3. Run [paket install](paket_install.html) with the `--hard` flag. This will analyze the dependencies, generate a [paket.lock](lock_file.html) file and remove all the old package references from your project files and install new references in Paket's syntax.