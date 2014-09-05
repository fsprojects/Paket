The Dependencies file
=====================

The Dependencies file is used to specify your applications's dependencies. 
It uses a similar syntax like [bundler](http://bundler.io/)'s GEMfile.
  
    source "http://nuget.org/api/v2"

    nuget "Castle.Windsor-log4net" "~> 3.2"
    nuget "Rx-Main" "~> 2.0"

Only direct dependencies are listed in this file.
Paket uses this definition to compute a concrete package resolution,
which also includes indirect dependencies, in a [Dependencies.lock](lockfile.html) file.

Sources
-------

It's possible to use multiple sources:

    source "http://nuget.org/api/v2" // nuget.org

    nuget "Castle.Windsor-log4net" "~> 3.2"
    nuget "Rx-Main" "~> 2.0"

    source "http://myserver/nuget/api/v2" // custom feed

    nuget "FAKE" "~> 3.2"

The [Lockfile](lockfile.html) will respect these settings.

NuGet-style pessimistic version constraints
-------------------------------------------

NuGet uses a pessimistic versiion resolution strategy. In order to make the transition easier Paket allows you to use NuGet's dependency resolution by prefixing your version constraint with **!**.

    source "http://nuget.org/api/v2"

    nuget "Nancy.Bootstrappers.Windsor" "!~> 0.23" // use pessimistic version resolver