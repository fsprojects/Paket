#### 0.3.0-alpha008 - 23.09.2014
* New command: paket add [--interactive] - http://fsprojects.github.io/Paket/paket-add.html
* New command: paket simplify - http://fsprojects.github.io/Paket/paket-simplify.html
* New package resolver algorithm
* Support for private NuGet feeds
* Better verbose mode - use -v flag
* Version info is shown at start
* Respect case sensitivity for package paths for Linux - https://github.com/fsprojects/Paket/pull/137
* Better Visual Studio integration by using paket.targets file - http://fsprojects.github.io/Paket/paket-init-auto-restore.html
* Improved convert-from-nuget command - http://fsprojects.github.io/Paket/convert-from-nuget.html
* paket.lock file is sorted alphabetical (case-insensitive)
* New paket.bootstrapper.exe (7KB) allows to download paket.exe from github.com
* BUGFIX: Ensure the NuGet cache folder exists
* BUGFIX: Async download fixed on mono
* New NuGet package version constraints - http://fsprojects.github.io/Paket/nuget-dependencies.html#Further-version-constraints
* Linked source files now all go underneath a "paket-files" folder.

#### 0.2.0 - 17.09.2014
* Allow to directly link GitHub files - http://fsprojects.github.io/Paket/github-dependencies.html
* Automatic NuGet conversion - http://fsprojects.github.io/Paket/convert-from-nuget.html
* Cleaner syntax in paket.dependencies - https://github.com/fsprojects/Paket/pull/95
* Strict mode - https://github.com/fsprojects/Paket/pull/104
* Detecting portable profiles
* Support content files from nuget - https://github.com/fsprojects/Paket/pull/84
* Package names in Dependencies file are no longer case-sensitive - https://github.com/fsprojects/Paket/pull/108

#### 0.1.4 - 16.09.2014
* Only vbproj, csproj and fsproj files are handled

#### 0.1.3 - 15.09.2014
* Detect FSharpx.Core in packages

#### 0.1.2 - 15.09.2014
* --hard parameter allows better transition from NuGet.exe

#### 0.1.0 - 12.09.2014
* We are live - yay!