#### 8.1.0-alpha005 - 2024-03-25
* Added support for central package managments to fix issue about [references: strict either does not work, or does not work as expected](https://github.com/fsprojects/Paket/issues/2257)

#### 8.1.0-alpha002 - 2024-03-14
* Preview support for .NET 9.0 - https://github.com/fsprojects/Paket/pull/4248

#### 8.1.0-alpha001 - 2024-01-30
* BUGFIX: Aliases should not be inherited - https://github.com/fsprojects/Paket/pull/4244

#### 8.0.3 - 2024-01-15
* Paket ignored the alias setting on SDK-based projects - https://github.com/fsprojects/Paket/pull/4238
* Fixed net8.0-windows target issue - https://github.com/fsprojects/Paket/pull/4242

#### 8.0.0 - 2023-11-14
* Support for .NET 8.0
* Support for net481 - https://github.com/fsprojects/Paket/pull/4227
* paket init: Default framework restriction - https://github.com/fsprojects/Paket/pull/4228

#### 7.2.1 - 2023-03-03
* BUGFIX: Increase process lock trials from 100 to 500 to support dotnet restore on larger solutions - https://github.com/fsprojects/Paket/pull/4203

#### 7.2.0 - 2022-11-18
* BUGFIX: Dependency manager looked up older tool version of paket - https://github.com/fsprojects/Paket/pull/4168

#### 7.2.0-alpha003 - 2022-11-18
* BUGFIX: dotnet publish multi-targeting was inferring incorrect versioning - https://github.com/fsprojects/Paket/pull/4184

#### 7.2.0-alpha001 - 2022-06-22
* Do not append TargetFramework subdirectory to OutputPath - https://github.com/fsprojects/Paket/pull/4159

#### 7.1.5 - 2022-04-22
* NuGet Cache: ensure for clitool nupkg is there even after skipped 2nd extraction - https://github.com/fsprojects/Paket/pull/4145

#### 7.1.4 - 2022-04-10
* Fix exception when searching for config file - https://github.com/fsprojects/Paket/pull/4138

#### 7.1.3 - 2022-03-30
* Do not set binding redirects all the time - https://github.com/fsprojects/Paket/issues/4134

#### 7.1.2 - 2022-03-29
* NuGet Cache: avoid duplicate nupkg file - https://github.com/fsprojects/Paket/pull/4135

#### 7.0.2 - 2022-03-01
* Support for .NET 7.0
* Support as .NET 6.0 tool
* Auto-Restore after paket install an paket update

#### 6.2.1 - 2021-10-13
* Roll forward for .NET tool - https://github.com/fsprojects/Paket/pull/4089

#### 6.1.3 - 2021-09-17
* Added paket version to user-agent for nuget calls - https://github.com/fsprojects/Paket/pull/4087

#### 6.1.2 - 2021-09-16
* Unified the setting of version + package metadata in the 461 and .net core builds of Paket - https://github.com/fsprojects/Paket/pull/4083

#### 6.1.0 - 2021-09-13
* Use a different nuget extraction routine to extract to versioned folders - https://github.com/fsprojects/Paket/pull/4081
* Unified packaging strategy for Paket.Core - https://github.com/fsprojects/Paket/pull/4080

#### 6.0.13 - 2021-08-31
* Support for .NET 5.0
* Support for .NET 6.0
* Full .NET Core / SDK compatible version
* Support for XCode
* FSharp.DependencyManager.Paket FSI extension for #r "paket: ..."

#### 5.258.1 - 2021-06-24
* Added NoDefaultExcludes - https://github.com/fsprojects/Paket/pull/4038

#### 5.257.0 - 2020-11-17
* Support for UAP v10.0.10240 - https://github.com/fsprojects/Paket/pull/3875

#### 5.256.0 - 2020-11-17
* Support net5-windows, net5000-windows and similar - https://github.com/fsprojects/Paket/pull/3944
* BUGFIX: Fix how msbuild condition is created based on target frameworks - https://github.com/fsprojects/Paket/pull/3943

#### 5.255.0 - 2020-11-12
* .NET5 OS dependent frameworks - https://github.com/fsprojects/Paket/pull/3934 https://github.com/fsprojects/Paket/pull/3938

#### 5.252.0 - 2020-11-08
* BUGFIX: Fix removal of duplicate nodes

#### 5.251.0 - 2020-10-22
* Backwards compatibility for netcoreapp5.0 moniker

#### 5.250.0 - 2020-10-21
* Allow net5.0 moniker

#### 5.249.2 - 2020-08-06
* BUGFIX: Do not include buildTransitve/buildMultiTargeting - https://github.com/fsprojects/Paket/pull/3891

#### 5.249.0 - 2020-07-25
* Add support for props and target files in root of file - https://github.com/fsprojects/Paket/pull/3889

#### 5.248.2 - 2020-07-25
* BUGFIX: Search all the sub-directories under the default packages directory and do not use CompareString while searching under packages directory - https://github.com/fsprojects/Paket/pull/3888

#### 5.248.1 - 2020-07-23
* BUGFIX: Corrects memozation of HttpHandlers - https://github.com/fsprojects/Paket/pull/3881

#### 5.248.0 - 2020-07-23
* Use AES encryption when not on windows - https://github.com/fsprojects/Paket/pull/3884

#### 5.247.4 - 2020-06-27
* BUGFIX: Do not add development dependencies to the nuspec of a target nupkg while paket packing - https://github.com/fsprojects/Paket/pull/3873

#### 5.247.3 - 2020-06-22
* Remove recursion from lock access test

#### 5.247.1 - 2020-06-14
* REVERT: Added protocolVersion for NuGet source - https://github.com/fsprojects/Paket/pull/3844

#### 5.247.0 - 2020-06-14
* Added protocolVersion for NuGet source - https://github.com/fsprojects/Paket/pull/3844

#### 5.246.1 - 2020-06-13
* BUGFIX: Fix a misspelled TFM - https://github.com/fsprojects/Paket/pull/3855

#### 5.246.0 - 2020-06-13
* Add support for licenseExpression in paket.template - https://github.com/fsprojects/Paket/pull/3824

#### 5.245.4 - 2020-06-12
* Allows to quit `paket find-packages` with Ctrl+c - https://github.com/fsprojects/Paket/pull/3865

#### 5.245.3 - 2020-05-25
* Treat NuGet repo as v3 if url ends with index.json  - https://github.com/fsprojects/Paket/issues/3806

#### 5.245.1 - 2020-05-05
* Allow to restore netcoreapp5.0 - https://github.com/fsprojects/Paket/issues/3811

#### 5.244.2 - 2020-04-29
* Detect when MSBuild 16 is not using SDK - https://github.com/fsprojects/Paket/issues/3837

#### 5.244.1 - 2020-04-26
* REVERT: Apply version ranges to nuspecs during fix-up command - https://github.com/fsprojects/Paket/pull/3835

#### 5.243.0 - 2020-03-27
* Add support for MonoAndroid10.0 - https://github.com/fsprojects/Paket/pull/3817
* Add support for uap10.0.14393 and uap10.0.18362 - https://github.com/fsprojects/Paket/pull/3818

#### 5.242.2 - 2020-02-17
* BUGFIX: Update matching platform check - https://github.com/fsprojects/Paket/pull/3797

#### 5.242.1 - 2020-02-10
* BUGFIX: Don't generate refs to full framework assemblies for netstandard scripts

#### 5.242.0 - 2020-02-03
* Change default TFM on paket init to netcoreap31

#### 5.241.6 - 2020-01-04
* REVERT: Do only disable automagic when FSharp.Core is actually used - https://github.com/fsprojects/Paket/issues/3769

#### 5.241.5 - 2019-12-22
* BUGFIX: Fixed #3763 partially extracted nugets - https://github.com/fsprojects/Paket/pull/3764

#### 5.241.2 - 2019-12-12
* BUGFIX: Paket pack failed when project contained Compile Update entries - https://github.com/fsprojects/Paket/issues/3752

#### 5.241.1 - 2019-12-05
* Added IgnoreConflict option for paket push - https://github.com/fsprojects/Paket/pull/3741

#### 5.240.1 - 2019-12-05
* SECURITY: Check against zip leak in the workaround case of 5.240.0 - https://github.com/fsprojects/Paket/pull/3747

#### 5.240.0 - 2019-12-04
* WORKAROUND: Microsoft pushed couple of invalid zips to nuget.org this works around it - https://github.com/fsprojects/Paket/issues/3743

#### 5.239.0 - 2019-12-03
* PaketUpdate failed with semver 2.0 version and jfrog hosted repository - https://github.com/fsprojects/Paket/issues/3601

#### 5.238.2 - 2019-11-26
* Exclude top-level linux folders for docker support - https://github.com/fsprojects/Paket/issues/3123
* More verbose logging

#### 5.237.0 - 2019-11-25
* Added BootstrapperOutputDir to config - https://github.com/fsprojects/Paket/pull/3733

#### 5.236.6 - 2019-11-21
* REVERT: Paket caused build warnings by adding references to NETStandard.Library - https://github.com/fsprojects/Paket/issues/2852

#### 5.236.1 - 2019-11-20
* BUGFIX: "Update group" kept old versions fixed - https://github.com/fsprojects/Paket/pull/3725

#### 5.236.0 - 2019-11-15
* Paket init is now a bit more opinionated and restricts to netcore3.0, nestandard2.0, nestandard2.1 - https://github.com/fsprojects/Paket/pull/3725

#### 5.235.0 - 2019-11-15
* BUGFIX: DisableImplicitFSharpCoreReference is only set if FSharp.Core is explicitly referenced - https://github.com/fsprojects/Paket/pull/3725
* PERFORMANCE: paket why uses HashSet to keep track of already visited nodes - https://github.com/fsprojects/Paket/pull/3722

#### 5.234.0 - 2019-11-14
* BUGFIX: Keep preferred versions for the correct group - https://github.com/fsprojects/Paket/issues/3717

#### 5.233.0 - 2019-11-13
* BUGFIX: Change cli target detection to not block on "paket --version" - https://github.com/fsprojects/Paket/pull/3706

#### 5.232.0 - 2019-11-11
* Add repository tag to template file - https://github.com/fsprojects/Paket/pull/3707
* BUGFIX: Fixed IndexOutOfRangeException starting from 5.231.0 - https://github.com/fsprojects/Paket/issues/3701
* BUGFIX: Allow GitHub package registry urls without trailing slash - https://github.com/fsprojects/Paket/issues/3700

#### 5.231.0 - 2019-11-05
* PERFORMANCE: Use NuGet v3 as default source in paket init

#### 5.230.0 - 2019-11-05
* PERFORMANCE: Use package details from extracted files

#### 5.229.0 - 2019-11-04
* PERFORMANCE: Prefer latest AutoComplete server

#### 5.227.0 - 2019-10-29
* Support for github NuGet repos - https://github.com/fsprojects/Paket/issues/3692

#### 5.226.0 - 2019-10-17
* New paket.targets to support global and local dotnet paket for old style projetcs - https://github.com/fsprojects/Paket/issues/3687

#### 5.225.0 - 2019-10-17
* Update frameworks to support netcoreapp3.1 - https://github.com/fsprojects/Paket/pull/3688

#### 5.224.0 - 2019-10-09
* Limit number of open connections to a server and add `PAKET_DEBUG_REQUESTS` to debug request failures - https://github.com/fsprojects/Paket/pull/3683

#### 5.223.3 - 2019-10-09
* BUGFIX: Update getResolverStrategy for rootRequirements - https://github.com/fsprojects/Paket/pull/3670

#### 5.220.0 - 2019-09-30
* BUGFIX: Make paket work as global tool again - https://github.com/fsprojects/Paket/issues/3671
* Add PaketCommand contidion for Paket installed as .NET Core 3.0 local tool - https://github.com/fsprojects/Paket/pull/3668/files
* Try hardcoded path for NuGetFallbackFolder - https://github.com/fsprojects/Paket/pull/3663
* BUGFIX: Fixed typo in targets file so that bootstrapper can be found - https://github.com/fsprojects/Paket/pull/3665
* BUGFIX: Fixed UnauthorizedAccessException writing to MyProject.paket.references.cached - https://github.com/fsprojects/Paket/pull/3617
* BUGFIX: Fixed CopyLocal support - https://github.com/fsprojects/Paket/pull/3659

#### 5.219.0 - 2019-09-07
* Support for creating snupkg symbol packages - https://github.com/fsprojects/Paket/pull/3636

#### 5.218.1 - 2019-09-04
* Nuke Paket files after install - https://github.com/fsprojects/Paket/issues/3618

#### 5.216.0 - 2019-08-10
* Add roll-forward config to enable running on later major versions of the runtime - https://github.com/fsprojects/Paket/pull/3635

#### 5.215.0 - 2019-07-03
* BUGFIX: Disable fast restore for MSBuild version < 15.8 - https://github.com/fsprojects/Paket/pull/3611

#### 5.214.0 - 2019-07-03
* PERFORMANCE: Fast restore reactivated - https://github.com/fsprojects/Paket/pull/3608

#### 5.213.0 - 2019-07-02
* PERFORMANCE: If Paket didn't download a package, then it will no longer try to extract it - https://github.com/fsprojects/Paket/pull/3607

#### 5.211.0 - 2019-06-24
* BUGFIX: Paket 5.207.4 broke the FAKE release process and creates empty packages - https://github.com/fsprojects/Paket/issues/3599
* BUGFIX: Change caching logic to be more suitable for FAKE and in particular the Ionide tooling - https://github.com/fsprojects/Paket/pull/3598
* BUGFIX: Fixed native library detection - enables FAKE native library support - https://github.com/fsprojects/Paket/pull/3593
* BUGFIX: Allow projects without Guids - https://github.com/fsprojects/Paket/issues/3528

#### 5.209.0 - 2019-05-29
* Relaxed NuGet v3 check. Allows URLs that does not end with /index.json - https://github.com/fsprojects/Paket/pull/3590

#### 5.208.0 - 2019-05-28
* BUGFIX: Rename paket.locked file to paket.processlock to avoid McAfee scanner to evaluate the file as threat - https://github.com/fsprojects/Paket/pull/3586
* BUGFIX: Fixed multiple dotnet pack invocations with different Version property - https://github.com/fsprojects/Paket/pull/3585
* BUGFIX: Nuke project.assets.json files after paket install - https://github.com/fsprojects/Paket/issues/3577
* BUGFIX: Keep casing of packages stable in paket.lock - https://github.com/fsprojects/Paket/issues/3340
* COSMETICS: Improved error message for missing monikers - https://github.com/fsprojects/Paket/pull/3583

#### 5.207.0 - 2019-05-11
* Simplifier removes unsupported frameworks - https://github.com/fsprojects/Paket/pull/3574

#### 5.206.0 - 2019-05-08
* BUGFIX: Paket considers dependencies target framework restrictions in paket pack - https://github.com/fsprojects/Paket/pull/3558

#### 5.205.0 - 2019-05-07
* BUGFIX: Fix issues with lock file simplification - https://github.com/fsprojects/Paket/pull/3570

#### 5.204.3 - 2019-05-07
* Allow to parse DNXCore in lock file and be backwards compatible again

#### 5.203.2 - 2019-04-15
* BUGFIX: Fixed #3459, #3418 and #3375 - https://github.com/fsprojects/Paket/pull/3554

#### 5.203.1 - 2019-04-15
* BUGFIX: Xamarin.Mac supports .Net Standard 2.0 - https://github.com/fsprojects/Paket/pull/3555

#### 5.203.0 - 2019-04-11
* Support for BaseIntermediateOutputPath - https://github.com/fsprojects/Paket/pull/3527

#### 5.202.0 - 2019-04-10
* EMERGENCY-RELEASE

#### 5.201.1 - 2019-04-10
* Adapt PackTask to breaking changes in MSBuild 16 - https://github.com/fsprojects/Paket/pull/3542
* BUGFIX: Simplify Fix Extra Settings - https://github.com/fsprojects/Paket/pull/3538
* BUGFIX: Always try and extract Paket.Restore.targets even if up to date - https://github.com/fsprojects/Paket/pull/3524
* Adding support for multiple target frameworks to the pack command - https://github.com/fsprojects/Paket/pull/3534
* New setting that shields packages from simplifier - https://github.com/fsprojects/Paket/pull/3523
* BUGFIX: Fixed symlinks on linux - https://github.com/fsprojects/Paket/pull/3372

#### 5.200.0 - 2019-04-02
* Support for .NET Standard 2.1 and .NET 4.8
* Removed .NET 5.0 moniker because it was never released
* Removed the old temporary monikers for dnxcore and dnx

#### 5.198.0 - 2019-02-22
* PERFORMANCE: Speedup for paket restore - https://github.com/fsprojects/Paket/pull/3512
* BUGFIX: Do not run in StackOverflow during ReleaseLock
* BUGFIX: Paket install writed restore cache file -https://github.com/fsprojects/Paket/issues/3508

#### 5.197.0 - 2019-02-18
* BUGFIX: Restore SDK projects during paket install - https://github.com/fsprojects/Paket/pull/3503

#### 5.196.2 - 2019-02-04
* BUGFIX: Fixed constant warn about new syntax - https://github.com/fsprojects/Paket/pull/3497

#### 5.196.1 - 2019-02-03
* New option to control interproject references version constraint - https://github.com/fsprojects/Paket/pull/3473
* BUGFIX: Fixedpack transitive dependencies with --include-referenced-projects - https://github.com/fsprojects/Paket/pull/3469
* BUGFIX: uri unescape when read project property - https://github.com/fsprojects/Paket/pull/3470
* BUGFIX: Added PaketRestoreDisabled when NCrunch enabled in targets - https://github.com/fsprojects/Paket/pull/3479
* BUGFIX: dotnet --no-restore was still doing a restore - https://github.com/fsprojects/Paket/pull/3486
* BUGFIX: Set AllowExplicitVersion to true for PackageReference in Paket.Restore.targets - https://github.com/fsprojects/Paket/pull/3482
* BUGFIX: Apply paket github api token on github requests - https://github.com/fsprojects/Paket/pull/3484
* BUGFIX: Do not change the AutoGenerateBindingRedirects for exe output type - https://github.com/fsprojects/Paket/pull/3471

#### 5.195.0 - 2019-01-10
* SQL project support - https://github.com/fsprojects/Paket/pull/3474
* BUGFIX: Fixed RestrictionsChanged Detection - https://github.com/fsprojects/Paket/pull/3464
* BUGFIX: Use the correct request header for paket push - https://github.com/fsprojects/Paket/pull/3466
* BUGFIX: Fixed zsh completer for clear-cache - https://github.com/fsprojects/Paket/pull/3457

#### 5.194.0 - 2018-12-08
* BUGFIX: Fixed conflict between native local-path style with global install style - https://github.com/fsprojects/Paket/pull/3451

#### 5.193.0 - 2018-12-02
* Zsh completion update - https://github.com/fsprojects/Paket/pull/3440

#### 5.192.0 - 2018-12-02
* Making Paket.Restore.targets work with Paket as a global tool - https://github.com/fsprojects/Paket/pull/3445

#### 5.191.0 - 2018-12-01
* BUGFIX: Fix bindingredirects - https://github.com/fsprojects/Paket/issues/3444

#### 5.190.0 - 2018-11-26
* BUGFIX: Allow Username/password to be UTF8 - https://github.com/fsprojects/Paket/pull/3431
* BUGFIX: Fixed handling of DotNetCoreAppVersion.V3_0 - https://github.com/fsprojects/Paket/pull/3437
 - 2018-11-17
* NuGet pack compat for SDK 2.1.500 - https://github.com/fsprojects/Paket/issues/3427
* Adjustable timeouts for NuGet - https://github.com/fsprojects/Paket/pull/3383
* REVERT: Retry automatically when a request times out - https://github.com/fsprojects/Paket/pull/3424

#### 5.187.0 - 2018-11-13
* Create a gitignore around paket's .cached file - https://github.com/fsprojects/Paket/issues/3060
* Paket template checks if restored with 5.185.3 or later - https://github.com/fsprojects/Paket/issues/3404
* BUGFIX: Remove ReadOnly flag before writing to files - https://github.com/fsprojects/Paket/issues/3410
* BUGFIX: Added compat fallback in case of older cache files - https://github.com/fsprojects/Paket/pull/3417
* BUGFIX: Used lowest_matching for paket's own FSharp.Core dependency - https://github.com/fsprojects/Paket/pull/3415
* BUGFIX: Retry automatically when a request times out - https://github.com/fsprojects/Paket/pull/3420

#### 5.184.0 - 2018-10-30
* REVERT: Adjustable timeouts for NuGet - https://github.com/fsprojects/Paket/pull/3383

#### 5.183.0 - 2018-10-30
* Add namespace to load scripts - https://github.com/fsprojects/Paket/pull/3396

#### 5.182.1 - 2018-10-30
* Adjustable timeouts for NuGet - https://github.com/fsprojects/Paket/pull/3383
* Full .NET Core support - https://github.com/fsprojects/Paket/pull/3183
* BUGFIX: generate-load-scripts ignored targetFramework constraint in frameworkAssembly config (.nuspec file) https://github.com/fsprojects/Paket/pull/3385

#### 5.181.1 - 2018-09-20
* BUGFIX: copy local had no effect when opening project in vs2017 - https://github.com/fsprojects/Paket/pull/3356

#### 5.181.0 - 2018-09-20
* Support for netcore3.0 moniker - https://github.com/fsprojects/Paket/pull/3367
* BUGFIX: Paket pack with `--template` failed trying to load the dependencies of templates who should be ignored instead - https://github.com/fsprojects/Paket/pull/3363

#### 5.180.0 - 2018-09-17
* Added .NETCoreApp2.2 moniker

#### 5.179.1 - 2018-09-14
* BUGFIX: Fixed potential race condition when redirecting output - https://github.com/fsprojects/Paket/pull/3359

#### 5.179.0 - 2018-09-12
* Added NuGet packaging output details to paket pack output - https://github.com/fsprojects/Paket/pull/3357

#### 5.178.0 - 2018-09-12
* Added MonoAndroid9.0 + compatibility between .NET Standard 2.0 and MonoAndroid8.0/XamariniOS - https://github.com/fsprojects/Paket/pull/3354
* BUGFIX: `copy_local: false` had no effect in paket.references (.NET SDK) - https://github.com/fsprojects/Paket/issues/3186
* TEMPORARY WORKAROUND: Do not reference Microsoft.Azure.WebJobs.Script.ExtensionsMetadataGenerator - https://github.com/fsprojects/Paket/issues/3345

#### 5.177.0 - 2018-08-30
* BUGFIX: fix 'Value cannot be null' with TFS feed - https://github.com/fsprojects/Paket/pull/3341
* BUGFIX: Fixed authentication problem in netcore (fake 5) - https://github.com/fsprojects/Paket/pull/3342
* BUGFIX: Fixed 'Value cannot be null' with TFS feed - https://github.com/fsprojects/Paket/pull/3341
* BUGFIX: netcoreapp2.0 packages are compatibile with netcoreapp2.1 - https://github.com/fsprojects/Paket/pull/3336
* BUGFIX: Paket uses ToLowerInvariant instead of ToLower when forming registrationUrl - https://github.com/fsprojects/Paket/pull/3330
* BUGFIX: Removed faulty creation of directories during `generate-load-scripts` - https://github.com/fsprojects/Paket/pull/3319

#### 5.176.0 - 2018-07-31
* paket pack with p2p dependencies and multitargeting - https://github.com/fsprojects/Paket/pull/3317
* BUGFIX: Revert impact of https://github.com/dotnet/corefx/issues/31098 by using WinHttpHandler - https://github.com/fsprojects/Paket/pull/3307

#### 5.175.0 - 2018-07-30
* Allow addition of <EmbedInteropTypes> for NuGet packages - https://github.com/fsprojects/Paket/pull/3314
* BUGFIX: "-T" switch removed when isMacOS, because it is not valid on OSX - https://github.com/fsprojects/Paket/pull/3298
* BUGFIX: Fixed exception during restore when accessing missing folders - https://github.com/fsprojects/Paket/pull/3293
* BUGFIX: Reports NuGet download time correctly - https://github.com/fsprojects/Paket/pull/3304
* BUGFIX: Accept netstandard20 in Visual Studion integration - https://github.com/fsprojects/Paket/issues/3284

#### 5.174.0 - 2018-07-06
* NEW FEATURE: Improved Visual Studio integration - https://github.com/fsprojects/Paket/pull/3273
* BUGFIX: Paket doesn't add Compile tags for packages when new project format ius used - https://github.com/fsprojects/Paket/issues/3269
* BUGFIX: Paket packs localized assemblies with new .csproj - https://github.com/fsprojects/Paket/pull/3276
* BUGFIX: Extended NuGetV3 source detection with Artifactory feed format - https://github.com/fsprojects/Paket/pull/3267
* BUGFIX: Paket add only runs update on the touched group - https://github.com/fsprojects/Paket/issues/3259
* COSMETICS: group parameter for outdated works like everywhere else - https://github.com/fsprojects/Paket/pull/3280

#### 5.173.0 - 2018-06-20
* BUGFIX: Don't serialize individual settings that match group settings in lock file - https://github.com/fsprojects/Paket/issues/3257

#### 5.172.4 - 2018-06-18
* BUGFIX: Fixed invalid syntax in packages config  - https://github.com/fsprojects/Paket/pull/3253

#### 5.172.3 - 2018-06-18
* BUGFIX: Fixed infinite recursion when handling errors - https://github.com/fsprojects/Paket/pull/3251

#### 5.172.2 - 2018-06-11
* BUGFIX: Report only transitive settings changes - https://github.com/fsprojects/Paket/issues/3218

#### 5.172.1 - 2018-06-10
* PERFORMANCE: Add support for dotnet SDK fallback folder - https://github.com/fsprojects/Paket/pull/3242

#### 5.171.0 - 2018-06-07
* PERFORMANCE: Improved binding redirects performance by removing mono.cecil - https://github.com/fsprojects/Paket/pull/3239
* BUGFIX: paket template semver support fixes - https://github.com/fsprojects/Paket/pull/3230
* BUGFIX: Improved restore behavior - https://github.com/fsprojects/Paket/pull/3237

#### 5.170.0 - 2018-06-05
* PERFORMANCE: Fixed filtered update performance - https://github.com/fsprojects/Paket/pull/3233
* PERFORMANCE: Check if everything is up-to-date after aquiring lock
* BUGFIX: Regression from semver2 support in paket tempates - https://github.com/fsprojects/Paket/pull/3229
* USABILITY: Always trace on early restore exit

#### 5.169.0 - 2018-05-26
* Ignore .git and .fable folder when scanning for projects - https://github.com/fsprojects/Paket/issues/3225

#### 5.168.0 - 2018-05-25
* Extended semver2 support in paket.template files - https://github.com/fsprojects/Paket/pull/3184
* BUGFIX: Trace paket version in shortcut routes

#### 5.167.1 - 2018-05-23
* BUGFIX: No longer search for CredentialProviders on an empty path - https://github.com/fsprojects/Paket/pull/3214

#### 5.167.0 - 2018-05-23
* Support slash in GitHub branch name - https://github.com/fsprojects/Paket/pull/3215

#### 5.166.0 - 2018-05-19
* Paket template packagetypes - https://github.com/fsprojects/Paket/pull/3212
* PAKET_DETAILED_ERRORS to print inner stack traces - https://github.com/fsprojects/Paket/pull/3192
* Support pack of global tooks `PackAsTool` - https://github.com/fsprojects/Paket/pull/3208
* PERFORMANCE: Do only one global restore in dotnet restore - https://github.com/fsprojects/Paket/pull/3211

#### 5.165.0 - 2018-05-17
* PERFORMANCE: Do only one global restore in dotnet restore - https://github.com/fsprojects/Paket/pull/3206

#### 5.164.0 - 2018-05-17
* BOOTSTRAPPER: Update magic mode file location - https://github.com/fsprojects/Paket/pull/3197

#### 5.163.2 - 2018-05-15
* PERFORMANCE: Some performance improvements in targets file - https://github.com/fsprojects/Paket/pull/3200

#### 5.162.0 - 2018-05-14
* PERFORMANCE: Some performance improvements - https://github.com/fsprojects/Paket/pull/3173
* BUGFIX: Fixed incorrect framework restrictions in lockfile -  https://github.com/fsprojects/Paket/pull/3176
* BUGFIX: Fixed semver support for v3 - https://github.com/fsprojects/Paket/pull/3179

#### 5.161.3 - 2018-05-08
* BUGFIX: Override versions properly with Update property in ProjectReferences

#### 5.160.0 - 2018-05-08
* Support for net472 - https://github.com/fsprojects/Paket/issues/3188
* Support for UAP10.0.16299 - https://github.com/fsprojects/Paket/issues/3189

#### 5.159.0 - 2018-05-08
* Allows to add git-repositories to paket.lock and paket.references files via CLI - https://github.com/fsprojects/Paket/pull/3125
* BUGFIX: Fixed incorrect framework restrictions in the lockfile - https://github.com/fsprojects/Paket/pull/3176

#### 5.158.0 - 2018-05-08
* BUGFIX: Paket restore silently failed when TargetFramework(s) are specified in Directory.Build.props and not csproj - https://github.com/fsprojects/Paket/pull/3013

#### 5.156.7 - 2018-04-26
* shasum now works when the path has spaces - https://github.com/fsprojects/Paket/pull/3169

#### 5.156.6 - 2018-04-25
* Paket pack works with BuildOutputTargetFolder and AppendTargetFrameworkToOutputPath - https://github.com/fsprojects/Paket/pull/3165

#### 5.156.5 - 2018-04-18
* Keeping direct dependencies distinct from external locks- https://github.com/fsprojects/Paket/pull/3159

#### 5.156.4 - 2018-04-17
* BUGFIX: `copy_local: false` now works with .NET SDK - https://github.com/fsprojects/Paket/issues/3154

#### 5.156.2 - 2018-04-17
* BUGFIX: Work around NuGet v3 SemVer2 issues - https://github.com/fsprojects/Paket/issues/3156

#### 5.156.1 - 2018-04-13
* BUGFIX: Paket convert-from-nuget doesn't crash on NuGet v2 syntax - https://github.com/fsprojects/Paket/issues/3151

#### 5.156.0 - 2018-04-12
* Support monoandroid version 8.1 - https://github.com/fsprojects/Paket/pull/3146

#### 5.155.0 - 2018-03-28
* New external_lock file parser to mitigate Azure Functions dependency trouble - https://fsprojects.github.io/Paket/dependencies-file.html#External-lock-files

#### 5.154.0 - 2018-03-27
* New storage option: symlink - https://github.com/fsprojects/Paket/pull/3128
* BUGFIX: Conditional references in paket.references now work with .NET SDK-style projects - https://github.com/fsprojects/Paket/issues/3091
* BUGFIX: "exclude" now works with new csproj format - https://github.com/fsprojects/Paket/issues/3133

#### 5.153.0 - 2018-03-16
* Adding AutoGenerateBindingRedirects automatically to project file when BindingRedirects are added to a config - https://github.com/fsprojects/Paket/pull/3120

#### 5.152.0 - 2018-03-16
* BUGFIX: Working around parallel restore issues with dotnet sdk 2.1.100-preview  - https://github.com/fsprojects/Paket/pull/3118

#### 5.151.4 - 2018-03-15
* EMERGENCY: Zero-Diff release to work around defender issue - https://github.com/fsprojects/Paket/issues/3121

#### 5.151.3 - 2018-03-14
* REVERT: Add duplicated references and let compiler warn about it - https://github.com/fsprojects/Paket/pull/3107

#### 5.151.2 - 2018-03-14
* REVERT: Working around parallel restore issues with dotnet sdk 2.1.100-preview - https://github.com/fsprojects/Paket/pull/3115

#### 5.151.1 - 2018-03-14
* USABILITY: Add duplicated references and let compiler warn about it - https://github.com/fsprojects/Paket/pull/3107
* BUGFIX: Working around parallel restore issues with dotnet sdk 2.1.100-preview - https://github.com/fsprojects/Paket/pull/3115

#### 5.150.0 - 2018-03-13
* PERFORMANCE: Using latest optimized Argu - https://github.com/fsprojects/Paket/pull/3112
* PERFORMANCE: Fixed the optimization that bypasses Argu - https://github.com/fsprojects/Paket/pull/3113

#### 5.149.0 - 2018-03-12
* BUGFIX: Edge case in resolver fixed where Paket downgraded a direct dependency - https://github.com/fsprojects/Paket/issues/3103

#### 5.148.0 - 2018-03-01
* New command to add GitHub sources in PublicAPI and CLI - https://github.com/fsprojects/Paket/pull/3023

#### 5.147.0 - 2018-03-01
* Added .NETCoreApp2.1 and UAP10.0.16300 TFMs - https://github.com/fsprojects/Paket/pull/3094

#### 5.146.0 - 2018-02-28
* BUGFIX: Fixed UriFormatException for gist references with GitHubApi token - https://github.com/fsprojects/Paket/issues/3086
* BUGFIX: convert-from-nuget fails with ArgumentException - https://github.com/fsprojects/Paket/issues/3089
* BUGFIX: Normalize Home directory (~) everywhere - https://github.com/fsprojects/Paket/pull/3096
* BUGFIX: Safer loading of assemblies during load script generation - https://github.com/fsprojects/Paket/pull/3098
* BUGFIX: Fixed inconsistent pinned version of referenced projects with include-referenced-projects enabled - https://github.com/fsprojects/Paket/issues/3076
* BUGFIX: Fixed repeated conflict detection and introduced optional resolver timeout - https://github.com/fsprojects/Paket/pull/3084
* BUGFIX: Paket pack was putting entire directory into package lib folder when files block contained an empty line - https://github.com/fsprojects/Paket/issues/2949
* BUGFIX: Better error message when a HTTP request fails - https://github.com/fsprojects/Paket/pull/3078
* BUGFIX: generate-load-script was broken - https://github.com/fsprojects/Paket/issues/3080
* PERFORMANCE: Faster "hot" restore - https://github.com/fsprojects/Paket/pull/3092
* USABILITY: Retry HTTP downloads - https://github.com/fsprojects/Paket/issues/3088

#### 5.145.0 - 2018-02-26
* Added support for credential managers - https://github.com/fsprojects/Paket/pull/3069

#### 5.144.0 - 2018-02-26
* BUGFIX: Fix https://github.com/fsharp/FAKE/issues/1744
* BUGFIX: Fix https://github.com/fsharp/FAKE/issues/1778
* BUGFIX: Fixed bug when attempting to pack multi-target frameworks - https://github.com/fsprojects/Paket/pull/3073

#### 5.144.0-alpha.2 - 2018-02-26
* Added support for credential managers - https://github.com/fsprojects/Paket/pull/3069

#### 5.142.0 - 2018-02-24
* BUGFIX: Fixed bootstrapper to handle Github TLS issue
* BUGFIX: Fixed unhandled exception when running 'paket config add-credentials' in Jenkins pipeline - https://github.com/fsprojects/Paket/issues/2884
* BUGFIX: Some NuGet v2 queries fail with normalized filter syntax and are now skipped/blacklisted - https://github.com/fsprojects/Paket/pull/3059
* BUGFIX: Be more robust with custom namespaces in app.config - https://github.com/fsprojects/Paket/issues/1607
* BUGFIX: Fix prerelease selection when having multiple prereleases - https://github.com/fsprojects/Paket/pull/3058
* USABILITY: Update process just ignores groups that where not in lock file - https://github.com/fsprojects/Paket/pull/3054

#### 5.138.0 - 2018-02-16
* Extended SemVer v2 compliance and reliability improvements - https://github.com/fsprojects/Paket/pull/3030
* BUGFIX: Putting local folder clearing under flag in "paket clear-cache" - https://github.com/fsprojects/Paket/issues/3049

#### 5.137.1 - 2018-02-14
* BUGFIX: Allow to use different versions from different groups if they are on different frameworks - https://github.com/fsprojects/Paket/issues/3045
* PERFORMANCE: Much faster "paket clear-cache"
* USABILITY: "paket clear-cache" empties packages folder and paket-files folder - https://github.com/fsprojects/Paket/pull/3043
* COSMETICS: Hide shasum output on osx/linux dotnet restore - https://github.com/fsprojects/Paket/pull/3043

#### 5.136.0 - 2018-02-12
* PERFORMANCE: Check if we already added the current package to the open requirement list - https://github.com/fsprojects/Paket/pull/3037

#### 5.135.0 - 2018-02-10
* BUGFIX: Fixed lowest_matching in transitive deps - https://github.com/fsprojects/Paket/issues/3032

#### 5.134.0 - 2018-02-09
* BUGFIX: Paket update doesn't prefer versions from lock file anymore - https://github.com/fsprojects/Paket/pull/3031

#### 5.133.0 - 2018-01-31
* Added `paket info --paket-dependencies-dir` to locate repo root
* API: Added overload for Dependencies.Init - https://github.com/fsprojects/Paket/pull/3019
* USABILITY: Trace detailed messages for missing package errors - https://github.com/fsprojects/Paket/pull/3001
* PERFORMANCE: Avoid duplicates in package source cache - https://github.com/fsprojects/Paket/pull/2999
* COSMETICS: Only overwrite NuGet metadata cache when needed - https://github.com/fsprojects/Paket/pull/2998
* COSMETICS: Trace not-found and blacklist warnings as actual warnings - https://github.com/fsprojects/Paket/pull/2997

#### 5.132.0 - 2018-01-18
* BUGFIX: Allow NuGet2 async query fallback to skip NotFound/404 - https://github.com/fsprojects/Paket/pull/2993
* COSMETICS: Reduce duplicate warnings for invalid framework requirements - https://github.com/fsprojects/Paket/pull/2996
* COSMETICS: Reduce next-link warning to once per method/endpoint, omitting the query - https://github.com/fsprojects/Paket/pull/2994

#### 5.131.1 - 2018-01-18
* New parameter `--type` for `paket add` - https://github.com/fsprojects/Paket/pull/2990
* WORKAROUND: Disable NuGt.Config to allow runtime deps restore - https://github.com/fsprojects/Paket/issues/2964
* BUGFIX: Fixed PaketExePath with shell script (without extension) - https://github.com/fsprojects/Paket/pull/2989
* BUGFIX: Fixed "Could not parse version range" - https://github.com/fsprojects/Paket/issues/2988

#### 5.130.3 - 2018-01-11
* Use ServiceFabric projects - https://github.com/fsprojects/Paket/issues/2977
* BUGFIX: Handle Nougat compilation (v7/7.1) target for Xamarin.Android when installing Xamarin.Forms - https://github.com/fsprojects/Paket/issues/2809
* BUGFIX: Fix split of target framework monikers - https://github.com/fsprojects/Paket/issues/2970
* BUGFIX: Fixed a few things when framework: lines are parsed in template - https://github.com/fsprojects/Paket/pull/2969

#### 5.129.0 - 2018-01-03
* BUGFIX: CliTools should not be added to redirects - https://github.com/fsprojects/Paket/issues/2955
* BUGFIX: Do not trace warnings for folders starting with _ - https://github.com/fsprojects/Paket/issues/2958
* BUGFIX: Fix auto-detect for multi targeting - https://github.com/fsprojects/Paket/pull/2956
* BUGFIX: Do not generate a dependency group for empty framework-neutral groups - https://github.com/fsprojects/Paket/pull/2954

#### 5.128.0 - 2017-12-31
* Implemented binding LOCKEDVERSION to particular group name - https://github.com/fsprojects/Paket/pull/2943
* BUGFIX: Fixed "Incorrect time metrics" - https://github.com/fsprojects/Paket/pull/2946
* USABILITY: Show parsing errors - https://github.com/fsprojects/Paket/pull/2952/files
* USABILITY: Better tracing if we have IO error in load script generation
* USABILITY: Do not download more than 5 packages at the same time
* USABILITY: Print download times
* USABILITY: Avoid replacing load script files if the contents are the same - https://github.com/fsprojects/Paket/pull/2940
* Update Paket.Restore.targets to deal with private assets

#### 5.126.0 - 2017-12-12
* BUGFIX: Remove possibly nonexistent extension safely - https://github.com/fsprojects/Paket/pull/2901

#### 5.125.1 - 2017-12-05
* Resolution by file order in paket.dependencies instead of alphabetical order (as new tie breaker) - https://github.com/fsprojects/Paket/issues/2898
* BUGFIX: Transitive dependencies should be kept as stable as possible in paket install - https://github.com/fsprojects/Paket/pull/2927
* PERFORMANCE: Boost conflicts and skip the "loop of doom" - https://github.com/fsprojects/Paket/pull/2928
* PERFORMANCE: Preferred versions do not need to query the NuGet server - https://github.com/fsprojects/Paket/pull/2927
* BOOTSTRAPPER: work around fileversion issues
* BOOTSTRAPPER: Don't lock files in the bootstrapper when we are only reading - https://github.com/fsprojects/Paket/pull/2936
* Resolution by file order in paket.dependencies instead of alphabetical order (as new tie breaker) - https://github.com/fsprojects/Paket/issues/2898

#### 5.124.0 - 2017-11-29
* Bootstrapper now correctly access files as ReadWrite only when needed - https://github.com/fsprojects/Paket/pull/2920
* BUGFIX: Fix cache parsing

#### 5.123.1 - 2017-11-27
* PERFORMANCE: Check if lock file has already the package when adding new one
* USABILITY: Delete dotnet core assets file on paket install - https://github.com/fsprojects/Paket/pull/2914

#### 5.122.0 - 2017-11-07
* Support for IronPython - https://github.com/fsprojects/Paket/pull/2885
* PERFORMANCE: Using shasum/awk for comparing hashes on osx and linux - https://github.com/fsprojects/Paket/pull/2870
* USABILITY: Added PAKET_VERSION posix compliant environment variable for bootstrapper - https://github.com/fsprojects/Paket/pull/2857
* USABILITY: Clarified `paket install` command documentation  - https://github.com/fsprojects/Paket/pull/2881

#### 5.120.0 - 2017-10-30
* Move Resource to Paket.Core and Refactorings - https://github.com/fsprojects/Paket/pull/2859
* BUGFIX: generate nuspecs in IntermediateOutputPath - https://github.com/fsprojects/Paket/pull/2871

#### 5.119.9 - 2017-10-27
* BUGFIX: Resolver doesn't fail on locked packages - https://github.com/fsprojects/Paket/issues/2777

#### 5.119.8 - 2017-10-24
* BUGFIX: Ensure directory was created before copying package from cache - https://github.com/fsprojects/Paket/pull/2864

#### 5.119.7 - 2017-10-20
* REVERT: HashSet used in paket why command - https://github.com/fsprojects/Paket/pull/2853
* REVERT: Clitool restore became unstable - https://github.com/fsprojects/Paket/issues/2854

#### 5.118.0 - 2017-10-18
* PERFORMANCE: Paket why command is now much faster - https://github.com/fsprojects/Paket/pull/2853

#### 5.117.0 - 2017-10-18
* PERFORMANCE: Paket restore is not longer called for CLI tool restore
* USABILITY: No need for .clitools file in /obj anymore

#### 5.115.0 - 2017-10-18
* PERFORMANCE: Fix performance problem introduced in 5.101.0 - https://github.com/fsprojects/Paket/pull/2850
* BUGFIX: Minor perf improvement for why command - https://github.com/fsprojects/Paket/pull/2851
* BUGFIX: Make json cache file reading more robust - https://github.com/fsprojects/Paket/issues/2838
* BUGFIX: Do not restore sdk projects when a group is given - https://github.com/fsprojects/Paket/issues/2838
* BUGFIX: Use maps instead of lists in why command - https://github.com/fsprojects/Paket/pull/2845
* BUGFIX: isExtracted function was falsely returning true to comparison - https://github.com/fsprojects/Paket/pull/2842
* USABILITY: Do not reference NETStandard.Library directly - https://github.com/fsprojects/Paket/issues/2852
* COSMETICS: Don't trace so much noise in dotnet restore

#### 5.114.0 - 2017-10-11
* BUGFIX: Invalidate internal NuGet caches

#### 5.113.2 - 2017-10-10
* BUGFIX: load scripts should only work on lock file - https://github.com/fsprojects/Paket/pull/2834
* BUGFIX: Fixed Syntax error: Expected end of input, got '<= net45' - https://github.com/fsprojects/Paket/pull/2835

#### 5.113.1 - 2017-10-09
* BUGFIX: Fixed incorrect warnings about obsolete command-line options - https://github.com/fsprojects/Paket/pull/2828

#### 5.113.0 - 2017-10-07
* BUGFIX: Lowercase package names in package cache for NuGet compat - https://github.com/fsprojects/Paket/pull/2826
* BREAKING: Stricter parsing of framework requirements - https://github.com/fsprojects/Paket/pull/2824
* Set RestoreSuccess property and let paket add/remove/install set it

#### 5.110.0 - 2017-10-05
* Send header for X-NuGet-Protocol-Version 4.1.0 - https://github.com/NuGet/Announcements/issues/10

#### 5.108.0 - 2017-10-04
* REVERT: Stricter parsing of framework requirements - https://github.com/fsprojects/Paket/pull/2816

#### 5.106.0 - 2017-10-04
* BREAKING: Stricter parsing of framework requirements - https://github.com/fsprojects/Paket/pull/2816

#### 5.105.0 - 2017-10-04
* BREAKING: Automatic license download is now disabled because of github rate limit trouble - https://fsprojects.github.io/Paket/dependencies-file.html
* Parallel execution of bootstrapper - https://github.com/fsprojects/Paket/pull/2752

#### 5.104.0 - 2017-10-03
* The `Paket.Restore.targets` will be extracted on paket restore - https://github.com/fsprojects/Paket/issues/2817
* Touch the `Paket.Restore.targets`file only if changes exist

#### 5.103.0 - 2017-10-02
* Support for .NET 4.7.1 - https://github.com/fsprojects/Paket/pull/2815

#### 5.102.0 - 2017-10-02
* CLEANUP: Remove dnxcore50 moniker from lock file - https://github.com/fsprojects/Paket/pull/2813

#### 5.101.0 - 2017-10-01
* PERFORMANCE: Improvements in framework restriction parsing - https://github.com/fsprojects/Paket/pull/2807
* BREAKING: To make the performance optimizations possible API-compat has been broken slightly

#### 5.100.3 - 2017-09-29
* BUGFIX: Add MonoAndroid v8 - https://github.com/fsprojects/Paket/issues/2800

#### 5.100.2 - 2017-09-22
* BUGFIX: Removed V3 -> V2 fallback - https://github.com/fsprojects/Paket/pull/2782

#### 5.100.1 - 2017-09-22
* BUGFIX: Sign paket.exe and paket.bootstrapper.exe again

#### 5.99.1 - 2017-09-21
* BUGFIX: Disable NU1603 - https://github.com/NuGet/Home/issues/5913

#### 5.99.0 - 2017-09-21
* Adding feature to verify the URL and credential correctness before storing them in paket.config - https://github.com/fsprojects/Paket/pull/2781

#### 5.98.0 - 2017-09-21
* BUGFIX: Properly extract cli tools to NuGet user folder - https://github.com/fsprojects/Paket/issues/2784
* BUGFIX: Use "--references-file" instead of "--references-files" in paket.targets - https://github.com/fsprojects/Paket/pull/2780

#### 5.97.0 - 2017-09-18
* BUGFIX: Do not evaluate all templates with --template switch - https://github.com/fsprojects/Paket/pull/2769
* BUGFIX: fix incorrect runtime assemblies - https://github.com/fsprojects/Paket/pull/2772
* BUGFIX: fix for #2755 - https://github.com/fsprojects/Paket/pull/2770
* BUGFIX: #2716 Duplicates appear in generated scripts. - https://github.com/fsprojects/Paket/pull/2767
* BUILD: do not export the MSBuild env-var to the outer shell - https://github.com/fsprojects/Paket/pull/2754
* BUGFIX: support proxy in netstandard - https://github.com/fsprojects/Paket/pull/2738
* BUGFIX: Make the resolver request only sources returned by GetVersion - https://github.com/fsprojects/Paket/pull/2771
* BUGFIX: Fix special cases with include-referenced-projects - https://github.com/fsprojects/Paket/issues/1848
* BUGFIX: Proper filter by target framework - https://github.com/fsprojects/Paket/issues/2759

#### 5.96.0 - 2017-09-13
* USABILITY: Print package version in "paket why" - https://github.com/fsprojects/Paket/pull/2760

#### 5.95.0 - 2017-09-12
* Allow to add packages without running the resolver - https://github.com/fsprojects/Paket/issues/2756

#### 5.94.0 - 2017-09-12
* Allow to set "redirects: force" on group level - https://github.com/fsprojects/Paket/pull/2666
* BUGFIX: Trim target frameworks for .NET cli - https://github.com/fsprojects/Paket/issues/2749

#### 5.93.0 - 2017-09-10
* BUGFIX: Don't depend on restore cache when using paket.local or force switch - https://github.com/fsprojects/Paket/pull/2734
* BUGFIX: MSBuild now tracks Paket.Restore.targets for incremental builds - https://github.com/fsprojects/Paket/pull/2742
* BUGFIX: Removed default 100sec timeout for Http dependencies download - https://github.com/fsprojects/Paket/pull/2737
* BUGFIX: Fixing root cause for casing issue in #2676 - https://github.com/fsprojects/Paket/pull/2743
* BUGFIX: Temporary fix for casing issue in #2676 - https://github.com/fsprojects/Paket/pull/2743
* BUGFIX: calculate hashfile after signing the assembly. - https://github.com/fsprojects/Paket/pull/27
* BUGFIX: always allow partial restore - https://github.com/fsprojects/Paket/pull/2724
* BUGFIX: always call GetVersions before GetDetails - https://github.com/fsprojects/Paket/pull/2721
* BUGFIX: Fix a crash when using `storage: package` in-line - https://github.com/fsprojects/Paket/pull/2713
* BOOTSTRAPPER: Add support for IgnoreCache to app.config - https://github.com/fsprojects/Paket/pull/2696
* DOCS: Fix GitHub project dependency description - https://github.com/fsprojects/Paket/pull/2707
* BUGFIX: Fix V3 implementation - https://github.com/fsprojects/Paket/pull/2708
* BUGFIX: Don't use the global cache when paket.local is given - https://github.com/fsprojects/Paket/pull/2709
* BUGFIX: Ignore unknown packages in fix-nuspec - https://github.com/fsprojects/Paket/pull/2710

#### 5.92.0 - 2017-08-30
* BUGFIX: Fix new restore cache - https://github.com/fsprojects/Paket/pull/2684
* PERFORMANCE: Make restore faster - https://github.com/fsprojects/Paket/pull/2675
* BUGFIX: Incorrect warnings on restore - https://github.com/fsprojects/Paket/pull/2687
* PERFORMANCE: Make install faster - https://github.com/fsprojects/Paket/pull/2688

#### 5.92.0-beta003 - 2017-08-30
* Paket comes as signed lib for better antivir support
* BUGFIX: Fix new restore cache - https://github.com/fsprojects/Paket/pull/2684

#### 5.92.0-alpha001 - 2017-08-26
* PERFORMANCE: Make restore faster - https://github.com/fsprojects/Paket/pull/2675

#### 5.91.0 - 2017-08-26
* BUGFIX: fix a bug in the runtime parser - https://github.com/fsprojects/Paket/pull/2665
* BUGFIX: Add props to correct Paket.Restore.targets - https://github.com/fsprojects/Paket/pull/2665
* Make packages folder optional - https://github.com/fsprojects/Paket/pull/2638

#### 5.90.1 - 2017-08-25
* Support for NTLM auth - https://github.com/fsprojects/Paket/pull/2658
* BUGFIX: fix-nuspecs should break at ; - https://github.com/fsprojects/Paket/issues/2661
* BUGFIX: V3 normalization fix for https://github.com/fsprojects/Paket/issues/2652
* BUGFIX: fix crash when a package contains an invalid file - https://github.com/fsprojects/Paket/pull/2644

#### 5.89.0 - 2017-08-21
* BUGFIX: dotnet sdk: disable implicitly adding system.valuetuple and fsharp.core - https://github.com/fsprojects/Paket/pull/2528

#### 5.87.0 - 2017-08-21
* BUGFIX: NuGet v3 protocol fixes - https://github.com/fsprojects/Paket/pull/2632
* BUGFIX: Restore Failure on Mono: System.Exception: Expected an result at this place - https://github.com/fsprojects/Paket/issues/2639

#### 5.86.0 - 2017-08-19
* BUGFIX: Fixed feed Warnings and added blacklisting - https://github.com/fsprojects/Paket/pull/2582
* BUGFIX: Special case System.Net.Http - https://github.com/fsprojects/Paket/pull/2628

#### 5.85.8 - 2017-08-18
* BUGFIX: No file links were created when using File: references in .NET Core projects - https://github.com/fsprojects/Paket/issues/2622

#### 5.85.7 - 2017-08-17
* BUGFIX: Small fixes in PCL detection - https://github.com/fsprojects/Paket/pull/2609

#### 5.85.5 - 2017-08-17
* BUGFIX: Simplify references in groups - https://github.com/fsprojects/Paket/pull/2619

#### 5.85.4 - 2017-08-17
* BUGFIX: Don't change BOM for existing project files - https://github.com/fsprojects/Paket/pull/2575
* BUGFIX: Don't call paket if not necessary on dotnet pack - https://github.com/fsprojects/Paket/pull/2624

#### 5.85.3 - 2017-08-16
* BUGFIX: Don't fail on myget
* USABILITY: Friendlier warnings about obsolete syntax - https://github.com/fsprojects/Paket/pull/2610

#### 5.85.1 - 2017-08-11
* Support for DevExpress feed

#### 5.85.0 - 2017-08-10
* PERFORMANCE: Do not scan packages folders for restore
* PERFORMANCE: Faster lookup in ProGet

#### 5.84.0 - 2017-07-30
* Better error reporting for conflicts that appear late in resolution
* Protecting Paket.Restore.targets against changes in dotnet template - https://github.com/fsprojects/Paket/pull/2569

#### 5.83.1 - 2017-07-29
* Paket allows to resolve prereleases in a transitive way - https://github.com/fsprojects/Paket/pull/2559
* BUGFIX: Fixed download of multiple HTTP resources - https://github.com/fsprojects/Paket/issues/2566
* Update to FSharp.Core 4.2.2

#### 5.82.0 - 2017-07-28
* The outdated command now allows to pass the -f flag
* BUGFIX: Fixed exception when paket outdated runs on a repo with a http zip dependency - https://github.com/fsprojects/Paket/pull/2565
* BUGFIX: Fixed edge case with endsWithIgnoreCase - https://github.com/fsprojects/Paket/pull/2562
* BUGFIX: Fixed push for large packages - https://github.com/fsprojects/Paket/pull/2555
* BUGFIX: Fixed generate-load-scripts case sensitivity - https://github.com/fsprojects/Paket/issues/2547

#### 5.81.0 - 2017-07-21
* BUGFIX: Pass along empty arguments in bootstrapper - https://github.com/fsprojects/Paket/issues/2551

#### 5.80.0 - 2017-07-20
* BUGFIX: Fixed find-packages - https://github.com/fsprojects/Paket/issues/2545
* BUGFIX: zsh completion: support paths with spaces - https://github.com/fsprojects/Paket/pull/2546
* BUGFIX: Allow feed element in getbyid response - https://github.com/fsprojects/Paket/pull/2541
* BUGFIX: Multi-Target support for new MSBuild - https://github.com/fsprojects/Paket/issues/2496#issuecomment-316057881
* BUGFIX: Version in path and load scripts should work together
* USABILITY: Check that we printed an error
* USABILITY: Do not spam script generation messages (these are no under -v)

#### 5.78.0 - 2017-07-18
* Support Xamarin.tvOS and Xamarin.watchOS - https://github.com/fsprojects/Paket/pull/2535
* BUGFIX: Version in path and load scripts should work together - https://github.com/fsprojects/Paket/issues/2534
* BUGFIX: Detect subfolders like "Lib" - https://github.com/fsprojects/Paket/issues/2533

#### 5.7.0 - 2017-07-17
* BUGFIX: Multi-Target support for new MSBuild (needs paket install to update the Paket.Restore.targets)
* NuGet convert can detect cli tools - https://github.com/fsprojects/Paket/issues/2518
* BUGFIX: Unescape urls in odata response - https://github.com/fsprojects/Paket/issues/2504
* BUGFIX: Fix nuspecs only if we use nuspecs
* BUGFIX: Better tracing while downloading packages and licenses
* BUGFIX: Carefuly handle cases when the .paket folder is present in .sln file, not present, or is empty - https://github.com/fsprojects/Paket/pull/2513
* BUGFIX: Better tracing around download link - https://github.com/fsprojects/Paket/issues/2508
* BUGFIX: Work around Proget perf issue - https://github.com/fsprojects/Paket/issues/2466
* BUGFIX: Work around sonatype bug - https://github.com/fsprojects/Paket/issues/2320
* BUGFIX: Work around https://github.com/NuGet/NuGetGallery/issues/4315
* BUGFIX: Check result of PutAsync - https://github.com/fsprojects/Paket/pull/2502
* BUGFIX: Fixed push command
* REVERT "Fixed NugetV2 querying"

#### 5.6.0 - 2017-07-10
* PERFORMANCE: Fixed access to multiple sources (performance) - https://github.com/fsprojects/Paket/pull/2499
* BUGFIX: Improved penalty system https://github.com/fsprojects/Paket/pull/2498
* BUGFIX: Trace warnings to stdout instead of stderr
* USABILITY: Convert-From-NuGet tries to restore into magic mode
* USABILITY: Better error message when references file parsing fails

#### 5.5.0 - 2017-07-07
* Support for dotnet cli tools with clitools keyword in paket.dependencies - https://fsprojects.github.io/Paket/nuget-dependencies.html#Special-case-dotnet-cli-tools
* GNU-compatible command line - https://github.com/fsprojects/Paket/pull/2429
* Add Tizen framework (v3 and v4) - https://github.com/fsprojects/Paket/pull/2492
* USABILITY: find-package-versions now includes default source to match behavior of find-packages - https://github.com/fsprojects/Paket/pull/2493

#### 5.4.8 - 2017-07-06
* BUGFIX: Added default NuGet source back to find-packages - https://github.com/fsprojects/Paket/pull/2489
* BUGFIX: Fixed NugetV2 querying - https://github.com/fsprojects/Paket/pull/2485
* BUGFIX: Show stack trace only in verbose mode - https://github.com/fsprojects/Paket/pull/2481
* BUGFIX: find-packages doesn't require paket.dependencies to be present - https://github.com/fsprojects/Paket/pull/2483
* BUGFIX: Fixed for usage of the new csproj with targetFramework - https://github.com/fsprojects/Paket/pull/2482
* BUGFIX: Fixed off-by-one error when inserting lines in already-existing .paket folder in sln file - https://github.com/fsprojects/Paket/pull/2484
* BUGFIX: Allow any whitespace to precede a comment, not only space in the references file - https://github.com/fsprojects/Paket/pull/2479
* BUGFIX: Doesn't always print the 'warning' message - https://github.com/fsprojects/Paket/pull/2463

#### 5.4.0 - 2017-07-01
* Allow comments in the references file - https://github.com/fsprojects/Paket/pull/2477
* BUGFIX: Allowed empty framework conditionals in paket.template - https://github.com/fsprojects/Paket/pull/2476
* BUGFIX: find-package-versions doesn't require paket.dependencies to be present as long as a source is explicitly specified - https://github.com/fsprojects/Paket/pull/2478

#### 5.3.0 - 2017-06-30
* BUGFIX: Ignoring pre-release status when deps file requested prerelease - https://github.com/fsprojects/Paket/pull/2474
* BUGFIX: Don't remove placeholder from file view - https://github.com/fsprojects/Paket/issues/2469
* BUGFIX: Automatic restore in VS should also work with bootstraper
* BUGFIX: Do not add old myget sources during NuGet convert
* BUGFIX: Increase download timeout - https://github.com/fsprojects/Paket/pull/2456

#### 5.2.0 - 2017-06-25
* BUGFIX: Paket init in "magic" mode deleted paket.exe - https://github.com/fsprojects/Paket/issues/2451
* BUGFIX: Take xamarin.*.csharp.targets into account when finding location - https://github.com/fsprojects/Paket/pull/2460
* BUGFIX: Fixed Package targetFramework for netstandard - https://github.com/fsprojects/Paket/pull/2453
* BUGFIX: Fixed the warning reported in #2440 - https://github.com/fsprojects/Paket/pull/2449
* BUGFIX: Paket.pack: Fixed another issue with duplicate files - https://github.com/fsprojects/Paket/issues/2445
* BUGFIX: Disable AutoRestore features for MSBuild 15 - https://github.com/fsprojects/Paket/issues/2446
* BUGFIX: Add proper versioning of MonoAndroid framework - https://github.com/fsprojects/Paket/pull/2427
* BUGFIX: Paket.pack: Fixed another issue with duplicate files - https://github.com/fsprojects/Paket/issues/2445

#### 5.1.0 - 2017-06-18
* Paket.pack: support for NuGet dependencies conditional on target framework - https://github.com/fsprojects/Paket/pull/2428
* BUGFIX: Overwrite target file when link option is false - https://github.com/fsprojects/Paket/pull/2433
* BUGFIX: Fixed interactive package search - https://github.com/fsprojects/Paket/pull/2424
* USABILITY: Better task cancellation - https://github.com/fsprojects/Paket/pull/2439
* DOGFOODING: Use latest bootstrapper in magic mode

#### 5.0.0 - 2017-06-16
* Live release from NDC room 4
* Support for Fable 1.1
* Using NuGet's new SemVer 2 support - https://github.com/fsprojects/Paket/pull/2402
* Xamarin targets integrate with netstandard - https://github.com/fsprojects/Paket/pull/2396
* Support for SpecificVersion attribute on assembly references - https://github.com/fsprojects/Paket/pull/2413
* New command `paket generate-nuspec`
* New command: `FixNuspecs` - Can fix a list of nuspec files now
* New restriction system - https://github.com/fsprojects/Paket/pull/2336
  * Paket is now more accurate in calculating restrictions and referencing libraries
  * Paket will convert (lock-)files to a new syntax (but still understands the old syntax)
  * This should fix a bunch of edge cases and invalid behavior in combination with portable profiles and netstandard
  * Add support for net403 (required for some portable profiles)
* BREAKING CHANGE: Paket simplify no longer support simplifying restrictions - https://github.com/fsprojects/Paket/pull/2336
* BREAKING CHANGE: Paket.PowerShell is no longer supported
* BREAKING CHANGE: `InstallModel` API changed and Paket.Core.dll users might need to adapt
* PERFORMANCE: Improved performance by pre-loading requests - https://github.com/fsprojects/Paket/pull/2336
* PERFORMANCE: Report performance in a more detailed way - https://github.com/fsprojects/Paket/pull/2336
* PERFORMANCE: Improved performance for some edge case - https://github.com/fsprojects/Paket/pull/2299
* PERFORMANCE: Limit the number of concurrent requests to 7 - https://github.com/fsprojects/Paket/pull/2362
* PERFORMANCE: Report how often the pre-loading feature worked - https://github.com/fsprojects/Paket/pull/2362
* PERFORMANCE: Request queue can now re-prioritize on-demand - https://github.com/fsprojects/Paket/pull/2362
* PERFOMANCE: Much faster paket pack https://github.com/fsprojects/Paket/pull/2409
* DEPRECATED: `FixNuspec` function is now obsolete, use `FixNuspecs` instead
* DEPRECATED: /package-versions API was deprecated for lookup from NuGet team - https://github.com/fsprojects/Paket/pull/2420
* BUGFIX: Better hash checks in bootstrapper - https://github.com/fsprojects/Paket/pull/2368
* BUGFIX: Improved C++ support
* BUGFIX: Fix Conditional Group Dependencies not working as expected - https://github.com/fsprojects/Paket/pull/2335
* BUGFIX: Treat runtime dependencies as transitive deps - https://github.com/fsprojects/Paket/issues/2334
* BUGFIX: Sort dependencies on obj/references files - https://github.com/fsprojects/Paket/issues/2310
* BUGFIX: Support .NET moniker ">= monoandroid" - https://github.com/fsprojects/Paket/issues/2246
* BUGFIX: Paket pack was placing two copies of the project binary to the package - https://github.com/fsprojects/Paket/issues/2421
* BUGFIX: Better dependencies file parser errors
* BUGFIX: "Dotnet restore" failed on .netstandard projects under 1.6 - https://github.com/fsprojects/Paket/issues/2243
* BUGFIX: Paket now accepts multiple nuspec files in fix-nuspec - https://github.com/fsprojects/Paket/pull/2296
* BUGFIX: Fixed pinning of .NETSTANDARD 1.6 packages - https://github.com/fsprojects/Paket/pull/2307
* BUGFIX: Fixed bug with ignored argument of getPackageDetails - https://github.com/fsprojects/Paket/pull/2293
* BUGFIX: HTTP dependency - strip query string to detect a file name - https://github.com/fsprojects/Paket/pull/2295
* BUGFIX: Proper encoding "+" in package download url - https://github.com/fsprojects/Paket/pull/2288
* BUGFIX: Paket failed when group is removed (or renamed) - https://github.com/fsprojects/Paket/pull/2281
* BUGFIX: Filter .targets / .props earlier - https://github.com/fsprojects/Paket/pull/2286
* BUGFIX: Downgrade to tooling 1.0 - https://github.com/fsprojects/Paket/pull/2380
* BUGFIX: Paket added too many targets and props - https://github.com/fsprojects/Paket/pull/2388
* BUGFIX: Paket failed with: String cannot be of zero length - https://github.com/fsprojects/Paket/pull/2407
* BOOTSTRAPPER: Don't crash in DownloadHashFile - https://github.com/fsprojects/Paket/pull/2376
* BOOTSTRAPPER: Search harder for the paket.dependencies file - https://github.com/fsprojects/Paket/pull/2384
* USABILITY: Don't let build continue when paket failed - https://github.com/fsprojects/Paket/pull/2302
* Cleanup https://github.com/fsprojects/Paket/pull/2412 https://github.com/fsprojects/Paket/pull/2410
* Internals: Started proper dotnetcore integration (disabled by default, can be enabled via setting `PAKET_DISABLE_RUNTIME_RESOLUTION` to `false`):
  * Paket now properly understands runtime and reference assemblies
  * Paket now understands the runtime graph and restores runtime dependencies
  * New API `InstallModel.GetRuntimeAssemblies` and `InstallModel.GetRuntimeLibraries` can be used to retrieve the correct assets for a particular RID and TFM

#### 4.8.8 - 2017-06-11
* paket adds too many targets and props - https://github.com/fsprojects/Paket/pull/2388

#### 4.8.6 - 2017-05-23
* USABILITY: Better error reporting - https://github.com/fsprojects/Paket/pull/2349

#### 4.8.5 - 2017-05-08
* BUGFIX: Support .NET moniker ">= monoandroid" - https://github.com/fsprojects/Paket/issues/2246

#### 4.8.4 - 2017-04-26
* BUGFIX: Proper encoding "+" in package download url - https://github.com/fsprojects/Paket/pull/2288

#### 4.8.3 - 2017-04-26
* BUGFIX: Paket failed when group is removed (or renamed) - https://github.com/fsprojects/Paket/pull/2281

#### 4.8.2 - 2017-04-26
* BUGFIX: Filter .targets / .props earlier - https://github.com/fsprojects/Paket/pull/2286

#### 4.8.1 - 2017-04-25
* BREAKING CHANGE: Made pushing changes from Git dependency repositories easier - https://github.com/fsprojects/Paket/pull/2226
    - Paket now clones git dependencies as bare repositories and configures clones under `paket-files` differently. Because of these incompatible changes, it is necessary to manually clear Paket local temp directory (under `%USERPROFILE%\.paket\git\db`) and respective `paket-files` directories after upgrading.

#### 4.7.0 - 2017-04-25
* Bootstrapper: Support NugetSource app-setting key - https://github.com/fsprojects/Paket/pull/2229
* Unity3d support - https://github.com/fsprojects/Paket/pull/2268

#### 4.6.1 - 2017-04-24
* Support for SourceLink v2 - https://github.com/fsprojects/Paket/pull/2200
* BUGFIX: Framework restriction was lost for global build folder - https://github.com/fsprojects/Paket/pull/2272
* BUGFIX: Fixed error when parsing version="*" - https://github.com/fsprojects/Paket/issues/2266

#### 4.5.0 - 2017-04-20
* Support Netstandard 2.0, Netframework 4.7, Netcore 2.0
* Encode '+' in Urls
* BUGFIX: Fix nuspec version attributes so that nuget.org is happy

#### 4.4.0 - 2017-04-12
* BUGFIX: Import .props/.targets better - https://github.com/fsprojects/Paket/pull/2234
* BUGFIX: Don't download boostrapper in auto-restore magic mode - https://github.com/fsprojects/Paket/pull/2235
* BUGFIX: Only include dlls in analyzers - https://github.com/fsprojects/Paket/pull/2236
* USABILITY: Fix rotating app.config entries when generating redirects - https://github.com/fsprojects/Paket/pull/2230

#### 4.3.0 - 2017-04-10
* BUGFIX: Check if a references file exists on disk - https://github.com/fsprojects/Paket/pull/2224

#### 4.2.0 - 2017-04-09
* BUGFIX: Improved output of the outdated warning and fix underlying bug - https://github.com/fsprojects/Paket/pull/2223
* BUGFIX: Make Paket.Restore.targets be called in more situations
* BUGFIX: Fix to handle weird malformed portable-only libraries - https://github.com/fsprojects/Paket/pull/2215
* BUGFIX: Detect changes in redirects settings
* BUGFIX: Workaround for TFS dependency resolution - https://github.com/fsprojects/Paket/pull/2214

#### 4.1.3 - 2017-03-30
* Support for dotnet pack
* BUGFIX: Handle empty references files for .NET Core
* BUGFIX: Better framework node detection
* BUGFIX: Better redirects for project dependent references files
* BUGFIX: Out-of-Sync check should work with auto-detection of framework settings
* BUGFIX: Convert from nuget with wildcard version - https://github.com/fsprojects/Paket/issues/2185
* BUGFIX: Support load script generation in restore
* BUGFIX: framework: auto-detect didn't work with Paket 4 - https://github.com/fsprojects/Paket/issues/2188
* USABILITY: Convert packages that do not have version specified
* COSMETICS: Use latest FSharp.Core

#### 4.0.0 - 2017-03-15
* Make Paket compatible with DotNet SDK / MSBuild 15 / Visual Sudio 2017
* Tail Recursive Package Resolution - https://github.com/fsprojects/Paket/pull/2066
* Reorganized resolver - https://github.com/fsprojects/Paket/pull/2039
* USABILITY: Added option to have paket restore fail on check failure - https://github.com/fsprojects/Paket/pull/1963
* USABILITY: Collect multiple install errors before failing - https://github.com/fsprojects/Paket/pull/2177
* Generate load scripts on install abidding to new paket.dependencies option - https://fsprojects.github.io/Paket/dependencies-file.html#Generate-load-scripts

#### 3.37.0 - 2017-03-15
* BUGFIX: auto-detect no longer causes Out of sync warning - https://github.com/fsprojects/Paket/issues/2096
* BUGFIX: Allow to add package when sources are splitted - https://github.com/fsprojects/Paket.VisualStudio/issues/137
* USABILITY: Remove confusing yellow diagnostics in pack - https://github.com/fsprojects/Paket/issues/2164
* USABILITY: Support TLS > 1.0 - https://github.com/fsprojects/Paket/issues/2174
* USABILITY: old bootstrapper did not work

#### 3.36.0 - 2017-02-25
* BUGFIX: Lower case group folder name - https://github.com/fsprojects/Paket/pull/2150
* BUGFIX: Fix resolver for Strategy.Min - https://github.com/fsprojects/Paket/issues/2148
* BUGFIX: Fix TFS-on-premise - https://github.com/fsprojects/Paket/pull/2147
* BUGFIX: Add a workaround for https://github.com/fsprojects/Paket/issues/2145
* BUGFIX: Ignore unknown frameworks - https://github.com/fsprojects/Paket/pull/2132
* COSMETICS: Do not spam "unlisted" - https://github.com/fsprojects/Paket/issues/2149
* USABILITY: Link to documentation on how to resolve a conflict - https://github.com/fsprojects/Paket/pull/2155

#### 3.35.0 - 2017-01-30
* Added "netcoreapp1.1" support - https://github.com/fsprojects/Paket/pull/2129
* BUGFIX: Ensures that boostrapper --help always work - https://github.com/fsprojects/Paket/pull/2128
* USABILITY: Reports broken project dependencies properly - https://github.com/fsprojects/Paket/pull/2131
* USABILITY: Added details for "clear-cache" in --verbose mode - https://github.com/fsprojects/Paket/pull/2130

#### 3.34.0 - 2017-01-29
* BUGFIX: Support GitHub dependencies with spaces - https://github.com/fsprojects/Paket/pull/2127
* BUGFIX: Convert from nuget: Local package source gave false error - https://github.com/fsprojects/Paket/pull/2112
* BUGFIX: Make config writer use XmlWriter for disk write - https://github.com/fsprojects/Paket/pull/2110
* BUGFIX: Ensure case when getting packages from nuget feed - https://github.com/fsprojects/Paket/pull/2106
* BUGFIX: Ensure stable ordering of references

#### 3.33.0 - 2017-01-06
* USABILITY: Ensure stable ordering of references in the same ItemGroup - https://github.com/fsprojects/Paket/pull/2105
* BUGFIX: Template with multiparagraph description was not working with LF line endings - https://github.com/fsprojects/Paket/issues/2104

#### 3.32.0 - 2017-01-02
* paket outdated: group -parameter added - https://github.com/fsprojects/Paket/pull/2097
* BUGFIX: Fix "directory doesn't exist" in NuGet v2 - https://github.com/fsprojects/Paket/pull/2102
* BUGFIX: Correctly escape no_proxy domains for bootstraper - https://github.com/fsprojects/Paket/pull/2100
* BUGFIX: Don't print incorrect warning in bootstraper - https://github.com/fsprojects/Paket/pull/2098
* BUGFIX: Update Argu to 3.6.1
* BUGFIX: Revert argu update
* BUGFIX: If we have ref and lib files then we prefer lib
* BUGFIX: Don't remove group with only remote files - https://github.com/fsprojects/Paket/pull/2089
* BUGFIX: Fix displayed package name for packages found in another group - https://github.com/fsprojects/Paket/pull/2088
* BUGFIX: Avoid infinite recursive calls in followODataLink - https://github.com/fsprojects/Paket/pull/2081
* BUGFIX: One of the file writes was missing a Directory.Create() - https://github.com/fsprojects/Paket/pull/2080
* BUGFIX: NuGetV2-OData: retrieve versions in descending order for artifactory - https://github.com/fsprojects/Paket/pull/2073
* BUGFIX: Default address of NuGet v3 stream points to https - https://github.com/fsprojects/Paket/pull/2071

#### 3.31.0 - 2016-12-04
* Added monoandroid70 moniker (Android 7 Nougat) - https://github.com/fsprojects/Paket/pull/2065
* BUGFIX: Package names are compared using non-linguistic Ordinal comparison - https://github.com/fsprojects/Paket/pull/2067
* BUGFIX: Fixed Git dependency change detection - https://github.com/fsprojects/Paket/pull/2061
* BUGFIX: Relax prerelease condition for --keep-patch - https://github.com/fsprojects/Paket/issues/2048
* BUGFIX: Allow specify auto-detect in specific groups - https://github.com/fsprojects/Paket/issues/2011

#### 3.30.0 - 2016-11-22
* Allow override of NuGetCacheFolder location through environment variable - https://github.com/fsprojects/Paket/pull/2035
* BUGFIX: Add authorization headers to Paket Push - https://github.com/fsprojects/Paket/pull/2034
* BUGFIX: Fix package name displayed when package is found in different group - https://github.com/fsprojects/Paket/issues/2031
* BUGFIX: Report which nuspec file is invalid when the nuspec cannot be loaded - https://github.com/fsprojects/Paket/issues/2026

#### 3.29.0 - 2016-11-18
* BUGFIX: Paket adds stricter prerelease dependencies to make NuGet happy - https://github.com/fsprojects/Paket/issues/2024

#### 3.28.0 - 2016-11-17
* BUGFIX: Optimize deps to make #2020 work - https://github.com/fsprojects/Paket/pull/2020
* BUGFIX: Added missing tolower() - https://github.com/fsprojects/Paket/pull/2023
* BUGFIX: Fix broken condition in WhenNode - https://github.com/fsprojects/Paket/pull/2022
* REVERT: NuGetV2-OData: retrieve versions in descending order - https://github.com/fsprojects/Paket/pull/2008
* BUGFIX: Git Dependency failed to install when space exists in User Folder name - https://github.com/fsprojects/Paket/pull/2015

#### 3.27.0 - 2016-11-09
* Verbose bootstrapper - https://github.com/fsprojects/Paket/pull/2007
* BUGFIX: NuGetV2-OData: retrieve versions in descending order - https://github.com/fsprojects/Paket/pull/2008
* BUGFIX: Paket doesn't reference libs for UWP apps - https://github.com/fsprojects/Paket/issues/2001
* BUGFIX: Version constraint was missing on referenced projects packed separately - https://github.com/fsprojects/Paket/issues/1976
* BUGFIX: Make download loop to terminate in max N=5 iterations - https://github.com/fsprojects/Paket/pull/1999

#### 3.26.0 - 2016-10-31
* New Command: paket why - http://theimowski.com/blog/2016/10-30-paket-why-command/index.html
* BUGFIX: Do not remove main group - https://github.com/fsprojects/Paket/issues/1950
* BUGFIX: Fix out-of-date-check
* BUGFIX: Be more conservative during paket add and paket remove - https://github.com/fsprojects/Paket/issues/1652

#### 3.25.0 - 2016-10-28
* Allow to put required paket version into the paket.dependencies file - https://github.com/fsprojects/Paket/pull/1983
* BUGFIX: Custom print for NugetSourceAuthentication types - https://github.com/fsprojects/Paket/pull/1985
* BUGFIX: DependenciesFileParser now tracks inner exceptions for package sources - https://github.com/fsprojects/Paket/pull/1987

#### 3.24.1 - 2016-10-25
* USABILITY: New magic mode bootstrapper - https://github.com/fsprojects/Paket/pull/1961
* USABILITY: Specify Chessie version - https://github.com/fsprojects/Paket/issues/1958
* REVERT: Support long paths for NTFS - https://github.com/fsprojects/Paket/pull/1944

#### 3.23.0 - 2016-10-10
* BUGFIX: Support long paths for NTFS - https://github.com/fsprojects/Paket/pull/1944

#### 3.22.0 - 2016-10-10
* BUGFIX: generate-include-scripts: don't check dll order when it can be skipped - https://github.com/fsprojects/Paket/pull/1945
* BUGFIX: generate-include-script doesn't not #r FSharp.Core.dll anymore - https://github.com/fsprojects/Paket/pull/1946
* BUGFIX: Paket failed to get packages from feed with credentials - https://github.com/fsprojects/Paket/pull/1947
* BUGFIX: Fix public API
* BUGFIX: Set network credentials - https://github.com/fsprojects/Paket/issues/1941
* BUGFIX: Swapped parameters of FindVersionsForPackage
* BUGFIX: Transforming wildcard syntax to regex, which is used by WebProxy for NoProxy bypassing - https://github.com/fsprojects/Paket/pull/1939
* BUGFIX: Work around dependencies issue in VSTS - https://github.com/fsprojects/Paket/issues/1798
* COSMETICS: XML paket.config is now beautified - https://github.com/fsprojects/Paket/pull/1954

#### 3.21.0 - 2016-10-04
* Added MsBuild reserved properties - https://github.com/fsprojects/Paket/pull/1934
* BUGFIX: Make VisualStudio.com nuget feed behave like nuget.org - https://github.com/fsprojects/Paket/issues/1798
* BUGFIX: Generate binding redirect that covers entire range of possible assembly versions - https://github.com/fsprojects/Paket/pull/1932
* COSMETICS: Paket shows context for missing references - https://github.com/fsprojects/Paket/issues/1936

#### 3.20.2 - 2016-09-29
* BUGFIX: Fix dependency compression issue - https://github.com/fsprojects/Paket/issues/1929
* BUGFIX: Calling `Paket.Dependencies.GetInstalledPackageModel` with wrong casing on mono failed - https://github.com/fsprojects/Paket/issues/1928
* BUGFIX: Convert from nuget with analyzers - https://github.com/fsprojects/Paket/pull/1922
* BUGFIX: Don't fail on restore - https://github.com/fsprojects/Paket/pull/1923
* BUGFIX: Fix double space encoding during pack - https://github.com/fsprojects/Paket/issues/1837
* BUGFIX: Try to resolve "$(TargetFrameworkIdentifier) == 'true'" issue
* BUGFIX: Push correct Paket.Core - https://github.com/fsprojects/Paket/pull/1911

#### 3.19.0 - 2016-09-04
* NEW Dotnetcore build for Paket.Core - https://github.com/fsprojects/Paket/pull/1785
* BUGFIX: Allow to overwrite copy_local settings for ref files
* BUGFIX: Fixed invalid Cache Folder when Current Directory is different - https://github.com/fsprojects/Paket/issues/1910

#### 3.18.0 - 2016-09-02
* BUGFIX: Fixed issues around .NET Standard resolution
* BUGFIX: Fixed toLower > tolower for odata url parameter - https://github.com/fsprojects/Paket/pull/1906
* BUGFIX: Fix deduplication condition
* Revert fix for #1898

#### 3.17.0 - 2016-08-29
* Added Add MonoAndroid44 moniker - https://github.com/fsprojects/Paket/pull/1897
* Notified about missing libs will only be shown on direct packages (too many false positives)
* Fixed props import for fsproj/cspro - https://github.com/fsprojects/Paket/issues/1898
* BUGFIX: Do not copy ref files to output dir - https://github.com/fsprojects/Paket/issues/1895
* BUGFIX: Scan group folder for packages
* BUGFIX: Better NuGet V3 API and async caching - https://github.com/fsprojects/Paket/pull/1892
* BUGFIX: Resolving .net standard depedencies for net46 - https://github.com/fsprojects/Paket/issues/1883
* BUGFIX: Change project file condition handling to be case-insensitive - https://github.com/fsprojects/Paket/pull/1890

#### 3.16.3 - 2016-08-25
* BUGFIX: Don't remove non-duplicate framework dependencies - https://github.com/fsprojects/Paket/pull/1888

#### 3.16.2 - 2016-08-25
* BUGFIX: Fixed lowest_matching constraint - https://github.com/fsprojects/Paket/pull/1882

#### 3.16.1 - 2016-08-25
* Allow printing of version number through command-line option - https://github.com/fsprojects/Paket/pull/1878
* BUGFIX: Async cache fix in multi-thread-environment for GitHub downloads - https://github.com/fsprojects/Paket/pull/1880

#### 3.16.0 - 2016-08-24
* Allow to use github access token from environment variable for github dependencies - http://fsprojects.github.io/Paket/github-dependencies.html#Using-a-GitHub-auth-key-from-environment-variable
* BUGFIX: Look for OutDir in .vcxproj - https://github.com/fsprojects/Paket/issues/1870
* USABILITY: Skip invalid meta-data in cpp projects - https://github.com/fsprojects/Paket/issues/1870
* USABILITY: Add better tracing during resolve - https://github.com/fsprojects/Paket/issues/1871
* USABILITY: Use .dll as default during pack - https://github.com/fsprojects/Paket/issues/1870

#### 3.15.0 - 2016-08-23
* When converting from Nuget Paket removes NuGetPackageImportStamp - https://github.com/fsprojects/Paket/pull/1865
* BUGFIX: Fixed strange issue during directory cleanup
* BUGFIX: Fallback to LocalApplicationData if we don't have UserProfile avaulable - https://github.com/fsprojects/Paket/issues/1863
* BUGFIX: Fixed octokit parsing - https://github.com/fsprojects/Paket/issues/1867
* BUGFIX: Faulty conditions were generated when using condition attributes - https://github.com/fsprojects/Paket/issues/1860

#### 3.14.0 - 2016-08-22
* Show message when a package version is not installed because it is unlisted
* BUGFIX: Bootstrapper had issues with partial download - https://github.com/fsprojects/Paket/pull/1859
* BUGFIX: Use ConcurrentDictionary correctly - https://github.com/fsprojects/Paket/pull/1853

#### 3.13.0 - 2016-08-12
* Allow to pack referenced projects by setting paket.template switch - https://github.com/fsprojects/Paket/issues/1851

#### 3.12.0 - 2016-08-12
* BUGFIX: Paket doesn't add duplicate references to framework assemblies anymore - https://github.com/fsprojects/Paket/issues/1333
* BUGFIX: Run resolver after convert
* BUGFIX: Selective paket update doesn't ignore paket.dependencies rules anymore - https://github.com/fsprojects/Paket/issues/1841
* BUGFIX: Update with any of the --keep-?? flags didn't honour redirects:on in paket.dependencies - https://github.com/fsprojects/Paket/issues/1844

#### 3.11.0 - 2016-08-04
* Allow Pack to pin only project references - https://github.com/fsprojects/Paket/issues/1649

#### 3.10.0 - 2016-08-03
* Allow to specify nupkg version for source override in paket.local file - https://github.com/fsprojects/Paket/issues/1803
* BUGFIX: Allow "auto-restore on" to be done twice - https://github.com/fsprojects/Paket/issues/1836
* BUGFIX: be careful with distinction between .NET 4.0 client and .NET 4.0 full profile - https://github.com/fsprojects/Paket/issues/1830
* BUGFIX: Don't allow empty string as description in template file - https://github.com/fsprojects/Paket/pull/1831
* BUGFIX: Respect comments in dependencies file

#### 3.9.0 - 2016-07-22
* Don't create runtime references for CoreClr anymore - new concept coming soon
* BUGFIX: Allow to install packages that have "native" in package name - https://github.com/fsprojects/Paket/issues/1829
* PERFORMANCE: Much faster computation of the InstallModel

#### 3.8.0 - 2016-07-18
* Paket automatically packs localized assemblies - https://github.com/fsprojects/Paket/pull/1816
* BUGFIX: Fix possible null ref when processing a vcxproj file - https://github.com/fsprojects/Paket/issues/1814
* BUGFIX: Changing NuGet uri from http to https in paket.dependencies don't causes error any more - https://github.com/fsprojects/Paket/issues/1820
* BUGFIX: Paket 'pack' should exclude 'project' template files correctly - https://github.com/fsprojects/Paket/issues/1818
* PERFORMANCE: Do not scan node_modules path for project files - https://github.com/fsprojects/Paket/issues/1782
* Exposed license url in public namespace - https://github.com/fsprojects/Paket/pull/1811

#### 3.7.0 - 2016-07-14
* Paket automatically packs localized assemblies - https://github.com/fsprojects/Paket/pull/1807
* BUGFIX: Fixed incorrect CopyRuntimeDependencies.ProjectFile causing 'Could not find paket.dependencies' - https://github.com/fsprojects/Paket/pull/1802

#### 3.6.0 - 2016-07-12
* Generate include script for each group - https://github.com/fsprojects/Paket/pull/1787
* USABILITY: Improve error messages for dependency groups - https://github.com/fsprojects/Paket/pull/1797

#### 3.5.0 - 2016-07-12
* Support for .NET 4.6.3 and .NET Standard 1.6
* Using Argu 3
* Support groups in paket.local - https://github.com/fsprojects/Paket/pull/1788
* Paket config can be run from everywhere - https://github.com/fsprojects/Paket/pull/1781
* BUGFIX: Install older frameworks if things don't work out - https://github.com/fsprojects/Paket/issues/1779
* BUGFIX: Fixed detection of framework version with spaces - https://github.com/fsprojects/Paket/pull/1791
* BUGFIX: Fixed error with local sources and run convert-from-nuget - https://github.com/fsprojects/Paket/pull/1795

#### 3.4.0 - 2016-06-30
* Inaccessible caches are excluded for the duration of running a command - https://github.com/fsprojects/Paket/pull/1770
* BUGFIX: NuGet OData search is now case-insensitive - https://github.com/fsprojects/Paket/issues/1775
* BUGFIX: Allows to use colons in git build argument - https://github.com/fsprojects/Paket/issues/1773
* BUGFIX: auto-restore on fixes old targets file references - https://github.com/fsprojects/Paket/issues/1768
* BUGFIX: Added handling for cache not being accessible - https://github.com/fsprojects/Paket/pull/1764
* BUGFIX: Fixed out-of-date check for remote files - https://github.com/fsprojects/Paket/issues/1760  https://github.com/fsprojects/Paket/issues/1762 https://github.com/fsprojects/Paket/issues/1766
* BUGFIX: Using network cache with invalid credentials should not fail restore - https://github.com/fsprojects/Paket/issues/1758
* BUGFIX: Make the copy task more robust if we can't parse target framework - https://github.com/fsprojects/Paket/issues/1756
* BUGFIX: Paket warns on dependencies file that has same package twice in same group - https://github.com/fsprojects/Paket/issues/1757
* USABILITY: Show out-of-sync warning message if paket.lock is not matching paket.dependencies - https://github.com/fsprojects/Paket/issues/1750
* COSMETICS: Don't trace download of remote files twice

#### 3.3.0 - 2016-06-25
* Paket fails on dependencies file that has same package twice in same group - https://github.com/fsprojects/Paket/issues/1757
* Paket.SemVer.Parse is now in PublicAPI.fs - https://github.com/fsprojects/Paket/pull/1754
* BUGFIX: Automatic repair of broken file paths in NuGet packages - https://github.com/fsprojects/Paket/issues/1755
* BUGFIX: Fixed out-of-date check for auto-detection of frameworks - https://github.com/fsprojects/Paket/issues/1750

#### 3.2.0 - 2016-06-24
* Show out-of-sync error message if paket.lock is not matching paket.dependencies - https://github.com/fsprojects/Paket/issues/1750
* BUGFIX: Dependency resolution for .NETFramework4.5 and .NETPortable0.0-wp8+netcore45+net45+wp81+wpa81 fixed - https://github.com/fsprojects/Paket/issues/1753
* BUGFIX: Don't report warnings for packages that are not installed for current target framework - https://github.com/fsprojects/Paket/issues/1693
* BUGFIX: Runtime deps are copied based on TargetFramework - https://github.com/fsprojects/Paket/issues/1751
* BUGFIX: Do not take over control over manual nodes - https://github.com/fsprojects/Paket/issues/1746
* BUGFIX: Better error message when log file is missing - https://github.com/fsprojects/Paket/issues/1743
* BUGFIX: Create folder if needed during package extraction - https://github.com/fsprojects/Paket/issues/1741
* BUGFIX: Simplify works with auto-detected target frameworks - https://github.com/fsprojects/Paket/pull/1740
* BUGFIX: Make sure Guid in project reference is parsed well - https://github.com/fsprojects/Paket/pull/1738
* BUGFIX: Added a username and password option scripting - https://github.com/fsprojects/Paket/pull/1736
* BUGFIX: Trailing slash will be removed from credentials - https://github.com/fsprojects/Paket/pull/1735
* COSMETICS: Add condition to AfterBuild target to unbreak nCrunch - https://github.com/fsprojects/Paket/pull/1734
* BUGFIX: Ignore case in aliases dll names - https://github.com/fsprojects/Paket/pull/1733

#### 3.1.0 - 2016-06-16
* Paket pack doesn't allow empty string as authors and description metadata - https://github.com/fsprojects/Paket/pull/1728
* Made Name and Guid in ProjectRefrence optional - https://github.com/fsprojects/Paket/issues/1729
* BUGFIX: Prerelease version range are working with ~> again
* BUGFIX: Filter empty When conditions - https://github.com/fsprojects/Paket/issues/1727
* BUGFIX: Do not garbage collect packages with version in path

#### 3.0.0 - 2016-06-15
* Allow to reference git repositories - http://fsprojects.github.io/Paket/git-dependencies.html
* Allow to run build commands on git repositories - http://fsprojects.github.io/Paket/git-dependencies.html#Running-a-build-in-git-repositories
* Allow to use git repositories as NuGet source - http://fsprojects.github.io/Paket/git-dependencies.html#Using-Git-repositories-as-NuGet-source
* Allow to override package sources in paket.local - http://fsprojects.github.io/Paket/local-file.html http://theimowski.com/blog/2016/05-19-paket-workflow-for-testing-new-nuget-package-before-release/index.html
* NEW COMMAND: "paket generate-include-scripts" creates package include scripts for F# Interactive - http://fsprojects.github.io/Paket/paket-generate-include-scripts.html
* Additional local caches - http://fsprojects.github.io/Paket/caches.html
* Garbage collection in packages folder - https://github.com/fsprojects/Paket/pull/1491
* Allows to exclude dll references from a NuGet package - http://fsprojects.github.io/Paket/references-files.html#Excluding-libraries
* Allows to use aliases for libraries - http://fsprojects.github.io/Paket/references-files.html#Library-aliases
* Create Choose nodes for .NET Standard
* Remove command removes empty group when removing last dependency - https://github.com/fsprojects/Paket/pull/1706
* New bootstrapper option --max-file-age - http://fsprojects.github.io/Paket/bootstrapper.html
* USABILITY: Removed "specs:" from paket.lock since it was copied from Bundler and had no meaning in Paket - https://github.com/fsprojects/Paket/pull/1608
* BREAKING CHANGE: "lib", "runtimes" are not allowed as group names
* BREAKING CHANGE: Removed --hard parameter from all commands.
    - Paket threads all commands as if --hard would have been set - https://github.com/fsprojects/Paket/pull/1567
    - For the --hard use in the binding redirects there is a new parameter --clean-redirects - https://github.com/fsprojects/Paket/pull/1692

#### 2.66.10 - 2016-06-15
* BUGFIX: Paket update failed on silverlight projects - https://github.com/fsprojects/Paket/pull/1719

#### 2.66.9 - 2016-06-03
* BUGFIX: Automatic prerelease expansion should not be done if explicit prereleases are requested - https://github.com/fsprojects/Paket/issues/1716 https://github.com/fsprojects/Paket/issues/1714

#### 2.66.6 - 2016-05-31
* BUGFIX: Groups with different sources should not resolve to wrong packages - https://github.com/fsprojects/Paket/issues/1711

#### 2.66.5 - 2016-05-30
* BUGFIX: Don't remove trailing zero if version is in package path - https://github.com/fsprojects/Paket/issues/1708

#### 2.66.4 - 2016-05-26
* BUGFIX: Optimization of local dependencies - https://github.com/fsprojects/Paket/issues/1703

#### 2.66.3 - 2016-05-24
* BUGFIX: Use utf-8 to download strings - https://github.com/fsprojects/Paket/pull/1702

#### 2.66.2 - 2016-05-23
* BUGFIX: Update with any of the --keep-major flag didn't honour content:none in paket.dependencies - https://github.com/fsprojects/Paket/issues/1701

#### 2.66.0 - 2016-05-23
* Package groups be excluded in a paket.template file - https://github.com/fsprojects/Paket/pull/1696
* BUGFIX: Fallback from portable to net45 must be conversative - https://github.com/fsprojects/Paket/issues/1117

#### 2.65.0 - 2016-05-18
* BUGFIX: Fixed compatibility issues with nuget.org and myget - https://github.com/fsprojects/Paket/pull/1694
* BUGFIX: DateTime in package should not be in the future
* BUGFIX: Don't push non existing files - https://github.com/fsprojects/Paket/pull/1688
* BUGFIX: Paket should imports build targets from packages in build dependency groups - https://github.com/fsprojects/Paket/pull/1674
* BUGFIX: Framework resolution strategy for Google.Apis.Oauth2.v2 - https://github.com/fsprojects/Paket/issues/1663
* BUGFIX: Blacklisting install.xdt and uninstall.xdt files - https://github.com/fsprojects/Paket/pull/1667

#### 2.64.0 - 2016-05-05
* Implemented support for NativeReference - https://github.com/fsprojects/Paket/issues/1658
* Added monoandroid60 to be matched as Some MonoAndroid - https://github.com/fsprojects/Paket/pull/1659
* BUGFIX: Understand InterprojectDependencies without Name - https://github.com/fsprojects/Paket/issues/1657
* BUGFIX: Fix path issue on linux - https://github.com/fsprojects/Paket/pull/1644/files
* BUGFIX: Don't pack template files in packages or paket-files

#### 2.63.0 - 2016-04-22
* Added monoandroid43 to be matched as Some MonoAndroid - https://github.com/fsprojects/Paket/pull/1631
* Added support for MonoAndroid22 and MonoAndroid23 - https://github.com/fsprojects/Paket/pull/1628
* BUGFIX: allow directory names with + in paket.template
* BUGFIX: Generates binding redirect for references targeting different profiles - https://github.com/fsprojects/Paket/pull/1634
* EXPERIMENTAL: paket resolves runtime dependency libs - https://github.com/fsprojects/Paket/pull/1626
* USABILITY: remove command restricts install to the specified group only - https://github.com/fsprojects/Paket/pull/1612

#### 2.62.0 - 2016-04-17
* Refactoring Bootstrapper to introduce better coverage and testing - https://github.com/fsprojects/Paket/pull/1603

#### 2.61.0 - 2016-04-17
* Support .NET platform standard packages - https://github.com/fsprojects/Paket/issues/1614
* Support .NET 4.6.2 - https://github.com/fsprojects/Paket/issues/1614
* BUGFIX: Don't set CopyToOutputDirectory for Compile items - https://github.com/fsprojects/Paket/issues/1592
* BUGFIX: Allow to pack packages with ReflectedDefinition - https://github.com/fsprojects/Paket/pull/1602

#### 2.60.0 - 2016-04-12
* Various performance optimizations - https://github.com/fsprojects/Paket/pull/1599
* BUGFIX: Fix CleanDir function - https://github.com/fsprojects/Paket/commit/1c2250ed5fae51a5f086325347fecefe16bba27a#commitcomment-17064085
* BUGFIX: Detect net30 moniker

#### 2.59.0 - 2016-04-12
* BUGFIX: Remove process should remove packages from specified groups - https://github.com/fsprojects/Paket/issues/1596
* BUGFIX: Compare full filename for pack with template file - https://github.com/fsprojects/Paket/issues/1594
* BUGFIX: Dependencies file should not take shortened versions - https://github.com/fsprojects/Paket/issues/1591
* BUGFIX: Breaking some parallism and trying to prevent race conditions - https://github.com/fsprojects/Paket/issues/1589
* BUGFIX: "paket.exe pack" with "include-referenced-projects" and "minimum-from-lock-file" did not work when project references have a paket.template file - https://github.com/fsprojects/Paket/issues/1586
* BUGFIX: Property Definitions are placed after FSharp Targets - https://github.com/fsprojects/Paket/issues/1585
* BUGFIX: Redirects for assemblies in the GAC were removed - https://github.com/fsprojects/Paket/issues/1574
* BUGFIX: Paket.dependency with version ranges failed when package has pinned dependency and that version is unlisted - https://github.com/fsprojects/Paket/issues/1579
* BUGFIX: Github dependencies reference transitive NuGet packages to projects - https://github.com/fsprojects/Paket/issues/1578
* BUGFIX: Add "*.fsi" files as <Compile> by default - https://github.com/fsprojects/Paket/pull/1573
* BUGFIX: Touch feature disabled by default in Add, Update, Install; enabled with --touch-affected-refs - https://github.com/fsprojects/Paket/pull/1571
* BUGFIX: Property Definitions: placed after csharp targets - https://github.com/fsprojects/Paket/pull/1522
* BUGFIX: Create folder for all source file dependencies
* USABILITY: Using saved api key credentials for the push operation - https://github.com/fsprojects/Paket/pull/1570
* USABILITY: Paket update supports combining filter with specific version - https://github.com/fsprojects/Paket/pull/1580

#### 2.57.0 - 2016-03-30
* BUGFIX: Property Definitions: placed after non-paket imports if they directly follow the top property groups - https://github.com/fsprojects/Paket/pull/1561
* BUGFIX: Fixed inconsistent condition generation in paket.lock file - https://github.com/fsprojects/Paket/issues/1552
* BUGFIX: Removing transitive dependencies from dependencies list during pack - https://github.com/fsprojects/Paket/pull/1547
* USABILITY: Better WPF support - https://github.com/fsprojects/Paket/pull/1550

#### 2.56.0 - 2016-03-24
* BUGFIX: Move props definitions further up in project files - https://github.com/fsprojects/Paket/issues/1537
* BUGFIX: Fixed missing src files when packing with symbols on Linux - https://github.com/fsprojects/Paket/pull/1545
* BUGFIX: Ensuring that dependent dll's are not included in the package when usng include-referenced-projects - https://github.com/fsprojects/Paket/pull/1543
* BUGFIX: Global redirects:false is not disabling everything below anymore - https://github.com/fsprojects/Paket/issues/1544

#### 2.55.0 - 2016-03-23
* Correct src folder structure for packing with symbols - https://github.com/fsprojects/Paket/pull/1538
* Fix resolver bug spotted by property based testing - https://github.com/fsprojects/Paket/issues/1524

#### 2.54.0 - 2016-03-21
* It's possible to influence the CopyToOutputDirectory property for content references in project files - http://fsprojects.github.io/Paket/nuget-dependencies.html#CopyToOutputDirectory-settings
* BUGFIX: Fix regression where paket skipped packages with name ending in lib - https://github.com/fsprojects/Paket/issues/1531
* USABILITY: Unknown package settings are now reported
* USABILITY: Improve warning text on conflict - https://github.com/fsprojects/Paket/pull/1530

#### 2.53.0 - 2016-03-19
* Allow to restore recursively from remote dependencies file - https://github.com/fsprojects/Paket/issues/1507
* BUGFIX: Fix mixed mode solutions with Native - https://github.com/fsprojects/Paket/issues/1523
* BUGFIX: Do not generate useless true conditions for Native - https://github.com/fsprojects/Paket/issues/1523
* BUGFIX: Native settings are filtered correctly - https://github.com/fsprojects/Paket/issues/1523
* BUGFIX: Force resolver to look into deeper levels - https://github.com/fsprojects/Paket/issues/1520
* COSMETICS: Emit net40-full moniker instead of net-40
* COSMETICS: Simplify single when conditions with single true statement
* USABILITY: Improved error message when paket.dependencies can't be found - https://github.com/fsprojects/Paket/pull/1519
* USABILITY: Automatically retry with force flag if we can't get package details for a given version - https://github.com/fsprojects/Paket/issues/1526
* USABILITY: Better error message when paket.lock an paket.dependencies are out of sync.
* USABILITY: Content:once doesn't add paket flags to the csproj file in order to make Orleans tools happy - https://github.com/fsprojects/Paket/issues/1513
* USABILITY: Be more robust in paket.references files - https://github.com/fsprojects/Paket/issues/1514
* USABILITY: Improved stability in lock acquiring process - https://github.com/fsprojects/Paket/issues/858

#### 2.52.0 - 2016-03-10
* Allow to restore dll from remote dependencies file - https://github.com/fsprojects/Paket/issues/1507
* Prevent paket holding locks on assemblies during binding redirects - https://github.com/fsprojects/Paket/pull/1492
* ProjectFile.save with forceTouch to only modify the last write time without content if unchanged - https://github.com/fsprojects/Paket/pull/1493
* BUGFIX: Don't accept "Unsupported0.0" as full framework - https://github.com/fsprojects/Paket/issues/1494
* BUGFIX: Revert 1487 - https://github.com/fsprojects/Paket/issues/1487
* BUGFIX: Fall back to v2 for VSTS - https://github.com/fsprojects/Paket/issues/1496
* BUGFIX: Fixed duplicate frameworks during auto-detection - https://github.com/fsprojects/Paket/issues/1500
* BUGFIX: Fixed conditional references created for group dependencies - https://github.com/fsprojects/Paket/issues/1505
* BUGFIX: Fixed parsing error in lock file parser - https://github.com/fsprojects/Paket/issues/1500
* BUGFIX: Merge Chessie into PowerShell package - https://github.com/fsprojects/Paket/issues/1499
* BUGFIX: Make v3 API more robust
* BUGFIX: Do not install packages with same version from different groups twice - https://github.com/fsprojects/Paket/issues/1458
* BUGFIX: When adding framework specification to paket.dependencies .props include was moved to the bottom of csproj file - https://github.com/fsprojects/Paket/issues/1487
* BUGFIX: Allow to use LOCKEDVERSION with packages that are not in main group - https://github.com/fsprojects/Paket/issues/1483
* USABILITY: only complain about missing references if there are references at all

#### 2.51.0 - 2016-02-29
* Experimental Visual C++ support in binding redirects - https://github.com/fsprojects/Paket/issues/1467
* Restore: optional --touch-affected-refs to touch refs affected by a restore - https://github.com/fsprojects/Paket/pull/1485
* BUGFIX: fixed group transitive dependency checking - https://github.com/fsprojects/Paket/pull/1479
* BUGFIX: Do not try to pack output folder - https://github.com/fsprojects/Paket/issues/1473
* BUGFIX: Fix StackOverflow from https://github.com/fsprojects/Paket/issues/1432
* BUGFIX: Do not pack absolute paths - https://github.com/fsprojects/Paket/issues/1472
* BUGFIX: Keep Auth from dependencies file for fast path - https://github.com/fsprojects/Paket/issues/1469
* BUGFIX: Fix Platform matching bug in CPP projects - https://github.com/fsprojects/Paket/issues/1467
* USABILITY: Touch project files when paket.lock changed in order to support incremental builds with MsBuild - https://github.com/fsprojects/Paket/issues/1471
* USABILITY: Prevent paket holding locks on assemblies during binding redirects
* USABILITY: Don't fail when we can't turn on auto-restote during convert

#### 2.50.0 - 2016-02-09
* Experimental Visual C++ support - https://github.com/fsprojects/Paket/issues/1467
* BUGFIX: Install packages that end in .dll - https://github.com/fsprojects/Paket/issues/1466
* BUGFIX: Prevent race condition - https://github.com/fsprojects/Paket/issues/1460
* BUGFIX: Download of HTTP dependencies should delete folder before we unzip
* BUGFIX: Do not touch project files in packages folder - https://github.com/fsprojects/Paket/issues/1455
* BUGFIX: Keep versions locked for dependencies during pack - https://github.com/fsprojects/Paket/issues/1457
* BUGFIX: Do not fail on auth check for remote dependencies file - https://github.com/fsprojects/Paket/issues/1456
* WORKAROUND: Don't use v3 getPackageDetails on nuget.org or myget

#### 2.49.0 - 2016-02-03
* Added paket pack switch minimum-from-lock-file - http://fsprojects.github.io/Paket/paket-pack.html#Version-ranges
* Automatic framework detection - http://fsprojects.github.io/Paket/dependencies-file.html#Automatic-framework-detection
* BUGFIX: Work around auth issues with VSTS feed - https://github.com/fsprojects/Paket/issues/1453
* USABILITY: Show warning if a dependency is installed for wrong target framework - https://github.com/fsprojects/Paket/pull/1445

#### 2.48.0 - 2016-01-28
* New lowest_matching option that allows to use lowest matching version of direct dependencies - http://fsprojects.github.io/Paket/dependencies-file.html#Lowest-matching-option
* BUGFIX: Fix convert-from-nuget command - https://github.com/fsprojects/Paket/pull/1437
* BUGFIX: paket pack with enabled include-referenced-projects flag doesn't throwh NRE - https://github.com/fsprojects/Paket/issues/1434
* BUGFIX: Fixed pack package dependencies for dependent projects - https://github.com/fsprojects/Paket/issues/1429
* BUGFIX: Fixed pack package dependencies for dependent projects - https://github.com/fsprojects/Paket/pull/1417
* BUGFIX: Pack with concrete template file should work for type project - https://github.com/fsprojects/Paket/issues/1414
* BUGFIX: Don't use symbol packages when using filesystem source with symbol package - https://github.com/fsprojects/Paket/issues/1413

#### 2.46.0 - 2016-01-19
* BootStrapper caches paket.exe in NuGet cache - https://github.com/fsprojects/Paket/pull/1400
* Case insensitive autocomplete for NuGet v2 protocol - https://github.com/fsprojects/Paket/pull/1410

#### 2.45.0 - 2016-01-18
* Initial support for autocomplete of private sources - https://github.com/fsprojects/Paket/issues/1298
* Allow to set project url in paket pack
* Added include-pdbs switch in paket.template files - https://github.com/fsprojects/Paket/pull/1403
* BUGFIX: Fixed symbol sources creation on projects that contain linked files - https://github.com/fsprojects/Paket/pull/1402
* BUGFIX: Fixed inter project dependencies
* BUGFIX: Reduce pressure from call stack - https://github.com/fsprojects/Paket/issues/1392
* BUGFIX: Symbols package fix for projects that contained linked files - https://github.com/fsprojects/Paket/pull/1390

#### 2.44.0 - 2016-01-14
* Paket pack for symbols packages allows for pulling in referenced projects. - https://github.com/fsprojects/Paket/pull/1383

#### 2.43.0 - 2016-01-14
* BUGFIX: Use registration data from normalized NuGet version - https://github.com/fsprojects/Paket/issues/1387
* BUGFIX: $(SolutionDir) in ProjectReference include attribute will be parsed - https://github.com/fsprojects/Paket/issues/1377
* BUGFIX: Restore groups sequentially - https://github.com/fsprojects/Paket/issues/1371
* PERFORMANCE: Fix issue with bad performance - https://github.com/fsprojects/Paket/issues/1387
* PERFORMANCE: Try relaxed resolver only when there is a chance to succeed
* USABILITY: Fail if credentials are invalid - https://github.com/fsprojects/Paket/issues/1382

#### 2.42.0 - 2016-01-10
* Nemerle projects support
* BUGFIX: Incorrect package dependencies graph resolution with prereleases - https://github.com/fsprojects/Paket/pull/1359
* BUGFIX: NuGetV2: avoid revealing password also if more than one source is defined - https://github.com/fsprojects/Paket/pull/1357

#### 2.41.0 - 2016-01-07
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

#### 2.40.0 - 2015-12-29
* BUGFIX: Better packaging of prerelease dependencies - https://github.com/fsprojects/Paket/issues/1316
* BUGFIX: Allow to overwrite versions in template files without id - https://github.com/fsprojects/Paket/issues/1321
* BUGFIX: Accept dotnet54 as moniker
* BUGFIX: Download file:/// to paket-files/localhost
* BUGFIX: Compare normalized Urls
* BUGFIX: Call OnCompleted in Observable.flatten - https://github.com/fsprojects/Paket/pull/1330
* BUGFIX: Allow to restore packages from private feeds - https://github.com/fsprojects/Paket/issues/1326
* PERFORMANCE: Cache which source contains versions in GetVersions - https://github.com/fsprojects/Paket/pull/1327
* PERFORMANCE: Prefer package-versions protocol for nuget.org and myget.org

#### 2.38.0 - 2015-12-22
* Support new NuGet version range for empty restrictions
* USABILITY: Don't use /odata for nuget.org or myget.org
* BUGFIX: paket pack ignored specific-version parameter - https://github.com/fsprojects/Paket/issues/1321
* COSMETICS: Better error messages in GetVersions
* COSMETICS: Normalize NuGet source feeds in lock files
* PERFORMANCE: Keep traffic for GetVersions and GetPackageDetails low

#### 2.37.0 - 2015-12-21
* New "clear-cache" command allows to clear the NuGet cache - http://fsprojects.github.io/Paket/paket-clear-cache.html
* Paket checks PackageDetails only for sources that responded with versions for a package - https://github.com/fsprojects/Paket/issues/1317
* Implemented support for specifying per-template versions in paket pack - https://github.com/fsprojects/Paket/pull/1314
* Added support for relative src link to package content - https://github.com/fsprojects/Paket/pull/1311
* BUGFIX: Fix NullReferenceException - https://github.com/fsprojects/Paket/issues/1307
* BUGFIX: Check that cached NuGet package belongs to requested package
* BUGFIX: NuGet packages with FrameworkAssembly nodes did not work - https://github.com/fsprojects/Paket/issues/1306
* Paket install did an unnecessary update when framework restriction were present - https://github.com/fsprojects/Paket/issues/1305
* COSMETICS: No need to show cache warnings

#### 2.36.0 - 2015-12-10
* Getting assembly metadata without loading the assembly - https://github.com/fsprojects/Paket/pull/1293

#### 2.35.0 - 2015-12-09
* "redirects off" skips binding redirects completely - https://github.com/fsprojects/Paket/pull/1299

#### 2.34.0 - 2015-12-07
* BootStrapper uses named temp files - https://github.com/fsprojects/Paket/pull/1296
* Making user prompts work with stdin - https://github.com/fsprojects/Paket/pull/1292

#### 2.33.0 - 2015-12-04
* Option to force a binding redirects - https://github.com/fsprojects/Paket/pull/1290
* Use GetCustomAttributesData instead of GetCustomAttributes - https://github.com/fsprojects/Paket/issues/1289
* Don't touch app.config if we don't logically change it - https://github.com/fsprojects/Paket/issues/1248
* Normalize versions in lock file for nuget.org - https://github.com/fsprojects/Paket/issues/1282
* Using AssemblyTitle if no title is specified in a project template - https://github.com/fsprojects/Paket/pull/1285
* Binding redirects should work with multiple groups - https://github.com/fsprojects/Paket/issues/1284
* Resolver is more tolerant with prereleases - https://github.com/fsprojects/Paket/issues/1280

#### 2.32.0 - 2015-12-02
* Provided more user-friendly messages for bootstrapper - https://github.com/fsprojects/Paket/pull/1278
* EXPERIMENTAL: Added ability to create symbol/source packages - https://github.com/fsprojects/Paket/pull/1275
* BUGFIX: Fixed coreProps root element in generated nuspec - https://github.com/fsprojects/Paket/pull/1276

#### 2.31.0 - 2015-12-01
* Add options to force Nuget source and use local file paths with bootstrapper - https://github.com/fsprojects/Paket/pull/1268
* Implement exclude parameter for pack - https://github.com/fsprojects/Paket/pull/1274
* Handle different platforms in ProjectFile.GetOutputPath - https://github.com/fsprojects/Paket/pull/1269
* Support local read-only .nupkg-files - https://github.com/fsprojects/Paket/pull/1272

#### 2.30.0 - 2015-12-01
* Switched to using Chessie Nuget package - https://github.com/fsprojects/Paket/pull/1266
* Adding .NET 4.6.1 support - https://github.com/fsprojects/Paket/issues/1270

#### 2.29.0 - 2015-11-27
* Allow specifying Nuget Source and provide option to specify parameters with config file in bootstrapper - https://github.com/fsprojects/Paket/pull/1261
* BUGFIX: Do not normalize versions since it might break Klondike - https://github.com/fsprojects/Paket/issues/1257
* COSMETICS: Better error message when lock file doesn't contain version pin - https://github.com/fsprojects/Paket/issues/1256
* COSMETICS: Show a warning when the resolver selects an unlisted version - https://github.com/fsprojects/Paket/pull/1258

#### 2.28.0 - 2015-11-25
* Reuse more of the NuGet v3 API for protocol selection
* Using new NuGet v3 protocol to retrieve unlisted packages - https://github.com/fsprojects/Paket/issues/1254
* Created installer demo - https://github.com/fsprojects/Paket/issues/1251
* Adding monoandroid41 framework moniker - https://github.com/fsprojects/Paket/pull/1245
* BUGFIX: Specifying prereleases did not work with pessimistic version constraint - https://github.com/fsprojects/Paket/issues/1252
* BUGFIX: Unlisted property get properly filled from NuGet v3 API - https://github.com/fsprojects/Paket/issues/1242
* BUGFIX: Bootstrapper compares version per SemVer - https://github.com/fsprojects/Paket/pull/1236
* PERFORMANCE: Avoid requests to teamcity that lead to server error
* USABILITY: If parsing of lock file fails Paket reports the lock file filename - https://github.com/fsprojects/Paket/issues/1247

#### 2.27.0 - 2015-11-19
* Binding redirects get cleaned during install - https://github.com/fsprojects/Paket/pull/1235
* BUGFIX: Bootstrapper compares version per SemVer - https://github.com/fsprojects/Paket/pull/1236
* BUGFIX: Do not print feed password to output - https://github.com/fsprojects/Paket/pull/1238
* USABILITY: Always write non-version into lock file to keep ProGet happy - https://github.com/fsprojects/Paket/issues/1239

#### 2.26.0 - 2015-11-18
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

#### 2.25.0 - 2015-11-13
* Unified cache implementation for V2 and V3 - https://github.com/fsprojects/Paket/pull/1222
* BUGFIX: Putting .props and .targets import on correct location in project files - https://github.com/fsprojects/Paket/issues/1219
* BUGFIX: Propagate framework restriction correctly - https://github.com/fsprojects/Paket/issues/1213
* BUGFIX: Match auth - https://github.com/fsprojects/Paket/issues/1210
* BUGFIX: Better error message when something goes wrong during package download

#### 2.24.0 - 2015-11-11
* Support for feeds that only provide NuGet v3 API - https://github.com/fsprojects/Paket/pull/1205
* BUGFIX: Made PublicAPI.ListTemplateFiles more robust - https://github.com/fsprojects/Paket/pull/1209
* BUGFIX: Allow to specify empty file patterns in paket.template
* BUGFIX: Filter excluded dependencies in template files - https://github.com/fsprojects/Paket/issues/1208
* BUGFIX: Framework dependencies were handled too strict - https://github.com/fsprojects/Paket/issues/1206

#### 2.23.0 - 2015-11-09
* Allow to exclude dependencies in template files - https://github.com/fsprojects/Paket/issues/1199
* Exposed TemplateFile types and Dependencies member - https://github.com/fsprojects/Paket/pull/1203
* Paket uses lock free version of Async.Choice
* Paket generates and parses strategy option in lock file - https://github.com/fsprojects/Paket/pull/1196
* BUGFIX: Fixed version requirement parse issue noticed in FsBlog
* USABILITY: Paket shows parsing errors in app.config files - https://github.com/fsprojects/Paket/issues/1195

#### 2.22.0 - 2015-11-05
* Paket adds binding redirect only for applicable assemblies - https://github.com/fsprojects/Paket/issues/1187
* BUGFIX: Add missing transitive dependencies after paket update - https://github.com/fsprojects/Paket/issues/1190
* BUGFIX: Work around issue with # in file names on mono - https://github.com/fsprojects/Paket/issues/1189
* USABILITY: Better error reporting when prereleases are involved - https://github.com/fsprojects/Paket/issues/1186

#### 2.21.0 - 2015-11-01
* Adding LOCKEDVERSION placeholder to templatefile - https://github.com/fsprojects/Paket/issues/1183

#### 2.20.0 - 2015-10-30
* Allow filtered updates of packages matching a regex - https://github.com/fsprojects/Paket/pull/1178
* Search for paket.references in startup directory (auto-restore feature) - https://github.com/fsprojects/Paket/pull/1179
* BUGFIX: Framework filtering for transisitve packages - https://github.com/fsprojects/Paket/issues/1182

#### 2.19.0 - 2015-10-29
* Resolver changed to breadth first search to escape more quickly from conflict situations - https://github.com/fsprojects/Paket/issues/1174
* Paket init downloads stable version of bootstraper - https://github.com/fsprojects/Paket/issues/1040
* BUGFIX: SemVer updates were broken

#### 2.18.0 - 2015-10-28
* Use branch and bound strategy to escape quickly from conflict situations - https://github.com/fsprojects/Paket/issues/1169
* Queries all feeds in parallel for package details
* New moniker monoandroid50 - https://github.com/fsprojects/Paket/pull/1171
* Reintroduced missing public API functions for docs
* USABILITY: Improved paket's conflict reporting during resolution time - https://github.com/fsprojects/Paket/pull/1168

#### 2.17.0 - 2015-10-24
* Global "oldest matching version" resolver strategy option - http://fsprojects.github.io/Paket/dependencies-file.html#Strategy-option
* Convert-from-nuget and simplify commands simplify framework restrictions if possible - https://github.com/fsprojects/Paket/pull/1159
* BUGFIX: Queries every NuGet feed in parallel and combines the results - https://github.com/fsprojects/Paket/pull/1163
* USABILITY: Give better error message when a file can't be found on a github repo - https://github.com/fsprojects/Paket/issues/1162

#### 2.16.0 - 2015-10-21
* Check that download http status code was 200
* Try to report better error when file is blocked by Firewall - https://github.com/fsprojects/Paket/pull/1155
* BUGFIX: Fixed loading of Project files on mono - https://github.com/fsprojects/Paket/pull/1149
* PERFORMANCE: Caching proxy scheme - https://github.com/fsprojects/Paket/pull/1153
* USABILITY: If caching fails Paket should recover - https://github.com/fsprojects/Paket/issues/1152

#### 2.15.1 - 2015-10-17
* BUGFIX: Fixed framework restriction filter - https://github.com/fsprojects/Paket/pull/1146
* BUGFIX: Fixed parsing of framework restrictions in lock file - https://github.com/fsprojects/Paket/pull/1144
* BUGFIX: Add monoandroid403 to be matched as Some MonoAndroid - https://github.com/fsprojects/Paket/pull/1140
* PERFORMANCE: Use locked version as prefered version when resolver strategy is min - https://github.com/fsprojects/Paket/pull/1141
* COSMETICS: Better error messages when resolver finds no matching version.
* COSMETICS: Fix error message when resolver already resolved to GlobalOverride - https://github.com/fsprojects/Paket/issues/1142

#### 2.14.0 - 2015-10-15
* BUGFIX: Handle silverlight framework identifiers comparison - https://github.com/fsprojects/Paket/pull/1138

#### 2.13.0 - 2015-10-14
* Show-Groups command - http://fsprojects.github.io/Paket/paket-show-groups.html
* BUGFIX: Fixed combine operation for framework restrictions - https://github.com/fsprojects/Paket/issues/1137
* BUGFIX: Lockfile-Parser did not to parse framework restrictions and therefore paket install could lead to wrong lock file - https://github.com/fsprojects/Paket/issues/1135
* USABILITY: Non-SemVer InformationalVersion are now allowed for paket pack - https://github.com/fsprojects/Paket/issues/1134
* USABILITY: Dependencies file parser should detects comma between install settings - https://github.com/fsprojects/Paket/issues/1129
* COSMETICS: Don't show the pin notice if dependency is transitive
* COSMETICS: Don't allow negative numbers in SemVer

#### 2.12.0 - 2015-10-12
* Better SemVer update by adding --keep-major, --keep-minor, --keep-patch to the CLI
* EXPERIMENTAL: Support for WiX installer projects

#### 2.11.0 - 2015-10-09
* Skip unchanged groups during install

#### 2.10.0 - 2015-10-08
* Make resolver to evaluate versions lazily
* BUGFIX: Paket.Pack was broken on filesystems with forward slash seperator - https://github.com/fsprojects/Paket/issues/1119
* BUGFIX: Wrong paket ProjectRefences name causes incorrect packaging - https://github.com/fsprojects/Paket/issues/1113

#### 2.9.0 - 2015-10-05
* Allow to use GitHub tokens to access GitHub files - http://fsprojects.github.io/Paket/paket-config.html
* Allow to update a single group
* BUGFIX: Resolver needs to consider Microsoft.Bcl.Build

#### 2.8.0 - 2015-10-03
* BUGFIX: Selective update needs to consider remote files
* BUGFIX: Ignore disabled upstream feeds - https://github.com/fsprojects/Paket/pull/1105
* BUGFIX: Don't forget to add settings from root dependencies
* COSMETICS: Do not write unnecessary framework restrictions into paket.lock

#### 2.7.0 - 2015-10-02
* Support for private GitHub repos - http://fsprojects.github.io/Paket/github-dependencies.html#Referencing-a-private-github-repository
* BUGFIX: Find the mono binary on OSX 10.11 - https://github.com/fsprojects/Paket/pull/1103

#### 2.6.0 - 2015-10-01
* Allow "content:once" as a package setting - http://fsprojects.github.io/Paket/nuget-dependencies.html#No-content-option
* BUGFIX: Don't add -prerelease to nuspec dependency nodes for project references - https://github.com/fsprojects/Paket/issues/1102
* BUGFIX: Do not create prerelease identifiers for transitive dependencies - https://github.com/fsprojects/Paket/issues/1099
* PERFORMANCE: Do not parse remote dependencies file twice - https://github.com/fsprojects/Paket/issues/1101
* PERFORMANCE: Check if we already downloaded paket.dependencies file for remote files in order to reduce stress on API limit - https://github.com/fsprojects/Paket/issues/1101
* PERFORMANCE: Run all calls against different NuGet protocols in parallel and take the fastest - https://github.com/fsprojects/Paket/issues/1085
* PERFORMANCE: Exclude duplicate NuGet feeds - https://github.com/fsprojects/Paket/issues/1085
* COSMETICS: Cache calls to GitHub in order to reduce stress on API limit - https://github.com/fsprojects/Paket/issues/1101

#### 2.5.0 - 2015-09-29
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

#### 2.4.0 - 2015-09-28
* BUGFIX: Paket does not touch config files when the list of binding redirects to add is empty - https://github.com/fsprojects/Paket/pull/1092
* BUGFIX: Fix unsupported https scheme in web proxy - https://github.com/fsprojects/Paket/pull/1080
* BUGFIX: Ignore DotNET 5.0 framework when TargetFramework 4 is specified - https://github.com/fsprojects/Paket/issues/1066
* BUGFIX: Paket failed with: The input sequence was empty - https://github.com/fsprojects/Paket/issues/1071
* BUGFIX: NullReferenceException in applyBindingRedirects during "update nuget package" - https://github.com/fsprojects/Paket/issues/1074
* COSMETICS: Improve error message for bootstrapper if download of Paket.exe fails - https://github.com/fsprojects/Paket/pull/1091

#### 2.3.0 - 2015-09-21
* Binding redirects from target platform only - https://github.com/fsprojects/Paket/pull/1070
* Allow to enable redirects per package - http://fsprojects.github.io/Paket/nuget-dependencies.html#redirects-settings
* BUGFIX: Install command without a lockfile failed when using groups - https://github.com/fsprojects/Paket/issues/1067
* BUGFIX: Only create packages.config entries for referenced packages - https://github.com/fsprojects/Paket/issues/1065
* BUGFIX: Paket update added an app.config to every project - https://github.com/fsprojects/Paket/issues/1068
* BUGFIX: Use commit w/gist download in RemoteDownload.downloadRemoteFiles - https://github.com/fsprojects/Paket/pull/1069

#### 2.1.0 - 2015-09-16
* Added support for custom internet proxy credentials with env vars - https://github.com/fsprojects/Paket/pull/1061
* Removed microsoft.bcl.build.targets from backlist and instead changed "import_targets" default for that package
* Fix handling of packages.config

#### 2.0.0 - 2015-09-15
* Support for `Dependency groups` in paket.dependencies files - http://fsprojects.github.io/Paket/groups.html
* Support for Roslyn-based analyzers - http://fsprojects.github.io/Paket/analyzers.html
* Support for reference conditions - https://github.com/fsprojects/Paket/issues/1026

#### 1.39.10 - 2015-09-13
* Fixed a bug where install and restore use different paths when specifying a project spec on a HTTP link - https://github.com/fsprojects/Paket/pull/1054
* Fix parsing of output path when condition has no spaces - https://github.com/fsprojects/Paket/pull/1058

#### 1.39.1 - 2015-09-08
* Eagerly create app.config files and add to all projects - https://github.com/fsprojects/Paket/pull/1044

#### 1.39.0 - 2015-09-08
* New Bootstrapper with better handling of Paket prereleases

#### 1.37.0 - 2015-09-07
* Support for authentication and complex hosts for HTTP dependencies - https://github.com/fsprojects/Paket/pull/1052
* Always redirect to the Redirect.Version - https://github.com/fsprojects/Paket/pull/1023
* Improvements in the BootStrapper - https://github.com/fsprojects/Paket/pull/1022

#### 1.34.0 - 2015-08-27
* Paket warns about pinned packages only when a new version is available - https://github.com/fsprojects/Paket/pull/1014
* Trace NuGet package URL if download fails
* Fallback to NuGet v2 feed if no version is found in v3

#### 1.33.0 - 2015-08-23
* Paket handles dynamic OutputPath - https://github.com/fsprojects/Paket/pull/942
* Paket warns when package is pinned - https://github.com/fsprojects/Paket/pull/999

#### 1.32.0 - 2015-08-19
* BUGFIX: Fixed compatibility issues with Klondike NuGet server - https://github.com/fsprojects/Paket/pull/997
* BUGFIX: Escape file names in a NuGet compatible way - https://github.com/fsprojects/Paket/pull/996
* BUGFIX: Paket now fails if an update of a nonexistent package is requested - https://github.com/fsprojects/Paket/pull/995

#### 1.31.0 - 2015-08-18
* BUGFIX: Delete old nodes from proj files - https://github.com/fsprojects/Paket/issues/992
* COSMETICS: Better conflict reporting - https://github.com/fsprojects/Paket/pull/994

#### 1.30.0 - 2015-08-18
* BUGFIX: Include prereleases when using NuGet3 - https://github.com/fsprojects/Paket/issues/988
* paket.template allows comments with # or // - https://github.com/fsprojects/Paket/pull/991

#### 1.29.0 - 2015-08-17
* Xamarin iOS + Mac Support - https://github.com/fsprojects/Paket/pull/980
* Handling fallbacks mainly for Xamarin against PCLs - https://github.com/fsprojects/Paket/pull/980
* Removed supported platforms for MonoTouch and MonoAndroid - https://github.com/fsprojects/Paket/pull/980
* Paket only creates requirements from lock file when updating a single package - https://github.com/fsprojects/Paket/pull/985

#### 1.28.0 - 2015-08-13
* Selective update shows better error message on conflict - https://github.com/fsprojects/Paket/pull/980
* Paket init adds default feed - https://github.com/fsprojects/Paket/pull/981
* Show better error message on conflict - https://github.com/fsprojects/Paket/issues/534
* Make option names for paket find-package-versions consistent with the other commands - https://github.com/fsprojects/Paket/issues/890
* Update specifying version does not pin version in paket.dependencies - https://github.com/fsprojects/Paket/pull/979

#### 1.27.0 - 2015-08-13
* Version range semantics changed for `>= x.y.z prerelease` - https://github.com/fsprojects/Paket/issues/976
* BUGFIX: Version trace got lost - https://twitter.com/indy9000/status/631201649219010561
* BUGFIX: copy_local behaviour was broken - https://github.com/fsprojects/Paket/issues/972

#### 1.26.0 - 2015-08-10
* BUGFIX: Paket mixed responses and downloads - https://github.com/fsprojects/Paket/issues/966

#### 1.25.0 - 2015-08-10
* Fix case-sensitivity of boostrapper on mono
* Reactive NuGet v3
* Check for conflicts in selective update - https://github.com/fsprojects/Paket/pull/964
* BUGFIX: Escape file names - https://github.com/fsprojects/Paket/pull/960

#### 1.23.0 - 2015-08-04
* BUGFIX: Selective update resolves the graph for selected package - https://github.com/fsprojects/Paket/pull/957

#### 1.22.0 - 2015-07-31
* Use FSharp.Core 4.0
* Fix build exe path which includes whitespace - https://github.com/fsprojects/ProjectScaffold/pull/185
* Preserve encoding upon saving solution - https://github.com/fsprojects/Paket/pull/940
* BUGFIX: If we specify a templatefile in paket pack it still packs all templates - https://github.com/fsprojects/Paket/pull/944
* BUGFIX: If we specify a type project templatefile in paket pack it should find the project - https://github.com/fsprojects/Paket/issues/945
* BUGFIX: Paket pack succeeded even when there're missing files - https://github.com/fsprojects/Paket/issues/948
* BUGFIX: FindAllFiles should handle paths that are longer than 260 characters - https://github.com/fsprojects/Paket/issues/949

#### 1.21.0 - 2015-07-23
* Allow NuGet packages to put version in the path - https://github.com/fsprojects/Paket/pull/928

#### 1.20.0 - 2015-07-21
* Allow to get version requirements from paket.lock instead of paket.dependencies - https://github.com/fsprojects/Paket/pull/924
* Add new ASP.NET 5.0 monikers - https://github.com/fsprojects/Paket/issues/921
* BUGFIX: Paket crashed with Null Ref Exception for MBrace - https://github.com/fsprojects/Paket/issues/923
* BUGFIX: Exclude submodules from processing - https://github.com/fsprojects/Paket/issues/918

#### 1.19.0 - 2015-07-13
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

#### 1.18.0 - 2015-06-22
* Exclusion syntax for paket.template files - https://github.com/fsprojects/Paket/pull/882
* BUGFIX: Issue with `paket pack` and multiple paket.template files fixed - https://github.com/fsprojects/Paket/issues/893

#### 1.17.0 - 2015-06-22
* Tab completion for installed packages in Paket.PowerShell - https://github.com/fsprojects/Paket/pull/892
* BUGFIX: Find-package-versions did not work - https://github.com/fsprojects/Paket/issues/886
* BUGFIX: Find-packages did not work - https://github.com/fsprojects/Paket/issues/888 https://github.com/fsprojects/Paket/issues/889
* COSMETICS: Improved the documentation for the commands - https://github.com/fsprojects/Paket/pull/891

#### 1.16.0 - 2015-06-21
* Make sure retrieved versions are ordered by version with latest version first - https://github.com/fsprojects/Paket/issues/886
* PowerShell argument tab completion for Paket-Add - https://github.com/fsprojects/Paket/pull/887
* Detection of DNX and DNXCore frameworks
* BUGFIX: Exceptions were not logged to command line - https://github.com/fsprojects/Paket/pull/885

#### 1.15.0 - 2015-06-18
* Paket.PowerShell support for Package Manager Console - https://github.com/fsprojects/Paket/pull/875
* Fix download of outdated files - https://github.com/fsprojects/Paket/issues/876

#### 1.14.0 - 2015-06-14
* Chocolatey support for Paket.PowerShell - https://github.com/fsprojects/Paket/pull/872
* BUGFIX: Single version in deps file created invalid dependend package- https://github.com/fsprojects/Paket/issues/871

#### 1.13.0 - 2015-06-12
* Paket.PowerShell support - https://github.com/fsprojects/Paket/pull/839
* EXPERIMENTAL: Allow link:false settings for file references in `paket.references` files
* BUGFIX: `paket update` did not pick latest prerelease version of indirect dependency - https://github.com/fsprojects/Paket/issues/866

#### 1.12.0 - 2015-06-09
* BUGFIX: Paket add should not update the package if it's already there
* BUGFIX: "copy_local" was not respected for indirect dependencies - https://github.com/fsprojects/Paket/issues/856
* BUGFIX: Suggest only packages from the installed sources - https://github.com/fsprojects/Paket.VisualStudio/issues/57
* BUGFIX: Trace license warning only in verbose mode - https://github.com/fsprojects/Paket/issues/862
* BUGFIX: Fix ./ issues during pack
* BUGFIX: Serialize != operator correctly - https://github.com/fsprojects/Paket/issues/857
* COSMETICS: Don't save the `paket.lock` file if it didn't changed

#### 1.11.0 - 2015-06-08
* Support for cancelling bootstrapper - https://github.com/fsprojects/Paket/pull/860
* Increase timeout for restricted access mode - https://github.com/fsprojects/Paket/issues/858

#### 1.10.0 - 2015-06-02
* `paket init` puts Paket binaries into the project path - https://github.com/fsprojects/Paket/pull/853
* Do not duplicate files in the nupkg - https://github.com/fsprojects/Paket/issues/851
* Pack command reuses project version if directly given - https://github.com/fsprojects/Paket/issues/837
* BUGFIX: `paket install` was not respecting `content:none` - https://github.com/fsprojects/Paket/issues/854

#### 1.9.0 - 2015-05-30
* Paket pack allows to specify current nuget version as dependency - https://github.com/fsprojects/Paket/issues/837
* BUGFIX: Fix long version of --silent flag - https://github.com/fsprojects/Paket/pull/849

#### 1.8.0 - 2015-05-28
* Implement --no-install and --redirects for "paket update" - https://github.com/fsprojects/Paket/pull/847
* BUGFIX: Fix inconsistent parameter names - https://github.com/fsprojects/Paket/pull/846

#### 1.7.2 - 2015-05-28
* New `--only-referenced` parameter for restore - https://github.com/fsprojects/Paket/pull/843
* Make the output path relative to the dependencies file - https://github.com/fsprojects/Paket/issues/829
* Analyze content files with case insensitive setting - https://github.com/fsprojects/Paket/issues/816
* BUGFIX: Parse NuGet package prerelease versions containing "-" - https://github.com/fsprojects/Paket/issues/841

#### 1.6.0 - 2015-05-26
* Paket init - init dependencies file with default NuGet source
* Allow to init paket in given directory
* Automatically query all package feeds in "Find packages"
* Allow to override install settings in 'paket.dependencies' with values from 'paket.references' - https://github.com/fsprojects/Paket/issues/836
* BUGFIX: `paket install` fails if package version doesn't match .nupkg file - https://github.com/fsprojects/Paket/issues/834
* BUGFIX: Try to work around issue with mono zip functions - https://github.com/fsharp/FAKE/issues/810

#### 1.5.0 - 2015-05-21
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

#### 1.4.0 - 2015-05-08
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

#### 1.3.0 - 2015-04-30
* Paket keeps paket.dependencies as stable as possible during edits - https://github.com/fsprojects/Paket/pull/802
* `paket push` doesn't need a dependencies file any more - https://github.com/fsprojects/Paket/issues/800
* Added `--self` for self update of bootstrapper - https://github.com/fsprojects/Paket/issues/791
* BUGFIX: `convert-from-nuget` doen`t duplicate sources anymore - https://github.com/fsprojects/Paket/pull/804

#### 1.2.0 - 2015-04-24
* Add Paket.BootStrapper NuGet package - https://github.com/fsprojects/Paket/issues/790

#### 1.1.3 - 2015-04-24
* Fix StackOverflowException when using local path - https://github.com/fsprojects/Paket/issues/795

#### 1.1.2 - 2015-04-24
* `paket add` should not change dependencies file if the package is misspelled - https://github.com/fsprojects/Paket/issues/798

#### 1.1.1 - 2015-04-24
* Support developmentDependency nuget dependencies - https://github.com/fsprojects/Paket/issues/796

#### 1.1.0 - 2015-04-23
* Pack command is able to detect portable frameworks - https://github.com/fsprojects/Paket/issues/797

#### 1.0.2 - 2015-04-23
* `Convert-from-nuget` removes custom import and targets - https://github.com/fsprojects/Paket/pull/792

#### 1.0.1 - 2015-04-20
* New bootstrapper protects paket.exe from incomplete github downloads - https://github.com/fsprojects/Paket/pull/788

#### 1.0.0 - 2015-04-17
* Big release from fsharpex

#### 0.42.1 - 2015-04-17
* BUGFIX: Smart Install is no longer adding dependencies to paket.dependencies if specified in paket.references but not in paket.dependencies - https://github.com/fsprojects/Paket/issues/779
* BUGFIX: Fix smart install when we add a pinned version - https://github.com/fsprojects/Paket/issues/777
* Trace NuGet server response in verbose mode - https://github.com/fsprojects/Paket/issues/775
* BUGFIX: Fixing wrong local path detection with `paket install` - https://github.com/fsprojects/Paket/pull/773
* BUGFIX: Fixed zip opening on mono - https://github.com/fsprojects/Paket/pull/774

#### 0.41.0 - 2015-04-13
* New Testimonials page - http://fsprojects.github.io/Paket/testimonials.html
* New `PAKET.VERSION` environment variable for bootstraper - https://github.com/fsprojects/Paket/pull/771
* `convert-from-nuget` aggregates target framework from packages.config files - https://github.com/fsprojects/Paket/pull/768
* Improved config file formatting with indented binding redirects - https://github.com/fsprojects/Paket/pull/769
* BUGFIX: Fixed home path detection - https://github.com/fsprojects/Paket/pull/770
* COSMETICS: Better error message when `paket.dependencies` is missing - https://github.com/fsprojects/Paket/issues/764

#### 0.40.0 - 2015-04-09
* Try to fix dates in Nuget packages - https://github.com/fsprojects/Paket/issues/761
* `convert-from-nuget` reads target framework from packages.config files - https://github.com/fsprojects/Paket/pull/760
* Allow . in target file names for pack - https://github.com/fsprojects/Paket/issues/756

#### 0.39.0 - 2015-04-08
* Upgrading to .NET 4.5
* Removing DotNetZip and using the .NET 4.5 Zip APIs instead - https://github.com/fsprojects/Paket/pull/732
* Boostrapper download without `nuget.exe` - https://github.com/fsprojects/Paket/pull/734
* Added frameworkAssemblies to nuspec templating - https://github.com/fsprojects/Paket/issues/740
* BUGFIX: Only pick up project output files for pack that exactly match assembly filename - https://github.com/fsprojects/Paket/issues/752
* BUGFIX: Detect Silverlight version in csproj files - https://github.com/fsprojects/Paket/issues/751
* BUGFIX: Fix mono timeout during license download - https://github.com/fsprojects/Paket/issues/746
* BUGFIX: Detect `sl` as Silverlight - https://github.com/fsprojects/Paket/issues/744

#### 0.38.0 - 2015-03-30
* The restore process downloads package licenses automatically - https://github.com/fsprojects/Paket/pull/737

#### 0.37.0 - 2015-03-28
* Fallback to NuGet.exe if the bootstrapper fails to download from GitHub - https://github.com/fsprojects/Paket/pull/733
* COSMETICS: Display the file name if Paket crashes on some invalid file - https://github.com/fsprojects/Paket/pull/730

#### 0.36.0 - 2015-03-27
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

#### 0.34.0 - 2015-03-12
* `paket pack` pretty-prints it's nuspec - https://github.com/fsprojects/Paket/issues/691
* Paket packs .MDBs docs into the nupkg - https://github.com/fsprojects/Paket/issues/693
* paket pack / paket.template support wildcard patterns - https://github.com/fsprojects/Paket/issues/690
* Allow empty lines in `paket.template` and report file name if parser fails - https://github.com/fsprojects/Paket/issues/692
* BUGFIX: paket.template - file type respects dir without slash at the end - https://github.com/fsprojects/Paket/issues/698
* BUGFIX: paket-files folder is alwaays relative to `paket.dependencies` - https://github.com/fsprojects/Paket/issues/564
* BUGFIX: `paket install` respects manual paket nodes - https://github.com/fsprojects/Paket/issues/679

#### 0.33.0 - 2015-03-10
* Paket packs XML docs into the nupkg - https://github.com/fsprojects/Paket/issues/689
* BUGFIX: Install settings from `paket.dependencies` should override package settings - https://github.com/fsprojects/Paket/issues/688

#### 0.32.0 - 2015-03-09
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

#### 0.31.2 - 2015-02-26
* BUGFIX: Robust and much faster template file parser - https://github.com/fsprojects/Paket/pull/660

#### 0.31.1 - 2015-02-25
* Use latest FAKE tasks

#### 0.31.0 - 2015-02-25
* BUGFIX: Fix help for init command - https://github.com/fsprojects/Paket/pull/654
* BUGFIX: Allow non-standard API endpoint for push - https://github.com/fsprojects/Paket/pull/652
* BUGFIX: Special case nuget.org
* BUGFIX: paket add/remove with just project name - https://github.com/fsprojects/Paket/pull/650
* BUGFIX: Uploading packages as multiform content type - https://github.com/fsprojects/Paket/pull/651
* BUGFIX: Handle transient dependencies better in pack command - https://github.com/fsprojects/Paket/pull/649
* BUGFIX: Only load custom attributes if not given in TemplateFile or cmd parameter
* BUGFIX: Detect .NET 4.5.1 - https://github.com/fsprojects/Paket/pull/647

#### 0.30.0 - 2015-02-23
* New command: `paket pack` - http://fsprojects.github.io/Paket/paket-pack.html
* New command: `paket push` - http://fsprojects.github.io/Paket/paket-push.html
* Improved command line help - https://github.com/fsprojects/Paket/pull/639
* BUGFIX: fix no_auto_restore option parsing - https://github.com/fsprojects/Paket/issues/632

#### 0.29.0 - 2015-02-18
* Allow local NuGet sources with spaces in `paket.dependencies` - https://github.com/fsprojects/Paket/issues/616
* Streamlined install options in `paket.dependencies` and `paket.references` - https://github.com/fsprojects/Paket/issues/587
* Allow to opt-out of targets import - https://github.com/fsprojects/Paket/issues/587
* New option to add/remove packages for a single project - https://github.com/fsprojects/Paket/pull/610
* BUGFIX: Blacklisted Microsoft.Bcl.Build.targets - https://github.com/fsprojects/Paket/issues/618
* BUGFIX: Selective update doesn't add package twice from `paket.references` anymore
* BUGFIX: `paket install` installs GitHub source files
* COSMETICS: Respect home directories on mono - https://github.com/fsprojects/Paket/issues/612
* COSMETICS: `paket add` inserts the new package in alphabetical position - https://github.com/fsprojects/Paket/issues/596

#### 0.28.0 - 2015-02-16
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

#### 0.27.0 - 2015-02-07
* Install process will reference `.props` and `.targets` files from NuGet packages - https://github.com/fsprojects/Paket/issues/516
* Don't internalize in paket.exe during ILMerge
* Allow to download from pre-authenticated MyGet feed - https://github.com/fsprojects/Paket/issues/466
* BUGFIX: Fix `paket install --hard` for FSharp.Core - https://github.com/fsprojects/Paket/issues/579
* BUGFIX: `paket convert-from-nuget` ignores casing when looking for nuget.targets - https://github.com/fsprojects/Paket/issues/580
* BUGFIX: `paket install` correctly parses HTTP references - https://github.com/fsprojects/Paket/pull/571
* BUGFIX: `paket.dependencies` parser now fails if tokens are not valid
* COSMETICS: Prerelease strings are checked that they don't contain operators
* COSMETICS: Create an install function in the API which takes a `paket.dependencies` file as text - https://github.com/fsprojects/Paket/issues/576

#### 0.26.0 - 2015-01-31
* Allow to opt-out of old frameworks in `paket.dependencies` - http://fsprojects.github.io/Paket/nuget-dependencies.html#Framework-restrictions
* Allow `copy_local` settings in `paket.references` - http://fsprojects.github.io/Paket/references-files.html#copy_local-settings
* COSMETICS: `paket.lock` beautification for HTTP specs - https://github.com/fsprojects/Paket/pull/571

#### 0.25.0 - 2015-01-25
* BUGFIX: If more than one TargetFramework-specific dependency to the same package exist, we take the latest one - https://github.com/fsprojects/Paket/pull/567
* BUGFIX: Removes interactive-shell-check on `add auth` - https://github.com/fsprojects/Paket/pull/565
* BUGFIX: Can parse open NuGet ranges in brackets - https://github.com/fsprojects/Paket/issues/560
* BUGFIX: Detect `net35-client` - https://github.com/fsprojects/Paket/issues/559
* BUGFIX: Show help for `auto-restore` command - https://github.com/fsprojects/Paket/pull/558

#### 0.24.0 - 2015-01-19
* Allow to disable Visual Studio NuGet package restore - http://fsprojects.github.io/Paket/paket-auto-restore.html
* BUGFIX: Probe for unnormalized and normalized versions in local NuGet feeds - https://github.com/fsprojects/Paket/issues/556

#### 0.23.0 - 2015-01-15
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

#### 0.22.0 - 2015-01-05
* Bootstrapper avoids github API - https://github.com/fsprojects/Paket/issues/510
* Refactoring to Railwal Oriented Programming - http://fsharpforfunandprofit.com/rop/
* Always trim line end in lockfile
* Improved binding redirects detection - https://github.com/fsprojects/Paket/pull/507
* Don't catch NullReferenceExceptions for now - https://github.com/fsprojects/Paket/issues/505
* BUGFIX: Paket update nuget X doesn't work - https://github.com/fsprojects/Paket/issues/512

#### 0.21.0 - 2015-01-02
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

#### 0.20.1 - 2014-12-30
* COSMETICS: Trim end of line in lockfile.

#### 0.20.0 - 2014-12-29
* `paket install` performs a selective update based on the changes in the dependencies file - http://fsprojects.github.io/Paket/lock-file.html#Performing-updates
* Paket.exe acquires a lock for all write processes - https://github.com/fsprojects/Paket/pull/469
* New command to add credentials - http://fsprojects.github.io/Paket/paket-config.html#Add-credentials
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

#### 0.18.0 - 2014-12-09
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

#### 0.17.0 - 2014-11-29
* FrameworkHandling: Support more portable profiles and reduce the impact in the XML file
* FrameworkHandling: support extracting Silverlight5.0 and NetCore4.5 - https://github.com/fsprojects/Paket/pull/389
* New command `paket init` - http://fsprojects.github.io/Paket/paket-init.html
* Better error message for missing files in paket.lock file - https://github.com/fsprojects/Paket/pull/402
* BUGFIX: Crash on 'install' when input seq was empty - https://github.com/fsprojects/Paket/pull/395
* BUGFIX: Handle multiple version results from NuGet - https://github.com/fsprojects/Paket/pull/393

#### 0.16.0 - 2014-11-23
* Integrate BindingRedirects into Paket install process - https://github.com/fsprojects/Paket/pull/383
* BUGFIX: Download of GitHub files should clean it's own directory - https://github.com/fsprojects/Paket/issues/385
* BUGFIX: Don't remove custom framework references - https://github.com/fsprojects/Paket/issues/376
* BUGFIX: Path to dependencies file is now relative after `convert-from-nuget` - https://github.com/fsprojects/Paket/pull/379
* BUGFIX: Restore command in targets file didn't work with spaces in paths - https://github.com/fsprojects/Paket/issues/375
* BUGFIX: Detect FrameworkReferences without restrictions in nuspec file and install these
* BUGFIX: Read sources even if we don't find packages - https://github.com/fsprojects/Paket/issues/372

#### 0.15.0 - 2014-11-19
* Allow to use basic framework restrictions in NuGet packages - https://github.com/fsprojects/Paket/issues/307
* Support feeds that don't support NormalizedVersion - https://github.com/fsprojects/Paket/issues/361
* BUGFIX: Use Nuget v2 as fallback
* BUGFIX: Accept and normalize versions like 6.0.1302.0-Preview - https://github.com/fsprojects/Paket/issues/364
* BUGFIX: Fixed handling of package dependencies containing string "nuget" - https://github.com/fsprojects/Paket/pull/363

#### 0.14.0 - 2014-11-14
* Uses Nuget v3 API, which enables much faster resolver
* BUGFIX: Keep project file order similar to VS order
* Support unlisted dependencies if nothing else fits - https://github.com/fsprojects/Paket/issues/327

#### 0.13.0 - 2014-11-11
* New support for general HTTP dependencies - http://fsprojects.github.io/Paket/http-dependencies.html
* New F# Interactive support - http://fsprojects.github.io/Paket/reference-from-repl.html
* New `paket find-refs` command - http://fsprojects.github.io/Paket/paket-find-refs.html
* Migration of NuGet source credentials during `paket convert-from-nuget` - http://fsprojects.github.io/Paket/paket-convert-from-nuget.html#Migrating-NuGet-source-credentials
* Bootstrapper uses .NET 4.0 - https://github.com/fsprojects/Paket/pull/355
* Adding --ignore-constraints to `paket outdated` - https://github.com/fsprojects/Paket/issues/308
* PERFORMANCE: If `paket add` doesn't change the `paket.dependencies` file then the resolver process will be skipped
* BUGFIX: `paket update nuget [PACKAGENAME]` should use the same update strategy as `paket add` - https://github.com/fsprojects/Paket/issues/330
* BUGFIX: Trailing whitespace is ignored in `paket.references`

#### 0.12.0 - 2014-11-07
* New global paket.config file - http://fsprojects.github.io/Paket/paket-config.html
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

#### 0.11.0 - 2014-10-29
* Build a merged install model with all packages - https://github.com/fsprojects/Paket/issues/297
* `paket update` command allows to set a version - http://fsprojects.github.io/Paket/paket-update.html#Updating-a-single-package
* `paket.targets` is compatible with specific references files - https://github.com/fsprojects/Paket/issues/301
* BUGFIX: Paket no longer leaves transitive dependencies in lockfile after remove command - https://github.com/fsprojects/Paket/pull/306
* BUGFIX: Don't use "global override" for selective update process - https://github.com/fsprojects/Paket/issues/310
* BUGFIX: Allow spaces in quoted parameter parsing - https://github.com/fsprojects/Paket/pull/311

#### 0.10.0 - 2014-10-24
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

#### 0.9.0 - 2014-10-22
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

#### 0.8.0 - 2014-10-15
* Smarter install in project files
* Paket handles .NET 4.5.2 and .NET 4.5.3 projects - https://github.com/fsprojects/Paket/issues/260
* New command: `paket update nuget <package id>` - http://fsprojects.github.io/Paket/paket-update.html#Updating-a-single-package
* BUGFIX: Do not expand auth when serializing dependencies file - https://github.com/fsprojects/Paket/pull/259
* BUGFIX: Create catch all case for unknown portable frameworks

#### 0.7.0 - 2014-10-14
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
* BUGFIX: Paket convert-from-nuget failed when package source keys contain invalid XML element chars - https://github.com/fsprojects/Paket/issues/253

#### 0.6.0 - 2014-10-11
* New restore command - http://fsprojects.github.io/Paket/paket-restore.html
* Report if we can't find packages for top level dependencies.
* Faster resolver
* Try /FindPackagesById before /Packages for nuget package version no. retrieval
* New Paket.Core package on NuGet - https://www.nuget.org/packages/Paket.Core/
* BUGFIX: Prefer full platform builds over portable builds

#### 0.5.0 - 2014-10-09
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

#### 0.4.0 - 2014-09-28
* Resolve dependencies for github modules - http://fsprojects.github.io/Paket/http-dependencies.html#Remote-dependencies
* New [--interactive] mode for paket simplify - http://fsprojects.github.io/Paket/paket-simplify.html
* Don't use version in path for github files.
* Better error message when a package resolution conflict arises.

#### 0.3.0 - 2014-09-25
* New command: paket add [--interactive] - http://fsprojects.github.io/Paket/paket-add.html
* New command: paket simplify - http://fsprojects.github.io/Paket/paket-simplify.html
* Better Visual Studio integration by using paket.targets file - http://fsprojects.github.io/Paket/paket-auto-restore.html
* Support for NuGet prereleases - http://fsprojects.github.io/Paket/nuget-dependencies.html#PreReleases
* Support for private NuGet feeds - http://fsprojects.github.io/Paket/nuget-dependencies.html#NuGet-feeds
* New NuGet package version constraints - http://fsprojects.github.io/Paket/nuget-dependencies.html#Further-version-constraints
* Respect case sensitivity for package paths for Linux - https://github.com/fsprojects/Paket/pull/137
* Improved convert-from-nuget command - http://fsprojects.github.io/Paket/paket-convert-from-nuget.html
* New paket.bootstrapper.exe (7KB) allows to download paket.exe from github.com - http://fsprojects.github.io/Paket/paket-auto-restore.html
* New package resolver algorithm
* Better verbose mode - use -v flag
* Version info is shown at paket.exe start
* paket.lock file is sorted alphabetical (case-insensitive)
* Linked source files now all go underneath a "paket-files" folder.
* BUGFIX: Ensure the NuGet cache folder exists
* BUGFIX: Async download fixed on mono

#### 0.2.0 - 2014-09-17
* Allow to directly link GitHub files - http://fsprojects.github.io/Paket/http-dependencies.html
* Automatic NuGet conversion - http://fsprojects.github.io/Paket/paket-convert-from-nuget.html
* Cleaner syntax in paket.dependencies - https://github.com/fsprojects/Paket/pull/95
* Strict mode - https://github.com/fsprojects/Paket/pull/104
* Detecting portable profiles
* Support content files from nuget - https://github.com/fsprojects/Paket/pull/84
* Package names in Dependencies file are no longer case-sensitive - https://github.com/fsprojects/Paket/pull/108

#### 0.1.4 - 2014-09-16
* Only vbproj, csproj, fsproj and pyproj files are handled

#### 0.1.3 - 2014-09-15
* Detect FSharpx.Core in packages

#### 0.1.2 - 2014-09-15
* --hard parameter allows better transition from NuGet.exe

#### 0.1.0 - 2014-09-12
* We are live - yay!
