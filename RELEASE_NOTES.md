#### 2.42.1 - 10.01.2016
* PERFORMANCE: Try relaxed resolver only when there is a chance

#### 2.42.0 - 10.01.2016
* Nemerle projects support
* BUGFIX: Incorrect package dependencies graph resolution with prereleases - https://github.com/fsprojects/Paket/pull/1359
* BUGFIX: NuGetV2: avoid revealing password also if more than one source is defined - https://github.com/fsprojects/Paket/pull/1357

#### 2.41.0 - 07.01.2016
* Allow to reference dlls from HTTP resources - https://github.com/fsprojects/Paket/issues/1341
* BUGFIX: Fixed prerelease comparision - https://github.com/fsprojects/Paket/issues/1316
* BUGFIX: Fixed problem with prerelease versions during pack - https://github.com/fsprojects/Paket/issues/1316
* BUGFIX: Do not copy dlls from paket-files - https://github.com/fsprojects/Paket/issues/1341
* BUGFIX: Fixed problem with @ char in paths during pack - https://github.com/fsprojects/Paket/pull/1351
* BUGFIX: Allow to reference dlls from HTTP resources on mono - https://github.com/fsprojects/Paket/pull/1349
* PERFORMANCE: Don't parse lock file in FullUpdate mode
* WORKAROUND: ConfigFile password encryption did not work on specific machines - https://github.com/fsprojects/Paket/pull/1347
* USABILITY: Show warning when paket.references is used in nupkg content - https://github.com/fsprojects/Paket/issues/1344
* USABILITY: Report group name in download trace - https://github.com/fsprojects/Paket/issues/1337
* USABILITY: Be more robust against flaky NuGet feeds

#### 2.40.0 - 29.12.2015
* BUGFIX: Better packaging of prerelease dependencies - https://github.com/fsprojects/Paket/issues/1316
* BUGFIX: Allow to overwrite versions in template files without id - https://github.com/fsprojects/Paket/issues/1321
* BUGFIX: Accept dotnet54 as moniker
* BUGFIX: Download file:/// to paket-files/localhost
* BUGFIX: Compare normalized Urls
* BUGFIX: Call OnCompleted in Observable.flatten - https://github.com/fsprojects/Paket/pull/1330
* BUGFIX: Allow to restore packages from private feeds - https://github.com/fsprojects/Paket/issues/1326
* PERFORMANCE: Cache which source contains versions in GetVersions - https://github.com/fsprojects/Paket/pull/1327
* PERFORMANCE: Prefer package-versions protocol for nuget.org and myget.org

#### 2.38.0 - 22.12.2015
* Support new NuGet version range for empty restrictions
* USABILITY: Don't use /odata for nuget.org or myget.org
* BUGFIX: paket pack ignored specific-version parameter - https://github.com/fsprojects/Paket/issues/1321
* COSMETICS: Better error messages in GetVersions
* COSMETICS: Normalize NuGet source feeds in lock files
* PERFORMANCE: Keep traffic for GetVersions and GetPackageDetails low

#### 2.37.0 - 21.12.2015
* New "clear-cache" command allows to clear the NuGet cache - http://fsprojects.github.io/Paket/paket-clear-cache.html
* Paket checks PackageDetails only for sources that responded with versions for a package - https://github.com/fsprojects/Paket/issues/1317
* Implemented support for specifying per-template versions in paket pack - https://github.com/fsprojects/Paket/pull/1314
* Added support for relative src link to package content - https://github.com/fsprojects/Paket/pull/1311
* BUGFIX: Fix NullReferenceException - https://github.com/fsprojects/Paket/issues/1307
* BUGFIX: Check that cached NuGet package belongs to requested package
* BUGFIX: NuGet packages with FrameworkAssembly nodes did not work - https://github.com/fsprojects/Paket/issues/1306
* Paket install did an unnecessary update when framework restriction were present - https://github.com/fsprojects/Paket/issues/1305
* COSMETICS: No need to show cache warnings

#### 2.36.0 - 10.12.2015
* Getting assembly metadata without loading the assembly - https://github.com/fsprojects/Paket/pull/1293

#### 2.35.0 - 09.12.2015
* "redirects off" skips binding redirects completely  - https://github.com/fsprojects/Paket/pull/1299

#### 2.34.0 - 07.12.2015
* BootStrapper uses named temp files - https://github.com/fsprojects/Paket/pull/1296
* Making user prompts work with stdin - https://github.com/fsprojects/Paket/pull/1292

#### 2.33.0 - 04.12.2015
* Option to force a binding redirects - https://github.com/fsprojects/Paket/pull/1290
* Use GetCustomAttributesData instead of GetCustomAttributes - https://github.com/fsprojects/Paket/issues/1289
* Don't touch app.config if we don't logically change it - https://github.com/fsprojects/Paket/issues/1248
* Normalize versions in lock file for nuget.org - https://github.com/fsprojects/Paket/issues/1282
* Using AssemblyTitle if no title is specified in a project template - https://github.com/fsprojects/Paket/pull/1285
* Binding redirects should work with multiple groups - https://github.com/fsprojects/Paket/issues/1284 
* Resolver is more tolerant with prereleases - https://github.com/fsprojects/Paket/issues/1280

#### 2.32.0 - 02.12.2015
* Provided more user-friendly messages for bootstrapper - https://github.com/fsprojects/Paket/pull/1278
* EXPERIMENTAL: Added ability to create symbol/source packages - https://github.com/fsprojects/Paket/pull/1275
* BUGFIX: Fixed coreProps root element in generated nuspec - https://github.com/fsprojects/Paket/pull/1276

#### 2.31.0 - 01.12.2015
* Add options to force Nuget source and use local file paths with bootstrapper - https://github.com/fsprojects/Paket/pull/1268
* Implement exclude parameter for pack - https://github.com/fsprojects/Paket/pull/1274
* Handle different platforms in ProjectFile.GetOutputPath - https://github.com/fsprojects/Paket/pull/1269
* Support local read-only .nupkg-files - https://github.com/fsprojects/Paket/pull/1272

#### 2.30.0 - 01.12.2015
* Switched to using Chessie Nuget package - https://github.com/fsprojects/Paket/pull/1266
* Adding .NET 4.6.1 support - https://github.com/fsprojects/Paket/issues/1270

#### 2.29.0 - 27.11.2015
* Allow specifying Nuget Source and provide option to specify parameters with config file in bootstrapper - https://github.com/fsprojects/Paket/pull/1261
* BUGFIX: Do not normalize versions since it might break Klondike - https://github.com/fsprojects/Paket/issues/1257
* COSMETICS: Better error message when lock file doesn't contain version pin - https://github.com/fsprojects/Paket/issues/1256
* COSMETICS: Show a warning when the resolver selects an unlisted version - https://github.com/fsprojects/Paket/pull/1258

