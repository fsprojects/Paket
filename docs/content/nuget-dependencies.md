# NuGet dependencies

Paket allows to reference [NuGet](http://www.nuget.org) packages in your application.

## Sources

Sources are defined by the `source "<address>"` statement.

Paket supports multiple sources per `paket.dependencies`. It's recommended to put all `source` statements at the top of `paket.dependencies`.

The [`paket.lock` file](lock-file.html) will reflect the sources selected by dependency resolution.

### NuGet feeds

Paket supports NuGet feeds like those provided by [nuget.org](http://www.nuget.org), [MyGet](https://www.myget.org/) or [TeamCity](http://www.jetbrains.com/teamcity/).

Please note that you need to specify all NuGet sources, including the default feed from [nuget.org](http://www.nuget.org). Paket does not take the current machine's NuGet Package Sources configuration (that you set up e.g. using Visual Studio) into account.

    [lang=paket]
    source https://nuget.org/api/v2     // nuget.org
    source http://myserver/nuget/api/v2 // custom feed

<div id="plaintext-credentials"></div>
It's also possible to provide login information for private NuGet feeds:

    [lang=paket]
    source http://myserver/nuget/api/v2 username: "my user" password: "my pw"

If you don't want to check your username and password into source control, you can use environment variables instead:

    [lang=paket]
    source http://myserver/nuget/api/v2 username: "%PRIVATE_FEED_USER%" password: "%PRIVATE_FEED_PASS%"

`%PRIVATE_FEED_USER%` and `%PRIVATE_FEED_PASS%` will be expanded with the contents of your `PRIVATE_FEED_USER` and `PRIVATE_FEED_PASS` environment variables.

The [`paket.lock` file](lock-file.html) will also reflect these settings.

* _ __Note__:_ In the case that a `paket.dependencies` file exists while running the `convert-from-nuget` command, the `PRIVATE_FEED_USER` and `PRIVATE_FEED_PASS`
will *not* be expanded. Please see [this document](convert-from-nuget-tutorial.html) for instructions on migrating credentials.

### Path sources

Paket also supports file paths such as local directories or references to UNC shares:

    [lang=paket]
    source C:\Nugets
    source ~/project/nugets

As well Paket supports the source directory to be specified relative to the `paket.dependencies` file:

    [lang=paket]
    source ext/nugets

## Dependencies

The dependencies of your project are defined by the `nuget <package ID> <version constraint>` statement.

Paket also supports file dependencies, such as [referencing files directly from GitHub repositories](http-dependencies.html).

<div id="package-id"></div>
### Package ID

The `package ID` parameter is the same as you find in NuGet's `packages.config` or on [nuget.org](http://www.nuget.org).

### Version constraints

One key feature of Paket is that it separates the definition of dependencies from the actual resolution. NuGet stores resolution information in `packages.config`, where both package IDs and their respective pinned version are combined.

With Paket you do not need to pin specific versions (although you can). Paket allows you to leverage [semantic versioning](http://semver.org) and define version constraints in a very flexible manner.

You can also [influence the resolution of transitive dependencies](#Paket-s-NuGet-style-dependency-resolution-for-transitive-dependencies).

#### Pinned version constraint

    [lang=paket]
    nuget Example = 1.2.3
    nuget Example 1.2.3   // same as above

    nuget Example 1.2.3-alpha001

This is the strictest version constraint. Use the `=` operator to specify an exact version match; the `=` is optional.

#### Omitting the version constraint

If you omit the version constraint then Paket will assume `>= 0`:

    [lang=paket]
    nuget Example

#### "Use exactly this version" constraint

<blockquote>We consider a situation with 3 packages A, B and C.

A and B are direct (or root level) dependencies, but both have a dependency on C.
A needs exactly C 1.0 and B wants version 1.1 of C.
This is a version conflict and Paket will complain during resolution phase.

If we specify C = 1.1 in the dependencies file then Paket still considers this as a version conflict with the version requirement in A.
By specifying C == 1.1 we overwrite all other requirements and Paket will not complain about the conflict anymore.</blockquote>

If your [transitive dependencies](faq.html#transitive) result in a version conflict you might want to instruct Paket to use a specific version. The `==` operator allows you to manually resolve the conflict:

    [lang=paket]
    nuget Example == 1.2.3 // take exactly this version

<blockquote>Important: If you want to restrict the version to a specific version then use the <a href="nuget-dependencies.html#Pinned-version-constraint">= operator</a>. The == operator should only be used if you need to overwrite a dependency resolution due to a conflict.</blockquote>

#### Further version constraints

    [lang=paket]
    nuget Example >= 1.2.3        // at least 1.2.3
    nuget Example > 1.2.3         // greater than 1.2.3
    nuget Example <= 1.2.3        // less than or equal to 1.2.3
    nuget Example < 1.2.3         // less than 1.2.3
    nuget Example >= 1.2.3 < 1.5  // at least 1.2.3 but less than 1.5

#### Pessimistic version constraint

    [lang=paket]
    nuget Example ~> 1.2.3

The `~>` "twiddle-wakka" operator is borrowed from [bundler](http://bundler.io/). It is used to specify a version range.

It translates to "use the minimum version specified, but allow upgrades up to, but not including, <version specified>, last part chopped off, last number incremented by 1". In other words, allow the last part stated explicitly to increase, but pin any of the elements before that.

Let us illustrate:

<table>
  <thead>
    <td>Constraint</td>
    <td>Version range</td>
  </thead>
  <tr>
    <td><pre>~> 0</pre></td>
    <td><pre>0 <= x < 1</pre></td>
  </tr>
  <tr>
    <td><pre>~> 1.0</pre></td>
    <td><pre>1.0 <= x < 2.0</pre></td>
  </tr>
  <tr>
    <td><pre>~> 1.2</pre></td>
    <td><pre>1.2 <= x < 2.0</pre></td>
  </tr>
  <tr>
    <td><pre>~> 1.2.3</pre></td>
    <td><pre>1.2.3 <= x < 1.3</pre></td>
  </tr>
  <tr>
    <td><pre>~> 1.2.3.4</pre></td>
    <td><pre>1.2.3.4 <= x < 1.2.4</pre></td>
  </tr>
  <tr>
    <td><pre>~> 1.2.3-alpha001</pre></td>
    <td><pre>1.2.3-alpha001 <= x < 1.3</pre></td>
  </tr>
<table>

#### Pessimistic version constraint with compound

If want to allow newer backward-compatible versions but also need a specific fix version within the allowed range, use a compound constraint.

    [lang=paket]
    nuget Example ~> 1.2 >= 1.2.3

The example above translates to `1.2.3 <= x < 2.0`.

### Prereleases

If you want to depend on prereleases then Paket can assist you. In contrast to NuGet, Paket allows you to depend on different prerelease channels:

    [lang=paket]
    nuget Example >= 1.2.3 alpha      // at least 1.2.3 including alpha versions
    nuget Example >= 2 beta rc        // at least 2.0 including rc and beta versions
    nuget Example >= 3 rc             // at least 3.0 but including rc versions
    nuget Example >= 3 prerelease     // at least 3.0 but including all prerelease versions

### Framework restrictions

Sometimes you don't want to generate dependencies for older framework versions. You can control this in the [`paket.dependencies` file](dependencies-file.html):

    [lang=paket]
    nuget Example >= 2.0 framework: net35, net40  // .NET 3.5 and .NET 4.0
    nuget Example >= 2.0 framework: >= net45      // .NET 4.5 and above

> Note: This feature is deprecated and can be seen as an expert feature. 
> Using framework restrictions on single packages might make you projects uncompilable.
> The recommended way is to globally (on a group) specifiy the frameworks you want to compile for.

This feature basically tells paket to only consider the specified frameworks for this package.
It means 

> Paket I use 'Example' only to compile against 'net35' and 'net40'.
> I never need this package to compile for another framework like 'net45'."

### Putting the version no. into the path

If you need to be NuGet compatible and want to have the version no. in the package path you can do the following:

    [lang=paket]
    source https://nuget.org/api/v2

    nuget xunit.runner.visualstudio >= 2.0 version_in_path: true
    nuget UnionArgParser ~> 0.7

### No content option

This option disables the installation of any content files:

    [lang=paket]
    source https://nuget.org/api/v2

    nuget jQuery content: none // we don't install jQuery content files
    nuget Fody   content: once // install content files but don't overwrite
    nuget ServiceStack.Swagger content: true // install content and always override
    nuget UnionArgParser ~> 0.7

The default is `content: true`.

### copy_local settings

It's possible to influence the `Private` property for references in project files:

    [lang=paket]
    source https://nuget.org/api/v2

    nuget Newtonsoft.Json copy_local: false

### specific_version settings

It's possible to influence the `SpecificVersion` property for references in project files:

    [lang=paket]
    source https://nuget.org/api/v2

    nuget Newtonsoft.Json specific_version: false

### CopyToOutputDirectory settings

It's possible to influence the `CopyToOutputDirectory` property for content references in project files:

    [lang=paket]
    source https://nuget.org/api/v2

    nuget jQuery copy_content_to_output_dir: always
	nuget Fody copy_content_to_output_dir: never
	nuget ServiceStack.Swagger copy_content_to_output_dir: preserve-newest

### redirects settings

You can instruct Paket to create assembly binding redirects for NuGet packages:

    [lang=paket]
    source https://nuget.org/api/v2

    nuget FSharp.Core redirects: on

Redirects are created only if they are required. However, you can instruct Paket to create it regardless:

    [lang=paket]
    source https://nuget.org/api/v2

    nuget FSharp.Core redirects: force

In contrast, you have the option to force Paket to not create a redirect:

    [lang=paket]
    source https://nuget.org/api/v2

    nuget FSharp.Core redirects: off

You can also override the redirects settings per project, from its [references file](references-files.html#Redirects-settings).

### import_targets settings

If you don't want to import `.targets` and `.props` files you can disable it via the `import_targets` switch:

    [lang=paket]
    source https://nuget.org/api/v2

    nuget Microsoft.Bcl.Build import_targets: false // we don't import .targets and .props
    nuget UnionArgParser ~> 0.7

### Strategy modifiers

To override the [strategy option](dependencies-file.html#Strategy-option) you can use one of the strategy modifiers.

Note, however, that all direct dependencies will still get their *latest matching versions*, no matter the value of the `strategy` option.
If you want to influence the resolution of direct dependencies then read about the [lowest_matching option](dependencies-file.html#Lowest_matching-option).

#### Max modifier

To request Paket to override the resolver strategy for the [transitive dependencies](faq.html#transitive) of a package, use the `strategy:max` setting:

    [lang=paket]
    strategy: min
    source https://nuget.org/api/v2

    nuget Example ~> 1.2 strategy: max

This effectively will get you the *latest matching versions* of `Example`'s dependencies.
The following code is doing the same by using the `@` operator in your version constraint:

    [lang=paket]
    strategy: min
    source https://nuget.org/api/v2

    nuget Example @~> 1.2 // use "max" version resolution strategy

#### Min modifier

To request Paket to override the resolver strategy for the [transitive dependencies](faq.html#transitive) of a package, use the `strategy:min` setting:

    [lang=paket]
    source https://nuget.org/api/v2

    nuget Example ~> 1.2 strategy: min

This effectively will get you the *lowest matching versions* of `Example`'s dependencies. Still, you will get the *latest matching version* of `Example` itself according to its [version constraint of `1.2 <= x < 2`](#Pessimistic-version-constraint).
The following code is doing the same by using the `!` operator in your version constraint:


    [lang=paket]
    source https://nuget.org/api/v2

    nuget Example !~> 1.2 // use "min" version resolution strategy

The strategy setting and the corresponding `!` and `@` modifiers are applicable to all [version constraints](#Version-constraints):

    [lang=paket]
    source https://nuget.org/api/v2

    nuget Example-A @> 0 // use "max" version resolution strategy
    nuget Example-B != 1.2  // use "min" version resolution strategy
    nuget Example-C !>= 1.2 // use "min" version resolution strategy
	nuget Example-C >= 1.2 strategy min

### Lowest_matching option

To override the [lowest_matching option](dependencies-file.html#Lowest_matching-option) you can use one of the following modifiers.

Note, however, that all [transitive dependencies](faq.html#transitive) will still get their *latest matching versions*, no matter the value of the `lowest_matching` option.
If you want to influence the resolution of [transitive dependencies](faq.html#transitive) then read about the [strategy option](dependencies-file.html#Strategy-option).

To request Paket to override the resolver strategy for a package, use the `lowest_matching:true` setting:

    [lang=paket]
    source https://nuget.org/api/v2

    nuget Example ~> 1.2 lowest_matching: true

### Specifying multiple targeting options

It is possible to apply more than one of the options above to a particular package.  To do so, simply separate them by commas, like so:

    [lang=paket]
    source https://nuget.org/api/v2

    nuget ClearScript.Installer import_targets: false, content: none // we don't import .targets and .props, and also don't add any content
