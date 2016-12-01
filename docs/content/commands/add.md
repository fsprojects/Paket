## Adding to a project

By default a package is only added to the solution, but not on any of its projects. It's possible to add the package to a specified project at the same:

    [lang=batchfile]
    $ paket add nuget PACKAGENAME [version VERSION] [project PROJECT] [--force]

See also [paket remove](paket-remove.html).

## Sample

Consider the following paket.dependencies file:

    [lang=paket]
    source https://nuget.org/api/v2

    nuget FAKE

Now we run `paket add nuget xunit --interactive` to install the package:

![alt text](img/interactive-add.png "Interactive paket add")

This will add the package to the selected paket.references files and also to the paket.dependencies file:

    [lang=paket]
    source https://nuget.org/api/v2

    nuget FAKE
    nuget xunit
