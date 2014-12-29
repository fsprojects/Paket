#### 0.20.0 - 29.12.2014
* `paket install` performs a selective update based on the changes in the dependencies file - http://fsprojects.github.io/Paket/lock-file.html#Performing-updates
* Paket.exe acquires a lock for all write processes - https://github.com/fsprojects/Paket/pull/469
* New command to add credentials - http://fsprojects.github.io/Paket/paket-config-file.html#Add-credentials
* Smarter conditional NuGet dependencies - https://github.com/fsprojects/Paket/pull/462
* If environment auth variables are empty a fallback to the config is used- https://github.com/fsprojects/Paket/pull/459
* Better handling for multiple files from same GitHub repository - https://github.com/fsprojects/Paket/pull/451
* Extend Public API for plugin
* BUGFIX: Remove parsing of invalid child element of ProjectReference - https://github.com/fsprojects/Paket/pull/453
* BUGFIX: Don't add NuGet packages twice to a references file - https://github.com/fsprojects/Paket/pull/460
* BUGFIX: Use Max strategy for `paket outdated --ingore-constraints` - https://github.com/fsprojects/Paket/pull/463
* BUGFIX: Don't delete downloaded github zip file
* BUGFIX: Cannot install nuget packages from local TeamCity feeds due to proxy - https://github.com/fsprojects/Paket/pull/482
* BUGFIX: Don't touch framework assemblies if not needed
* BUGFIX: Check versions file synchronously
* BUGFIX: Restore console color after handling exception - https://github.com/fsprojects/Paket/pull/467
* COSMETICS: `>= 0` version range simplified to empty string - https://github.com/fsprojects/Paket/pull/449
* COSMETICS: Paket.exe and paket.bootstrapper.exe have a logo - https://github.com/fsprojects/Paket/pull/473

#### 0.18.0 - 09.12.2014
* Show command help on `--help` - https://github.com/fsprojects/Paket/pull/437
* Allow to opt in to BindingRedirects - https://github.com/fsprojects/Paket/pull/436
* Don't run simplify in strict mode - https://github.com/fsprojects/Paket/pull/443
* Allow to remove NuGet packages in interactive mode - https://github.com/fsprojects/Paket/pull/432
* Added auto-unzip of downloaded archives - https://github.com/fsprojects/Paket/pull/430
* Allow to reference binary files via http reference - https://github.com/fsprojects/Paket/pull/427
* Faster BindingRedirects - https://github.com/fsprojects/Paket/pull/414
* Using a different FSharp.Core NuGet package - https://github.com/fsprojects/Paket/pull/416
* Find the paket.references file in upper directories - https://github.com/fsprojects/Paket/pull/409
* Allow `paket.references` files in upper directories - https://github.com/fsprojects/Paket/pull/403
* Clear failure message for `paket simplify`, when lock file is outdated - https://github.com/fsprojects/Paket/pull/403
* BUGFIX: `Selective update` updates only dependent packages - https://github.com/fsprojects/Paket/pull/410
* BUGFIX: If there are only prereleases we should just take these
* BUGFIX: `paket update nuget <name>` fails if <name> was not found in lockfile - https://github.com/fsprojects/Paket/issues/404
* BUGFIX: Unescape library filename - https://github.com/fsprojects/Paket/pull/412
* BUGFIX: Allow to reference multiple files from same repository directory - https://github.com/fsprojects/Paket/pull/445
* BUGFIX: Don't reference satellite assemblies - https://github.com/fsprojects/Paket/pull/444
* BUGFIX: Binding redirect version is picked from highest library version - https://github.com/fsprojects/Paket/pull/422
* BUGFIX: Handle numeric part of PreRelease identifiers correctly - https://github.com/fsprojects/Paket/pull/426
* BUGFIX: Fixed casing issue in selective update - https://github.com/fsprojects/Paket/pull/434
* BUGFIX: Parse http links from lockfile
* BUGFIX: Calculate dependencies file name for http resources - https://github.com/fsprojects/Paket/pull/428

#### 0.17.0 - 29.11.2014
* FrameworkHandling: Support more portable profiles and reduce the impact in the XML file
* FrameworkHandling: support extracting Silverlight5.0 and NetCore4.5 - https://github.com/fsprojects/Paket/pull/389
* New command `paket init` - http://fsprojects.github.io/Paket/paket-init.html
* Better error message for missing files in paket.lock file - https://github.com/fsprojects/Paket/pull/402
* BUGFIX: Crash on 'install' when input seq was empty - https://github.com/fsprojects/Paket/pull/395
* BUGFIX: Handle multiple version results from NuGet - https://github.com/fsprojects/Paket/pull/393