#### 2.28.0 - 25.11.2015
* Reuse more of the NuGet v3 API for protocol selection
* Using new NuGet v3 protocol to retrieve unlisted packages - https://github.com/fsprojects/Paket/issues/1254
* Created installer demo - https://github.com/fsprojects/Paket/issues/1251
* Adding monoandroid41 framework moniker - https://github.com/fsprojects/Paket/pull/1245
* BUGFIX: Specifying prereleases did not work with pessimistic version constraint - https://github.com/fsprojects/Paket/issues/1252
* BUGFIX: Unlisted property get properly filled from NuGet v3 API - https://github.com/fsprojects/Paket/issues/1242
* BUGFIX: Bootstrapper compares version per SemVer - https://github.com/fsprojects/Paket/pull/1236
* PERFORMANCE: Avoid requests to teamcity that lead to server error
* USABILITY: If parsing of lock file fails Paket reports the lock file filename - https://github.com/fsprojects/Paket/issues/1247

#### 2.27.0 - 19.11.2015
* Binding redirects get cleaned during install - https://github.com/fsprojects/Paket/pull/1235
* BUGFIX: Bootstrapper compares version per SemVer - https://github.com/fsprojects/Paket/pull/1236
* BUGFIX: Do not print feed password to output - https://github.com/fsprojects/Paket/pull/1238
* USABILITY: Always write non-version into lock file to keep ProGet happy - https://github.com/fsprojects/Paket/issues/1239

#### 2.26.0 - 18.11.2015
* BUGFIX: Better parsing of framework restrictions - https://github.com/fsprojects/Paket/issues/1232
* BUGFIX: Fix props files - https://github.com/fsprojects/Paket/issues/1233
* BUGFIX: Detect AssemblyName from project file name if empty - https://github.com/fsprojects/Paket/issues/1234
* BUGFIX: Fixed issue with V3 feeds doing api requests even when the paket.lock is fully specified - https://github.com/fsprojects/Paket/pull/1231
* BUGFIX: Update ProjectFile.GetTargetProfile to work with conditional nodes - https://github.com/fsprojects/Paket/pull/1227
* BUGFIX: Putting .targets import on correct location in project files - https://github.com/fsprojects/Paket/issues/1226
* BUGFIX: Putting braces around OData conditions to work around ProGet issues - https://github.com/fsprojects/Paket/issues/1225
* USABILITY: Always write nomalized version into lock file to keep the lockfile as stable as possible
* USABILITY: Always try 3 times to download and extract a package
* USABILITY: Sets default resolver strategy for convert from nuget to None - https://github.com/fsprojects/Paket/pull/1228

#### 2.25.0 - 13.11.2015
* Unified cache implementation for V2 and V3 - https://github.com/fsprojects/Paket/pull/1222
* BUGFIX: Putting .props and .targets import on correct location in project files - https://github.com/fsprojects/Paket/issues/1219
* BUGFIX: Propagate framework restriction correctly - https://github.com/fsprojects/Paket/issues/1213
* BUGFIX: Match auth - https://github.com/fsprojects/Paket/issues/1210
* BUGFIX: Better error message when something goes wrong during package download

#### 2.24.0 - 11.11.2015
* Support for feeds that only provide NuGet v3 API - https://github.com/fsprojects/Paket/pull/1205
* BUGFIX: Made PublicAPI.ListTemplateFiles more robust - https://github.com/fsprojects/Paket/pull/1209
* BUGFIX: Allow to specify empty file patterns in paket.template
* BUGFIX: Filter excluded dependencies in template files - https://github.com/fsprojects/Paket/issues/1208
* BUGFIX: Framework dependencies were handled too strict - https://github.com/fsprojects/Paket/issues/1206

#### 2.23.0 - 09.11.2015
* Allow to exclude dependencies in template files - https://github.com/fsprojects/Paket/issues/1199
* Exposed TemplateFile types and Dependencies member - https://github.com/fsprojects/Paket/pull/1203
* Paket uses lock free version of Async.Choice
* Paket generates and parses strategy option in lock file - https://github.com/fsprojects/Paket/pull/1196
* BUGFIX: Fixed version requirement parse issue noticed in FsBlog
* USABILITY: Paket shows parsing errors in app.config files - https://github.com/fsprojects/Paket/issues/1195

#### 2.22.0 - 05.11.2015
* Paket adds binding redirect only for applicable assemblies - https://github.com/fsprojects/Paket/issues/1187
* BUGFIX: Add missing transitive dependencies after paket update - https://github.com/fsprojects/Paket/issues/1190
* BUGFIX: Work around issue with # in file names on mono - https://github.com/fsprojects/Paket/issues/1189
* USABILITY: Better error reporting when prereleases are involved - https://github.com/fsprojects/Paket/issues/1186

#### 2.21.0 - 01.11.2015
* Adding LOCKEDVERSION placeholder to templatefile - https://github.com/fsprojects/Paket/issues/1183

#### 2.20.0 - 30.10.2015
* Allow filtered updates of packages matching a regex - https://github.com/fsprojects/Paket/pull/1178
* Search for paket.references in startup directory (auto-restore feature) - https://github.com/fsprojects/Paket/pull/1179
* BUGFIX: Framework filtering for transisitve packages - https://github.com/fsprojects/Paket/issues/1182

#### 2.19.0 - 29.10.2015
* Resolver changed to breadth first search to escape more quickly from conflict situations - https://github.com/fsprojects/Paket/issues/1174
* Paket init downloads stable version of bootstraper - https://github.com/fsprojects/Paket/issues/1040
* BUGFIX: SemVer updates were broken

#### 2.18.0 - 28.10.2015
* Use branch and bound strategy to escape quickly from conflict situations - https://github.com/fsprojects/Paket/issues/1169
* Queries all feeds in parallel for package details
* New moniker monoandroid50 - https://github.com/fsprojects/Paket/pull/1171
* Reintroduced missing public API functions for docs
* USABILITY: Improved paket's conflict reporting during resolution time - https://github.com/fsprojects/Paket/pull/1168

#### 2.17.0 - 24.10.2015
* Global "oldest matching version" resolver strategy option - http://fsprojects.github.io/Paket/dependencies-file.html#Strategy-option
* Convert-from-nuget and simplify commands simplify framework restrictions if possible - https://github.com/fsprojects/Paket/pull/1159
* BUGFIX: Queries every NuGet feed in parallel and combines the results - https://github.com/fsprojects/Paket/pull/1163
* USABILITY: Give better error message when a file can't be found on a github repo - https://github.com/fsprojects/Paket/issues/1162

#### 2.16.0 - 21.10.2015
* Check that download http status code was 200
* Try to report better error when file is blocked by Firewall - https://github.com/fsprojects/Paket/pull/1155
* BUGFIX: Fixed loading of Project files on mono - https://github.com/fsprojects/Paket/pull/1149
* PERFORMANCE: Caching proxy scheme - https://github.com/fsprojects/Paket/pull/1153
* USABILITY: If caching fails Paket should recover - https://github.com/fsprojects/Paket/issues/1152

