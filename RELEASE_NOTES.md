#### 0.4.11 - 30.09.2014
* Support log4net

#### 0.4.10 - 29.09.2014
* Use credentials from nuget.config on paket convert-fro-nuget - https://github.com/fsprojects/Paket/issues/198

#### 0.4.9 - 29.09.2014
* Deploy fixed targets file - https://github.com/fsprojects/Paket/issues/172

#### 0.4.8 - 29.09.2014
* Executable libs are also added to project file

#### 0.4.7 - 29.09.2014
* Store source authentication options in paket.dependencies only

#### 0.4.6 - 29.09.2014
* Don't look for auth in cache 

#### 0.4.5 - 29.09.2014
* New [--pre] and [--strict] modes for paket outdated - http://fsprojects.github.io/Paket/paket-outdated.html 

#### 0.4.3 - 29.09.2014
* Cache package source - Fixes issue with multiple sources

#### 0.4.2 - 29.09.2014
* New --no-auto-restore option for convert-from-nuget command - http://fsprojects.github.io/Paket/convert-from-nuget.html#Automated-process

#### 0.4.1 - 29.09.2014
* Adding support for portable-net45+wp80+win8+wpa81

#### 0.4.0 - 28.09.2014
* Resolve dependencies for github modules - http://fsprojects.github.io/Paket/github-dependencies.html#Remote-dependencies
* New [--interactive] mode for paket simplify - http://fsprojects.github.io/Paket/paket-simplify.html
* Don't use version in path for github files.
* Better error message when a package resolution conflict arises.

#### 0.3.0 - 25.09.2014
* New command: paket add [--interactive] - http://fsprojects.github.io/Paket/paket-add.html
* New command: paket simplify - http://fsprojects.github.io/Paket/paket-simplify.html
* Better Visual Studio integration by using paket.targets file - http://fsprojects.github.io/Paket/paket-init-auto-restore.html
* Support for NuGet prereleases - http://fsprojects.github.io/Paket/nuget-dependencies.html#PreReleases
* Support for private NuGet feeds - http://fsprojects.github.io/Paket/nuget-dependencies.html#NuGet-feeds
* New NuGet package version constraints - http://fsprojects.github.io/Paket/nuget-dependencies.html#Further-version-constraints
* Respect case sensitivity for package paths for Linux - https://github.com/fsprojects/Paket/pull/137
* Improved convert-from-nuget command - http://fsprojects.github.io/Paket/convert-from-nuget.html
* New paket.bootstrapper.exe (7KB) allows to download paket.exe from github.com - http://fsprojects.github.io/Paket/paket-init-auto-restore.html
* New package resolver algorithm
* Better verbose mode - use -v flag
* Version info is shown at paket.exe start
* paket.lock file is sorted alphabetical (case-insensitive) 
* Linked source files now all go underneath a "paket-files" folder.
* BUGFIX: Ensure the NuGet cache folder exists
* BUGFIX: Async download fixed on mono

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