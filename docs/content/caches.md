# Additional Caches

<blockquote>This feature is only available in Paket 3.0 alpha versions.</blockquote>

Paket works well with central repositories like nuget.org or myget.org. Under normal circumstances these repositories are always available and allow you to retrieve all packages for the rest of all time.
Unfortunately there are times when the central repository is not reachable or even worse packages can be permanently removed from the feed.

This happened in other ecosystems, but also at nuget.org and can lead to breaking builds and a lot of trouble for the users.

Let's consider a small example:

    [lang=paket]
    source https://nuget.org/api/v2
	cache ./nupkgs versions:current
    cache //hive/dependencies versions:all

    nuget Newtonsoft.Json
    nuget UnionArgParser
    nuget FSharp.Core

    github forki/FsUnit FsUnit.fs
    github fsharp/FAKE src/app/FakeLib/Globbing/Globbing.fs
    github fsprojects/Chessie src/Chessie/ErrorHandling.fs