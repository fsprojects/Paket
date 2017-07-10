# FAQ — Frequently Asked Questions

## I do not understand why I need Paket to manage my packages. Why can't I just use NuGet?

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
`xunit.runners.visualstudio`) you
[can still do that](nuget-dependencies.html#Putting-the-version-number-in-the-path).

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

Paket creates a number of files in your repository, and most of them should be
committed to source control. To be clear, these are the files that should be
committed to source control:

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
* All [`paket.template`](template-files.html). Tf a project is supposed to be
  deployed as a NuGet project it will have a
  [`paket.template` file](template-files.html) that specifies package metadata.
  Each of these files should be committed to source control.

The following files can be committed, but are not essential:

* [`.paket/paket.targets`](paket-folder.html) allows you to enable automatic
  package restore in Visual Studio.
* [`.paket/paket.bootstrapper.exe`](bootstrapper.html) is a small,
  rarely updated executable that will download the latest version of the main
  `paket.exe`. It is not necessary, but can be very useful for other developers
  and build servers, so they can easily retrieve `paket.exe` and restore
  packages without having Paket already installed and in the `PATH`. For
  example, it is common to have a
  [`build.sh`](https://github.com/fsprojects/Paket/blob/master/build.sh) or
  [`build.cmd`](https://github.com/fsprojects/Paket/blob/master/build.cmd) file
  in the root of a repository that will do the equivalent of:

  ```sh
  .paket/paket.bootstrapper.exe
  .paket/paket.exe restore

  // Invoke build tool/scripts to build solution.
  ```

The following files should *not* be committed to your version control system,
and should be added to any ignore files:

* `.paket/paket.exe`, the main Paket executable, downloaded by
  [`.paket/paket.bootstrapper.exe`](bootstrapper.html). It should not be
  committed, as it is a binary file which can unnecessarily bloat repositories,
  and because it is likely to be updated on a regular basis.
* `paket-files` directory, as [`paket install`](paket-install.html) will restore
  this.
* Same applies to the `packages` directory.

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
