# FAQ — Frequently Asked Questions

## I do not understand why I need Paket to manage my packages. Why can't I just use NuGet.exe and packages.config?

NuGet does not separate out the concept of
[transitive dependencies](faq.html#transitive); if you install a package into
your project and that package has further dependencies then all transitive
packages are included in the `packages.config`. There is no way to tell which
packages are only transitive dependencies.

Even more importantly: If two packages reference conflicting versions of a
package, NuGet will silently take the latest version
([read more](controlling-nuget-resolution.html)). You have no control over this
process.

Paket on the other hand maintains this information on a consistent and stable
basis within the [`paket.lock` file](lock-file.html) in the solution root. This
file, together with the [`paket.dependencies` file](dependencies-file.html)
enables you to determine exactly what's happening with your dependencies.

The [`paket outdated` command](paket-outdated.html) lists packages that have new
versions available.

Paket also enables one to reference files directly from
[GitHub repositories, Gists](github-dependencies.html) and
[HTTP](http-dependencies.html).

<div id="no-version"></div>

## NuGet puts the package version into the path. Is Paket doing the same?

No, since Paket provides a global view of your dependencies it usually installs
only one version of a package and therefore the version number is not needed in
the path. This makes it much easier to reference files in the package and you
don't have to edit these references when you update a package.

If you really need to have the version in the path for certain packages (like
`xunit.runners.visualstudio` or `MSTest.TestAdapter`) you
[can still do that](nuget-dependencies.html#Putting-the-version-number-in-the-path).
Without the `version_in_path` flag, your unit tests will disappear from the Visual Studio Test Explorer.

## NuGet allows to use multiple versions of the same package. Can I do that with Paket?

Usually you don't want that to happen. Most solutions that have multiple
versions of the same package installed did this by accident. Since NuGet has no
global lock file and stores version information in packages.config (per
project), it's hard to keep all projects consolidated. Paket on the other gives
you a global/consolidated view of all your dependencies in the
[`paket.lock` file](lock-file.html).

In the very rare cases when you really need to maintain different versions of
the same package you can use the
[dependency groups feature](groups.html). Every dependency group gets resolved
independently so it also deals with the conflict resolution of indirect
dependencies, but the most important difference is that using groups is a
deliberate action. You need to explicitly name the group in
[`paket.references` files](references-files.html), so it won't happen by accident.

## Why does Paket add references to the libraries associated with each supported framework version within a NuGet package to my projects?

A NuGet package installation adds references only for the currently selected
target .NET framework version of your project at the time of installation.
Whenever you switch the framework version of your project, there's a potential
need to reinstall all of the packages.

However the Visual Studio tooling does not address this — it's up to you to
remember to reinstall. In the best case, this leads to compiler errors about
missing methods/types etc. In the worst case, it's a variance that's either
deeply buried within the code (meaning it might be difficult to trap in a test
cycle) or a more difficult to detect 'silent' problem.

Paket adds references to all of them, but with `Condition` attributes filtering
them based on the currently selected `TargetFramework` and other relevant
MSBuild properties.

If you only want to use a subset of the target frameworks you can use
[framework restrictions](nuget-dependencies.html#Framework-restrictions).

## Why does Paket use a different package resolution strategy than NuGet?

Paket tries to embrace [semantic versioning](http://semver.org/) while NuGet
uses a pessimistic version resolution strategy. You can prefix your
[version constraints](nuget-dependencies.html#Version-constraints) with
[`!`](nuget-dependencies.html#Strategy-modifiers) if you need to
[stay compatible to NuGet](dependencies-file.html#Strategy-option).
[Read about more about Paket's resolver algorithm.](resolver.html)

<div id="paket-vs-powershell-install-scripts"></div>

## Does Paket run install.ps1 scripts?

No, Paket does not run any script or program from NuGet packages and we have no
plans to do this in the future. We know that this might cause you some manual
work for some of the currently available NuGet packages, but we think these
install scripts cause more harm than good. In fact our current model would not
be able to work consistently alongside an `install.ps1` script like the
following from `FontAwesome.4.1.0`:

```powershell
param($installPath, $toolsPath, $package, $project)

foreach ($fontFile in $project.ProjectItems.Item("fonts").ProjectItems)
{
  $fontFile.Properties.Item("BuildAction").Value = 2;
}
```

The reason is simply that even if we would support PowerShell on Windows we
can't access the Visual Studio project system. Paket is a command line tool and
doesn't run inside of Visual Studio. There is no reasonable way to make this
work – and even NuGet.exe can't do it in command line mode.

Instead we encourage the .NET community to use a declarative install process and
we will help to fix this in the affected packages.

## What files should I commit?

There are two main ways to incorporate paket in your repository which are
outlined in the [get started section](get-started.html). .NET Core 3.0+ or
the legacy ["magic mode" approach](bootstrapper.html#Magic-mode). The first
has the least files to commit, but to be clear here are first what you should
always commit to source control no matter the approach:

* [`paket.dependencies`](dependencies-file.html) specifies your application's
  dependencies, and how they should be fulfilled.
* [`paket.lock`](lock-file.html) records the actual versions used during
  resolution. If it exists, Paket will ensure that the same versions are used
  when [restoring packages](paket-restore.html). It is not strictly necessary to
  commit this file, but strongly recommended. See
  [this question](faq.html#Why-should-I-commit-the-lock-file) for details.
* All [`paket.references` files](references-files.html). Each project will have
  a [`paket.references` file](references-files.html) that specifies which of the
  dependencies are installed in the project. Each of these files should be
  committed to source control.
* All [`paket.template`](template-files.html). If a project is supposed to be
  deployed as a NuGet project it will have a
  [`paket.template` file](template-files.html) that specifies package metadata.
  Each of these files should be committed to source control.

When using legacy ["magic mode" approach](bootstrapper.html#Magic-mode) the
following files should also be committed:

* [`.paket/paket.targets`](paket-folder.html) allows you to enable automatic
  package restore in Visual Studio.
* [`.paket/paket.exe`](bootstrapper.html) should really be the renamed
  paket.bootstrapper.exe which will automatically download the latest version
  of `paket.exe` and redirect the calls to that file instead.

The following files should *not* be committed to your version control system,
and should be added to any ignore files:

* `paket-files` directory, as [`paket install`](paket-install.html) will restore
  this.
* Same applies to the `packages` directory.
* `.paket/paket.bootstrapper.exe`, if you have this in your repo, you have not
  yet updated to the recommended ["magic mode" approach](bootstrapper.html#Magic-mode),
  when running legacy (pre .NET Core 3.0) applications.

## Why should I commit the lock file?

Committing the [`paket.lock` file](lock-file.html) to your version control
system guarantees that other developers and/or build servers will always end up
with a reliable and consistent set of packages regardless of where or when
[`paket restore`](paket-restore.html) is run.

If your *project is an application* you should always commit the
[`paket.lock` file](lock-file.html).

If your *project is a library* then you probably want to commit it as well.
There are rare cases where you always want to test your lib against the latest
version of your dependencies, but we recommend to set up a second CI build
instead. This new build should be run regularly (maybe once a day) and execute
[`paket update`](paket-update.html) at the beginning. This will ensure that you
get notified whenever a dependency update breaks your library.

## I'm already using NuGet. How can I convert to Paket?

The process can be automated with
[`paket convert-from-nuget`](paket-convert-from-nuget.html) command.

In case of the command's failure, you can fallback to manual approach:

1. Analyze your `packages.config` files and extract the referenced packages into
   a [`paket.dependencies` file](dependencies-file.html).
1. Convert each `packages.config` file to a
   [`paket.references` file](references-files.html). This is very easy — you
   just have to remove all the XML and keep the package names.
1. Run [`paket install`](paket-install.html). This will analyze the
   dependencies, generate a [`paket.lock` file](lock-file.html), remove all the
   old package references from your project files and replace them with
   equivalent `Reference`s in a syntax that can be managed automatically by
   Paket.
1. We encourage you to raise
   [a corresponding issue](https://github.com/fsprojects/Paket/issues) so that
   we can make the command even better.

## How do I convert a new project to Paket when my solution is already using Paket

In this case it's okay to use the `--force` flag for the
[`convert-from-nuget` command](paket-convert-from-nuget.html) as described in
[partial NuGet conversion](getting-started.html#Partial-NuGet-conversion). Paket
will then go through your solution and convert all new NuGet projects to Paket.

## Paket stores `paket.dependencies` and `paket.lock` files in the root of a repository. How can I change that?

Very old Paket versions allowed to specify the location. We disabled that
because we have very strong opinions about the location of the
[`paket.dependencies` file](dependencies-file.html). We believe dependency
management is so important that these files belong in the root of the
repository. People should know about the project's dependencies.

That said: if you don't agree with that (but please take some time and think
about it) you can use batch file to change the working directory.

## Can I use a NuGet package stored as a local file?

[Yes](nuget-dependencies.html#Path-sources), either from a local directory, a UNC share or relative to the location of the [`paket.dependencies` file](dependencies-file.html).

## Can I use Paket to manage npm/bower/whatever dependencies?

[No.](https://github.com/fsprojects/Paket/issues/61) We don't believe in
reinventing the wheel.

On top of that, such a "meta package manager" abstraction is likely to be less
flexible and behind on what native tools have to offer. Paket serves a specific
need, that is [SemVer-compatible](http://semver.org) NuGet.

<div id="transitive"></div>

## What does "transitive dependencies" mean?

If you install NuGet packages into your project then these packages can have
dependencies on other NuGet packages. Paket calls these dependencies
"transitive". They will be automatically uninstalled if none of your "direct
dependencies" (the packages that you actually installed) still depend on them.

## I am behind a proxy. Can I use Paket?

If your proxy uses default (Active Directory) credentials, you have nothing to
do, Paket will handle it automatically.

If your proxy uses custom credentials, you need to set the following environment
variables:

* `HTTP_PROXY`: HTTP proxy to use for all connections
* `HTTPS_PROXY`: HTTPS proxy to use for all connections
* `NO_PROXY`: hosts that should bypass the proxy

```sh
set HTTP_PROXY=http://user:password@proxy.company.com:port/
set HTTPS_PROXY=https://user:password@proxy.company.com:port/
set NO_PROXY=.company.com,localhost
```

## I want to use Paket with .NET Core — is that possible?

Short answer: Yes. For information about Paket with .NET SDK, .NET Core and the
`dotnet` CLI see the
["Paket and the .NET SDK / .NET Core CLI tools" guide](paket-and-dotnet-cli.html).

## The download of packages times out, is there a way to prevent this?

Since version 5.190.0 there are three environment variables you can set to try to prevent this:

* `PAKET_REQUEST_TIMEOUT`: Timeout for the request
* `PAKET_RESPONSE_STREAM_TIMEOUT`: Timeout for the response of the request
* `PAKET_STREAMREADWRITE_TIMEOUT`: Timeout for streaming the read and write operations

Note that values should be specified in milliseconds.

The default timeout value for all three settings is 3 minutes (180 seconds).

The following example will set all three values to 10 minutes (600 seconds) on Windows

```sh
set PAKET_REQUEST_TIMEOUT=600000
set PAKET_RESPONSE_STREAM_TIMEOUT=600000
set PAKET_STREAMREADWRITE_TIMEOUT=600000
```

Use 'export' instead of 'set' for bash and similar shells

```sh
export PAKET_REQUEST_TIMEOUT=600000
export PAKET_RESPONSE_STREAM_TIMEOUT=600000
export PAKET_STREAMREADWRITE_TIMEOUT=600000
```

If set to -1 the timeout is infinite.

```sh
set PAKET_REQUEST_TIMEOUT=-1
set PAKET_RESPONSE_STREAM_TIMEOUT=-1
set PAKET_STREAMREADWRITE_TIMEOUT=-1
```


