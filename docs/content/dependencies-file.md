# The paket.dependencies file

The `paket.dependencies` file is used to specify rules regarding your application's dependencies. It contains top level dependencies from all projects in the solution, while [`paket.references` file](references-files.html) specifies dependencies only for particular project.

To give you an overview, consider the following `paket.dependencies` file:

    [lang=paket]
    source https://nuget.org/api/v2

    // NuGet packages
    nuget NUnit ~> 2.6.3
    nuget FAKE ~> 3.4
    nuget DotNetZip >= 1.9

    // Files from GitHub repositories
    github forki/FsUnit FsUnit.fs

    // Gist files
    gist Thorium/1972349 timestamp.fs

    // HTTP resources
    http http://www.fssnip.net/1n decrypt.fs

The file specifies that Paket's NuGet dependencies should be downloaded from [nuget.org](http://www.nuget.org) and that we need:

  * [NUnit](http://www.nunit.org/) in version [2.6.3 <= x < 2.7](nuget-dependencies.html#Pessimistic-version-constraint)
  * [FAKE](http://fsharp.github.io/FAKE/) in version [3.4 <= x < 4.0](nuget-dependencies.html#Pessimistic-version-constraint) as a build tool
  * [DotNetZip](http://dotnetzip.codeplex.com/) with version which is at [least 1.9](http://fsprojects.github.io/Paket/nuget-dependencies.html#Greater-than-or-equal-version-constraint)
  * [FSUnit.fs](https://github.com/forki/FsUnit) from GitHub.
  * Gist number [1972349](https://gist.github.com/Thorium/1972349) from GitHub Gist
  * External HTTP resource, e.g. [1n](http://www.fssnip.net/1n) from [FSSnip](http://www.fssnip.net/)

Paket uses this definition to compute a concrete dependency resolution, which also includes [transitive dependencies](faq.html#transitive). The resulting dependency graph is then persisted to the [`paket.lock` file](lock-file.html).

Only direct dependencies should be listed and you can use the [`paket simplify` command](paket-simplify.html) to remove [transitive dependencies](faq.html#transitive).

## Sources

Paket supports the following source types:

* [NuGet](nuget-dependencies.html)
* [Git](git-dependencies.html)
* [GitHub and Gist](github-dependencies.html)
* [HTTP](http-dependencies.html) (any single file from any site without version control)

## Global options

### Required Paket version

It is possible to require a specific Paket version for a [`paket.dependencies` file](dependencies-file.html).
This can be achieved by a line which starts with `version` followed by a requested `paket.exe` version and optionally [bootstrapper command line](bootstrapper.html) arguments:

```paket
version 3.24.1

source https://api.nuget.org/v3/index.json
nuget FAKE
nuget FSharp.Core ~> 4
```

or 

```paket
version 3.24.1 --prefer-nuget

source https://api.nuget.org/v3/index.json
nuget FAKE
nuget FSharp.Core ~> 4
```

### Strict references

Paket usually references all direct and [transitive dependencies](faq.html#transitive) that are listed in your [`paket.references` files](references-files.html) to your project file.
In `strict` mode it will **only** reference *direct* dependencies.

    [lang=paket]
    references: strict
    source https://nuget.org/api/v2

    nuget Newtonsoft.Json ~> 6.0
    nuget UnionArgParser ~> 0.7

Note that the resolution phase is not affected by this flag, it will still resolve, lock and download all transitive references.

### Framework restrictions

Sometimes you don't want to generate dependencies for older framework versions. You can control this in the [`paket.dependencies` file](dependencies-file.html):

    [lang=paket]
    framework: net35, net40
    source https://nuget.org/api/v2

    nuget Example >= 2.0 // only .NET 3.5 and .NET 4.0

It means

> Paket, I only compile for 'net35' and 'net40', please leave out all other stuff I don't need to compile for this set of frameworks.

#### Automatic framework detection

Paket can detect the target frameworks from your project and then limit the installation to these target frameworks. You can control this in the [`paket.dependencies` file](dependencies-file.html):

    [lang=paket]
    framework: auto-detect
    source https://nuget.org/api/v2

    nuget Example >= 2.0 // only the target frameworks that are used in projects

If you change the target frameworks in the projects then you need to run `paket install` again.

### No content option

This option disables the installation of any content files:

    [lang=paket]
    content: none
    source https://nuget.org/api/v2

    nuget jQuery >= 0 // we don't install jQuery content files
    nuget UnionArgParser ~> 0.7

### CopyToOutputDirectory settings

It's possible to influence the `CopyToOutputDirectory` property for all content references in a group:

    [lang=paket]
    source https://nuget.org/api/v2
	copy_content_to_output_dir: always

    nuget jQuery 
	nuget Fody
	nuget ServiceStack.Swagger

It is also possible to define this behavior on level of individual NuGet packages:

    [lang=paket]
    source https://nuget.org/api/v2
    
    nuget jQuery 
	nuget Fody copy_content_to_output_dir: always
	nuget ServiceStack.Swagger
	
### import_targets settings

If you don't want to import `.targets` and `.props` files you can disable it via the `import_targets` switch:

    [lang=paket]
    import_targets: false
    source https://nuget.org/api/v2

    nuget Microsoft.Bcl.Build // we don't import .targets and .props
    nuget UnionArgParser ~> 0.7

### copy_local settings

It's possible to influence the `Private` property for references via the `copy_local` switch:

    [lang=paket]
    copy_local: false
    source https://nuget.org/api/v2

    nuget Newtonsoft.Json

### Redirects option

This option tells paket to create [Assembly Binding Redirects](https://msdn.microsoft.com/en-us/library/433ysdt1(v=vs.110).aspx) for all referenced libraries. This option only instructs Paket to create and manage binding redirects in **existing `App.config` files**, it will not create a new `App.config` file for you. However you can create `App.config` files by adding the `--createnewbindingfiles` flag to [`paket install`](commands/install.html)

    [lang=paket]
    redirects: on
    source https://nuget.org/api/v2

    nuget UnionArgParser ~> 0.7

On the other hand, you can instruct Paket to create no [Assembly Binding Redirects](https://msdn.microsoft.com/en-us/library/433ysdt1(v=vs.110).aspx), regardless a package instructs otherwise.

    [lang=paket]
    redirects: off
    source https://nuget.org/api/v2

    nuget UnionArgParser ~> 0.7 redirects: on
    nuget FSharp.Core redirects: force

If you're using multiple groups, you must set `redirects: off` for each one of them.

    [lang=paket]
    redirects: off
    source https://nuget.org/api/v2

    nuget UnionArgParser ~> 0.7 redirects: on
    nuget FSharp.Core redirects: force

    group Build
        redirects: off
	    source https://nuget.org/api/v2

        nuget FAKE redirects: on

### Strategy option

This option tells Paket what resolver strategy it should use for [transitive dependencies](faq.html#transitive).

NuGet's dependency syntax led to a lot of incompatible packages on nuget.org. 
To make your transition to Paket easier and to allow package authors to correct their version constraints you can have Paket behave like NuGet when resolving [transitive dependencies](faq.html#transitive) (i.e. defaulting to lowest matching versions).

The strategy can be either `min` or `max` with max being the default.

    [lang=paket]
    strategy: min
    source https://nuget.org/api/v2

    nuget UnionArgParser ~> 0.7

A `min` strategy means you get the *lowest matching version* of your [transitive dependencies](faq.html#transitive) (i.e. NuGet-style). In contrast, a `max` strategy will get you the *highest matching version*.

Note, however, that all direct dependencies will still get their *latest matching versions*, no matter the value of the `strategy` option.
If you want to influence the resolution of direct dependencies then read about the [lowest_matching option](dependencies-file.html#Lowest_matching-option).

The only exception is when you are updating a single package and one of your direct dependencies is a [transitive dependency](faq.html#transitive) for that specific package. 
In this case, only the updating package will get its *latest matching version* and the dependency is treated as transitive.

To override a strategy for a single NuGet package, you can use the package specific [strategy modifiers](nuget-dependencies.html#Strategy-modifiers).

### Lowest_matching option

This option tells Paket what resolver strategy it should use for direct dependencies.

The `lowest_matching` option can be either `true` or `false` with `false` being the default.

    [lang=paket]
    lowest_matching: true
    source https://nuget.org/api/v2

    nuget UnionArgParser ~> 0.7

A `lowest_matching: true` setting means you get the *lowest matching version* of your direct dependencies. In contrast, a `lowest_matching:false` will get you the *highest matching version*.

Note, however, that all [transitive dependencies](faq.html#transitive) will still get their *latest matching versions*, no matter the value of the `lowest_matching` option.
If you want to influence the resolution of [transitive dependencies](faq.html#transitive) then read about the [strategy option](dependencies-file.html#Strategy-option).

To override a `lowest_matching` option for a single NuGet package, you can use the package specific [lowest_matching option](nuget-dependencies.html#Lowest_matching-option).

### Generate load scripts

This option tells Paket to generate include scripts which reference installed packages during package installation.

The `generate_load_scripts` option can be either `true` or `false` with `false` being the default.

    [lang=paket]
    generate_load_scripts: true
    source https://nuget.org/api/v2

    nuget Suave

Generated load scripts can be loaded like this:

    [lang=fsharp]
    #load @".paket/load/net45/suave.fsx"

## Comments

All lines starting with with `//` or `#` are considered comments.
