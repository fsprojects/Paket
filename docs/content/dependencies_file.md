# The paket.dependencies file

The `paket.dependencies` file is used to specify rules regarding your application's dependencies.

To give you an overview, this is what Paket's `paket.dependencies` file looks like as of the time of writing:

    source "http://nuget.org/api/v2"

    nuget "Newtonsoft.Json" "~> 6.0"
    nuget "UnionArgParser" "~> 0.7"
    nuget "NUnit.Runners" "~> 2.6.3"
    nuget "NUnit" "~> 2.6.3"
    nuget "FAKE" "~> 3.4"
    nuget "FSharp.Formatting" "~> 2.4"
    nuget "DotNetZip" "~> 1.9.3"
    nuget "SourceLink.Fake" "~> 0.3"  
	github forki/FsUnit FsUnit.fs       // Only in 0.2.0 alpha versions

The syntax looks familiar to users of Ruby's [bundler](http://bundler.io/) [Gemfile](http://bundler.io/gemfile.html). This is intended because it proved to work well for the authors of Paket.

The file specifies that Paket's dependencies should be downloaded from [nuget.org](http://www.nuget.org) and that we need e.g. 
[`FAKE`](http://fsharp.github.io/FAKE/) [in version `3.4 <= x < 3.5`](#pessimistic-version-constraint) as a build tool.

Only direct dependencies should be listed. Paket uses this definition to compute a concrete dependency resolution, which also includes indirect dependencies. The resulting dependency graph is then persisted to the [`paket.lock` file](lock_file.html).

## Sources

Paket supports the following source types:

* [NuGet](nuget_dependencies.html)
* [Github](github_dependencies.html) ** Only in [0.2.0 alpha versions](https://www.nuget.org/packages/Paket/0.2.0-alpha001) **