The packages.fsx file
=====================

The Packages file is used to specify your applications's dependencies. 
It uses a similar syntax like [bundler](http://bundler.io/)'s GEMfile.
  
    source "http://nuget.org/api/v2"

    nuget "Castle.Windsor-log4net" "~> 3.2"
    nuget "Rx-Main" "~> 2.0"

Only direct dependencies are listed in `packages.fsx`.
Paket uses this definition to compute a concrete package resolution,
which includes indirect dependencies, in a [Lockfile](lockfile.html).

Sources
-------

It's possible to use multiple sources:

    source "http://nuget.org/api/v2" // nuget.org

    nuget "Castle.Windsor-log4net" "~> 3.2"
    nuget "Rx-Main" "~> 2.0"

    source "http://myserver/nuget/api/v2" // custom feed

    nuget "FAKE" "~> 3.2"

The [Lockfile](lockfile.html) will respect this settings.