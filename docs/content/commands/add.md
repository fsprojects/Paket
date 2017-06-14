## Adding to a project

By default a package is only added to the solution, but not on any of its projects. It's possible to add the package to a specified project at the same:

    [lang=batchfile]
    $ paket add PACKAGEID [--version VERSION] [--project PROJECT] [--force]

See also [`paket remove`](paket-remove.html).

## Example

Consider the following [`paket.dependencies` file](dependencies-file.html):

    [lang=paket]
    source https://nuget.org/api/v2

    nuget FAKE

Now we run `paket add xunit --interactive` to install the package:

![paket add --interactive](img/interactive-add.png "paket add --interactive")

This will add the package to the selected [`paket.references` files](references-files.html) and also to the [`paket.dependencies` file](dependencies-file.html):

    [lang=paket]
    source https://nuget.org/api/v2

    nuget FAKE
    nuget xunit