#### 2.15.1 - 17.10.2015
* BUGFIX: Fixed framework restriction filter - https://github.com/fsprojects/Paket/pull/1146
* BUGFIX: Fixed parsing of framework restrictions in lock file - https://github.com/fsprojects/Paket/pull/1144
* BUGFIX: Add monoandroid403 to be matched as Some MonoAndroid - https://github.com/fsprojects/Paket/pull/1140
* PERFORMANCE: Use locked version as prefered version when resolver strategy is min - https://github.com/fsprojects/Paket/pull/1141
* COSMETICS: Better error messages when resolver finds no matching version.
* COSMETICS: Fix error message when resolver already resolved to GlobalOverride - https://github.com/fsprojects/Paket/issues/1142

#### 2.14.0 - 15.10.2015
* BUGFIX: Handle silverlight framework identifiers comparison - https://github.com/fsprojects/Paket/pull/1138

#### 2.13.0 - 14.10.2015
* Show-Groups command - http://fsprojects.github.io/Paket/paket-show-groups.html
* BUGFIX: Fixed combine operation for framework restrictions - https://github.com/fsprojects/Paket/issues/1137
* BUGFIX: Lockfile-Parser did not to parse framework restrictions and therefore paket install could lead to wrong lock file - https://github.com/fsprojects/Paket/issues/1135
* USABILITY: Non-SemVer InformationalVersion are now allowed for paket pack - https://github.com/fsprojects/Paket/issues/1134
* USABILITY: Dependencies file parser should detects comma between install settings - https://github.com/fsprojects/Paket/issues/1129
* COSMETICS: Don't show the pin notice if dependency is transitive
* COSMETICS: Don't allow negative numbers in SemVer

#### 2.12.0 - 12.10.2015
* Better SemVer update by adding --keep-major, --keep-minor, --keep-patch to the CLI
* EXPERIMENTAL: Support for WiX installer projects

#### 2.11.0 - 09.10.2015
* Skip unchanged groups during install

#### 2.10.0 - 08.10.2015
* Make resolver to evaluate versions lazily
* BUGFIX: Paket.Pack was broken on filesystems with forward slash seperator - https://github.com/fsprojects/Paket/issues/1119
* BUGFIX: Wrong paket ProjectRefences name causes incorrect packaging - https://github.com/fsprojects/Paket/issues/1113

#### 2.9.0 - 05.10.2015
* Allow to use GitHub tokens to access GitHub files - http://fsprojects.github.io/Paket/paket-config.html
* Allow to update a single group
* BUGFIX: Resolver needs to consider Microsoft.Bcl.Build

#### 2.8.0 - 03.10.2015
* BUGFIX: Selective update needs to consider remote files
* BUGFIX: Ignore disabled upstream feeds - https://github.com/fsprojects/Paket/pull/1105
* BUGFIX: Don't forget to add settings from root dependencies
* COSMETICS: Do not write unnecessary framework restrictions into paket.lock

#### 2.7.0 - 02.10.2015
* Support for private GitHub repos - http://fsprojects.github.io/Paket/github-dependencies.html#Referencing-a-private-github-repository
* BUGFIX: Find the mono binary on OSX 10.11 - https://github.com/fsprojects/Paket/pull/1103

#### 2.6.0 - 01.10.2015
* Allow "content:once" as a package setting - http://fsprojects.github.io/Paket/nuget-dependencies.html#No-content-option
* BUGFIX: Don't add -prerelease to nuspec dependency nodes for project references - https://github.com/fsprojects/Paket/issues/1102
* BUGFIX: Do not create prerelease identifiers for transitive dependencies - https://github.com/fsprojects/Paket/issues/1099
* PERFORMANCE: Do not parse remote dependencies file twice - https://github.com/fsprojects/Paket/issues/1101
* PERFORMANCE: Check if we already downloaded paket.dependencies file for remote files in order to reduce stress on API limit - https://github.com/fsprojects/Paket/issues/1101
* PERFORMANCE: Run all calls against different NuGet protocols in parallel and take the fastest - https://github.com/fsprojects/Paket/issues/1085
* PERFORMANCE: Exclude duplicate NuGet feeds - https://github.com/fsprojects/Paket/issues/1085
* COSMETICS: Cache calls to GitHub in order to reduce stress on API limit - https://github.com/fsprojects/Paket/issues/1101

#### 2.5.0 - 29.09.2015
* Remove all Paket entries from projects which have no paket.references - https://github.com/fsprojects/Paket/issues/1097
* Allow to format VersionRequirements in NuGet syntax
* BUGFIX: Fix KeyNotFoundException when project is net4.0-client - https://github.com/fsprojects/Paket/issues/1095
* BUGFIX: Put prerelease requirement into NuSpec during paket pack - https://github.com/fsprojects/Paket/issues/1088
* BUGFIX: Inconsistent framework exclusion in paket.dependencies - https://github.com/fsprojects/Paket/issues/1093
* BUGFIX: Commands add/remove stripped link:false from file references - https://github.com/fsprojects/Paket/issues/1089
* BUGFIX: Do not create double prerelease identifiers - https://github.com/fsprojects/Paket/issues/1099
* COSMETICS: Only fixup dates in zip archive under Mono - https://github.com/fsprojects/Paket/pull/1094
* PERFORMANCE: Skip asking for versions if only a specific version is requested
* PERFORMANCE: Check if a feed supports a protocol and never retry if not - https://github.com/fsprojects/Paket/issues/1085

#### 2.4.0 - 28.09.2015
* BUGFIX: Paket does not touch config files when the list of binding redirects to add is empty - https://github.com/fsprojects/Paket/pull/1092
* BUGFIX: Fix unsupported https scheme in web proxy - https://github.com/fsprojects/Paket/pull/1080
* BUGFIX: Ignore DotNET 5.0 framework when TargetFramework 4 is specified - https://github.com/fsprojects/Paket/issues/1066
* BUGFIX: Paket failed with: The input sequence was empty - https://github.com/fsprojects/Paket/issues/1071
* BUGFIX: NullReferenceException in applyBindingRedirects during "update nuget package" - https://github.com/fsprojects/Paket/issues/1074
* COSMETICS: Improve error message for bootstrapper if download of Paket.exe fails - https://github.com/fsprojects/Paket/pull/1091

#### 2.3.0 - 21.09.2015
* Binding redirects from target platform only - https://github.com/fsprojects/Paket/pull/1070
* Allow to enable redirects per package - http://fsprojects.github.io/Paket/nuget-dependencies.html#redirects-settings
* BUGFIX: Install command without a lockfile failed when using groups - https://github.com/fsprojects/Paket/issues/1067
* BUGFIX: Only create packages.config entries for referenced packages - https://github.com/fsprojects/Paket/issues/1065
* BUGFIX: Paket update added an app.config to every project - https://github.com/fsprojects/Paket/issues/1068
* BUGFIX: Use commit w/gist download in RemoteDownload.downloadRemoteFiles - https://github.com/fsprojects/Paket/pull/1069

