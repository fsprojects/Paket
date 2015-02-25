## Command steps

The `paket convert-from-nuget` command:

1. Finds all `packages.config` files, generates a paket.dependencies file in the solution root and replaces each `packages.config` with an equivalent paket.references file. 
2. If there is a solution-level `packages.config`, then it will be removed and its dependencies will be included into the paket.dependencies file.
3. If you use NuGet Package Restore ([MSBuild-Integrated or Automatic Visual Studio Package Restore](http://docs.nuget.org/docs/workflows/migrating-to-automatic-package-restore)), then the [`paket auto-restore`](paket-auto-restore.html) command will be invoked.
4. Next (unless `--no-install` is specified), the [paket install](paket-install.html) process with the `--hard` flag will be executed. This will:

  - analyze the dependencies.
  - generate a paket.lock file.
  - remove all the old package references from your project files and install new references in Paket's syntax.

5. If you specify `--force`, the conversion will attempt to infer additional dependencies from newly added / previously unprocessed `packages.config` files and 

  - add any newly discovered dependencies to the end of an existing `paket.dependencies` file.
  - transfer/append references from the `packages.config` files into `paket.references` files alongside.
    
## Migrating NuGet source credentials

If you are using authorized NuGet feeds, convert-from-nuget command will automatically migrate the credentials for you.
Following are valid modes for `--creds-migration` option:

1. `encrypt` -  Encrypt your credentials and save in [paket configuration file](paket-config-file.html).
2. `plaintext` - Include your credentials in plaintext in paket.dependencies file. See [example](nuget-dependencies.html#plaintext-credentials)
3. `selective` - Use this switch, if you're using more than one authorized NuGet feed, and want to apply different mode for each of them.

## Simplify direct dependencies

After converting your solution from NuGet, you may end up with many transitive dependencies in your Paket files.
Consider using [`paket simplify`](paket-simplify.html) to remove unnecessary transitive dependencies from your paket.dependencies file and paket.references files.