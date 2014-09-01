FAQ - Frequently Asked Questions
================================

Why can't I just use NuGet?
---------------------------

Q: I don't understand why I need Paket to manage my packages. Why can't I just use NuGet?

A: NuGet doesn't really have the concept of indirect dependencies. If you install a package into your project and that package has further dependencies then all inderect packages are included in the `packages.config` file.
There is no way to tell which packages are only indirect dependencies. Even more important: if two packages reference conflicting versions of a package than NuGet will silently take the latest version.
You have no control over this process.
 
Paket on the other hand is always generating the [lockfile](lockfile.html) in the solution root. This file allows you to see exactly what's happening with you dependencies.

Resolving dependencies from NuGet is really slow
------------------------------------------------

Q: When I resolve the dependencies from NuGet.org it is really slow. Why is that?

A: Paket uses the NuGet ODATA API to discover package dependencies. Unfortunately this AAI is very slow. 
Good news is that the NuGet is currently developing a faster API. Paket might take advantage of that.