#### 2.1.0 - 16.09.2015
* Added support for custom internet proxy credentials with env vars - https://github.com/fsprojects/Paket/pull/1061
* Removed microsoft.bcl.build.targets from backlist and instead changed "import_targets" default for that package
* Fix handling of packages.config

#### 2.0.0 - 15.09.2015
* Support for `Dependency groups` in paket.dependencies files - http://fsprojects.github.io/Paket/groups.html
* Support for Roslyn-based analyzers - http://fsprojects.github.io/Paket/analyzers.html
* Support for reference conditions - https://github.com/fsprojects/Paket/issues/1026

#### 1.39.10 - 13.09.2015
* Fixed a bug where install and restore use different paths when specifying a project spec on a HTTP link - https://github.com/fsprojects/Paket/pull/1054
* Fix parsing of output path when condition has no spaces - https://github.com/fsprojects/Paket/pull/1058

#### 1.39.1 - 08.09.2015
* Eagerly create app.config files and add to all projects - https://github.com/fsprojects/Paket/pull/1044

#### 1.39.0 - 08.09.2015
* New Bootstrapper with better handling of Paket prereleases

#### 1.37.0 - 07.09.2015
* Support for authentication and complex hosts for HTTP dependencies - https://github.com/fsprojects/Paket/pull/1052
* Always redirect to the Redirect.Version - https://github.com/fsprojects/Paket/pull/1023
* Improvements in the BootStrapper - https://github.com/fsprojects/Paket/pull/1022

#### 1.34.0 - 27.08.2015
* Paket warns about pinned packages only when a new version is available - https://github.com/fsprojects/Paket/pull/1014
* Trace NuGet package URL if download fails
* Fallback to NuGet v2 feed if no version is found in v3

#### 1.33.0 - 23.08.2015
* Paket handles dynamic OutputPath - https://github.com/fsprojects/Paket/pull/942
* Paket warns when package is pinned - https://github.com/fsprojects/Paket/pull/999

#### 1.32.0 - 19.08.2015
* BUGFIX: Fixed compatibility issues with Klondike NuGet server - https://github.com/fsprojects/Paket/pull/997
* BUGFIX: Escape file names in a NuGet compatible way - https://github.com/fsprojects/Paket/pull/996
* BUGFIX: Paket now fails if an update of a nonexistent package is requested - https://github.com/fsprojects/Paket/pull/995

#### 1.31.0 - 18.08.2015
* BUGFIX: Delete old nodes from proj files - https://github.com/fsprojects/Paket/issues/992
* COSMETICS: Better conflict reporting - https://github.com/fsprojects/Paket/pull/994

#### 1.30.0 - 18.08.2015
* BUGFIX: Include prereleases when using NuGet3 - https://github.com/fsprojects/Paket/issues/988
* paket.template allows comments with # or // - https://github.com/fsprojects/Paket/pull/991

#### 1.29.0 - 17.08.2015
* Xamarin iOS + Mac Support - https://github.com/fsprojects/Paket/pull/980
* Handling fallbacks mainly for Xamarin against PCLs - https://github.com/fsprojects/Paket/pull/980
* Removed supported platforms for MonoTouch and MonoAndroid - https://github.com/fsprojects/Paket/pull/980
* Paket only creates requirements from lock file when updating a single package - https://github.com/fsprojects/Paket/pull/985

#### 1.28.0 - 13.08.2015
* Selective update shows better error message on conflict - https://github.com/fsprojects/Paket/pull/980
* Paket init adds default feed - https://github.com/fsprojects/Paket/pull/981
* Show better error message on conflict - https://github.com/fsprojects/Paket/issues/534
* Make option names for paket find-package-versions consistent with the other commands - https://github.com/fsprojects/Paket/issues/890
* Update specifying version does not pin version in paket.dependencies - https://github.com/fsprojects/Paket/pull/979

#### 1.27.0 - 13.08.2015
* Version range semantics changed for `>= x.y.z prerelease` - https://github.com/fsprojects/Paket/issues/976
* BUGFIX: Version trace got lost - https://twitter.com/indy9000/status/631201649219010561
* BUGFIX: copy_local behaviour was broken - https://github.com/fsprojects/Paket/issues/972

#### 1.26.0 - 10.08.2015
* BUGFIX: Paket mixed responses and downloads - https://github.com/fsprojects/Paket/issues/966

#### 1.25.0 - 10.08.2015
* Fix case-sensitivity of boostrapper on mono
* Reactive NuGet v3
* Check for conflicts in selective update - https://github.com/fsprojects/Paket/pull/964
* BUGFIX: Escape file names - https://github.com/fsprojects/Paket/pull/960

#### 1.23.0 - 04.08.2015
* BUGFIX: Selective update resolves the graph for selected package - https://github.com/fsprojects/Paket/pull/957

#### 1.22.0 - 31.07.2015
* Use FSharp.Core 4.0
* Fix build exe path which includes whitespace - https://github.com/fsprojects/ProjectScaffold/pull/185
* Preserve encoding upon saving solution - https://github.com/fsprojects/Paket/pull/940
* BUGFIX: If we specify a templatefile in paket pack it still packs all templates - https://github.com/fsprojects/Paket/pull/944
* BUGFIX: If we specify a type project templatefile in paket pack it should find the project - https://github.com/fsprojects/Paket/issues/945
* BUGFIX: Paket pack succeeded even when there're missing files - https://github.com/fsprojects/Paket/issues/948
* BUGFIX: FindAllFiles should handle paths that are longer than 260 characters - https://github.com/fsprojects/Paket/issues/949

#### 1.21.0 - 23.07.2015
* Allow NuGet packages to put version in the path - https://github.com/fsprojects/Paket/pull/928

#### 1.20.0 - 21.07.2015
* Allow to get version requirements from paket.lock instead of paket.dependencies - https://github.com/fsprojects/Paket/pull/924
* Add new ASP.NET 5.0 monikers - https://github.com/fsprojects/Paket/issues/921
* BUGFIX: Paket crashed with Null Ref Exception for MBrace - https://github.com/fsprojects/Paket/issues/923
* BUGFIX: Exclude submodules from processing - https://github.com/fsprojects/Paket/issues/918

