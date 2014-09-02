FAQ - Frequently Asked Questions
================================

Why can't I just use NuGet?
---------------------------

**Q:** I don't understand why I need Paket to manage my packages. Why can't I just use NuGet?

**A:** NuGet doesn't separate out the concept of indirect dependencies; if you install a package into your project and that package has further dependencies then all indirect packages are included in the `packages.config` file. There is no way to tell which packages are only indirect dependencies. 

Even more importantly: if two packages reference conflicting versions of a package, NuGet will silently take the latest version; You have no control over this process.
 
Paket on the other hand maintains this information on a consistent and stable basis within the [lockfile](lockfile.html) in the solution root. This file, together with the `packages.fsx` enables you to determine exactly what's happening with your dependencies.

The [paket outdated](paket_outdated.html) command lists packages that have new versions available.

Future versions of Paket will also enable one to [reference files directly from git repositories](https://github.com/fsprojects/Paket/issues/9).

Resolving dependencies from NuGet is really slow
------------------------------------------------

**Q:** When I resolve the dependencies from NuGet.org it is really slow. Why is that?

**A:** Paket uses the NuGet oData API to discover package dependencies. Unfortunately this API is very slow. 

Some good news is that [the NuGet team are currently developing a faster API](http://blog.nuget.org/20140711/nuget-architecture.html); Paket may be able to take advantage of that.

Once the Lockfile is written Paket won't use the oData API anymore and therefore package restore is very fast. 
