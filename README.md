# Paket

A package dependency manager for .NET with support for NuGet packages and GitHub files.

## Online resources

 - NuGet package [![NuGet Status](http://img.shields.io/nuget/v/Paket.svg?style=flat)](https://www.nuget.org/packages/Paket/)
 - [Source code][1]
 - [Documentation][2]
 - [Download][3]

 [1]: https://github.com/fsprojects/Paket/
 [2]: http://fsprojects.github.io/Paket/
 [3]: https://github.com/fsprojects/Paket/releases/latest

## Troubleshooting and support

 - Found a bug or missing a feature? Feed the [issue tracker][4]
 - Announcements and related miscellanea through Twitter ([@PaketManager][5])
 
 [4]: https://github.com/fsprojects/Paket/issues
 [5]: http://twitter.com/PaketManager

## Build the project

|  |  BuildScript | Status of last build |
| :------ | :------: | :------: |
| **Mono** | [build.sh](https://github.com/fsprojects/Paket/blob/master/build.sh) | [![Travis build status](https://travis-ci.org/fsprojects/Paket.png)](https://travis-ci.org/fsprojects/Paket) |
| **Windows** | [build.cmd](https://github.com/fsprojects/Paket/blob/master/build.cmd) | [![AppVeyor Build status](https://ci.appveyor.com/api/projects/status/aqs8eux16x4g5p47/branch/master)](https://ci.appveyor.com/project/SteffenForkmann/paket/branch/master) |

## Quick contributing guide

 - Fork and clone locally.
 - To build the solution, run the ```build.cmd``` (Windows) or ```build.sh``` (Mono). This will:
	- download all dependencies using Paket
	- build Paket and the tests
	- run all tests
 - Create a topic specific branch in git. Add a nice feature in the code. Do not forget to add tests.
 - Run the build to make sure everything still passes.
 - Send a Pull Request.

If you want to contribute to the [docs][2] then please send a pull request to the markdown files in `/docs/content`.

## License

The [MIT license][6]

 [6]: https://github.com/fsprojects/Paket/blob/master/LICENSE.txt