#### 1.19.0 - 13.07.2015
* Support Odata query fallback for package details with /odata prefix - https://github.com/fsprojects/Paket/pull/922
* Establish beta-level comatibility with Klondike nuget server - https://github.com/fsprojects/Paket/pull/907
* BUGFIX: Improved SemVer parser - https://github.com/fsprojects/Paket/pull/920
* BUGFIX: Added fix for windows-style network source-paths in dependencies parser - https://github.com/fsprojects/Paket/pull/903
* BUGFIX: Settings for dependent packages are now respected - https://github.com/fsprojects/Paket/pull/919
* BUGFIX: `--force` option is working for install/update/restore remote files too
* BUGFIX: Delete cached errors if all sources fail - https://github.com/fsprojects/Paket/issues/908
* BUGFIX: Use updated globbing for paket.template
* COSMETICS: Better error message when package doesn't exist
* COSMETICS: Show better error message when a package is used in `paket.references` but not in `paket.lock`

#### 1.18.0 - 22.06.2015
* Exclusion syntax for paket.template files - https://github.com/fsprojects/Paket/pull/882
* BUGFIX: Issue with `paket pack` and multiple paket.template files fixed - https://github.com/fsprojects/Paket/issues/893

#### 1.17.0 - 22.06.2015
* Tab completion for installed packages in Paket.PowerShell - https://github.com/fsprojects/Paket/pull/892
* BUGFIX: Find-package-versions did not work - https://github.com/fsprojects/Paket/issues/886
* BUGFIX: Find-packages did not work - https://github.com/fsprojects/Paket/issues/888 https://github.com/fsprojects/Paket/issues/889
* COSMETICS: Improved the documentation for the commands - https://github.com/fsprojects/Paket/pull/891

#### 1.16.0 - 21.06.2015
* Make sure retrieved versions are ordered by version with latest version first - https://github.com/fsprojects/Paket/issues/886
* PowerShell argument tab completion for Paket-Add - https://github.com/fsprojects/Paket/pull/887
* Detection of DNX and DNXCore frameworks
* BUGFIX: Exceptions were not logged to command line - https://github.com/fsprojects/Paket/pull/885

#### 1.15.0 - 18.06.2015
* Paket.PowerShell support for Package Manager Console - https://github.com/fsprojects/Paket/pull/875
* Fix download of outdated files - https://github.com/fsprojects/Paket/issues/876

#### 1.14.0 - 14.06.2015
* Chocolatey support for Paket.PowerShell - https://github.com/fsprojects/Paket/pull/872
* BUGFIX: Single version in deps file created invalid dependend package- https://github.com/fsprojects/Paket/issues/871

#### 1.13.0 - 12.06.2015
* Paket.PowerShell support - https://github.com/fsprojects/Paket/pull/839
* EXPERIMENTAL: Allow link:false settings for file references in `paket.references` files
* BUGFIX: `paket update` did not pick latest prerelease version of indirect dependency - https://github.com/fsprojects/Paket/issues/866

#### 1.12.0 - 09.06.2015
* BUGFIX: Paket add should not update the package if it's already there
* BUGFIX: "copy_local" was not respected for indirect dependencies - https://github.com/fsprojects/Paket/issues/856
* BUGFIX: Suggest only packages from the installed sources - https://github.com/fsprojects/Paket.VisualStudio/issues/57
* BUGFIX: Trace license warning only in verbose mode - https://github.com/fsprojects/Paket/issues/862
* BUGFIX: Fix ./ issues during pack
* BUGFIX: Serialize != operator correctly - https://github.com/fsprojects/Paket/issues/857
* COSMETICS: Don't save the `paket.lock` file if it didn't changed

#### 1.11.0 - 08.06.2015
* Support for cancelling bootstrapper - https://github.com/fsprojects/Paket/pull/860
* Increase timeout for restricted access mode - https://github.com/fsprojects/Paket/issues/858

#### 1.10.0 - 02.06.2015
* `paket init` puts Paket binaries into the project path - https://github.com/fsprojects/Paket/pull/853
* Do not duplicate files in the nupkg - https://github.com/fsprojects/Paket/issues/851
* Pack command reuses project version if directly given - https://github.com/fsprojects/Paket/issues/837
* BUGFIX: `paket install` was not respecting `content:none` - https://github.com/fsprojects/Paket/issues/854

#### 1.9.0 - 30.05.2015
* Paket pack allows to specify current nuget version as dependency - https://github.com/fsprojects/Paket/issues/837
* BUGFIX: Fix long version of --silent flag - https://github.com/fsprojects/Paket/pull/849

#### 1.8.0 - 28.05.2015
* Implement --no-install and --redirects for "paket update" - https://github.com/fsprojects/Paket/pull/847
* BUGFIX: Fix inconsistent parameter names - https://github.com/fsprojects/Paket/pull/846

#### 1.7.2 - 28.05.2015
* New `--only-referenced` parameter for restore - https://github.com/fsprojects/Paket/pull/843
* Make the output path relative to the dependencies file - https://github.com/fsprojects/Paket/issues/829
* Analyze content files with case insensitive setting - https://github.com/fsprojects/Paket/issues/816
* BUGFIX: Parse NuGet package prerelease versions containing "-" - https://github.com/fsprojects/Paket/issues/841

#### 1.6.0 - 26.05.2015
* Paket init - init dependencies file with default NuGet source
* Allow to init paket in given directory
* Automatically query all package feeds in "Find packages"
* Allow to override install settings in 'paket.dependencies' with values from 'paket.references' - https://github.com/fsprojects/Paket/issues/836
* BUGFIX: `paket install` fails if package version doesn't match .nupkg file - https://github.com/fsprojects/Paket/issues/834
* BUGFIX: Try to work around issue with mono zip functions - https://github.com/fsharp/FAKE/issues/810

#### 1.5.0 - 21.05.2015
* Property tests for dependencies files parser - https://github.com/fsprojects/Paket/pull/807
* EXPERIMENTAL: Query NuGet feeds in parallel
* Allow to specify the directory for `convert-to-nuget` in PublicAPI
* Expose project Guids from project files
* Allow simplify on concrete dependencies file
* Allow to specify a concrete template file for `paket pack`
* Add overload in PublicAPI for default Restore
* Better tracing during "update package"
* Allow to register trace functions
* Allow to specify a source feed for Find-Packages and Find-Package-Versions command
* BUGFIX: Fix dates in local nuget packages
* BUGFIX: NullReferenceException in `convert-from-nuget` - https://github.com/fsprojects/Paket/pull/831
* BUGFIX: `Convert-from-nuget` quotes source feeds - https://github.com/fsprojects/Paket/pull/833
* BUGFIX: Observable.ofAsync fires OnCompleted - https://github.com/fsprojects/Paket/pull/835
* BUGFIX: Work around issue with CustomAssemblyAttributes during `paket pack` - https://github.com/fsprojects/Paket/issues/827
* BUGFIX: Fix dates after creating a package
* BUGFIX: Always trim package names from command line
* BUGFIX: Always show default nuget stream in completion

