# The paket.dependencies file

The `paket.dependencies` file is used to specify rules regarding your application's dependencies.

To give you an overview, consider the following `paket.dependencies` file:

    source https://nuget.org/api/v2

    nuget NUnit ~> 2.6.3
    nuget FAKE ~> 3.4
    nuget DotNetZip >= 1.9
    nuget SourceLink.Fake
    github forki/FsUnit FsUnit.fs

The file specifies that Paket's NuGet dependencies should be downloaded from [nuget.org](http://www.nuget.org) and that we need: 

  * [NUnit](http://www.nunit.org/) in version [2.6.3 <= x < 2.7](nuget-dependencies.html#Pessimistic-version-constraint)
  * [FAKE](http://fsharp.github.io/FAKE/) in version [3.4 <= x < 4.0](nuget-dependencies.html#Pessimistic-version-constraint) as a build tool
  * [DotNetZip](http://dotnetzip.codeplex.com/) with version which is at [least 1.9](http://fsprojects.github.io/Paket/nuget-dependencies.html#Greater-than-or-equal-version-constraint)
  * [SourceLink.Fake](https://github.com/ctaggart/SourceLink) in the latest version
  * [FSUnit.fs](https://github.com/forki/FsUnit) from github.

Paket uses this definition to compute a concrete dependency resolution, which also includes indirect dependencies. The resulting dependency graph is then persisted to the [`paket.lock` file](lock-file.html).

Only direct dependencies should be listed and you can use the [`paket simplify` command](paket-simplify.html) to remove indirect dependencies.

## Sources

Paket supports the following source types:

* [NuGet](nuget-dependencies.html)
* [GitHub](github-dependencies.html)
 
## Strict references

Paket usually references all direct and indirect dependencies that are listed in your [paket.references](references-files.md) files to your project file.
In `strict` mode it will **only** reference *direct* dependencies.

    references strict
    source https://nuget.org/api/v2

    nuget Newtonsoft.Json ~> 6.0
    nuget UnionArgParser ~> 0.7
    
## No content option

This option disables the installation of any content files.

    content none
    source https://nuget.org/api/v2

    nuget jQuery >= 0 // we don't install jQuery content files
    nuget UnionArgParser ~> 0.7
