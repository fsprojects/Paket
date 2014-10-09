#### 0.5.0-alpha008 - 09.10.2014
* Use default proxy in paket.exe and bootstrapper.exe - https://github.com/fsprojects/Paket/issues/226

#### 0.5.0-alpha006 - 09.10.2014
* Detect _._ files
* Removed --dependencies-file option
* Signing the assembly  

#### 0.5.0-alpha005 - 08.10.2014
* Trying new installer

#### 0.4.31 - 08.10.2014
* Supporting dot for references file - http://fsprojects.github.io/Paket/github-dependencies.html

#### 0.4.29 - 07.10.2014
* Supporting pagination for long nuget feeds - https://github.com/fsprojects/Paket/issues/223

#### 0.4.26 - 06.10.2014
* Create a "use exactly this version" operator in order to override package conflicts - http://fsprojects.github.io/Paket/nuget-dependencies.html#Use-exactly-this-version-constraint

#### 0.4.25 - 06.10.2014
* Throw if we don't get any versions

#### 0.4.23 - 06.10.2014
* BUGFIX: paket-files need to go to the top

#### 0.4.22 - 06.10.2014
* BUGFIX: Do not look for MinimalVisualStudioVersion when adding paket folder to solution - https://github.com/fsprojects/Paket/pull/221
* BUGFIX: Fix path in content link

#### 0.4.21 - 06.10.2014
* Content none mode - http://fsprojects.github.io/Paket/dependencies-file.html#No-content-option
* Allow source files in content
* No -D needed for Linux installer - https://github.com/fsprojects/Paket/pull/210

#### 0.4.20 - 02.10.2014
* Fix potential casing issue on windows

#### 0.4.19 - 01.10.2014
* Content files like `_._`, `*.transform` and `*.pp` are ignored - https://github.com/fsprojects/Paket/issues/207

#### 0.4.16 - 30.09.2014
* paket convert-from-nuget adds .paket folder to the sln - https://github.com/fsprojects/Paket/issues/206
 
#### 0.4.15 - 30.09.2014
* Removed duplicate indirect dependencies from lock file - https://github.com/fsprojects/Paket/issues/200

#### 0.4.14 - 30.09.2014
* Automatic retry with force flag if the package download failed

#### 0.4.13 - 30.09.2014
* paket convert-from-nuget sorts the dependencies file

#### 0.4.11 - 30.09.2014
* Support log4net

#### 0.4.10 - 29.09.2014
* Use credentials from nuget.config on paket convert-from-nuget - https://github.com/fsprojects/Paket/issues/198

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