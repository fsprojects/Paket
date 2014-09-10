The paket.references files
==========================

A `paket.references` file is used to specify which dependencies are to be installed in a given Project.

It acts a lot like NuGet's `packages.config` files but there are some key differences:

- one does not specify the versions, these are instead sourced from the [`paket.lock`](lock_file.html) (which are in turn derived from the rules contained within the [`paket.dependencies`](Dependencies_file.html) in the course of the *initial* `paket install` or `paket update` commands)
- only root dependencies should be listed (see below)
- it's just a plain text file, e.g. the content looks like this:-

      Newtonsoft.Json
      UnionArgParser
      DotNetZip
      RestSharp

If you place a `paket.references` file next to a `.csproj` or `.fsproj` file then the [install command](paket_install.html) will add references to these packages *and all indirect dependencies*.
