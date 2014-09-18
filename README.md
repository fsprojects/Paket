# Paket

A package dependency manager for .NET with support for NuGet packages and GitHub files.

## Online resources

 - NuGet package [![NuGet Status](http://img.shields.io/nuget/v/Paket.svg?style=flat)](https://www.nuget.org/packages/Paket/)
 - [Source code][1]
 - [Documentation][2]

 [1]: https://github.com/fsprojects/Paket/
 [2]: http://fsprojects.github.io/Paket/

## Troubleshooting and support

 - Found a bug or missing a feature? Feed the [issue tracker][3]
 - Announcements and related miscellanea through Twitter ([@PaketManager][4])

 [3]: https://github.com/fsprojects/Paket/issues
 [4]: http://twitter.com/PaketManager

## Build the project

|  |  BuildScript | Status of last build |
| :------ | :------: | :------: |
| **Mono** | [build.sh](https://github.com/fsprojects/Paket/blob/master/build.sh) | [![Travis build status](https://travis-ci.org/fsprojects/Paket.png)](https://travis-ci.org/fsprojects/Paket) |
| **Windows** | [build.cmd](https://github.com/fsprojects/Paket/blob/master/build.cmd) | [![AppVeyor Build status](https://ci.appveyor.com/api/projects/status/aqs8eux16x4g5p47/branch/master)](https://ci.appveyor.com/project/SteffenForkmann/paket/branch/master) |

## Quick contributing guide

 - Fork and clone locally.
 - To build the solution, run the ```build.cmd``` (Windows) or ```build.sh``` (Mono) file to retrieve dependencies.
 - Create a topic specific branch in git. Add some nice feature in the code. Do not forget to add tests.
 - Run the build to make sure everything passes.
 - Send a Pull Request and celebrate!

If you want to contribute to the [docs][2] then please send a pull request to the markdown files in `/docs/content`.

## License

The [MIT license][5]

 [5]: https://github.com/fsprojects/Paket/blob/master/LICENSE.txt