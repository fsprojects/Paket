FAQ - Frequently Asked Questions
================================

I don't understand why I need Paket to manage my packages. Why can't I just use NuGet?
--------------------------------------------------------------------------------------

NuGet doesn't separate out the concept of indirect dependencies; if you install a package into your project and that package has further dependencies then all indirect packages are included in the `packages.config` file. There is no way to tell which packages are only indirect dependencies. 

Even more importantly: if two packages reference conflicting versions of a package, NuGet will silently take the latest version; You have no control over this process.
 
Paket on the other hand maintains this information on a consistent and stable basis within the [lockfile](lockfile.html) in the solution root. This file, together with the [Paket.dependencies](Dependencies_file.html) file enables you to determine exactly what's happening with your dependencies.

The [paket outdated](paket_outdated.html) command lists packages that have new versions available.

Future versions of Paket will also enable one to [reference files directly from git repositories](https://github.com/fsprojects/Paket/issues/9).

Why does Paket add references to the libraries associated with each supported framework version within a NuGet package to my projects?
--------------------------------------------------------------------------------------------------------------------------------------

A NuGet package installation adds references only for the currently selected target .NET framework version of your project;
whenever you want to change the framework version you have to reinstall your NuGet packages (assuming you notice the problem).

Paket adds references to all of them; but with *Condition* properties filtering them based on the currently selected *TargetFramework*.

Why does Paket use a different package resolution strategy than NuGet?
----------------------------------------------------------------------

Paket tries to embrace [SemVer](http://semver.org/) while NuGet uses a pessimistic version resolution strategy. You can always prefix your version constraints with **!** if you need to use [NuGet compatibility](packages_file.html).

When I resolve the dependencies from NuGet.org it is really slow. Why is that?
------------------------------------------------------------------------------

Paket uses the NuGet oData API to discover package dependencies. Unfortunately this API is very slow. 

Some good news is that [the NuGet team are currently developing a faster API](http://blog.nuget.org/20140711/nuget-architecture.html); Paket may be able to take advantage of that.

Once the [Lockfile](lockfile.html)  is written Paket won't use the oData API anymore and therefore package restore is very fast. 
