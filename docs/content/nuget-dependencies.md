# NuGet dependencies

Paket allows to reference [NuGet](http://www.nuget.org) packages in your application.

## Sources

Sources are defined by the `source "<address>"` statement.

Paket supports multiple sources per `paket.dependencies`. It's recommended to put all `source` statements at the top of `paket.dependencies`. The ordering of sources is important, since the first `source` a [dependency](#package-id) is found in will be the one used for download.

The [`paket.lock` file](lock-file.html) will reflect the sources selected by dependency resolution.

### NuGet feeds

Paket supports NuGet feeds like those provided by [nuget.org](http://www.nuget.org) or [TeamCity](http://www.jetbrains.com/teamcity/).

Please note that you need to specify all NuGet sources, including the default feed from [nuget.org](http://www.nuget.org). Paket does not take the current machine's NuGet Package Sources configuration (that you set up e.g. using Visual Studio) into account.

    source http://nuget.org/api/v2      // nuget.org
    source http://myserver/nuget/api/v2 // custom feed
    
It's also possible to provide login information for private NuGet feeds:

    source http://myserver/nuget/api/v2 username: "my user" password: "my pw"

The [paket.lock](lock-file.html) will also reflect these settings.

### Path sources

Paket also supports file paths such as local directories or references to UNC shares:

    source C:\Nugets
    source ~/project/nugets
    source \\server\nugets

<div id="strict-mode"></div>
### Strict references

Paket usually references all direct and indirect dependencies that are listed in your [paket.references](references-files.md) files to your project file.
In `strict` mode it will **only** reference *direct* dependencies.

    references strict
    source http://nuget.org/api/v2

    nuget Newtonsoft.Json ~> 6.0
    nuget UnionArgParser ~> 0.7

<div id="dependencies"></div>
## Dependencies

The dependencies of your project are defined by the `nuget "<package ID>" "<version constraint>"` statement.

Paket also supports file dependencies, such as [referencing files directly from GitHub repositories](github-dependencies.html).

<div id="package-id"></div>
### Package ID

The `package ID` parameter is the same as you find in NuGet's `packages.config` or on [nuget.org](http://www.nuget.org).

<div id="version-constraints"></div>
### Version constraints

One key feature of Paket is that it separates the definition of dependencies from the actual resolution. NuGet stores resolution information in `packages.config`, where both package IDs and their respective pinned version are combined.

With Paket you do not need to pin specific versions (although you can). Paket allows you to leverage [semantic versioning](http://semver.org) and define version constraints in a very flexible manner.

You can also [influence the resolution of indirect dependencies](#Paket-s-NuGet-style-dependency-resolution-for-indirect-dependencies).

#### Pinned version constraint

    nuget Example = 1.2.3
    nuget Example 1.2.3   // same as above

    nuget Example 1.2.3-alpha001

This is the strictest version constraint. Use the `=` operator to specify an exact version match; the `=` is optional.

#### Omitting the version constraint

    nuget Example

If you omit the version constraint then Paket will assume `>= 0`.

#### Further version constraints

    nuget Example >= 1.2.3        // at least 1.2.3
    nuget Example > 1.2.3         // greater than 1.2.3
    nuget Example <= 1.2.3        // at least 1.2.3
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

### Controlling dependency resolution

#### A word on NuGet

The NuGet dependency resolution strategy was a major motivation for us to develop Paket in the first place.

NuGet made the decision very early that if package `A` depends on package `B`, you will always get the *lowest matching version* of package `B`, regardless of available newer versions. One side effect of this decision is that after you install `A` (and `B`), you will  be able to immediately update `B` to a newer version as long as it satisfies the version constraint defined by `A`.

Installing packages like this is almost always a two-step operation: install and then try to update. We found it very hard to keep our project's dependencies at the latest version, that's why we chose to handle things differently.

#### How Paket works by default

Paket uses the definitions from `paket.dependencies` to compute the dependency graph.

The resolution algorithm balances direct and indirect dependencies such that you will get the *latest matching versions* of direct dependencies (defined using the [`nuget` statement](#dependencies)) and indirect dependencies (defined by your direct dependency's [nuspec](http://docs.nuget.org/docs/reference/nuspec-reference)). Paket checks compatibility by comparing available versions to the constraints of either source, `paket.dependencies`, or [nuspec](http://docs.nuget.org/docs/reference/nuspec-reference).

As stated above, the algorithm defaults to resolving the latest versions matching the constraints.

As long as everybody follows [SemVer](http://semver.org) and you define sane version constraints (e.g. within a major version) for your direct dependencies the system is very likely to work well.

While developing Paket we found that many packages available on [NuGet](http://www.nuget.org) today (September 2014) don't follow [SemVer](http://semver.org) very well with regard to specifying their own dependencies.

#### A real-world example

For example, an assembly inside a NuGet package `A` might have a reference to a strong-named assembly that is pulled from another NuGet package `B`. Despite strong-naming the `B` assembly, `A` still specifies an open version constraint (`> <version of B that was compiled against>`).

This might be due to the fact that the [nuspec file format](http://docs.nuget.org/docs/reference/nuspec-reference) requires you to pin the dependency version using double brackets: `<dependency id="B" version="[1.2.3]" />`. Even the authors of Paket made the mistake of omitting the brackets, effectively specifying `> 1.2.3`. Newer releases of `B` package might still work together with `A` using [assembly binding redirects](http://msdn.microsoft.com/en-us/library/7wd6ex19(v=vs.110).aspx), a feature of .NET that the authors of Paket are not very fond of. Even if you are OK with binding redirects, what would happen after `B` `2.0` is released? If you assume that `B` follows [SemVer](http://semver.org), the `2.0` version, by definition, *will* have breaking changes. NuGet will allow the update regardless, giving the false impression that your app still works.

<div id="nuget-style-dependency-resolution"></div>
#### Paket's NuGet-style dependency resolution for indirect dependencies

To make your transition to Paket easier and to allow package authors to correct their version constraints you can have Paket behave like NuGet when resolving indirect dependencies (i.e. defaulting to lowest matching versions).

To request that Paket applies NuGet-style dependency resolution for indirect dependencies, use the `!` operator in your version constraint.

    source http://nuget.org/api/v2

    nuget Example !~> 1.2 // use "min" version resolution strategy

This effectively will get you the *lowest matching versions* of `Example`'s dependencies. Still, you will get the *latest matching version* of `Example` itself according to its [version constraint of `1.2 <= x < 2`](#Pessimistic-version-constraint).

The `!` modifier is applicable to all [version constraints](#version-constraints):

    source http://nuget.org/api/v2

    nuget Example-B != 1.2  // use "min" version resolution strategy
    nuget Example-C !>= 1.2 // use "min" version resolution strategy
