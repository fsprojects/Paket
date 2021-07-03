# NuGet dependencies

Paket allows to reference [NuGet](http://www.nuget.org) packages in your
application.

## Sources

Sources are defined by the `source <address>` statement.

Paket supports multiple sources in one
[`paket.dependencies`](dependencies-file.html). It's recommended to put all
`source` statements at the top of
[`paket.dependencies`](dependencies-file.html).

The [`paket.lock` file](lock-file.html) will reflect the sources selected by
dependency resolution.

### NuGet feeds

Paket supports NuGet feeds like those provided by
[nuget.org](http://www.nuget.org), [MyGet](https://www.myget.org/) or
[TeamCity](http://www.jetbrains.com/teamcity/).

Please note that you need to specify all NuGet sources, including the default
feed from [nuget.org](http://www.nuget.org). Paket does not take the current
machine's NuGet Package Sources configuration (that you set up e.g. using Visual
Studio) into account.

```paket
source https://nuget.org/api/v2     // nuget.org
source http://myserver/nuget/api/v2 // Custom feed.
```

<div id="plaintext-credentials"></div>

It's also possible to provide login information for private NuGet feeds:

```paket
source http://example.com/nuget/api/v2 username: "user name" password: "the password" authtype: "basic"
```

If you don't want to check your username and password into source control, you
can use environment variables instead:

```paket
source http://myserver/nuget/api/v2 username: "%PRIVATE_FEED_USER%" password: "%PRIVATE_FEED_PASS%" authtype: "ntlm"
```

`%PRIVATE_FEED_USER%` and `%PRIVATE_FEED_PASS%` will be expanded with the
contents of your `PRIVATE_FEED_USER` and `PRIVATE_FEED_PASS` environment
variables.

The [`paket.lock` file](lock-file.html) will also reflect these settings.

`authtype` is an optional parameter to specify the authentication scheme. Allowed
values are `basic` and `ntlm`. If no authentication type is specified, basic
authentication will be used.

**Note:** If [`paket.dependencies` file](dependencies-file.html) exists while
running the [`convert-from-nuget` command](paket-convert-from-nuget.html), the
`PRIVATE_FEED_USER` and `PRIVATE_FEED_PASS` will *not* be expanded. Please see
[this document](convert-from-nuget-tutorial.html) for instructions on
migrating credentials.

### Path sources

Paket also supports file paths such as local directories or references to UNC
shares:

```paket
source C:\nugets
source ~/project/nugets
source \\server\share
```

Paket supports the source directory to be specified relative to the
`paket.dependencies` file as well:

```paket
source directory/relative/to/paket.dependencies
source .    // To use a package in the root of the repository
```

## Dependencies

The dependencies of your project are defined by the `nuget <package ID> <version
constraint>` statement.

Paket also supports file dependencies, such as
[referencing files directly from GitHub repositories](http-dependencies.html).

<div id="package-id"></div>

### Package ID

The `package ID` parameter is the same as you find in NuGet's `packages.config`
or on [nuget.org](http://www.nuget.org).

### Version constraints

One key feature of Paket is that it separates the definition of dependencies
from the actual resolution. NuGet stores resolution information in
`packages.config`, where both package IDs and their respective pinned version
are combined.

With Paket you do not need to pin specific versions (although you can). Paket
allows you to leverage [semantic versioning](http://semver.org) and define
version constraints in a very flexible manner.

You can also
[influence the resolution of transitive dependencies](#Strategy-modifiers).

#### Pinned version constraint

```paket
nuget Example = 1.2.3
nuget Example 1.2.3   // Same as above.

nuget Example 1.2.3-alpha001
```

This is the strictest version constraint. Use the `=` operator to specify an
exact version match; the `=` is optional.

#### Omitting the version constraint

If you omit the version constraint then Paket will assume `>= 0`:

```paket
nuget Example
```

#### "Use exactly this version" constraint

> We consider a situation with 3 packages `A`, `B` and `C`.
>
> `A` and `B` are direct (or root-level) dependencies, but both have a
> dependency on `C`. `A` needs exactly `C 1.0` and `B` wants version `C 1.1`.
> This is a version conflict and Paket will complain during resolution phase.
>
> If we specify `C = 1.1` in the
> [`paket.dependencies` file](dependencies-file.html) then Paket still considers
> this as a version conflict with the version requirement in `A`. By specifying
> `C == 1.1` we overwrite all other requirements and Paket will not complain
> about the conflict anymore.

If your [transitive dependencies](faq.html#transitive) result in a version
conflict you might want to instruct Paket to use a specific version. The `==`
operator allows you to manually resolve the conflict:

```paket
nuget Example == 1.2.3 // Take exactly this version.
```

**Note:** If you want to restrict the version to a specific version then
use the [`= operator`](#Pinned-version-constraint). The
`==` operator should only be used if you need to overwrite a dependency
resolution due to a conflict.

#### Further version constraints

```paket
nuget Example >= 1.2.3       // At least 1.2.3
nuget Example > 1.2.3        // Greater than 1.2.3
nuget Example <= 1.2.3       // Less than or equal to 1.2.3
nuget Example < 1.2.3        // Less than 1.2.3
nuget Example >= 1.2.3 < 1.5 // At least 1.2.3 but less than 1.5
```

#### Pessimistic version constraint

```paket
nuget Example ~> 1.2.3
```

The `~>` "twiddle-wakka" operator is borrowed from
[bundler](http://bundler.io/). It is used to specify a version range.

It translates to:

> Use the minimum version specified, but allow upgrades up to,
> but not including, `<version specified>`, last part chopped off, last number
> incremented by 1.

In other words, allow the last part stated explicitly to increase, but pin any
of the elements before that.

Let us illustrate:

| Constraint          | Version range               |
| :------------------ | :---------------------------|
| `~> 0`              | `0 <= x < 1`                |
| `~> 1.0`            | `1.0 <= x < 2.0`            |
| `~> 1.2`            | `1.2 <= x < 2.0`            |
| `~> 1.2.3`          | `1.2.3 <= x < 1.3`          |
| `~> 1.2.3.4`        | `1.2.3.4 <= x < 1.2.4`      |
| `~> 1.2.3-alpha001` | `1.2.3-alpha001 <= x < 1.3` |

#### Pessimistic version constraint with compound

If want to allow newer backward-compatible versions but also need a specific fix
version within the allowed range, use a compound constraint.

```paket
nuget Example ~> 1.2 >= 1.2.3
```

The example above translates to `1.2.3 <= x < 2.0`.

### Prereleases

If you want to depend on prereleases then Paket can assist you. In contrast to
NuGet, Paket allows you to depend on different prerelease channels:

```paket
nuget Example >= 1.2.3 alpha  // At least 1.2.3 including alpha versions.
nuget Example >= 2 beta rc    // At least 2.0 including rc and beta versions.
nuget Example >= 3 rc         // At least 3.0 including rc versions.
nuget Example >= 3 prerelease // At least 3.0 including all prerelease versions.
```

### Framework restrictions

Sometimes you do not want to generate dependencies for other .NET Framework
versions than the ones your projects use. You can control this in the
[`paket.dependencies` file](dependencies-file.html).

The `framework` modifier tells Paket to only consider the specified frameworks
for a package:

```paket
nuget Example >= 2.0 framework: net35, net40 // .NET 3.5 and .NET 4.0.
```

It translates to:

> I use `Example` only to compile against `net35` and `net40`. I never compile
> projects against another framework like `net45`.

Another example restricts to frameworks greater or equal than:

```paket
nuget Example >= 2.0 framework: >= net45     // .NET 4.5 and above.
```

**Note:** This feature is deprecated and can be seen as an expert feature. Using
framework restrictions on a single package might make you projects uncompilable.
It is recommended to specify the frameworks you want to compile for globally or
for a [group](groups.html)).

### Putting the version number in the path

If you need to be NuGet-compatible and want to have the package version number
in the package path below `packages` you can do the following:

```paket
source https://nuget.org/api/v2

nuget xunit.runner.visualstudio >= 2.0 version_in_path: true
```

You need this if you are using custom test runners in Visual Studio, 
like `xunit.runners.visualstudio` or `MSTest.TestAdapter`.
The [Visual Studio Test Runner caches the 
packages](https://stackoverflow.com/questions/35103781/why-is-the-visual-studio-2015-2017-test-runner-not-discovering-my-xunit-v2-tests)
in `%TEMP%\VisualStudioTestExplorerExtensions`, 
and depends on the directory names being unique per version, and the presence of a packages.config in the test project.
Adding the `version_in_path` flag makes your unit tests appear in the Visual Studio Test Explorer again.


### Controlling whether content files should be copied to the project

The `content` modifier controls the installation of any content files:

```paket
source https://nuget.org/api/v2

nuget jQuery content: none               // Do not install jQuery content files.
nuget Fody   content: once               // Install content files but do not overwrite.
nuget ServiceStack.Swagger content: true // Install content and always overwrite.
```

The default is `content: true`. `content: false` is equivalent to setting `ExcludeAssets=contentFiles` for a `PackageReference` in NuGet.

### Controlling whether content files should be copied to the output directory during build

It's possible to influence the
[`CopyToOutputDirectory` property](https://msdn.microsoft.com/en-us/library/bb629388.aspx#Anchor_0)
for content references via the `copy_content_to_output_dir` modifier:

```paket
source https://nuget.org/api/v2

nuget jQuery copy_content_to_output_dir: always
nuget Fody copy_content_to_output_dir: never
nuget ServiceStack.Swagger copy_content_to_output_dir: preserve_newest
```

### Controlling whether assemblies should be copied to the output directory during build

It's possible to influence the
[`Private` property](https://msdn.microsoft.com/en-us/library/bb629388.aspx#Anchor_0)
for references via the `copy_local` modifier:

```paket
source https://nuget.org/api/v2

nuget Newtonsoft.Json copy_local: false
```

The default is `copy_local: true`. `copy_local: false` is equivalent to setting `ExcludeAssets=runtime` for a `PackageReference` in NuGet.

### Importing `*.targets` and `*.props` files

If you don't want to import `*.targets` and `*.props` files from packages, you
can disable it via the `import_targets` modifier:

```paket
source https://nuget.org/api/v2

nuget Microsoft.Bcl.Build import_targets: false // Do not import *.targets and *.props.
```

`import_targets: false` is equivalent to setting `ExcludeAssets=build;buildMultitargeting;buildTransitive` for a `PackageReference` in NuGet.

### License download

If you want paket to download licenses automatically you can use the `license_download` modifier. It is disabled by default.

```paket
source https://nuget.org/api/v2

nuget suave license_download: true
```

### Controlling assembly binding redirects

You can instruct Paket to create
[Assembly Binding Redirects](https://msdn.microsoft.com/en-us/library/433ysdt1(v=vs.110).aspx)
for NuGet packages:

```paket
source https://nuget.org/api/v2

nuget FSharp.Core redirects: on
```

Redirects are created only if they are required. However, you can instruct Paket
to create them regardless:

```paket
source https://nuget.org/api/v2

nuget FSharp.Core redirects: force
```

In contrast, you may force Paket to not create a redirect:

```paket
source https://nuget.org/api/v2

nuget FSharp.Core redirects: off
```

You can also override the redirects settings per project, from its
[`paket.references` file](references-files.html#Redirects-settings).

### Referencing specific versions in projects

It's possible to influence the
[`SpecificVersion` property](https://msdn.microsoft.com/en-us/library/bb629388.aspx#Anchor_0)
for references via the `specific_version` modifier:

```paket
source https://nuget.org/api/v2

nuget Newtonsoft.Json specific_version: false
```

### Strategy modifiers

To override the
[`strategy` option](dependencies-file.html#Resolver-strategy-for-transitive-dependencies)
you can use one of the `strategy` modifiers below.

Note, however, that all direct dependencies will still get their *latest
matching versions*, no matter the value of the `strategy` option. If you want to
influence the resolution of direct dependencies then read about the
[`lowest_matching` option](dependencies-file.html#Resolver-strategy-for-direct-dependencies).

#### The `max` modifier

To override the resolver strategy for the
[transitive dependencies](faq.html#transitive) of a package, use the
`strategy: max` setting:

```paket
strategy: min
source https://nuget.org/api/v2

nuget Example ~> 1.2 strategy: max
```

This effectively will get you the *latest matching versions* of `Example`'s
dependencies.

The following code is doing the same by using the `@` operator in
your version constraint:

```paket
strategy: min
source https://nuget.org/api/v2

nuget Example @~> 1.2 // Use "max" version resolution strategy.
```

#### The `min` modifier

To override the resolver strategy for the
[transitive dependencies](faq.html#transitive) of a package, use the
`strategy: min` modifier:

```paket
source https://nuget.org/api/v2

nuget Example ~> 1.2 strategy: min
```

This effectively will get you the *lowest matching versions* of `Example`'s
dependencies. Still, you will get the *latest matching version* of `Example`
itself according to its
[version constraint of `1.2 <= x <2`](#Pessimistic-version-constraint).

The following code is doing the same by using the `!` modifier in your version
constraint:

```paket
source https://nuget.org/api/v2

nuget Example !~> 1.2 // use "min" version resolution strategy
```

The strategy modifier and the corresponding `!` and `@` modifiers are applicable
to all [version constraints](#Version-constraints):

```paket
source https://nuget.org/api/v2

nuget Example-A @> 0    // Use "max" version resolution strategy.
nuget Example-B != 1.2  // Use "min" version resolution strategy.
nuget Example-C !>= 1.2 // Use "min" version resolution strategy.
nuget Example-C >= 1.2 strategy: min
```

### Resolver strategy for direct dependencies

To override the
[`lowest_matching` option](dependencies-file.html#Resolver-strategy-for-direct-dependencies)
you can use the following modifier.

Note, however, that all [transitive dependencies](faq.html#transitive) will
still get their *latest matching versions*, no matter the value of the
`lowest_matching` modifier. If you want to influence the resolution of [transitive
dependencies](faq.html#transitive) then read about the [`strategy`
option](dependencies-file.html##Resolver-strategy-for-transitive-dependencies).

To request Paket to override the resolver strategy for a package, use the
`lowest_matching: true` modifier:

```paket
source https://nuget.org/api/v2

nuget Example ~> 1.2 lowest_matching: true
```

### Combine multiple modifiers

It is possible to apply more than one of the modifiers above to a particular
package. To do so, simply separate them by commas:

```paket
source https://nuget.org/api/v2

// Do not import .targets and .props and also do not add any content.
nuget ClearScript.Installer import_targets: false, content: none
```

### Special case: `dotnet` CLI tools

```paket
clitool dotnet-fable 1.17
nuget Fable.Core
```

Command line (CLI) tools that hook into the `dotnet` CLI are special NuGet
packages. These packages are added with the keyword `clitool` instead of
`nuget`. This will exclude the tool and all its dependencies from getting
referenced as a dependency for any project.