#### 0.16.0 - 23.11.2014
* Integrate BindingRedirects into Paket install process - https://github.com/fsprojects/Paket/pull/383
* BUGFIX: Download of GitHub files should clean it's own directory - https://github.com/fsprojects/Paket/issues/385
* BUGFIX: Don't remove custom framework references - https://github.com/fsprojects/Paket/issues/376
* BUGFIX: Path to dependencies file is now relative after `convert-from-nuget` - https://github.com/fsprojects/Paket/pull/379
* BUGFIX: Restore command in targets file didn't work with spaces in paths - https://github.com/fsprojects/Paket/issues/375
* BUGFIX: Detect FrameworkReferences without restrictions in nuspec file and install these
* BUGFIX: Read sources even if we don't find packages - https://github.com/fsprojects/Paket/issues/372

#### 0.15.0 - 19.11.2014
* Allow to use basic framework restrictions in NuGet packages - https://github.com/fsprojects/Paket/issues/307
* Support feeds that don't support NormalizedVersion - https://github.com/fsprojects/Paket/issues/361
* BUGFIX: Use Nuget v2 as fallback
* BUGFIX: Accept and normalize versions like 6.0.1302.0-Preview - https://github.com/fsprojects/Paket/issues/364
* BUGFIX: Fixed handling of package dependencies containing string "nuget" - https://github.com/fsprojects/Paket/pull/363

#### 0.14.0 - 14.11.2014
* Uses Nuget v3 API, which enables much faster resolver
* BUGFIX: Keep project file order similar to VS order
* Support unlisted dependencies if nothing else fits - https://github.com/fsprojects/Paket/issues/327

#### 0.13.0 - 11.11.2014
* New support for general HTTP dependencies - http://fsprojects.github.io/Paket/http-dependencies.html
* New F# Interactive support - http://fsprojects.github.io/Paket/reference-from-repl.html
* New `paket find-refs` command - http://fsprojects.github.io/Paket/paket-find-refs.html 
* Migration of NuGet source credentials during `paket convert-from-nuget` - http://fsprojects.github.io/Paket/paket-convert-from-nuget.html#Migrating-NuGet-source-credentials
* Bootstrapper uses .NET 4.0 - https://github.com/fsprojects/Paket/pull/355
* Adding --ignore-constraints to `paket outdated` - https://github.com/fsprojects/Paket/issues/308 
* PERFORMANCE: If `paket add` doesn't change the `paket.dependencies` file then the resolver process will be skipped
* BUGFIX: `paket update nuget [PACKAGENAME]` should use the same update strategy as `paket add` - https://github.com/fsprojects/Paket/issues/330
* BUGFIX: Trailing whitespace is ignored in `paket.references`

#### 0.12.0 - 07.11.2014
* New global paket.config file - http://fsprojects.github.io/Paket/paket-config-file.html
* Trace warning when we replace NuGet.exe with NuGet.CommandLine - https://github.com/fsprojects/Paket/issues/320
* Allow to parse relative NuGet folders - https://github.com/fsprojects/Paket/issues/317
* When paket skips a framework install because of custom nodes it shows a warning - https://github.com/fsprojects/Paket/issues/316
* Remove the namespaces from the nuspec parser - https://github.com/fsprojects/Paket/pull/315
* New function which extracts the TargetFramework of a given projectfile.
* New function which calculates dependencies for a given projectfile.
* Project output type can be detected from a project file
* Allow to retrieve inter project dependencies from a project file
* BUGFIX: Exclude unlisted NuGet packages in Resolver - https://github.com/fsprojects/Paket/issues/327
* BUGFIX: Detect Lib vs. lib folder on Linux - https://github.com/fsprojects/Paket/issues/332
* BUGFIX: Paket stopwatch was incorrect - https://github.com/fsprojects/Paket/issues/326
* BUGFIX: Paket failed on generating lockfile for LessThan version requirement - https://github.com/fsprojects/Paket/pull/314
* BUGFIX: Don't match suffixes in local NuGet packages - https://github.com/fsprojects/Paket/issues/317
* BUGFIX: Don't fail with NullReferenceException when analyzing nuget.config - https://github.com/fsprojects/Paket/issues/319

#### 0.11.0 - 29.10.2014
* Build a merged install model with all packages - https://github.com/fsprojects/Paket/issues/297
* `paket update` command allows to set a version - http://fsprojects.github.io/Paket/paket-update.html#Updating-a-single-package
* `paket.targets` is compatible with specific references files - https://github.com/fsprojects/Paket/issues/301
* BUGFIX: Paket no longer leaves indirect dependencies in lockfile after remove command - https://github.com/fsprojects/Paket/pull/306 
* BUGFIX: Don't use "global override" for selective update process - https://github.com/fsprojects/Paket/issues/310
* BUGFIX: Allow spaces in quoted parameter parsing - https://github.com/fsprojects/Paket/pull/311

#### 0.10.0 - 24.10.2014
* Initial version of `paket remove` command - http://fsprojects.github.io/Paket/paket-remove.html
* Paket add doesn't fail on second attempt - https://github.com/fsprojects/Paket/issues/295
* Report full paths when access is denied - https://github.com/fsprojects/Paket/issues/242
* Visual Studio restore only restores for the current project
* BUGFIX: Selective update keeps all other versions
* BUGFIX: Install process accepts filenames with `lib`
* BUGFIX: Fix !~> resolver
* BUGFIX: Use normal 4.0 framework libs when we only specify net40
* BUGFIX: Fix timing issue with paket install --hard - https://github.com/fsprojects/Paket/issues/293
* BUGFIX: Fix namespace handling in nuspec files
* BUGFIX: Add default nuget source to dependencies file if original project has no source

