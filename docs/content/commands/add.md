## Adding to a single project

It's also possible to add a package to a specified project only: 

    [lang=batchfile]
    $ paket add nuget PACKAGENAME [version VERSION] [project PROJECT] [--force] [--hard]

See also [paket remove](paket-remove.html).

## Sample

Consider the following paket.dependencies file:

	source https://nuget.org/api/v2

	nuget FAKE

Now we run `paket add nuget xunit --interactive` install the package:

![alt text](img/interactive-add.png "Interactive paket add")

This will add the package to the selected paket.references files and also to the paket.dependencies file:

	source https://nuget.org/api/v2

	nuget FAKE
	nuget xunit