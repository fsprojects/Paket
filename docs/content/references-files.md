# The paket.references files

`paket.references` is used to specify which dependencies are to be installed
into the MSBuild projects in your repository. Paket determines the set of
dependencies that are to be referenced by each MSBuild project within a
directory from its `paket.references` file.

It acts a lot like NuGet's `packages.config` files but there are some key
differences:

* One does not specify package versions; these are instead sourced from the
  [`paket.lock` file](lock-file.html). Versions in turn derived from the rules
  contained within the [`paket.dependencies` file](dependencies-file.html) in
  the course of the *initial* [`paket install`](paket-install.html) or
  subsequent [`paket update`](paket-update.html) commands.
* Only direct dependencies should be listed unless you use
  [`strict` references](dependencies-file.html#Strict-references).
* It's just a plain text file.

## Location

Paket looks for `paket.references` files underneath the directory where
[`paket.dependencies`](dependencies-file.html) is located.

## Layout

The file whitelists any dependencies from the
[`paket.lock` file](lock-file.html) set that are to be referenced within the
projects alongside it in a given directory:

```paket
Newtonsoft.Json
UnionArgParser
DotNetZip
RestSharp

group Test
  NUnit
```

For each MSBuild project alongside a `paket.references`,
[`paket install`](paket-install.html) and [`paket update`](paket-update.html)
will add references to the dependencies listed in `paket.references` *and all
their transitive dependencies* (unless
[noted otherwise](dependencies-file.html#Strict-references)).

The references injected into the MSBuild project reflect the complete set of
rules specified within the package for each `lib` and `Content` item; each
reference is `Condition`al on an MSBuild expression predicated on the project's
active framework etc. This allows you to change the target version of the
MSBuild project (either within Visual Studio or e.g. as part of a multi-pass
build) without reinstalling dependencies or incurring an impenetrable set of
diffs.

Any [Roslyn based analyzer](analyzers.html) present in the packages will also be
installed in the project.

## Overriding settings from [`paket.dependencies`](dependencies-file.html)

You can override these options either defined globally or per package in the
[`paket.dependencies`](dependencies-file.html):

* [`content`](nuget-dependencies.html#Controlling-whether-content-files-should-be-copied-to-the-project)
* [`copy_local`](nuget-dependencies.html#Controlling-whether-assemblies-should-be-copied-to-the-output-directory-during-build)
* [`framework`](nuget-dependencies.html#Framework-restrictions)
* [`import_targets`](nuget-dependencies.html#Importing-and-files)
* [`license_download`](nuget-dependencies.html#License-download)
* [`redirects`](nuget-dependencies.html#Controlling-assembly-binding-redirects)
* [`specific_version`](nuget-dependencies.html#Referencing-specific-versions-in-projects)

A couple of examples:

```paket
Newtonsoft.Json copy_local: false
Newtonsoft.Json specific_version: false
Microsoft.Bcl.Build import_targets: false
Fody content: once
DotNetZip framework: >= net45
FSharp.Core redirects: on
```

## Adding support for COM interop DLL
Follows the same syntax as the previous one:
`PkgName embed_interop_types: true`
In case it is not enabled, the default behavior is to drop `<EmbedInteropTypes>` from the project file.

## Excluding libraries

This option allows you to exclude libraries from being referenced in project files:

```paket
PackageA
  exclude A1.dll
  exclude A2.dll
Dapper
NUnit
  exclude nunit.framework.dll
```

## Library aliases

This option allows you to specify library aliases:

```paket
PackageA
  alias A1.dll Name2,Name3
  alias A2.dll MyAlias1
Dapper
NUnit
```

## File name conventions

If Paket finds `paket.references` in a directory, the dependencies it specifies
will be added to all MSBuild projects in that directory.

If you have multiple MSBuild projects in a directory that require a
non-homogeneous set of references, you have two options:

* Have a shared `paket.references` file that acts as a default for all except
  ones that require special treatment. For each special-case project, add a
  `<MSBuild project>.paket.references` file.
* Add a project-specific `<MSBuild project>.paket.references` file for each and
  every project that requires any references.

Please note that Paket does not union the directory's shared reference list with
the project-specific references. If a specific references file is present, it
overrides the default (even if that file is empty, in which case it prevents
anything being added to its sibling project file).

### Global `paket.references`

```text
.
├── src
│   ├── Example.csproj
│   ├── Example.fsproj
│   ├── Example.vbproj
│   └── paket.references
├── test
│   └── Example.csproj
├── paket.dependencies
└── paket.lock
```

In this example,

* the dependencies specified in `/src/paket.references` will be added to
  `/src/Example.csproj`, `/src/Example.fsproj` and `/src/Example.vbproj`,
* `/test/Example.csproj` is left untouched.

### Global `paket.references` with project-specific override

```text
.
├── src
│   ├── Example.csproj
│   ├── Example.fsproj
│   ├── Example.vbproj
│   ├── Example.vbproj.paket.references
│   └── paket.references
├── paket.dependencies
└── paket.lock
```

In this example,

* the dependencies specified in `/src/paket.references` will be added to
  `/src/Example.csproj` and `/src/Example.fsproj`,
* the dependencies specified in `/src/Example.vbproj.paket.references` will be
  added to `/src/Example.vbproj`,
* Paket does not merge the dependencies of `/src/paket.references` and
  `/src/Example.vbproj.paket.references`.

### Project-specific references only

```text
.
├── src
│   ├── Example.csproj
│   ├── Example.csproj.paket.references
│   ├── Example.fsproj
│   └── Example.fsproj.paket.references
├── paket.dependencies
└── paket.lock
```

In this example,

* the dependencies specified in `/src/Example.csproj.paket.references` will be
  added to `/src/Example.csproj`,
* the dependencies specified in `/src/Example.fsproj.paket.references` will be
  added to `/src/Example.fsproj`.
