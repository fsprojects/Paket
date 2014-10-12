#### 0.6.8 - 12.10.2014
* Use https in DefaultNugetStream - https://github.com/fsprojects/Paket/pull/251
* Allow to use all branches in GitHub sources - https://github.com/fsprojects/Paket/pull/249

#### 0.6.7 - 12.10.2014
* Download github source files with correct encoding - https://github.com/fsprojects/Paket/pull/248

#### 0.6.6 - 12.10.2014
* Add FSharp.Core.Microsoft.Signed as dependency

#### 0.6.5 - 11.10.2014
* Install model uses portable versions for net40 and net45 when package doesn't contain special versions

#### 0.6.4 - 11.10.2014
* Paket install displays versions found if constraint does not match any available version 

#### 0.6.3 - 11.10.2014
* Restore doesn't need to calc install model. 

#### 0.6.0 - 11.10.2014
* New restore command - http://fsprojects.github.io/Paket/paket-restore.html
* Report if we can't find packages for top level dependencies.
* Faster resolver
* New Paket.Core package on NuGet - https://www.nuget.org/packages/Paket.Core/
* BUGFIX: Prefer full platform builds over portable builds

#### 0.5.2 - 10.10.2014
* Try /FindPackagesById before /Packages for nuget package version no. retrieval

#### 0.5.0 - 09.10.2014
* Bootstrapper will only download stable releases by default - http://fsprojects.github.io/Paket/bootstrapper.html
* New installer model allows better compatibility with NuGet and should be much faster
* Supporting dot for references file - http://fsprojects.github.io/Paket/github-dependencies.html
* Supporting pagination for long NuGet feeds - https://github.com/fsprojects/Paket/issues/223
* Create a "use exactly this version" operator in order to override package conflicts - http://fsprojects.github.io/Paket/nuget-dependencies.html#Use-exactly-this-version-constraint
* New `content none` mode in paket.dependencies - http://fsprojects.github.io/Paket/dependencies-file.html#No-content-option
* Allow source files in content folder of NuGet packages
* No -D needed for Linux installer - https://github.com/fsprojects/Paket/pull/210
* Content files like `_._`, `*.transform` and `*.pp` are ignored - https://github.com/fsprojects/Paket/issues/207
* The `convert-from-nuget` command adds .paket folder to the sln - https://github.com/fsprojects/Paket/issues/206
* Removed duplicate indirect dependencies from lock file - https://github.com/fsprojects/Paket/issues/200
* If the package download failed Paket retries with force flag
* The `convert-from-nuget` commands sorts the dependencies file
* Use credentials from nuget.config on paket convert-from-nuget - https://github.com/fsprojects/Paket/issues/198
* Deploy fixed targets file - https://github.com/fsprojects/Paket/issues/172
* New [--pre] and [--strict] modes for paket outdated - http://fsprojects.github.io/Paket/paket-outdated.html 
* New --no-auto-restore option for `convert-from-nuget` command - http://fsprojects.github.io/Paket/convert-from-nuget.html#Automated-process
* Adding support for new portable profiles
* paket.exe is now signed
* Allow to reference .exe files from NuGet packages
* Use default proxy in paket.exe and bootstrapper.exe - https://github.com/fsprojects/Paket/issues/226
* Keep order of sources in paket.dependencies - https://github.com/fsprojects/Paket/issues/233
* BREAKING CHANGE: Removed --dependencies-file option - from now on it's always paket.dependencies
* BUGFIX: Bootstrapper will not throw NullReferenceException on broken paket.exe downloads
* BUGFIX: Authentication information will not be put in cache
* BUGFIX: Fixes cache issue when using multiple NuGet sources
* BUGFIX: Fixes potential casing issue on Windows
* BUGFIX: paket-files need to go to the top of a project file
* BUGFIX: Do not look for MinimalVisualStudioVersion when adding paket folder to solution - https://github.com/fsprojects/Paket/pull/221
* COSMETICS: Throw better error message if we don't get any versions from NuGet source

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