# The paket.template files

The `paket.template` files are used to specify rules to create nupkgs by using the [`paket pack` command](paket-pack.html).

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
### Sample 1

A `paket.template` file using `type project` may look like this:

    type project
    licenseUrl http://opensource.org/licenses/MIT

This template file will create a nupkg:
 - Named `Test.Paket.Package.[Version].nupkg`
 - Version, Author and Description from assembly attributes
 - Containing `$(OutDir)\$(ProjectName).*` (all files matching project name in the output directory) directory in the `lib` directory of the package.
 - Referencing all packages referenced by the project.
   - Package references
   - Project references, for projects in the sln that has `paket.template` files.

### Sample 2

A `paket.template` file using `type file` may look like this:

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

* title (Inferred as the value of the `AssemblyTitleAttribute` if omitted in a project template)
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

    files
	    relative/to/template/file ==> folder/in/nupkg
	    second/thing/to/pack ==> folder/in/nupkg
		second/thing/**/file.* ==> folder/in/nupkg

If the source part refers to a file then it is copied into the target directory. If it
refers to a directory, the contents of the directory will be copied into the target folder.
If you omit the target folder, then the source is copied into the `lib` folder of the package.

Excluding certain files looks like this:

    files
        relative/to/template/file ==> folder/in/nupkg
        second/thing/**/file.* ==> folder/in/nupkg
        !second/thing/**/file.zip
        ../outside/file.* ==> folder/in/nupkg/other
        !../outside/file.zip

The pattern needs to match file-names, excluding directories like `!second` won't have an effect, please use `!second/*.*` instead.

In a project template, the files included will be:

* the output assembly of the matching project (in the correct lib directory if a library, or tools if an exe)
* the output assemblies of any project references which do not have a matching template file

#### References

A references block looks like this:

    references
	    filename1.dll
	    filename2.dll

If you omit the references block then all libraries in the packages will get referenced.

#### Framework assembly references

A block with framework assembly references looks like this:

    frameworkAssemblies
	    System.Xml
		System.Xml.Linq

If you omit the references block then all libraries in the packages will get referenced.

#### Dependencies

A dependency block looks like this:

	dependencies
	  FSharp.Core >= 4.3.1
	  Other.Dep ~> 2.5
	  Any.Version

The syntax for specifying allowed dependency ranges are identical to in the ranges in [`paket.dependencies` files](dependencies-file.html).

It's possible to use `CURRENTVERSION` as a placeholder for the current version of the package:

	dependencies
	  FSharp.Core >= 4.3.1
	  Other.Dep ~> CURRENTVERSION

The `LOCKEDVERSION` placeholder allows to reference the currently used dependency version from the paket.lock file:

	dependencies
	  FSharp.Core >= 4.3.1
	  Other.Dep ~> LOCKEDVERSION

In a project file, the following dependencies will be added:

* any paket dependency with the range specified in the [`paket.dependencies` file](dependencies-file.html).
* any paket dependency with the range specified in the [`paket.lock` file](lock-file.html) (if `lock-dependencies` parameter is used in [`paket pack`](paket-pack.html)).
* any project reference with a matching paket.template file with a minimum version requirement of the version currently being packaged.

If you need to exclude dependencies from the automatic discovery then you can use the `excludeddependencies` block:

	excludeddependencies
	  FSharp.Core
	  Other.Dep

### Comments

A line starting with a # or // is considered a comment and will be ignored by the parser.
