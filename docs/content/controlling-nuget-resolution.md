# Controlling NuGet dependency resolution

## A word on NuGet

The NuGet dependency resolution strategy was a major motivation for us to
develop Paket.

NuGet made the decision very early that if package `A` depends on package `B`,
you will always get the *lowest matching version* of package `B`, regardless of
available newer versions. One side effect of this decision is that after you
install `A` (and `B`), you will  be able to immediately update `B` to a newer
version as long as it satisfies the version constraint defined by `A`.

Installing packages like this is almost always a two-step operation: install and
then try to update. We found it very hard to keep our project's dependencies at
the latest version, that's why we chose to handle things differently.

## How Paket works by default

Paket uses the definitions from `paket.dependencies` to compute the dependency
graph.

The resolution algorithm balances direct and
[transitive dependencies](faq.html#transitive) such that you will get the
*latest matching versions* of direct dependencies (defined using the
[`nuget` statement](#dependencies)) and transitive dependencies (defined by your
direct dependency's
[nuspec](http://docs.nuget.org/docs/reference/nuspec-reference)). Paket checks
compatibility by comparing available versions to the constraints of either
source, `paket.dependencies`, or
[nuspec](http://docs.nuget.org/docs/reference/nuspec-reference).

As stated above, the algorithm defaults to resolving the latest versions
matching the constraints.

As long as everybody follows [semantic versioning](http://semver.org) and you
define sane version constraints (e.g. within a major version) for your direct
dependencies the system is very likely to work well.

While developing Paket we found that many packages available on
[nuget.org](http://www.nuget.org) (as of September 2014) don't follow
[semantic versioning](http://semver.org) very well with regard to specifying
their own dependencies.

## A real-world example

For example, an assembly inside a NuGet package `A` might have a reference to a
strong-named assembly that is pulled from another NuGet package `B`. Despite
strong-naming the `B` assembly, `A` still specifies an open version constraint
(`>=`).

This might be due to the fact that the [nuspec file
format](http://docs.nuget.org/docs/reference/nuspec-reference) requires you to
pin the dependency version using double brackets: `<dependency id="B"
version="[1.2.3]" />`. Many package authors made the mistake of omitting the
brackets, effectively specifying `>= 1.2.3` when they wanted to specify `=
1.2.3`. Newer releases of `B` package might still work together with `A` using
[assembly binding redirects](http://msdn.microsoft.com/en-us/library/7wd6ex19(v=vs.110).aspx),
a feature of .NET that the authors of Paket are not very fond of. Even if you
are OK with binding redirects, what would happen after `B` `2.0` is released? If
you assume that `B` follows [semantic versioning](http://semver.org), the `2.0`
version, by definition, **will** have breaking changes. NuGet will allow the
update regardless, giving the false impression that your app still works.

To make your transition to Paket easier and to allow package authors to correct
their version constraints you can have Paket behave like NuGet by using the
[`!` prefix](nuget-dependencies.html#Paket-s-NuGet-style-dependency-resolution-for-transitive-dependencies).
