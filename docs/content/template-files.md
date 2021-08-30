# The paket.template files

The `paket.template` files are used to specify rules to create `.nupkg`s with
the [`paket pack` command](paket-pack.html).

The `type` specifier must be the first line of the template file. It has two
possible values:

* `file`: All of the information to build the `.nupkg` is contained within the
  template file
* `project`: Paket will look for a matching project file, and infer dependencies
  and metadata from the project

Matching project and template files must be in the same directory. If only one
project is in the directory the template file can be called `paket.template`,
otherwise the name of the template file must be the name of the project file
with `.paket.template` added to the end.

For example:

```text
Paket.Project.fsproj
Paket.Project.fsproj.paket.template
```

are matching files.

## Examples

### Example 1

A `paket.template` file using `type project` may look like this:

```text
type project
licenseExpression MIT
```

This template file will be used to create a `.nupkg`

* named `Test.Paket.Package.[Version].nupkg`,
* with `Version`, `Author` and `Description` from assembly attributes,
* containing `$(OutDir)\$(ProjectName).*` (all files matching project name in
  the output directory) directory in the `lib` directory of the package.
* referencing all packages referenced by the project,
* including package references,
* including project references for projects in the solution that have a
  `paket.template` file.

### Example 2

A `paket.template` file using `type file` may look like this:

```text
type file
id Test.Paket.Package
version 1.0
authors Michael Newton
description
  description of this test package
files
  src/Test.Paket.Package/bin/Debug ==> lib
```

This template file will create a `.nupkg` called
`Test.Paket.Package.<version>.nupkg` with the contents of the
`src/Test.Paket.Package/bin/Debug` directory in the `lib` directory of the
package file.

## General metadata

Metadata fields can be specified in two ways; either on a single line prefixed
with the property name (case insensitive), or in an indented block following a
line containing nothing but the property name.

For example:

```text
description This is a valid description

DESCRIPTION
  So is this
  description here

description This would
  cause an error
```

There are 4 compulsory fields required to create a `.nupkg`. These can always be
specified in the template file, or in a project based template can be omitted
and an attempt will be made to infer them as below:

* `id`: The package ID of the resulting `.nupkg` (which also determines the
  output filename). If omitted in a project template, reflection will be used to
  determine the assembly name.
* `version`: The version of the resulting `.nupkg`. If omitted in a project
  template, reflection will be used to obtain the value of the
  `AssemblyInformationalVersionAttribute` or if that is missing the
  `AssemblyVersionAttribute`.
* `authors`: A comma separated list of authors for the `.nupkg`. Inferred as the
  value of the `AssemblyCompanyAttribute` if omitted in a project template.
* `description`: This will be displayed as the `.nupkg` description. Inferred
  from the `AssemblyDescriptionAttribute` if unspecified.

The other general metadata properties are all optional, and map directly to the
field of the same name in the `.nupkg`.

* `title`: Inferred as the value of the `AssemblyTitleAttribute` if omitted in a
  project template.
* `owners`
* `releaseNotes`
* `summary`
* `readme`: This is a path to a readme file *in* the package. It should be added with the `files` block (see below).
* `language`
* `projectUrl`
* `iconUrl`
* `licenseExpression`: More info on what you can specify: <https://docs.microsoft.com/de-de/nuget/reference/nuspec#license>  
* `licenseUrl` (deprecated by NuGet)
* `repositoryType`
* `repositoryUrl`
* `repositoryBranch` (requires `repositoryUrl` + Nuget 4.7+)
* `repositoryCommit` (requires `repositoryUrl` + Nuget 4.7+)
* `copyright`
* `requireLicenseAcceptance` (`true` or `false`)
* `tags`
* `developmentDependency` (`true` or `false`)

### Dependencies and files

The dependencies the package relies on, and the files to package are specified
in a slightly different format. These two fields will be ignored in project
templates if specified, and instead the rules below will be used to decide on
the files and dependencies added.

#### Files

A files block looks like this:

```text
files
  relative/to/template/file ==> directory/in/nupkg
  second/thing/to/pack ==> directory/in/nupkg
  second/thing/**/file.* ==> directory/in/nupkg
```

If the source part refers to a file then it is copied into the target directory.
If it refers to a directory, the contents of the directory will be copied into
the target directory. If you omit the target directory, then the source is copied into
the `lib` directory of the package. If you use `.` as the target, the source is copied
into the root of the package.

Excluding certain files looks like this:

```text
files
  relative/to/template/file ==> directory/in/nupkg
  second/thing/**/file.* ==> directory/in/nupkg
  !second/thing/**/file.zip
  ../outside/file.* ==> directory/in/nupkg/other
  !../outside/file.zip
```

The pattern needs to match file names, excluding directories like `!second`
won't have an effect. Please use `!second/*.*` instead.

In a project template, the files included will be:

* The output assembly of the matching project (in the correct `lib` directory
  for a library, or `tools` for an executable).
* The output assemblies of any project references which do not have a matching
  template file.