#### 0.9.0 - 22.10.2014
* Allow to restore packages from paket.references files - http://fsprojects.github.io/Paket/paket-restore.html
* Detect local nuspec with old XML namespace - https://github.com/fsprojects/Paket/issues/283
* `paket add` command tries to keep all other packages stable.
* Added another profile mapping for Profile136 - https://github.com/fsprojects/Paket/pull/262
* More portable profiles - https://github.com/fsprojects/Paket/issues/281
* Added net11 to framework handling - https://github.com/fsprojects/Paket/pull/269
* Create references for Win8 - https://github.com/fsprojects/Paket/issues/280
* Detect VS automatic nuget restore and create paket restore - http://fsprojects.github.io/Paket/paket-convert-from-nuget.html#Automated-process
* `paket convert-from-nuget` doesn't duplicate paket solution items - https://github.com/fsprojects/Paket/pull/286
* BUGFIX: Paket removes old framework references if during install - https://github.com/fsprojects/Paket/issues/274
* BUGFIX: Don't let the bootstrapper fail if we already have a paket.exe
* BUGFIX: Use the Id property when NuGet package name and id are different - https://github.com/fsprojects/Paket/issues/265

#### 0.8.0 - 15.10.2014
* Smarter install in project files
* Paket handles .NET 4.5.2 and .NET 4.5.3 projects - https://github.com/fsprojects/Paket/issues/260
* New command: `paket update nuget <package id>` - http://fsprojects.github.io/Paket/paket-update.html#Updating-a-single-package
* BUGFIX: Do not expand auth when serializing dependencies file - https://github.com/fsprojects/Paket/pull/259
* BUGFIX: Create catch all case for unknown portable frameworks

#### 0.7.0 - 14.10.2014
* Initial support for referencing full github projects - http://fsprojects.github.io/Paket/http-dependencies.html#Referencing-a-GitHub-repository
* Allow to use all branches in GitHub sources - https://github.com/fsprojects/Paket/pull/249
* Initial support for frameworkAssemblies from nuspec - https://github.com/fsprojects/Paket/issues/241
* Download github source files with correct encoding - https://github.com/fsprojects/Paket/pull/248
* Add FSharp.Core.Microsoft.Signed as dependency
* Install model uses portable versions for net40 and net45 when package doesn't contain special versions
* Install command displays existing versions if constraint does not match any version 
* Restore command doesn't calc install model. 
* Use https in DefaultNugetStream - https://github.com/fsprojects/Paket/pull/251
* BUGFIX: Paket only deletes files which will are downloaded by init-auto-restore process - https://github.com/fsprojects/Paket/pull/254
* BUGFIX: Paket convert-from-nuget failed when package source keys contain invalid XML element chars  - https://github.com/fsprojects/Paket/issues/253

#### 0.6.0 - 11.10.2014
* New restore command - http://fsprojects.github.io/Paket/paket-restore.html
* Report if we can't find packages for top level dependencies.
* Faster resolver
* Try /FindPackagesById before /Packages for nuget package version no. retrieval
* New Paket.Core package on NuGet - https://www.nuget.org/packages/Paket.Core/
* BUGFIX: Prefer full platform builds over portable builds

#### 0.5.0 - 09.10.2014
* Bootstrapper will only download stable releases by default - http://fsprojects.github.io/Paket/bootstrapper.html
* New installer model allows better compatibility with NuGet and should be much faster
* Supporting dot for references file - http://fsprojects.github.io/Paket/http-dependencies.html
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
* New --no-auto-restore option for `convert-from-nuget` command - http://fsprojects.github.io/Paket/paket-convert-from-nuget.html#Automated-process
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
* Resolve dependencies for github modules - http://fsprojects.github.io/Paket/http-dependencies.html#Remote-dependencies
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
* Improved convert-from-nuget command - http://fsprojects.github.io/Paket/paket-convert-from-nuget.html
* New paket.bootstrapper.exe (7KB) allows to download paket.exe from github.com - http://fsprojects.github.io/Paket/paket-init-auto-restore.html
* New package resolver algorithm
* Better verbose mode - use -v flag
* Version info is shown at paket.exe start
* paket.lock file is sorted alphabetical (case-insensitive) 
* Linked source files now all go underneath a "paket-files" folder.
* BUGFIX: Ensure the NuGet cache folder exists
* BUGFIX: Async download fixed on mono

#### 0.2.0 - 17.09.2014
* Allow to directly link GitHub files - http://fsprojects.github.io/Paket/http-dependencies.html
* Automatic NuGet conversion - http://fsprojects.github.io/Paket/paket-convert-from-nuget.html
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