#### 1.4.0 - 08.05.2015
* EXPERIMENTAL: Find-Packages command - http://fsprojects.github.io/Paket/paket-find-packages.html
* EXPERIMENTAL: Find-Package-Versions command - http://fsprojects.github.io/Paket/paket-find-package-versions.html
* EXPERIMENTAL: Show-Installed-Packages command - http://fsprojects.github.io/Paket/paket-show-installed-packages.html
* Expose GetDefinedNuGetFeeds in Public API
* Expose GetSources in Public API
* BUGFIX: NuGet Convert works with empty version strings - https://github.com/fsprojects/Paket/pull/821
* BUGFIX: Don't shortcut conflicting addition
* BUGFIX: Better pin down behaviour during "Smart Update""
* BUGFIX: Only replace nuget package during add if the old one had no version
* BUGFIX: Put fixed packages to the end - https://github.com/fsprojects/Paket/issues/814
* BUGFIX: Fix `paket add` if package is already there - https://github.com/fsprojects/Paket/issues/814
* BUGFIX: Fix `paket add` for very first dependency - https://github.com/fsprojects/Paket/issues/814
* BUGFIX: Paket pack had issues with \ in subfolders - https://github.com/fsprojects/Paket/issues/812
* BZGFIX: Use https://api.nuget.org/v3/index.json for Autocomplete
* BUGFIX: Set exit code to 1 if the command line parser finds error
* BUGFIX: Windows restrictions were not parsed from lockfile - https://github.com/fsprojects/Paket/issues/810
* BUGFIX: Paket tries to keep the alphabetical order when using `paket add`
* BUGFIX: Do not generate entries for empty extensions in nupkg
* BUGFIX: Portable framework restrictions were not parsed from lockfile - https://github.com/fsprojects/Paket/issues/810
* COSMETICS: "Done" message in bootstrapper
* COSMETICS: -s parameter for Bootstrapper
* COSMETICS: Don't perform unnecessary installs during `paket add`
* COSMETICS: Always print the command on command parser error

