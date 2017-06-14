# Converting from NuGet

## Automatic NuGet conversion

Paket comes with a command that helps to convert existing solution from NuGet's `packages.config` format to Paket's format.
If you want to use the command then:

  * Please start by making a **back-up of your repository**
  * Download Paket and it's bootstrapper as described in the ["Getting started" tutorial](getting-started.html#Downloading-Paket-and-it-s-BootStrapper)
  * Run the `convert-from-nuget` command:


    [lang=batchfile]
    $ .paket/paket.exe convert-from-nuget

You can read more about the details and specific parameters for `convert-from-nuget` in the [docs](paket-convert-from-nuget.html).

### Preparation

Choose a folder to run the conversion from that is parent to **all** the projects to be converted.

When using NuGet package restore, the ``packages`` folder is alongside the solution. It is possible with a solution that the folder parent to ``packages`` is **not** also parent to all the projects in the solution.

A solution is in effect acting as a symlink but this indirection via the solution is not possible with Paket because Paket manages projects and not solutions. In the example below, it would not be possible to run the ``paket convert-from-nuget`` command from the ``Build`` folder but it would be from the root folder.

<pre>
+---Build
|   |   Numbers.sln
|   |   
|   +---.nuget
|   |   NuGet.Config
|   |   NuGet.exe
|   |   NuGet.targets
|   |   
|   +---packages
|   
+---Encoding
|   |   Encoding.fsproj
|   |   packages.config
</pre>

After running the conversion from the root folder:

<pre>
+---.paket
|   |   paket.bootstrapper.exe
|   |   paket.exe
|   |   paket.targets
|
+---packages
|
+---Build
|   |   Numbers.sln
|   
+---Encoding
|   |   Encoding.fsproj
|   |   paket.references
</pre>


### Command steps

The `paket convert-from-nuget` command:

1. Finds all `packages.config` files, generates a paket.dependencies file in the solution root and replaces each `packages.config` with an equivalent paket.references file. 
2. If there is a solution-level `packages.config`, then it will be removed and its dependencies will be included into the paket.dependencies file.
3. If you use NuGet Package Restore ([MSBuild-Integrated or Automatic Visual Studio Package Restore](http://docs.nuget.org/docs/workflows/migrating-to-automatic-package-restore)), then the [`paket auto-restore`](paket-auto-restore.html) command will be invoked.
4. Next (unless `--no-install` is specified), the [`paket install`](paket-install.html) process will be executed. This will:

  - analyze the dependencies.
  - generate a paket.lock file.
  - remove all the old package references from your project files and install new references in Paket's syntax.

5. If you specify `--force`, the conversion will attempt to infer additional dependencies from newly added / previously unprocessed `packages.config` files and 

  - add any newly discovered dependencies to the end of an existing `paket.dependencies` file.
  - transfer/append references from the `packages.config` files into `paket.references` files alongside.
    
### Migrating NuGet source credentials

If you are using authorized NuGet feeds, convert-from-nuget command will automatically migrate the credentials for you.
Following are valid modes for the `--creds-migration` option:

1. `encrypt` -  Encrypt your credentials and save them in the [Paket configuration file](paket-config-file.html).
2. `plaintext` - Include your credentials in plaintext in the paket.dependencies file. See [example](nuget-dependencies.html#plaintext-credentials).
3. `selective` - Use this switch if you're using more than one authorized NuGet feed, and you want to apply different modes for each of them.

## Simplify direct dependencies

After converting your solution from NuGet, you may end up with many [transitive dependencies](faq.html#transitive)in your Paket files.
Consider using [`paket simplify`](paket-simplify.html) to remove unnecessary [transitive dependencies](faq.html#transitive) from your paket.dependencies and paket.references files.


## Partial NuGet conversion

The `convert-from-nuget` will not work if it discovers that the codebase already utilizes Paket (when [`paket.dependencies` file](dependencies-file.html) is found).
However, if for some reason you happen to have a mixture of projects already migrated to Paket and projects still using NuGet, you can pass the `--force` flag to `convert-from-nuget` for the remaining projects.
