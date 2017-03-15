## Generating load scripts for all NuGet packages

It is possible to generate load scripts for all registered NuGet packages defined in the [`paket.dependencies` file](dependencies-file.html).

    [lang=batchfile]
    $ paket generate-load-scripts framework net45

This will create .csx and .fsx scripts under `.paket/load/net45/`, those files can now be 
used in your scripts without having to bother with the list and order of all dependencies for given package.

Notes:

* this command only works after packages have been restored, please call `paket restore` before using `paket generate-load-scripts` or `paket install` if you just changed your `paket.dependencies` file
* this command was called `generate-include-scripts` in V3 and used to put files under `paket-files/include-scripts` instead of `.paket/load`

## Sample

Consider the following [`paket.dependencies` file](dependencies-file.html):

    [lang=paket]
    source https://nuget.org/api/v2

    nuget FsLab

Now we run `paket install` to install the packages.

Then we run `paket generate-load-scripts framework net45` to generate include scripts.

In a .fsx script file you can now use
    
    [lang=fsharp]
    
    #load @".paket/load/net45/fslab.fsx"

    // now ready to use FsLab and any of it's dependencies

    ## Generate load scripts while installing packages

Alternatively, the load scripts can be generated automatically when running the `paket install` command.

To enable this, add the `generate_load_scripts` option to the `paket.dependencies` file:

    [lang=paket]
    generate_load_scripts: true
    source https://nuget.org/api/v2

    nuget Suave
