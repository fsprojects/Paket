[![Travis build status](https://api.travis-ci.org/fsprojects/Paket.svg)](https://travis-ci.org/fsprojects/Paket)
[![AppVeyor Build status](https://ci.appveyor.com/api/projects/status/aqs8eux16x4g5p47/branch/master?svg=true)](https://ci.appveyor.com/project/SteffenForkmann/paket/branch/master)
[![NuGet Status](https://img.shields.io/nuget/v/Paket.svg?style=flat)](https://www.nuget.org/packages/Paket/)
[![Join the chat at https://gitter.im/fsprojects/Paket](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/fsprojects/Paket?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Twitter](https://img.shields.io/badge/Twitter-PaketManager-blue.svg)](https://twitter.com/PaketManager)

# Paket

A dependency manager for .NET with support for NuGet packages and git repositories.

## Why Paket?

NuGet does not separate out the concept of transitive dependencies.
If you install a package into your project and that package has further dependencies then all transitive packages are included in the packages.config.
There is no way to tell which packages are only transitive dependencies.

Even more importantly: If two packages reference conflicting versions of a package, NuGet will silently take the latest version ([read more](https://fsprojects.github.io/Paket/controlling-nuget-resolution.html)). You have no control over this process.

Paket on the other hand maintains this information on a consistent and stable basis within the [`paket.lock` file][7] in the solution root.
This file, together with the [`paket.dependencies` file][8] enables you to determine exactly what's happening with your dependencies.

Paket also enables you to [reference files directly from git][9] repositories or any [http-resource][11].

For more reasons see the [FAQ][10].

## Online resources

 - [Source code][1]
 - [Documentation][2]
 - [Getting started guide](https://fsprojects.github.io/Paket/getting-started.html)
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

If you want to contribute to the [docs][2] then please modify the markdown files in `/docs/content` and send a pull request.
Note, that short description and syntax for each command is generated automatically from the `Paket.Commands` module.

## License

The [MIT license][6]

Long path support provided by [PRI.LongPath](https://github.com/peteraritchie/longPath) ([LGPL](src/PRI.LongPath/PRI.LongPath/LICENSE))


## Maintainer(s)

- [@forki](https://github.com/forki)
- [@agross](https://github.com/agross)
- [@cloudroutine](https://github.com/cloudroutine)
- [@matthid](https://github.com/matthid)
- [@isaacabraham](https://github.com/isaacabraham)
- [@theimowski](https://github.com/theimowski)

The default maintainer account for projects under "fsprojects" is [@fsprojectsgit](https://github.com/fsprojectsgit) - F# Community Project Incubation Space (repo management)

 [1]: https://github.com/fsprojects/Paket/
 [2]: https://fsprojects.github.io/Paket/
 [3]: https://github.com/fsprojects/Paket/releases/latest
 [4]: https://github.com/fsprojects/Paket/issues
 [5]: https://twitter.com/PaketManager
 [6]: https://github.com/fsprojects/Paket/blob/master/LICENSE.txt
 [7]: https://fsprojects.github.io/Paket/lock-file.html
 [8]: https://fsprojects.github.io/Paket/dependencies-file.html
 [9]: https://fsprojects.github.io/Paket/git-dependencies.html
 [10]: https://fsprojects.github.io/Paket/faq.html
 [11]: https://fsprojects.github.io/Paket/http-dependencies.html
 [badge-pr-stats]: https://www.issuestats.com/github/fsprojects/Paket/badge/pr
 [badge-issue-stats]: https://www.issuestats.com/github/fsprojects/Paket/badge/issue
 [link-issue-stats]: https://www.issuestats.com/github/fsprojects/Paket
