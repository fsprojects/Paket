# FAQ — Frequently Asked Questions

## I don't understand why I need Paket to manage my packages. Why can't I just use NuGet?

NuGet does not separate out the concept of indirect dependencies; if you install a package into your project and that package has further dependencies then all indirect packages are included in the `packages.config`. There is no way to tell which packages are only indirect dependencies.

Even more importantly: If two packages reference conflicting versions of a package, NuGet will silently take the latest version. You have no control over this process.

Paket on the other hand maintains this information on a consistent and stable basis within the [`paket.lock` file](lock-file.html) in the solution root. This file, together with the [`paket.dependencies` file](dependencies-file.html) enables you to determine exactly what's happening with your dependencies.

The [`paket outdated` command](paket-outdated.html) lists packages that have new versions available.

Paket also enables one to [reference files directly from GitHub repositories](github-dependencies.html).

<div id="no-version"></div>
## NuGet puts the package version into the path. Is Paket doing the same?

No, since Paket provides a global view to your dependencies it installs only one version of a package and therefor the version number is not needed in the path.
This makes it much easier to reference files in the package and you don't have to edit these references when you update a package.

## Why does Paket add references to the libraries associated with each supported framework version within a NuGet package to my projects?

A NuGet package installation adds references only for the currently selected target .NET framework version of your project at the time of installation. Whenever you switch the framework version of your project, there's a potential need to reinstall all of the packages.

However the Visual Studio tooling does not address this – it's up to you to remember to reinstall. In the best case, this leads to compiler errors about missing methods/types etc. In the worst case, it's a variance that's either deeply buried within the code (meaning it might be difficult to trap in a test cycle) or a more difficult to detect 'silent' problem.

Paket adds references to all of them, but with `Condition` attributes filtering them based on the currently selected `TargetFramework` and other relevant MSBuild properties.

See [`paket.references`](references-files.html) for more information.

## Why does Paket use a different package resolution strategy than NuGet?

Paket tries to embrace [SemVer](http://semver.org/) while NuGet uses a pessimistic version resolution strategy. You can prefix your version constraints with `!` if you need to use [NuGet compatibility](dependencies-file.html#nuget-style-dependency-resolution).

## Does Paket run install.ps1 scripts?
<div id="paket-vs-powershell-install-scripts"></div>

No, we don't run any script or program from NuGet packages and we have no plans to do this in the future.
We know that this might cause you some manual work for some of the currently available NuGet packages, but we think these install scripts cause more harm than good.
In fact our current model doesn't allow to run install.ps1 script like the following from `FontAwesome.4.1.0`:

    [lang=batchfile]
    param($installPath, $toolsPath, $package, $project)
    
    foreach ($fontFile in $project.ProjectItems.Item("fonts").ProjectItems)
    {
        $fontFile.Properties.Item("BuildAction").Value = 2;        
    }
    
The reason is simply that even if we would support PowerShell on Windows we can't access the Visual Studio project system. Paket is a command line tool and doesn't run inside of Visual Studio.
There is no way to make this work – and even NuGet.exe can't do it in command line mode. 

Instead we encourage the .NET community to use a declarative install process and we will help to fix this in the affected packages.

## When I resolve the dependencies from NuGet.org it is really slow. Why is that?

Paket uses the NuGet OData API to discover package dependencies. Unfortunately this API is very slow.

Some good news is that [the NuGet team is currently developing a faster API](http://blog.nuget.org/20140711/nuget-architecture.html). Paket may be able to take advantage of that in the future.

Once the [`paket.lock` file](lock-file.html) is written, Paket no longer needs to use the OData API any futher; as a result, [`paket install`](paket-install.html) is very fast.

## I'm already using NuGet. How can I convert to Paket?

The process is very easy and you can read more about it in the [convert from NuGet](convert-from-nuget.html) section.

## Can I use Paket to manage npm/bower/whatever dependencies?

[No.](https://github.com/fsprojects/Paket/issues/61) We don't believe in reinventing the wheel.

On top of that, such a "meta package manager" abstraction is likely to be less flexible and behind on what native tools have to offer. Paket serves a specific need, that is [SemVer-compatible](http://semver.org) NuGet.
