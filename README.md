[![Issue Stats][badge-issue-stats]][link-issue-stats]
[![Pull Requests Stats][badge-pr-stats]][link-issue-stats]
[![Travis build status](https://travis-ci.org/fsprojects/Paket.png)](https://travis-ci.org/fsprojects/Paket)
[![AppVeyor Build status](https://ci.appveyor.com/api/projects/status/aqs8eux16x4g5p47/branch/master)](https://ci.appveyor.com/project/SteffenForkmann/paket/branch/master)
[![NuGet Status](http://img.shields.io/nuget/v/Paket.svg?style=flat)](https://www.nuget.org/packages/Paket/)

# Paket

A package dependency manager for .NET with support for NuGet packages and GitHub repositories.

## Why Paket?

NuGet does not separate out the concept of transitive dependencies.
If you install a package into your project and that package has further dependencies then all transitive packages are included in the packages.config.
There is no way to tell which packages are only transitive dependencies.

Even more importantly: If two packages reference conflicting versions of a package, NuGet will silently take the latest version ([read more](http://fsprojects.github.io/Paket/controlling-nuget-resolution.html)). You have no control over this process.

Paket on the other hand maintains this information on a consistent and stable basis within the [`paket.lock` file][7] in the solution root.
This file, together with the [`paket.dependencies` file][8] enables you to determine exactly what's happening with your dependencies.

Paket also enables you to [reference files directly from GitHub (and Gist)][9] repositories or any [http-resource](11).

For more reasons see the [FAQ][10].

## Online resources

 - [Source code][1]
 - [Documentation][2]
 - Download [paket.exe][3]
 - Download [paket.bootstrapper.exe][3]

## Troubleshooting and support

 - Found a bug or missing a feature? Feed the [issue tracker][4].
 - Announcements and related miscellanea through Twitter ([@PaketManager][5])

## Quick contributing guide

 - Fork and clone locally.
 - Build the solution with Visual Studio, `build.cmd` or `build.sh`.
 - Create a topic specific branch in git. Add a nice feature in the code. Do not forget to add tests and/or docs.
 - Run `build.cmd` (`build.sh` on Mono) to make sure all tests are still passing.
 - Send a Pull Request.

If you want to contribute to the [docs][2] then please the modify markdown files in `/docs/content` and send a pull request.
Note, that short description and syntax for each command is generated automatically from the `Paket.Commands` module.

## License

The [MIT license][6]

## Maintainer(s)

- [@forki](https://github.com/forki)
- [@agross](https://github.com/agross)
- [@mavnn](https://github.com/mavnn)
- [@mexx](https://github.com/mexx)
- [@isaacabraham](https://github.com/isaacabraham)
- [@theimowski](https://github.com/theimowski)

The default maintainer account for projects under "fsprojects" is [@fsgit](https://github.com/fsgit) - F# Community Project Incubation Space (repo management)

 [1]: https://github.com/fsprojects/Paket/
 [2]: http://fsprojects.github.io/Paket/
 [3]: https://github.com/fsprojects/Paket/releases/latest
 [4]: https://github.com/fsprojects/Paket/issues
 [5]: http://twitter.com/PaketManager
 [6]: https://github.com/fsprojects/Paket/blob/master/LICENSE.txt
 [7]: http://fsprojects.github.io/Paket/lock-file.html
 [8]: http://fsprojects.github.io/Paket/dependencies-file.html
 [9]: http://fsprojects.github.io/Paket/github-dependencies.html
 [10]: http://fsprojects.github.io/Paket/faq.html
 [11]: http://fsprojects.github.io/Paket/http-dependencies.html
 [badge-pr-stats]: http://www.issuestats.com/github/fsprojects/Paket/badge/pr
 [badge-issue-stats]: http://www.issuestats.com/github/fsprojects/Paket/badge/issue
 [link-issue-stats]: http://www.issuestats.com/github/fsprojects/Paket
