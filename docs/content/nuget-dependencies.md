# NuGet dependencies

Paket allows to reference [NuGet](http://www.nuget.org) packages in your application.

## Sources

Sources are defined by the `source "<address>"` statement.

Paket supports multiple sources per `paket.dependencies`. It's recommended to put all `source` statements at the top of `paket.dependencies`.

The [`paket.lock` file](lock-file.html) will reflect the sources selected by dependency resolution.

### NuGet feeds

Paket supports NuGet feeds like those provided by [nuget.org](http://www.nuget.org), [MyGet](https://www.myget.org/) or [TeamCity](http://www.jetbrains.com/teamcity/).

Please note that you need to specify all NuGet sources, including the default feed from [nuget.org](http://www.nuget.org). Paket does not take the current machine's NuGet Package Sources configuration (that you set up e.g. using Visual Studio) into account.

    source https://nuget.org/api/v2     // nuget.org
    source http://myserver/nuget/api/v2 // custom feed

<div id="plaintext-credentials"></div>
It's also possible to provide login information for private NuGet feeds:

    source http://myserver/nuget/api/v2 username: "my user" password: "my pw"

If you don't want to check your username and password into source control, you can use environment variables instead:

    source http://myserver/nuget/api/v2 username: "%PRIVATE_FEED_USER%" password: "%PRIVATE_FEED_PASS%"

`%PRIVATE_FEED_USER%` and `%PRIVATE_FEED_PASS%` will be expanded with the contents of your `PRIVATE_FEED_USER` and `PRIVATE_FEED_PASS` environment variables.

The [`paket.lock` file](lock-file.html) will also reflect these settings.

### Path sources

Paket also supports file paths such as local directories or references to UNC shares:

    source C:\Nugets
    source ~/project/nugets

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

    nuget Example = 1.2.3
    nuget Example 1.2.3   // same as above

    nuget Example 1.2.3-alpha001

This is the strictest version constraint. Use the `=` operator to specify an exact version match; the `=` is optional.

#### Omitting the version constraint

If you omit the version constraint then Paket will assume `>= 0`:

    nuget Example

#### "Use exactly this version" constraint

If your transitive dependencies result in a version conflict you might want to instruct Paket to use a specific version. The `==` operator allows you to manually resolve the conflict:

    nuget Example == 1.2.3 // take exactly this version

#### Further version constraints

    nuget Example >= 1.2.3        // at least 1.2.3
    nuget Example > 1.2.3         // greater than 1.2.3
    nuget Example <= 1.2.3        // less than or equal to 1.2.3
    nuget Example < 1.2.3         // less than 1.2.3
    nuget Example >= 1.2.3 < 1.5  // at least 1.2.3 but less than 1.5

#### Pessimistic version constraint

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

    nuget Example ~> 1.2 >= 1.2.3

The example above translates to `1.2.3 <= x < 2.0`.

### PreReleases

If you want to dependend on prereleases then Paket can assist you. In contrast to NuGet, Paket allows you to depend on different prerelease channels:

    nuget Example >= 1.2.3 alpha      // at least 1.2.3 including alpha versions
    nuget Example >= 2 beta rc        // at least 2.0 including rc and beta versions
    nuget Example >= 3 rc             // at least 3.0 but including rc versions
    nuget Example >= 3 prerelease     // at least 3.0 but including all prerelease versions

### Framework restrictions

Sometimes you don't want to generate dependencies for older framework versions. You can control this in the [`paket.dependencies` file](dependencies-file.html):

    nuget Example >= 2.0 framework: net35, net40  // .NET 3.5 and .NET 4.0
    nuget Example >= 2.0 framework: >= net45      // .NET 4.5 and above

### No content option

This option disables the installation of any content files:
    
    source https://nuget.org/api/v2

    nuget jQuery >= 0 content: none // we don't install jQuery content files
    nuget UnionArgParser ~> 0.7

### copy_local settings

It's possible to influence the `Private` property for references in project files:

    source https://nuget.org/api/v2

    nuget Newtonsoft.Json copy_local: false

### import_targets settings

If you don't want to import `.targets` and `.props` files you can disable it via the `import_targets` switch:

    source https://nuget.org/api/v2

    nuget Microsoft.Bcl.Build import_targets: false // we don't import .targets and .props
    nuget UnionArgParser ~> 0.7

### Paket's NuGet-style dependency resolution for transitive dependencies

NuGet's dependency syntax led to a lot of incompatible packages on Nuget.org ([read more](controlling-nuget-resolution.html)). To make your transition to Paket easier and to allow package authors to correct their version constraints you can have Paket behave like NuGet when resolving transitive dependencies (i.e. defaulting to lowest matching versions).

To request that Paket applies NuGet-style dependency resolution for transitive dependencies, use the `!` operator in your version constraint.

    source https://nuget.org/api/v2

    nuget Example !~> 1.2 // use "min" version resolution strategy

This effectively will get you the *lowest matching versions* of `Example`'s dependencies. Still, you will get the *latest matching version* of `Example` itself according to its [version constraint of `1.2 <= x < 2`](#Pessimistic-version-constraint).

The `!` modifier is applicable to all [version constraints](#Version-constraints):

    source https://nuget.org/api/v2

    nuget Example-B != 1.2  // use "min" version resolution strategy
    nuget Example-C !>= 1.2 // use "min" version resolution strategy
