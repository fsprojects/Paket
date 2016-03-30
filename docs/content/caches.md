# Additional Caches

<blockquote>This feature is only available in Paket 3.0 alpha versions.</blockquote>

Paket works well with central repositories like nuget.org or myget.org. Under normal circumstances these repositories are always available and allow you to retrieve all packages for the rest of all time.
Unfortunately there are times when the central repository is not reachable or even worse packages can be permanently removed from the feed.

This happened in other ecosystems, but also at nuget.org and can lead to breaking builds and a lot of trouble for the users.

There are different solutions to this problem and one is using "additional caches" with Paket.

## Caching on a network share

By using a network share as an additional package cache Paket will store all used packages automatically on that network share.
Every restore process tries to retrieve the package from the cache before hitting the central repository.
As long as nobody deletes packages from the network share, package restore will continue to work.

Configuration of additional network share as caches can be done like:

    [lang=paket]
    source https://nuget.org/api/v2
    cache //hive/dependencies

    nuget Newtonsoft.Json
    nuget UnionArgParser
    nuget FSharp.Core

## Caching locally inside the repository

Many projects decide to commit all dependencies into the version control system. This was always possible with Paket if you commited the *packages* folder.
Usually this would commit the zipped packages file and also all unzipped files. With a local dependencies cache you can instruct Paket to store all zipped packages in another folder.
Optionally it is possible to tell Paket to only store the currently used versions of the packages.

Let's consider a small example:

    [lang=paket]
    source https://nuget.org/api/v2
	cache ./nupkgs versions:current

    nuget Newtonsoft.Json
    nuget UnionArgParser
    nuget FSharp.Core