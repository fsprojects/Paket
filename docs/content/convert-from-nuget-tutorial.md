# Converting from NuGet

## Automatic NuGet conversion

Paket comes with a command that helps to convert existing solution from NuGet's `packages.config` format to Paket's format.
If you want to use the command then:

  * Please start by making a **back-up of your repository**
  * Download Paket and it's BootStrapper as [described above](getting-started.html#Downloading-Paket-and-it-s-BootStrapper)
  * Run the `convert-from-nuget` command:


    [lang=batchfile]
    $ .paket/paket.exe convert-from-nuget

You can read more about the details and specific parameters for `convert-from-nuget` in the [docs](paket-convert-from-nuget.html).

### Partial NuGet conversion

The `convert-from-nuget` will not work if it discovers that the codebase already utilizes Paket (when [`paket.dependencies` file](dependencies-file.html) is found).
However, if for some reason you happen to have a mixture of projects already migrated to Paket and projects still using NuGet, you can pass the `--force` flag to `convert-from-nuget` for the remaining projects.
