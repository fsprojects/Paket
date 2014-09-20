#### 0.2.20 - 20.09.2014
* Release paket.targets file

#### 0.2.19 - 20.09.2014
* Use VS targets

#### 0.2.18 - 20.09.2014
* Fix paket outdated

#### 0.2.16 - 20.09.2014
* Remove nuget targets on convert-from-nuget - https://github.com/fsprojects/Paket/pull/127

#### 0.2.15 - 19.09.2014
* Dogfooding without NuGet.exe

#### 0.2.14 - 19.09.2014
* Lockfile is sorted alphabetical (case-insensitive)

#### 0.2.12 - 19.09.2014
* Using paket.bootstrapper

#### 0.2.8 - 19.09.2014
* Ensure the NuGet cache folder exists

#### 0.2.7 - 19.09.2014
* New async download strategy

#### 0.2.6 - 19.09.2014
* --no-install and --force flags for convert-from-nuget - https://github.com/fsprojects/Paket/pull/120

#### 0.2.4 - 18.09.2014
* Fix bug in Portable profile generation

#### 0.2.3 - 18.09.2014
* If you omit the version constraint then Paket will assume `>= 0`.

#### 0.2.2 - 18.09.2014
* Linked source files now all go underneath a "paket-files" folder.

#### 0.2.1 - 18.09.2014
* Don't clear packages folder during convert-from-nuget - our own paket might live there

#### 0.2.0 - 17.09.2014
* Allow to directly link GitHub files - http://fsprojects.github.io/Paket/github_dependencies.html
* Automatic NuGet conversion - http://fsprojects.github.io/Paket/convert_from_nuget.html
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