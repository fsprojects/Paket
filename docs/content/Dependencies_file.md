The Paket.dependencies file
===========================

The `Paket.dependencies` file is used to specify rules regarding your application's dependencies. 

It uses a similar syntax to that of [bundler](http://bundler.io/)'s [Gemfile](http://bundler.io/gemfile.html):
  
    source "http://nuget.org/api/v2"

    nuget "Castle.Windsor-log4net" "~> 3.2"
    nuget "Rx-Main" "~> 2.0"

Only direct dependencies should be listed in this file.

Paket uses this definition to compute a concrete package resolution, which also includes indirect dependencies, in a [Paket.lock](lock_file.html) file.

Sources
-------

It's possible to use multiple sources:

    source "http://nuget.org/api/v2" // nuget.org

    nuget "Castle.Windsor-log4net" "~> 3.2"
    nuget "Rx-Main" "~> 2.0"

    source "http://myserver/nuget/api/v2" // custom feed
    
    nuget "CustomLib" "~> 1.5" // downloaded from the custom feed

The [Paket.lock](lock_file.html) will also reflect these settings.

Path sources
------------

Paket supports NuGet feeds like [nuget.org](http://nuget.org) or [TeamCity](http://www.jetbrains.com/teamcity/), but it also supports paths:

    source "C:\Nugets"
    source "\\server\nugets"

    nuget "FAKE" "~> 3.2"

NuGet-style pessimistic version constraints
-------------------------------------------

NuGet uses a pessimistic version resolution strategy. In order to make the transition easier, Paket allows you to apply NuGet's dependency resolution strategy by prefixing your version constraint with **`!`**.

    source "http://nuget.org/api/v2"

    nuget "Nancy.Bootstrappers.Windsor" "!~> 0.23" // use pessimistic version resolution strategy
