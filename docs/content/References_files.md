The Paket.references files
==========================

A `Paket.references` file is used to specify which dependencies are needed in a gicen Project file.
It acts like NuGet's `packages.config` files but it's just a plain file which specifies the direct dependencies of a project:
  
    Newtonsoft.Json
	UnionArgParser
	DotNetZip
	RestSharp

If you put such `Paket.references` next to a `csproj` or `fsproj` file then the [install command](paket_install.html) will add references to these packages and all indirect dependencies.