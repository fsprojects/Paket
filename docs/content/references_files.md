The paket.references files
==========================

`paket.references` is used to specify which dependencies are to be installed in MSBuild projects.

It acts a lot like NuGet's `packages.config` files but there are some key differences:

- One does not specify the versions, these are instead sourced from the [`paket.lock`](lock_file.html) (which are in turn derived from the rules contained within the [`paket.dependencies`](dependencies_file.html) in the course of the *initial* [`paket install`](paket_install.html) or [`paket update`](paket_update.html) commands)
- Only direct dependencies should be listed (see below, [we're evaluation options for other reference modes](https://github.com/fsprojects/Paket/issues/38))
- It's just a plain text file

## Layout

Paket expects a list of dependencies that need to be referenced by your projects inside the `paket.references` file:

    Newtonsoft.Json
    UnionArgParser
    DotNetZip
    RestSharp

The dependencies listed are cross-checked against [`paket.lock`](lock_file.html).

If you place `paket.references` next to MSBuild projects then [`paket install`](paket_install.html) and [`paket update`](paket_update.html) will add references to the dependencies listed in `paket.references` *and all their indirect dependencies*.

The MSBuild references added are conditional depending on the libraries and content contained in the dependency. This means you can change the target version of the MSBuild project without reinstalling dependencies.

Having `paket.references` is not required. It may also be empty (then, no references are added).

## File name conventions

When Paket finds `paket.references` in a folder, the dependencies it specifies will be added to all MSBuild projects in that folder.

If you have multiple MSBuild projects in a folder that require a different set of references, you have two options:

- Have a global `paket.references` file for all projects except the ones that need special care. These special-care projects will have a `<MSBuild project>.paket.references` file
- Have a project-specific `<MSBuild project>.paket.references` file for all projects

Please note that Paket does not merge global and project-specific references.

### Global paket.references

    /
    /paket.dependencies
    /paket.lock
    /src/Example.csproj
    /src/Example.fsproj
    /src/Example.vbproj
    /src/paket.references
    /test/Example.csproj

In this example,

- the dependencies specified in `/src/paket.references` will be added to `/src/Example.csproj`, `/src/Example.fsproj` and `/src/Example.vbproj`
- `/test/Example.csproj` is left untouched

### Global paket.references with project-specific override

    /
    /paket.dependencies
    /paket.lock
    /src/Example.csproj
    /src/Example.fsproj
    /src/Example.vbproj
    /src/Example.vbproj.paket.references
    /src/paket.references

In this example,

- the dependencies specified in `/src/paket.references` will be added to `/src/Example.csproj` and `/src/Example.fssproj`
- the dependencies specified in `/src/Example.vbproj.paket.references` will be added to `/src/Example.vbproj`
- Paket does not merge the dependencies of `/src/paket.references` and `/src/Example.vbproj.paket.references`

### Project-specific references only

    /
    /paket.dependencies
    /paket.lock
    /src/Example.csproj
    /src/Example.csproj.paket.references
    /src/Example.fsproj
    /src/Example.fsproj.paket.references

In this example,

- the dependencies specified in `/src/Example.csproj.paket.references` will be added to `/src/Example.csproj`
- the dependencies specified in `/src/Example.fsproj.paket.references` will be added to `/src/Example.fsproj`
