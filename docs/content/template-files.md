# The paket.template files

The `paket.template` files are used to specify rules to create nupkgs by using the [`paket pack` command](paket-pack.html).

An example `paket.template` file might look like the following:

    [lang=batchfile]
    type file
    id Test.Paket.Package
    version 1.0
    authors Michael Newton
    description
		    description of this test package
    files
        src/Test.Paket.Package/bin/Debug ==> lib

This template file will create a nupkg called `Test.Paket.Package.[Version].nupkg` with the
contents of the `src/Test.Paket.Package/bin/Debug` directory in the `lib` directory
of the package file.

The `type` specifier must be the first line of the template file. It has two possible
values:

* file - All of the information to build the nupkg is contained within the template file
* project - Paket will look for a matching project file, and infer dependencies and metadata from the project

Matching project and template files must be in the same directory. If only one project is in the directory the template file
can be called `paket.template`, otherwise the name of the template file must be the name of the project file with ".paket.template" added to the end.

For example:

	Paket.Project.fsproj
	Paket.Project.fsproj.paket.template

are matching files.

### General Metadata

Metadata fields can be specified in two ways; either on a single line prefixed with the property
name (case insensitive), or in an indented block following a line containing nothing but the property name.

For example:

	description This is a valid description

	DESCRIPTION
	  So is this
	  description here

	description This would
	  cause an error

There are 4 compulsory fields required to create a nupkg. These can always be specified in the
template file, or in a project based template can be omitted and an attempt will be made to infer
them as below:

* id - the Id of the resulting nupkg (which also determines the output filename). If omitted in a
  project template, reflection will be used to determine the assembly name.
* version - the version of the resulting nupkg. If omitted in a project template, reflection will
  be used to obtain the value of the `AssemblyInformationalVersionAttribute` or if that is missing
  the `AssemblyVersionAttribute`.
* authors - a comma separated list of authors for the nuget package. Inferred as the value of the
  `AssemblyCompanyAttribute` if omitted in a project template.
* description - this will be displayed as the nupkg description. Inferred from the `AssemblyDescriptionAttribute`
  if unspecified.

The other general metadata properties are all optional, and map directly to the field of the same
name in the nupkg.

* title
* owners
* releaseNotes
* summary
* language
* projectUrl
* iconUrl
* licenseUrl
* copyright
* requireLicenseAcceptance (boolean)
* tags
* developmentDependency (boolean)

### Dependencies and Files

The dependencies the package relies on, and the files to package are specified in a slightly different format.
These two fields will be ignored in project templates if specified, and instead the rules below will be used
to decide on the files and dependencies added.

#### Files

A files block looks like this:

    [lang=batchfile]
    files
	    relative/to/template/file ==> folder/in/nupkg
	    second/thing/to/pack ==> folder/in/nupkg
		second/thing/**/file.* ==> folder/in/nupkg

If the source part refers to a file then it is copied into the target directory. If it
refers to a directory, the contents of the directory will be copied into the target folder.
If you omit the target folder, then the source is copied into the `lib` folder of the package.

In a project template, the files included will be:

* the output assembly of the matching project (in the correct lib directory if a library, or tools if an exe)
* the output assemblies of any project references which do not have a matching template file

#### Dependencies

A dependency block looks like this:

	dependencies
	  FSharp.Core >= 4.3.1
	  Other.Dep ~> 2.5
	  Any.Version

The syntax for specifying allowed dependency ranges are identical to in the ranges in [`paket.dependencies` files](dependencies-file.html).

In a project file, the following dependencies will be added:

* any paket dependency with the range specified in the [`paket.dependencies` file](dependencies-file.html).
* any project reference with a matching paket.template file with a minimum version requirement of the version currently being packaged.