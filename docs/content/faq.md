# FAQ — Frequently Asked Questions

## I don't understand why I need Paket to manage my packages. Why can't I just use NuGet?

NuGet does not separate out the concept of transitive dependencies; if you install a package into your project and that package has further dependencies then all transitive packages are included in the `packages.config`. There is no way to tell which packages are only transitive dependencies.

Even more importantly: If two packages reference conflicting versions of a package, NuGet will silently take the latest version ([read more](controlling-nuget-resolution.html)). You have no control over this process.

Paket on the other hand maintains this information on a consistent and stable basis within the [`paket.lock` file](lock-file.html) in the solution root. This file, together with the [`paket.dependencies` file](dependencies-file.html) enables you to determine exactly what's happening with your dependencies.

The [`paket outdated` command](paket-outdated.html) lists packages that have new versions available.

Paket also enables one to reference files directly from [GitHub repositories, Gists](github-dependencies.html) and [HTTP](http-dependencies.html).

<div id="no-version"></div>
## NuGet puts the package version into the path. Is Paket doing the same?

No, since Paket provides a global view of your dependencies it installs only one version of a package and therefore the version number is not needed in the path.
This makes it much easier to reference files in the package and you don't have to edit these references when you update a package.

If you really need to have the version in the path for certain packages (like xunit.runners.visualstudio) you [can still do that](nuget-dependencies.html#Putting-the-version-no-into-the-path).

## Why does Paket add references to the libraries associated with each supported framework version within a NuGet package to my projects?

A NuGet package installation adds references only for the currently selected target .NET framework version of your project at the time of installation. Whenever you switch the framework version of your project, there's a potential need to reinstall all of the packages.

However the Visual Studio tooling does not address this – it's up to you to remember to reinstall. In the best case, this leads to compiler errors about missing methods/types etc. In the worst case, it's a variance that's either deeply buried within the code (meaning it might be difficult to trap in a test cycle) or a more difficult to detect 'silent' problem.

Paket adds references to all of them, but with `Condition` attributes filtering them based on the currently selected `TargetFramework` and other relevant MSBuild properties.

If you only want to use a subset of the target frameworks you can use [framework restrictions](http://fsprojects.github.io/Paket/nuget-dependencies.html#Framework-restrictions).

## Why does Paket use a different package resolution strategy than NuGet?

Paket tries to embrace [SemVer](http://semver.org/) while NuGet uses a pessimistic version resolution strategy. You can prefix your version constraints with `!` if you need to use [NuGet compatibility](dependencies-file.html#Paket-s-NuGet-style-dependency-resolution-for-transitive-dependencies).
If you want to know more about Paket's resolver algorithm, then you can read [this article](resolver.html).

## Does Paket run install.ps1 scripts?
<div id="paket-vs-powershell-install-scripts"></div>

No, we don't run any script or program from NuGet packages and we have no plans to do this in the future.
We know that this might cause you some manual work for some of the currently available NuGet packages, but we think these install scripts cause more harm than good.
In fact our current model would not be able to work consistently alongside an `install.ps1` script like the following from `FontAwesome.4.1.0`:

    [lang=batchfile]
    param($installPath, $toolsPath, $package, $project)

    foreach ($fontFile in $project.ProjectItems.Item("fonts").ProjectItems)
    {
        $fontFile.Properties.Item("BuildAction").Value = 2;
    }

The reason is simply that even if we would support PowerShell on Windows we can't access the Visual Studio project system. Paket is a command line tool and doesn't run inside of Visual Studio.
There is no reasonable way to make this work – and even NuGet.exe can't do it in command line mode.

Instead we encourage the .NET community to use a declarative install process and we will help to fix this in the affected packages.

## Why should I commit the lock file?

Committing the [`paket.lock` file](lock-file.html) to your version control system guarantees that other developers and/or build servers will always end up with a reliable and consistent set of packages regardless of where or when [`paket install`](paket-install.html) is run.

If your *project is an application* you should always commit the [`paket.lock` file](lock-file.html). 

If your *project is a library* then you probably want to commit it as well. There are rare cases where you always want to test your lib against the latest version of your dependencies, 
but we recommend to set up a second CI build instead. This new build should be run regularly (maybe once a day) and execute [`paket update`](paket-update.html) at the beginning.
This will ensure that you get notified whenever a dependency update breaks your library.

## I'm already using NuGet. How can I convert to Paket?

The process can be automated with [paket convert-from-nuget](paket-convert-from-nuget.html) command.

In case of the command's failure, you can fallback to manual approach:

1. Analyse your `packages.config` files and extract the referenced packages into a paket.dependencies file.
2. Convert each `packages.config` file to a paket.references file. This is very easy - you just have to remove all the XML and keep the package names.
3. Run [paket install](paket-install.html) with the `--hard` flag. This will analyze the dependencies, generate a paket.lock file, remove all the old package references from your project files and replace them with equivalent `Reference`s in a syntax that can be managed automatically by Paket.
4. (Optional) Raise corresponding issue [here](https://github.com/fsprojects/Paket/issues) so that we can make the command even better.

## How do I convert a new project to Paket when my solution is already using Paket

In this case it's okay to use the `--force` flag for the `convert-from-nuget` command as described in [partial NuGet conversion](getting-started.html#Partial-NuGet-conversion). Paket will then go through your solution and convert all new NuGet projects to Paket.

## Can I use Paket to manage npm/bower/whatever dependencies?

[No.](https://github.com/fsprojects/Paket/issues/61) We don't believe in reinventing the wheel.

On top of that, such a "meta package manager" abstraction is likely to be less flexible and behind on what native tools have to offer. Paket serves a specific need, that is [SemVer-compatible](http://semver.org) NuGet.