#### Referenced projects

With the `include-referenced-projects` switch you can tell Paket to pack
referenced projects into the package.

```text
include-referenced-projects true
```

If the referenced project has its own template file then it will be added to the package 
as NuGet dependency. You can control the version constraint for such dependencies with 
the `interproject-references` option.  
There are several possible values for this options. Consider them with an example.

`ProjectA` references `ProjectB`. Both projects have template files. 
`ProjectB`'s version is `1.2.3`.   

(The first column is a line from `ProjectA`'s template file, 
the second column is a version constraint for the `ProjectB` dependency in `ProjectA.nupkg`.)  

|||
| --- | --- |
| `interproject-references min` | `1.2.3` |
| `interproject-references fix` | `[1.2.3]` |
| `interproject-references keep-major` | `[1.2.3,2.0.0)` |
| `interproject-references keep-minor` | `[1.2.3,1.3.0)` |
| `interproject-references keep-patch` | `[1.2.3,1.2.4)` |
|||

The default value is `interproject-references min`.

You can override the template file option with the CLI parameter 
`--interproject-references` which supports the same values.

#### References

A references block looks like this:

```text
references
  filename1.dll
  filename2.dll
```

If you omit the `references` block then all libraries in the packages will be
references.

#### Framework assembly references

A block with framework assembly references looks like this:

```text
frameworkAssemblies
  System.Xml
  System.Xml.Linq
```

If you omit the `frameworkAssemblies` block then all libraries in the packages
will be framework assemblies.

#### Dependencies

A dependency block looks like this:

```text
dependencies
  FSharp.Core >= 4.3.1
  Other.Dep ~> 2.5
  Any.Version
```

The syntax for specifying allowed dependency ranges are identical to in the
ranges in [`paket.dependencies` files](dependencies-file.html).

It's possible to use `CURRENTVERSION` as a placeholder for the current version
of the package:

```text
dependencies
  FSharp.Core >= 4.3.1
  Other.Dep ~> CURRENTVERSION
```

The `LOCKEDVERSION` placeholder allows to reference the currently used
dependency version from the [`paket.lock` file](lock-file.html):

```text
dependencies
  FSharp.Core >= 4.3.1
  Other.Dep ~> LOCKEDVERSION
```

`LOCKEDVERSION` can be constrained to a particular group by suffixing
the placeholder with the particular group name.

```text
dependencies
  FSharp.Core >= LOCKEDVERSION-NetStandard
```

`LOCKEDVERSION` and `CURRENTVERSION` support using partial constraints,
to allow transitive dependencies on specific SemVer compatibility level.
This allows to create permissive pessimistic constraints automatically.

Use only `LOCKED` or `CURRENT` and semicolon-delimited name of the latest
SemVer segment (`Major`, `Minor`, `Patch` or `Build`), or bracketed number
of segments from the original constraint to be used.
Negative numbers denote count down from the last non-zero segment.

```text
dependencies
  FSharp.Core ~> LOCKED:Minor
  My.Own.Package ~> CURRENT:[2]
```

Using `0` or `-4` is not supported; `[4]` or `Build` will result in the original
version but with any prerelease specifiers and metedata cut off.

Combining with group-binding syntax is supported, in either order;
assuming group "NetStandard" and 4-segment version, these constraints are equal:

```text
  LOCKED-NetStandard:Patch
  LOCKED-NetStandard:[3]
  LOCKED-NetStandard:[-1]
  LOCKED:Patch-NetStandard
  LOCKED:[3]-NetStandard
  LOCKED:[-1]-NetStandard
```

It's possible to add a line to constrain the target framework:

```text
dependencies
  framework: net45
    FSharp.Core 4.3.1
    My.OtherThing
  framework: netstandard11
    FSharp.Core 4.3.1
```

Like that the package is only going to be used by a project `>= net45` and for
`>= netstandard11` it will not use the `My.OtherThing` package.

In a project file, the following dependencies will be added:

* Any Paket dependency with the range specified in the
  [`paket.dependencies` file](dependencies-file.html).
* Any Paket dependency with the range specified in the
  [`paket.lock` file](lock-file.html) (if `lock-dependencies` parameter is used
  in [`paket pack`](paket-pack.html)).
* Any project reference with a matching `paket.template` file with a minimum
  version requirement of the version currently being packaged.

If you need to exclude dependencies from the automatic discovery then you can
use the `excludeddependencies` block:

```text
excludeddependencies
  FSharp.Core
  Other.Dep
```

Another way to exclude dependencies is to exclude a whole dependency group with
the `excludedgroups` block:

```text
excludedgroups
  build
  test
```

#### PDB files

With the `include-pdbs` switch you can tell Paket to pack PDBs into the package.

```text
include-pdbs true
```

This only works for `paket.template` files of type `project`.

## Comments

A line starting with a `#` or `//` is considered a comment and will be ignored
by the parser. 
Endline comments are only allowed in dependency constraint lines.
