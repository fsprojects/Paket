# The paket.references files

`paket.references` is used to specify which dependencies are to be installed into the MSBuild projects in your repository. Paket determines the set of dependencies that are to be referenced by each MSBuild project within a directory from its `paket.references` file.

It acts a lot like NuGet's `packages.config` files but there are some key differences:

- One does not specify package versions; these are instead sourced from the [`paket.lock` file](lock-file.html) (which are in turn derived from the rules contained within the [`paket.dependencies` file](dependencies-file.html) in the course of the *initial* [`paket install`](paket-install.html) or subsequent [`paket update`](paket-update.html) commands)
- Only direct dependencies should be listed (see below, [we're currently evaluating options for other reference modes](https://github.com/fsprojects/Paket/issues/38))
- It's just a plain text file

## Location

Paket looks for `paket.references` files underneath the folder where [`paket.dependencies`](dependencies-file.html) is located.

## Layout

The file whitelists any dependencies from the [`paket.lock` file](lock-file.html) set that are to be referenced within the projects alongside it in a given directory:

    [lang=paket]
    Newtonsoft.Json
    UnionArgParser
    DotNetZip
    RestSharp

    group Test
        NUnit

For each MSBuild project alongside a `paket.references`, [`paket install`](paket-install.html) and [`paket update`](paket-update.html) will add references to the dependencies listed in `paket.references` *and all their transitive dependencies* (unless [noted otherwise](dependencies-file.html#Strict-references)).

The references injected into the MSBuild project reflect the complete set of rules specified within the package for each `lib` and `Content` item; each reference is `Condition`al on an MSBuild expression predicated on the project's active framework etc. This allows you to change the target version of the MSBuild project (either within Visual Studio or e.g. as part of a multi-pass build) without reinstalling dependencies or incurring an impenetrable set of diffs.

Any [Roslyn based analyzer](analyzers.html) present in the packages will also be installed in the project.

## copy_local settings

It's possible to influence the `Private` property for references in project files:

    [lang=paket]
    Newtonsoft.Json copy_local: false

## specific_version settings

It's possible to influence the `SpecificVersion` property for references in project files:

    [lang=paket]
    Newtonsoft.Json specific_version: false

## import_targets settings

If you don't want to import `.targets` and `.props` files you can disable it via the `import_targets` switch:

    [lang=paket]
    Microsoft.Bcl.Build import_targets: false

## No content option

This option allows you to disable the installation of any content files for the given package:

    [lang=paket]
    jQuery content: none
    Fody   content: once
    ServiceStack.Swagger content: true

In this example,
- `once` will install content files but not overwrite
- `content: true` will install content and always override

## Framework restrictions

Sometimes you don't want to generate dependencies for older framework versions. You can control this in the [`paket.dependencies` file](nuget-dependencies.html#Framework-restrictions) or via the `framework` switch:

    [lang=paket]
    Newtonsoft.Json framework: net35, net40
    DotNetZip framework: >= net45

> Note: This feature is deprecated and can be seen as an expert feature. 
> Using framework restrictions on single packages might make you projects uncompilable.
> The recommended way is to globally (on a group) specifiy the frameworks you want to compile for.

This feature basically tells paket to only consider the specified frameworks for this package on this particular project.
The resolution will not be modified when restrictions are specified in the reference file.
It means 

> In this project `Newtonsoft.Json` is only required when compiling against `net35` or `net40`.
> I never need this package to compile for another framework like `net45`, therefore don't add references for other frameworks.


## Redirects settings

You can instruct Paket to create assembly binding redirects for NuGet packages:

    [lang=paket]
    FSharp.Core redirects: on

Redirects are created only if they are required. However, you can instruct Paket to create it regardless:

    [lang=paket]
    FSharp.Core redirects: force

In contrast, you have the option to force Paket to not create a redirect:

    [lang=paket]
    FSharp.Core redirects: off

Redirects settings in [references files](references-files.html#Redirects-settings) takes precedence over settings in [dependencies file](nuget-dependencies.html#redirects-settings).

## Excluding libraries

This option allows you to exclude libraries from being referenced in project files:

    [lang=paket]
    PackageA
      exclude A1.dll
      exclude A2.dll
    Dapper
    NUnit
      exclude nunit.framework.dll

## Library aliases

This option allows you to specify library aliases:

    [lang=paket]
    PackageA
      alias A1.dll Name2,Name3
      alias A2.dll MyAlias1
    Dapper
    NUnit

## File name conventions

If Paket finds `paket.references` in a folder, the dependencies it specifies will be added to all MSBuild projects in that folder.

If you have multiple MSBuild projects in a folder that require a non-homogeneous set of references, you have two options:

- Have a shared `paket.references` file that acts as a default for all except ones that require special treatment. For each special-case project, add a `<MSBuild project>.paket.references` file
- Add a project-specific `<MSBuild project>.paket.references` file for each and every project that requires any references

Please note that Paket does not union the directory's shared reference list with the project-specific references. If a specific references file is present, it overrides the default (even if that file is empty, in which case it prevents anything being added to its sibling project file).

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

- the dependencies specified in `/src/paket.references` will be added to `/src/Example.csproj` and `/src/Example.fsproj`
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