#### 1.3.0 - 30.04.2015
* Paket keeps paket.dependencies as stable as possible during edits - https://github.com/fsprojects/Paket/pull/802
* `paket push` doesn't need a dependencies file any more - https://github.com/fsprojects/Paket/issues/800
* Added `--self` for self update of bootstrapper - https://github.com/fsprojects/Paket/issues/791
* BUGFIX: `convert-from-nuget` doen`t duplicate sources anymore - https://github.com/fsprojects/Paket/pull/804

#### 1.2.0 - 24.04.2015
* Add Paket.BootStrapper NuGet package - https://github.com/fsprojects/Paket/issues/790

#### 1.1.3 - 24.04.2015
* Fix StackOverflowException when using local path - https://github.com/fsprojects/Paket/issues/795

#### 1.1.2 - 24.04.2015
* `paket add` should not change dependencies file if the package is misspelled - https://github.com/fsprojects/Paket/issues/798

#### 1.1.1 - 24.04.2015
* Support developmentDependency nuget dependencies - https://github.com/fsprojects/Paket/issues/796

#### 1.1.0 - 23.04.2015
* Pack command is able to detect portable frameworks - https://github.com/fsprojects/Paket/issues/797

#### 1.0.2 - 23.04.2015
* `Convert-from-nuget` removes custom import and targets - https://github.com/fsprojects/Paket/pull/792

#### 1.0.1 - 20.04.2015
* New bootstrapper protects paket.exe from incomplete github downloads - https://github.com/fsprojects/Paket/pull/788

#### 1.0.0 - 17.04.2015
* Big release from fsharpex

#### 0.42.1 - 17.04.2015
* BUGFIX: Smart Install is no longer adding dependencies to paket.dependencies if specified in paket.references but not in paket.dependencies - https://github.com/fsprojects/Paket/issues/779
* BUGFIX: Fix smart install when we add a pinned version - https://github.com/fsprojects/Paket/issues/777
* Trace NuGet server response in verbose mode - https://github.com/fsprojects/Paket/issues/775
* BUGFIX: Fixing wrong local path detection with `paket install` - https://github.com/fsprojects/Paket/pull/773
* BUGFIX: Fixed zip opening on mono - https://github.com/fsprojects/Paket/pull/774

#### 0.41.0 - 13.04.2015
* New Testimonials page - http://fsprojects.github.io/Paket/testimonials.html
* New `PAKET.VERSION` environment variable for bootstraper - https://github.com/fsprojects/Paket/pull/771
* `convert-from-nuget` aggregates target framework from packages.config files - https://github.com/fsprojects/Paket/pull/768
* Improved config file formatting with indented binding redirects - https://github.com/fsprojects/Paket/pull/769
* BUGFIX: Fixed home path detection - https://github.com/fsprojects/Paket/pull/770
* COSMETICS: Better error message when `paket.dependencies` is missing - https://github.com/fsprojects/Paket/issues/764

#### 0.40.0 - 09.04.2015
* Try to fix dates in Nuget packages - https://github.com/fsprojects/Paket/issues/761
* `convert-from-nuget` reads target framework from packages.config files - https://github.com/fsprojects/Paket/pull/760
* Allow . in target file names for pack - https://github.com/fsprojects/Paket/issues/756

#### 0.39.0 - 08.04.2015
* Upgrading to .NET 4.5
* Removing DotNetZip and using the .NET 4.5 Zip APIs instead - https://github.com/fsprojects/Paket/pull/732
* Boostrapper download without `nuget.exe` - https://github.com/fsprojects/Paket/pull/734
* Added frameworkAssemblies to nuspec templating - https://github.com/fsprojects/Paket/issues/740
* BUGFIX: Only pick up project output files for pack that exactly match assembly filename - https://github.com/fsprojects/Paket/issues/752
* BUGFIX: Detect Silverlight version in csproj files - https://github.com/fsprojects/Paket/issues/751
* BUGFIX: Fix mono timeout during license download - https://github.com/fsprojects/Paket/issues/746
* BUGFIX: Detect `sl` as Silverlight - https://github.com/fsprojects/Paket/issues/744

#### 0.38.0 - 30.03.2015
* The restore process downloads package licenses automatically - https://github.com/fsprojects/Paket/pull/737

#### 0.37.0 - 28.03.2015
* Fallback to NuGet.exe if the bootstrapper fails to download from GitHub - https://github.com/fsprojects/Paket/pull/733
* COSMETICS: Display the file name if Paket crashes on some invalid file - https://github.com/fsprojects/Paket/pull/730

#### 0.36.0 - 27.03.2015
* Allow to add references section to paket.template file - https://github.com/fsprojects/Paket/issues/721
* Allow to compute libraries for specific framework - https://github.com/fsprojects/Paket/issues/723
* Detect .NET 4.6 - https://github.com/fsprojects/Paket/issues/727
* SemVer allows "number + build metadata" format - https://github.com/fsprojects/Paket/issues/704
* `paket push` shows status information - https://github.com/fsprojects/Paket/pull/695
* BUGFIX: Maintain order of content file items - https://github.com/fsprojects/Paket/pull/722
* BUGFIX: `Convert-from-nuget` ignores disabled NuGet feeds - https://github.com/fsprojects/Paket/pull/720
* BUGFIX: Smart install should not remove sources from `paket.dependencies` - https://github.com/fsprojects/Paket/pull/726
* BUGFIX: Smart install should create paket.lock if we have references files - https://github.com/fsprojects/Paket/pull/725
* COSMETICS: better tracing of intermediate resolution conflicts

#### 0.34.0 - 12.03.2015
* `paket pack` pretty-prints it's nuspec - https://github.com/fsprojects/Paket/issues/691
* Paket packs .MDBs docs into the nupkg - https://github.com/fsprojects/Paket/issues/693
* paket pack / paket.template support wildcard patterns - https://github.com/fsprojects/Paket/issues/690
* Allow empty lines in `paket.template` and report file name if parser fails - https://github.com/fsprojects/Paket/issues/692
* BUGFIX: paket.template - file type respects dir without slash at the end - https://github.com/fsprojects/Paket/issues/698
* BUGFIX: paket-files folder is alwaays relative to `paket.dependencies` - https://github.com/fsprojects/Paket/issues/564
* BUGFIX: `paket install` respects manual paket nodes - https://github.com/fsprojects/Paket/issues/679

#### 0.33.0 - 10.03.2015
* Paket packs XML docs into the nupkg - https://github.com/fsprojects/Paket/issues/689
* BUGFIX: Install settings from `paket.dependencies` should override package settings - https://github.com/fsprojects/Paket/issues/688

#### 0.32.0 - 09.03.2015
* PERFORMANCE: If resolver runs into conflict then use Warnsdorff's rule - https://github.com/fsprojects/Paket/pull/684
* BUGFIX: Fixed Linux install scripts - https://github.com/fsprojects/Paket/pull/681
* Support for WinExe output type - https://github.com/fsprojects/Paket/pull/675
* BUGFIX: Fix Nuget compat issue with leading zeros - https://github.com/fsprojects/Paket/pull/672
* BUGFIX: Detect inter project dependencies without matching package id - https://github.com/fsprojects/Paket/pull/671
* BUGFIX: Parse prerelease numbers into bigint since ints might overflow - https://github.com/fsprojects/Paket/pull/667
* BUGFIX: Optional fields in template files are read correctly - https://github.com/fsprojects/Paket/pull/666
* BUGFIX: Better url and endpoint handling in `paket push` - https://github.com/fsprojects/Paket/pull/663
* COSMETICS: Better tracing when resolver runs into conflict - https://github.com/fsprojects/Paket/pull/684
* COSMETICS: Better error message when a package is listed twice in `paket.references` - https://github.com/fsprojects/Paket/pull/686
* COSMETICS: Use Chessie for ROP - https://github.com/fsprojects/Chessie

#### 0.31.2 - 26.02.2015
* BUGFIX: Robust and much faster template file parser - https://github.com/fsprojects/Paket/pull/660

#### 0.31.1 - 25.02.2015
* Use latest FAKE tasks

#### 0.31.0 - 25.02.2015
* BUGFIX: Fix help for init command - https://github.com/fsprojects/Paket/pull/654
* BUGFIX: Allow non-standard API endpoint for push - https://github.com/fsprojects/Paket/pull/652
* BUGFIX: Special case nuget.org
* BUGFIX: paket add/remove with just project name - https://github.com/fsprojects/Paket/pull/650
* BUGFIX: Uploading packages as multiform content type - https://github.com/fsprojects/Paket/pull/651
* BUGFIX: Handle transient dependencies better in pack command - https://github.com/fsprojects/Paket/pull/649
* BUGFIX: Only load custom attributes if not given in TemplateFile or cmd parameter
* BUGFIX: Detect .NET 4.5.1 - https://github.com/fsprojects/Paket/pull/647

#### 0.30.0 - 23.02.2015
* New command: `paket pack` - http://fsprojects.github.io/Paket/paket-pack.html
* New command: `paket push` - http://fsprojects.github.io/Paket/paket-push.html
* Improved command line help - https://github.com/fsprojects/Paket/pull/639
* BUGFIX: fix no_auto_restore option parsing  - https://github.com/fsprojects/Paket/issues/632

#### 0.29.0 - 18.02.2015
* Allow local NuGet sources with spaces in `paket.dependencies` - https://github.com/fsprojects/Paket/issues/616
* Streamlined install options in `paket.dependencies` and `paket.references` - https://github.com/fsprojects/Paket/issues/587
* Allow to opt-out of targets import - https://github.com/fsprojects/Paket/issues/587
* New option to add/remove packages for a single project - https://github.com/fsprojects/Paket/pull/610
* BUGFIX: Blacklisted Microsoft.Bcl.Build.targets - https://github.com/fsprojects/Paket/issues/618
* BUGFIX: Selective update doesn't add package twice from `paket.references` anymore
* BUGFIX: `paket install` installs GitHub source files
* COSMETICS: Respect home directories on mono - https://github.com/fsprojects/Paket/issues/612
* COSMETICS: `paket add` inserts the new package in alphabetical position - https://github.com/fsprojects/Paket/issues/596

#### 0.28.0 - 16.02.2015
* Add a simple API which allows to retrieve NuGet v3 autocomplete
* Allow unix-style comments in `paket.dependencies` file
* BUGFIX: `paket restore` does not fail on missing `paket.version` files - https://github.com/fsprojects/Paket/issues/600
* BUGFIX: Parsing of conditional dependencies should detect portable case - https://github.com/fsprojects/Paket/issues/594
* BUGFIX: Prerelease requirements in `paket.dependencies` should override package dependencies - https://github.com/fsprojects/Paket/issues/607
* BUGFIX: Try to ease the pain with mono bug in Process class - https://github.com/fsprojects/Paket/issues/599
* BUGFIX: `paket restore` does not re-download http references - https://github.com/fsprojects/Paket/issues/592
* BUGFIX: Make DeletePaketNodes more robust - https://github.com/fsprojects/Paket/issues/591
* BUGFIX: Install content files on mono - https://github.com/fsprojects/Paket/issues/561
* BUGFIX: Install process doesn't duplicate Imports of targets files any more - https://github.com/fsprojects/Paket/issues/588
* BUGFIX: Don't remove comments from `paket.dependencies` file - https://github.com/fsprojects/Paket/issues/584
* COSMETICS: Paket should not reformat app/web.config files while changing assembly redirects - https://github.com/fsprojects/Paket/issues/597

#### 0.27.0 - 07.02.2015
* Install process will reference `.props` and `.targets` files from NuGet packages - https://github.com/fsprojects/Paket/issues/516
* Don't internalize in paket.exe during ILMerge
* Allow to download from pre-authenticated MyGet feed - https://github.com/fsprojects/Paket/issues/466
* BUGFIX: Fix `paket install --hard` for FSharp.Core - https://github.com/fsprojects/Paket/issues/579
* BUGFIX: `paket convert-from-nuget` ignores casing when looking for nuget.targets - https://github.com/fsprojects/Paket/issues/580
* BUGFIX: `paket install` correctly parses HTTP references - https://github.com/fsprojects/Paket/pull/571
* BUGFIX: `paket.dependencies` parser now fails if tokens are not valid
* COSMETICS: Prerelease strings are checked that they don't contain operators
* COSMETICS: Create an install function in the API which takes a `paket.dependencies` file as text - https://github.com/fsprojects/Paket/issues/576

#### 0.26.0 - 31.01.2015
* Allow to opt-out of old frameworks in `paket.dependencies` - http://fsprojects.github.io/Paket/nuget-dependencies.html#Framework-restrictions
* Allow `copy_local` settings in `paket.references` - http://fsprojects.github.io/Paket/references-files.html#copy_local-settings
* COSMETICS: `paket.lock` beautification for HTTP specs - https://github.com/fsprojects/Paket/pull/571

#### 0.25.0 - 25.01.2015
* BUGFIX: If more than one TargetFramework-specific dependency to the same package exist, we take the latest one - https://github.com/fsprojects/Paket/pull/567
* BUGFIX: Removes interactive-shell-check on `add auth` - https://github.com/fsprojects/Paket/pull/565
* BUGFIX: Can parse open NuGet ranges in brackets - https://github.com/fsprojects/Paket/issues/560
* BUGFIX: Detect `net35-client` - https://github.com/fsprojects/Paket/issues/559
* BUGFIX: Show help for `auto-restore` command - https://github.com/fsprojects/Paket/pull/558

#### 0.24.0 - 19.01.2015
* Allow to disable Visual Studio NuGet package restore - http://fsprojects.github.io/Paket/paket-auto-restore.html
* BUGFIX: Probe for unnormalized and normalized versions in local NuGet feeds - https://github.com/fsprojects/Paket/issues/556

#### 0.23.0 - 15.01.2015
* Refactored `init` & `init auto restore` to Railway Oriented Programming - https://github.com/fsprojects/Paket/pull/533
* Refactored FindRefs to Railway Oriented Programming - https://github.com/fsprojects/Paket/pull/529
* BUGFIX: paket.bootstrapper.exe and paket.exe use better proxy detection - https://github.com/fsprojects/Paket/pull/552
* BUGFIX: `paket add` offered to add dependencies even when they are already added - https://github.com/fsprojects/Paket/issues/550
* BUGFIX: Detect `Net20-client` - https://github.com/fsprojects/Paket/issues/547
* BUGFIX: Give better error message when package is not found in a local feed - https://github.com/fsprojects/Paket/issues/545
* BUGFIX: Don't download gists that are up-to-date - https://github.com/fsprojects/Paket/issues/513
* BUGFIX: fix parsing of longer http links - https://github.com/fsprojects/Paket/pull/536
* BUGFIX: Detect correct `paket.references` filenames during convert-from-nuget
* BUGFIX: If no package source is found during convert-from-nuget we use the default NuGet feed
* COSMETICS: Config file is only saved when needed
* COSMETICS: Ignore completely empty lib folders
* COSMETICS: `paket convert-from-nuget` warns if it can't find a NuGet feed - https://github.com/fsprojects/Paket/issues/548
* COSMETICS: Remove icon from bootstrapper to make file size much smaller

#### 0.22.0 - 05.01.2015
* Bootstrapper avoids github API - https://github.com/fsprojects/Paket/issues/510
* Refactoring to Railwal Oriented Programming - http://fsharpforfunandprofit.com/rop/
* Always trim line end in lockfile
* Improved binding redirects detection - https://github.com/fsprojects/Paket/pull/507
* Don't catch NullReferenceExceptions for now - https://github.com/fsprojects/Paket/issues/505
* BUGFIX: Paket update nuget X doesn't work - https://github.com/fsprojects/Paket/issues/512

#### 0.21.0 - 02.01.2015
* New `--log-file` parameter allows to trace into logfile - https://github.com/fsprojects/Paket/pull/502
* Trace stacktrace on all NullReferenceExceptions - https://github.com/fsprojects/Paket/issues/500
* Paket.locked file has 2 minute timeout
* BUGFIX: Detect the version of a GitHub gist correctly - https://github.com/fsprojects/Paket/issues/499
* BUGFIX: Dependencies file saves http and gist links correctly - https://github.com/fsprojects/Paket/issues/498
* BUGFIX: Don't relax "OverrideAll" conditions during `paket install`
* BUGFIX: fix priority of parsing atom nuget feed for package Id - https://github.com/fsprojects/Paket/issues/494
* BUGFIX: fix JSON deserializer and reactivate cache - https://github.com/fsprojects/Paket/pull/495
* BUGFIX: Make the file search for app.config and web.config case insensitive - https://github.com/fsprojects/Paket/issues/493
* BUGFIX: Don't add duplicate lines in `packet.dependencies` - https://github.com/fsprojects/Paket/issues/492
* BUGFIX: Keep framework restrictions in `paket install`- https://github.com/fsprojects/Paket/issues/486
* WORKAROUND: Do not fail on BadCrcException during unzip and only show a warning - https://github.com/fsprojects/Paket/issues/484
* WORKAROUND: Disable NuGet v3 feed for now - seems to be unreliable.
* PERFORMANCE: Don't parse project files twice - https://github.com/fsprojects/Paket/issues/487
* PERFORMANCE: Cache platform penalty calculation - https://github.com/fsprojects/Paket/issues/487
* PERFORMANCE: Use StringBuilder for path replacement - https://github.com/fsprojects/Paket/issues/487
* PERFORMANCE: Cache feed errors - https://github.com/fsprojects/Paket/issues/487
* PERFORMANCE: Put feed url into cache filename - https://github.com/fsprojects/Paket/issues/487
* PERFORMANCE: Relax prerelease requirements for pinned versions - https://github.com/fsprojects/Paket/issues/487
* PERFORMANCE: Don't enumerate all files, since we only need lib files - https://github.com/fsprojects/Paket/issues/487
* PERFORMANCE: Pin sourcefile dependencies - https://github.com/fsprojects/Paket/issues/487
* PERFORMANCE: Cache path penalty calculation - https://github.com/fsprojects/Paket/issues/487
* PERFORMANCE: Cache path extraction - https://github.com/fsprojects/Paket/issues/487

#### 0.20.1 - 30.12.2014
* COSMETICS: Trim end of line in lockfile.

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
* BUGFIX: Paket no longer leaves transitive dependencies in lockfile after remove command - https://github.com/fsprojects/Paket/pull/306
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
* Removed duplicate transitive dependencies from lock file - https://github.com/fsprojects/Paket/issues/200
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
