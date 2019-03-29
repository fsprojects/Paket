# The paket.dependencies file

The `paket.dependencies` file is used to specify rules regarding your
application's dependencies. It contains top level dependencies from all projects
in the solution, while [`paket.references` file](references-files.html)
specifies dependencies for particular project.

To give you an overview, consider the following `paket.dependencies` file:

```paket
source https://api.nuget.org/v3/index.json

// NuGet packages
nuget NUnit ~> 2.6.3
nuget FAKE ~> 3.4
nuget DotNetZip >= 1.9

// Files from GitHub repositories.
github forki/FsUnit FsUnit.fs

// Gist files.
gist Thorium/1972349 timestamp.fs

// HTTP resources.
http http://www.fssnip.net/1n decrypt.fs
```

The file specifies that Paket's NuGet dependencies should be downloaded from
[nuget.org](https://www.nuget.org) and that projects require:

* [NUnit](http://www.nunit.org/) version
  [2.6.3 <= x < 2.7](nuget-dependencies.html#Pessimistic-version-constraint)
* [FAKE](http://fsharp.github.io/FAKE/) version
  [3.4 <= x < 4.0](nuget-dependencies.html#Pessimistic-version-constraint)
* [DotNetZip](http://dotnetzip.codeplex.com/) with a version that is at
  [least 1.9](nuget-dependencies.html#Greater-than-or-equal-version-constraint)
* [FSUnit.fs](https://github.com/forki/FsUnit) from GitHub
* [GitHub Gist](https://gist.github.com) number
  [1972349](https://gist.github.com/Thorium/1972349)
* External HTTP resource, i.e. [1n](http://www.fssnip.net/1n) from [FSSnip](http://www.fssnip.net/)

Paket uses this definition to compute a concrete dependency resolution, which
also includes [transitive dependencies](faq.html#transitive). The resulting
dependency graph is then persisted to the [`paket.lock` file](lock-file.html).

Only direct dependencies should be listed; you can use the
[`paket simplify` command](paket-simplify.html) to remove [transitive
dependencies](faq.html#transitive).

## Sources

Paket supports the following source types:

* [NuGet](nuget-dependencies.html)
* [.NET CLI Tools](nuget-dependencies.html#Special-case-CLI-tools)
* [Git](git-dependencies.html)
* [GitHub and Gist](github-dependencies.html)
* [HTTP](http-dependencies.html) (any single file from any site without version
  control)

## Global options

### Required Paket version

It is possible to require a specific Paket version for a
[`paket.dependencies` file](dependencies-file.html). This can be achieved by a
line which starts with `version` followed by a requested `paket.exe` version and
optionally [bootstrapper command line](bootstrapper.html) arguments:

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

Paket usually adds all direct and [transitive dependencies](faq.html#transitive)
that are listed in [`paket.references` files](references-files.html) to project
files next to the respective [`paket.references` file](references-files.html).
In `strict` mode it will **only add** *direct* dependencies and you need to
add transitive dependencies yourself to
[`paket.references`](references-files.html).

```paket
references: strict
source https://nuget.org/api/v2

nuget Newtonsoft.Json ~> 6.0
nuget UnionArgParser ~> 0.7
```

Note that the resolution phase is not affected by this option, it will still
resolve, lock and download all transitive references.

### Prerelease versions

If you want to depend on prereleases then Paket can assist you.

```paket
source https://nuget.org/api/v2

nuget xunit prerelease
nuget xunit.runner.visualstudio prerelease
```

More details and information about prerelease channels can be found [here](nuget-dependencies.html#Prereleases).

### Framework restrictions

Sometimes you do not want to generate dependencies for other .NET Framework
versions than the ones your projects use. You can control this in the
[`paket.dependencies` file](dependencies-file.html):

```paket
// Download and install only for .NET 3.5 and .NET 4.0.
framework: net35, net40

source https://nuget.org/api/v2

nuget Example >= 2.0
```

Which may be translated to:

> Paket, I only compile for `net35` and `net40`, please leave out all other
> stuff I don't need to compile for this set of frameworks.

The supported framework identifiers include:

* .NET Framework: `net{version}`
* .NET Core: `netcoreapp{version}`
* .NET Standard: `netstandard{version}`
* .NET Framework for Unity: `net{version}-Unity {Web|Micro|Subset|Full} v{version}`
* Mono for Android: `monoandroid{version}`
* Mono for Mac: `monomac`
* MonoTouch: `monotouch`
* Native: `native` or `native({buildmode},{platform})`
* Xamarin for Mac: `xamarinmac`
* Xamarin for iOS: `xamarinios`
* Xamarin for watchOS: `xamarinwatchos`
* Xamarin for tvOS: `xamarintvos`
* Universal Windows Platform: `uap{version}`
* Windows: `win{version}`
* Windows Phone: `wp{version}`
* Windows Phone App: `wpa{version}`
* Silverlight: `sl{version}`
* Tizen: `tizen{version}`

#### Automatic framework detection

Paket is able to detect the target frameworks from your projects and then limit
the installation to these. You can control this in the
[`paket.dependencies` file](dependencies-file.html):

```paket
// Only the target frameworks that are used in projects.
framework: auto-detect

source https://nuget.org/api/v2

nuget Example >= 2.0
```

If you change the target framework of the projects then you need to run
[`paket install`](paket-install.html) again.


#### External lock files

Paket is able to consume external [`paket.lock` files](lock-file.html). 
External lock files allow to pin dependencies to the versions that are used on a target runtime platform like [Azure Functions](https://azure.microsoft.com/en-us/services/functions/).

In the [`paket.dependencies` file](dependencies-file.html) you can use `external_lock` and point to a http resource or a local file:

```paket
source https://nuget.org/api/v2

external_lock https://myUrl/azurefunctions-v1-paket.lock

nuget Example >= 2.0
```

The [`paket install` process](paket-install.html) will pin all dependencies to exactly the versions from the external [`paket.lock` file](lock-file.html).

### Disable packages folder

With the net netcore release and the switch to provide more and more netstandard-only packages
the Paket team noticed a dramatic increase of the well known "packages" folder.
Historically one way was to tell Paket that you only want to compile for `framework: net45`.
However, this doesn't prevent netstandard dependencies in all situations.
On the other side more features are provided by Paket and the packages folder has become more and more redundant:

 - Load scripts can reference the files in the global cache
 - csproj/fsproj files can references files in the global cache
 - netcore project files don't require any explicit dll-references
 
Therefore, the paket team decided to make the "packages" folder opt-out.

You can opt-out of generating the `packages` folder by using the `storage` option:

```paket
// Do not extract into the "packages" folder but use a globally shared directory
storage: none
source https://nuget.org/api/v2

nuget jQuery
```

The option may be overriden by packages. However, the behavior is undefined and may change (please open an issue if you depend on the current behavior or we break you).

The storage option has three values:

- `storage: packages` (the default, relevant for FAKE 5 where the default for inline-dependencies is `storage: none`)
- `storage: none` disable the packages folder and use the global NuGet cache (default in FAKE 5 inline-dependencies)
- `storage: symlink` use the packages folder but use symlinks instead of physical files

```paket
// make a symlink instead copy the packages.
storage: symlink
source https://nuget.org/api/v2

nuget jQuery
```
In this mode, paket will use a directory symbolic link (soft) between nuget cache and packages folder.
Symlink option can save a disk space on CI server. 
Before setting this option, configure the user rights assignment / create symbolic links and check your security prerequisites : https://docs.microsoft.com/en-us/windows/security/threat-protection/security-policy-settings/create-symbolic-links

Known issue : "You do not have sufficient privilege to perform this operation"
- Remove account from Administrators group
- Configure the create symbolic links (SeCreateSymbolicLinkPrivilege)
- Check symlink behavior

```bat
fsutil behavior query SymlinkEvaluation
```
Symlink behavior should be set to at least "Local to local symbolic links are enabled" (L2L enabled)

### Controlling whether content files should be copied to the project

The `content` option controls the installation of any content files:

```paket
// Do not install jQuery content files.
content: none

source https://nuget.org/api/v2

nuget jQuery >= 0
```

The global option may be
[overridden per package](nuget-dependencies.html#Controlling-whether-content-files-should-be-copied-to-the-project).

### Controlling whether content files should be copied to the output directory during build

It's possible to influence the
[`CopyToOutputDirectory` property](https://msdn.microsoft.com/en-us/library/bb629388.aspx#Anchor_0)
for content references via the `copy_content_to_output_dir` option:

```paket
copy_content_to_output_dir: always

source https://nuget.org/api/v2

nuget jQuery
nuget Fody
nuget ServiceStack.Swagger
```

The global option may be
[overridden per package](nuget-dependencies.html#Controlling-whether-content-files-should-be-copied-to-the-output-directory-during-build).

### Controlling whether assemblies should be copied to the output directory during build

It's possible to influence the
[`Private` property](https://msdn.microsoft.com/en-us/library/bb629388.aspx#Anchor_0)
for references via the `copy_local` option:

```paket
copy_local: false

source https://nuget.org/api/v2

nuget Newtonsoft.Json
```

The global option may be
[overridden per package](nuget-dependencies.html#Controlling-whether-assemblies-should-be-copied-to-the-output-directory-during-build).

### Importing `*.targets` and `*.props` files

If you don't want to import `*.targets` and `*.props` files from packages, you
can disable it via the `import_targets` option:

```paket
// Do not import *.targets and *.props.
import_targets: false

source https://nuget.org/api/v2

nuget Microsoft.Bcl.Build
nuget UnionArgParser ~> 0.7
```

The global option may be
[overridden per package](nuget-dependencies.html#Importing-and-files).

### License download

If you want paket to download licenses automatically you can use the `license_download` modifier. It is disabled by default.

```paket
source https://nuget.org/api/v2
license_download: true

nuget suave
```

The global option may be
[overridden per package](nuget-dependencies.html#License-download).

### Controlling assembly binding redirects

This option tells Paket to create
[Assembly Binding Redirects](https://msdn.microsoft.com/en-us/library/433ysdt1(v=vs.110).aspx)
for all referenced libraries. This option only instructs Paket to create and manage
binding redirects in **existing `App.config` files**, it will not create new
`App.config` files for you.

However you can create `App.config` files as necessary by running
[`paket install --create-new-binding-files`](paket-install.html).

```paket
redirects: on

source https://nuget.org/api/v2

nuget UnionArgParser ~> 0.7
```

The global option may be
[overridden per package](nuget-dependencies.html#Controlling-assembly-binding-redirects).

If you're using multiple [groups](groups.html), you must set `redirects: off`
for each one.

```paket
redirects: off

source https://nuget.org/api/v2

nuget UnionArgParser ~> 0.7 redirects: on
nuget FSharp.Core redirects: force

group Build
  redirects: off

  source https://nuget.org/api/v2

  nuget FAKE redirects: on
```

If you want Paket to always create redirects for all packages then you can use the following:

```paket
redirects: force
source https://nuget.org/api/v2

nuget FSharp.Core
nuget Newtonsoft.JSON
```

### Resolver strategy for transitive dependencies

The `strategy` option tells Paket what resolver strategy it should use for
[transitive dependencies](faq.html#transitive). The strategy can be either `min`
or `max` with `max` being the default.

NuGet's dependency syntax led to a lot of incompatible packages on
[nuget.org](https://www.nuget.org). To make your transition to Paket easier and
to allow package authors to correct their version constraints you can have Paket
behave like NuGet when resolving [transitive dependencies](faq.html#transitive)
(i.e. defaulting to lowest matching versions).

```paket
strategy: min

source https://nuget.org/api/v2

nuget UnionArgParser ~> 0.7
```

The `min` strategy means you get the *lowest matching version* of your
[transitive dependencies](faq.html#transitive) (i.e. NuGet-style). In contrast,
a `max` strategy will get you the *highest matching version*.

Note, however, that all direct dependencies will still get their *latest
matching versions*, no matter the value of the `strategy` option. If you want to
influence the resolution of direct dependencies then read about the
[lowest_matching option](dependencies-file.html#Resolver-strategy-for-direct-dependencies).

The only exception is when you are updating a single package and one of your
direct dependencies is a [transitive dependency](faq.html#transitive) for that
specific package. In this case, only the updating package will get its *latest
matching version* and the dependency is treated as transitive.

The global option may be
[overridden per package](nuget-dependencies.html#Strategy-modifiers).

### Resolver strategy for direct dependencies

The `lowest_matching` option tells Paket what resolver strategy it should use
for direct dependencies. It can be either `true` or `false` with `false` being
the default.

```paket
lowest_matching: true
source https://nuget.org/api/v2

nuget UnionArgParser ~> 0.7
```

`lowest_matching: true` means you get the *lowest matching version* of your
direct dependencies. In contrast, a `lowest_matching: false` will get you the
*highest matching version*.

Note, however, that all [transitive dependencies](faq.html#transitive) will
still get their *latest matching versions*, no matter the value of the
`lowest_matching` option. If you want to influence the resolution of
[transitive dependencies](faq.html#transitive) then read about the
[strategy option](dependencies-file.html#Strategy-option).

The global option may be
[overridden per package](nuget-dependencies.html#Resolver-strategy-for-direct-dependencies).

### Load script generation

This option tells Paket to
[generate load scripts](paket-generate-load-scripts.html) for interactive
environments like F# Interactive or ScriptCS. These scripts reference installed
packages.

The `generate_load_scripts` option can be either `true` or `false` with `false`
being the default.

```paket
generate_load_scripts: true

source https://nuget.org/api/v2

nuget Suave
```

Generated load scripts can be loaded like this:

```fsharp
#load @".paket/load/net45/suave.fsx"
```

### Simplify prevention

This option gives paket a hint around your itention for the paket [simplify](commands/simplify.html).
The paket simplify is a heuristic simplification & can sometimes think a dependency is not required in your dependencies file.
By adding this option, you can prevent the simplify command from removing the dependency.


The `simplify` option can be either `never` or `false`.  If it's not present, no simplify preference has been set

```paket
source https://nuget.org/api/v2

nuget Suave simplify: never
```

## Comments

All lines starting with with `//` or `#` are considered comments.
