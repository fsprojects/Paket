module paket.dependenciesFile.DependencyChangesSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain

[<Test>]
let ``should detect remove of single nuget package``() = 
    let before = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net"""

    let lockFileData = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Core (3.3.3)
    Castle.Core-log4net (3.3.3)
      Castle.Core (>= 3.3.3)
      log4net (1.2.10)
    Castle.LoggingFacility (3.3.0)
      Castle.Core (>= 3.3.0)
      Castle.Windsor (>= 3.3.0)
    Castle.Windsor (3.3.0)
      Castle.Core (>= 3.3.0)
    Castle.Windsor-log4net (3.3.0)
      Castle.Core-log4net (>= 3.3.0)
      Castle.LoggingFacility (>= 3.3.0)
    log4net (1.2.10)
"""

    let after = """source http://nuget.org/api/v2"""

    let cfg = DependenciesFile.FromCode(after)
    let lockFile = LockFile.Parse("",toLines lockFileData)
   
    let changedDependencies = DependencyChangeDetection.findNuGetChangesInDependenciesFile(cfg,lockFile)
    let newDependencies = DependencyChangeDetection.GetPreferredNuGetVersions lockFile
    newDependencies
    |> Map.filter (fun k v -> not <| changedDependencies.Contains(k))
    |> shouldEqual Map.empty

[<Test>]
let ``should detect addition of single nuget package``() = 
    let before = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net"""

    let lockFileData = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Core (3.3.3)
    Castle.Core-log4net (3.3.3)
      Castle.Core (>= 3.3.3)
      log4net (1.2.10)
    Castle.LoggingFacility (3.3.0)
      Castle.Core (>= 3.3.0)
      Castle.Windsor (>= 3.3.0)
    Castle.Windsor (3.3.0)
      Castle.Core (>= 3.3.0)
    Castle.Windsor-log4net (3.3.0)
      Castle.Core-log4net (>= 3.3.0)
      Castle.LoggingFacility (>= 3.3.0)
    log4net (1.2.10)
"""

    let after = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net
nuget NUnit"""

    let cfg = DependenciesFile.FromCode(after)
    let lockFile = LockFile.Parse("",toLines lockFileData)
    let changedDependencies = DependencyChangeDetection.findNuGetChangesInDependenciesFile(cfg,lockFile)
   
    let newDependencies = DependencyChangeDetection.GetPreferredNuGetVersions lockFile
    let expected =
        Map.ofList
             [(Constants.MainDependencyGroup,PackageName "Castle.Core"), (SemVer.Parse "3.3.3");
              (Constants.MainDependencyGroup,PackageName "Castle.Core-log4net"), (SemVer.Parse "3.3.3");
              (Constants.MainDependencyGroup,PackageName "Castle.LoggingFacility"), (SemVer.Parse "3.3.0");
              (Constants.MainDependencyGroup,PackageName "Castle.Windsor"), (SemVer.Parse "3.3.0");
              (Constants.MainDependencyGroup,PackageName "Castle.Windsor-log4net"), (SemVer.Parse "3.3.0");
              (Constants.MainDependencyGroup,PackageName "log4net"), (SemVer.Parse "1.2.10")]

    
    newDependencies
    |> Map.filter (fun k v -> not <| changedDependencies.Contains(k))
    |> shouldEqual expected

[<Test>]
let ``should ignore compatible version requirement change for nuget package``() = 
    let before = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net >= 3.2.0"""

    let lockFileData = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Core (3.3.3)
    Castle.Core-log4net (3.3.3)
      Castle.Core (>= 3.3.3)
      log4net (1.2.10)
    Castle.LoggingFacility (3.3.0)
      Castle.Core (>= 3.3.0)
      Castle.Windsor (>= 3.3.0)
    Castle.Windsor (3.3.0)
      Castle.Core (>= 3.3.0)
    Castle.Windsor-log4net (3.3.0)
      Castle.Core-log4net (>= 3.3.0)
      Castle.LoggingFacility (>= 3.3.0)
    log4net (1.2.10)
"""

    let after = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net >= 3.3.0"""

    let cfg = DependenciesFile.FromCode(after)
    let lockFile = LockFile.Parse("",toLines lockFileData)
    let changedDependencies = DependencyChangeDetection.findNuGetChangesInDependenciesFile(cfg,lockFile)
   
    let newDependencies = DependencyChangeDetection.GetPreferredNuGetVersions lockFile
    let expected =
        Map.ofList
            ([(Constants.MainDependencyGroup,PackageName "Castle.Core"), (SemVer.Parse "3.3.3");
              (Constants.MainDependencyGroup,PackageName "Castle.Core-log4net"), (SemVer.Parse "3.3.3");
              (Constants.MainDependencyGroup,PackageName "Castle.LoggingFacility"), (SemVer.Parse "3.3.0");
              (Constants.MainDependencyGroup,PackageName "Castle.Windsor"), (SemVer.Parse "3.3.0");
              (Constants.MainDependencyGroup,PackageName "Castle.Windsor-log4net"), (SemVer.Parse "3.3.0");
              (Constants.MainDependencyGroup,PackageName "log4net"),  (SemVer.Parse "1.2.10")])
    
    newDependencies
    |> Map.filter (fun k v -> not <| changedDependencies.Contains(k))
    |> shouldEqual expected

[<Test>]
let ``should detect incompatible version requirement change for nuget package``() = 
    let before = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net >= 3.2.0"""

    let lockFileData = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Core (3.3.3)
    Castle.Core-log4net (3.3.3)
      Castle.Core (>= 3.3.3)
      log4net (1.2.10)
    Castle.LoggingFacility (3.3.0)
      Castle.Core (>= 3.3.0)
      Castle.Windsor (>= 3.3.0)
    Castle.Windsor (3.3.0)
      Castle.Core (>= 3.3.0)
    Castle.Windsor-log4net (3.3.0)
      Castle.Core-log4net (>= 3.3.0)
      Castle.LoggingFacility (>= 3.3.0)
    log4net (1.2.10)
"""

    let after = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net >= 3.4.0"""

    let cfg = DependenciesFile.FromCode(after)
    let lockFile = LockFile.Parse("",toLines lockFileData)
    let changedDependencies = DependencyChangeDetection.findNuGetChangesInDependenciesFile(cfg,lockFile)
   
    let newDependencies = DependencyChangeDetection.GetPreferredNuGetVersions lockFile
    newDependencies
    |> Map.filter (fun k v -> not <| changedDependencies.Contains(k))
    |> shouldEqual Map.empty

[<Test>]
let ``should detect addition content:none of single nuget package``() = 
    let before = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net"""

    let lockFileData = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Core (3.3.3)
    Castle.Core-log4net (3.3.3)
      Castle.Core (>= 3.3.3)
      log4net (1.2.10)
    Castle.LoggingFacility (3.3.0)
      Castle.Core (>= 3.3.0)
      Castle.Windsor (>= 3.3.0)
    Castle.Windsor (3.3.0)
      Castle.Core (>= 3.3.0)
    Castle.Windsor-log4net (3.3.0)
      Castle.Core-log4net (>= 3.3.0)
      Castle.LoggingFacility (>= 3.3.0)
    log4net (1.2.10)
"""

    let after = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net content:none"""

    let cfg = DependenciesFile.FromCode(after)
    let lockFile = LockFile.Parse("",toLines lockFileData)
    let changedDependencies = DependencyChangeDetection.findNuGetChangesInDependenciesFile(cfg,lockFile)
    changedDependencies.IsEmpty |> shouldEqual false

[<Test>]
let ``should repase detailed lock file``() = 
    let before = """source https://nuget.org/api/v2

nuget AutoMapper ~> 3.2
nuget Castle.Windsor !~> 3.3
nuget DataAnnotationsExtensions 1.1.0.0
nuget EntityFramework 5.0.0
nuget FakeItEasy ~> 1.23
nuget FluentAssertions ~> 3.1
nuget Machine.Specifications ~> 0.9
nuget Machine.Specifications.Runner.Console ~> 0.9
nuget NDbfReader 1.1.1.0
nuget Newtonsoft.Json ~> 6.0
nuget Plossum.CommandLine != 0.3.0.14
nuget PostSharp 3.1.52
nuget SharpZipLib 0.86.0
nuget Topshelf ~> 3.1"""

    let lockFileData = """NUGET
  remote: https://nuget.org/api/v2
  specs:
    AutoMapper (3.3.1)
    C5 (1.0.2.0)
    Castle.Core (3.3.0)
    Castle.Windsor (3.3.0)
      Castle.Core (>= 3.3.0)
    DataAnnotationsExtensions (1.1.0.0)
    EntityFramework (5.0.0)
    FakeItEasy (1.25.2)
    FluentAssertions (3.3.0)
    Machine.Specifications (0.9.1)
    Machine.Specifications.Runner.Console (0.9.0)
    NDbfReader (1.1.1.0)
    Newtonsoft.Json (6.0.8)
    Plossum.CommandLine (0.3.0.14)
      C5 (>= 1.0.2.0)
    PostSharp (3.1.52)
    SharpZipLib (0.86.0)
    Topshelf (3.1.4)
"""

    let after = """source https://nuget.org/api/v2

nuget AutoMapper ~> 3.2
nuget Castle.Windsor !~> 3.3
nuget DataAnnotationsExtensions 1.1.0.0
nuget EntityFramework 5.0.0
nuget FakeItEasy ~> 1.23
nuget FluentAssertions ~> 3.1
nuget Machine.Specifications ~> 0.9
nuget Machine.Specifications.Runner.Console ~> 0.9
nuget NDbfReader 1.1.1.0
nuget Newtonsoft.Json ~> 6.0
nuget Plossum.CommandLine != 0.3.0.14
nuget PostSharp 3.1.52
nuget SharpZipLib 0.86.0
nuget Topshelf ~> 3.1
nuget Caliburn.Micro !~> 2.0.2"""

    let cfg = DependenciesFile.FromCode(after)
    let lockFile = LockFile.Parse("",toLines lockFileData)
    let changedDependencies = DependencyChangeDetection.findNuGetChangesInDependenciesFile(cfg,lockFile)
    changedDependencies.Count |> shouldEqual 1
    (changedDependencies |> Seq.head) |> shouldEqual (Constants.MainDependencyGroup, PackageName "Caliburn.Micro")

[<Test>]
let ``should detect if nothing changes in github dependency``() = 
    let before = """source https://www.nuget.org/api/v2

nuget FAKE

github zurb/bower-foundation css/normalize.css
github zurb/bower-foundation js/foundation.min.js"""

    let lockFileData = """NUGET
  remote: https://nuget.org/api/v2
  specs:
    FAKE (4.4.4)
GITHUB
  remote: zurb/bower-foundation
  specs:
    css/normalize.css (eb5e3ed178ef3b678cb520f1366a737a32aafeca)
    js/foundation.min.js (eb5e3ed178ef3b678cb520f1366a737a32aafeca)
"""

    let after = """source https://www.nuget.org/api/v2

nuget FAKE

github zurb/bower-foundation css/normalize.css
github zurb/bower-foundation js/foundation.min.js"""

    let cfg = DependenciesFile.FromCode(after)
    let lockFile = LockFile.Parse("",toLines lockFileData)
    let changedDependencies = DependencyChangeDetection.findRemoteFileChangesInDependenciesFile(cfg,lockFile)
    changedDependencies.Count |> shouldEqual 0

[<Test>]
let ``should detect new github dependency``() = 
    let before = """source https://www.nuget.org/api/v2

nuget FAKE

github zurb/bower-foundation css/normalize.css
github zurb/bower-foundation js/foundation.min.js"""

    let lockFileData = """NUGET
  remote: https://nuget.org/api/v2
  specs:
    FAKE (4.4.4)
GITHUB
  remote: zurb/bower-foundation
  specs:
    css/normalize.css (eb5e3ed178ef3b678cb520f1366a737a32aafeca)
    js/foundation.min.js (eb5e3ed178ef3b678cb520f1366a737a32aafeca)
"""

    let after = """source https://www.nuget.org/api/v2

nuget FAKE

github zurb/bower-foundation css/normalize.css
github zurb/bower-foundation js/foundation.min.js
github SignalR/bower-signalr jquery.signalR.js"""

    let cfg = DependenciesFile.FromCode(after)
    let lockFile = LockFile.Parse("",toLines lockFileData)
    let changedDependencies = DependencyChangeDetection.findRemoteFileChangesInDependenciesFile(cfg,lockFile)
    changedDependencies.Count |> shouldEqual 1

[<Test>]
let ``should detect removed github dependency``() = 
    let before = """source https://www.nuget.org/api/v2

nuget FAKE

github zurb/bower-foundation css/normalize.css
github zurb/bower-foundation js/foundation.min.js"""

    let lockFileData = """NUGET
  remote: https://nuget.org/api/v2
  specs:
    FAKE (4.4.4)
GITHUB
  remote: zurb/bower-foundation
  specs:
    css/normalize.css (eb5e3ed178ef3b678cb520f1366a737a32aafeca)
    js/foundation.min.js (eb5e3ed178ef3b678cb520f1366a737a32aafeca)
"""

    let after = """source https://www.nuget.org/api/v2

nuget FAKE

github zurb/bower-foundation js/foundation.min.js"""

    let cfg = DependenciesFile.FromCode(after)
    let lockFile = LockFile.Parse("",toLines lockFileData)
    let changedDependencies = DependencyChangeDetection.findRemoteFileChangesInDependenciesFile(cfg,lockFile)
    changedDependencies.Count |> shouldEqual 1

[<Test>]
let ``should detect new github dependency in new group``() = 
    let before = """source https://www.nuget.org/api/v2

nuget FAKE

github zurb/bower-foundation css/normalize.css
github zurb/bower-foundation js/foundation.min.js"""

    let lockFileData = """NUGET
  remote: https://nuget.org/api/v2
  specs:
    FAKE (4.4.4)
GITHUB
  remote: zurb/bower-foundation
  specs:
    css/normalize.css (eb5e3ed178ef3b678cb520f1366a737a32aafeca)
    js/foundation.min.js (eb5e3ed178ef3b678cb520f1366a737a32aafeca)
"""

    let after = """source https://www.nuget.org/api/v2

nuget FAKE

github zurb/bower-foundation css/normalize.css
github zurb/bower-foundation js/foundation.min.js

group Build
github SignalR/bower-signalr jquery.signalR.js"""

    let cfg = DependenciesFile.FromCode(after)
    let lockFile = LockFile.Parse("",toLines lockFileData)
    let changedDependencies = DependencyChangeDetection.findRemoteFileChangesInDependenciesFile(cfg,lockFile)
    changedDependencies.Count |> shouldEqual 1
    changedDependencies |> Set.filter (fun (g,_) -> g = GroupName "Build") |> Set.count |> shouldEqual 1

[<Test>]
let ``should detect removal of group``() = 
    let before = """source https://www.nuget.org/api/v2

nuget FAKE

github zurb/bower-foundation css/normalize.css
github zurb/bower-foundation js/foundation.min.js

group Build
github SignalR/bower-signalr jquery.signalR.js"""

    let lockFileData = """NUGET
  remote: https://nuget.org/api/v2
  specs:
    FAKE (4.4.4)
GITHUB
  remote: zurb/bower-foundation
  specs:
    css/normalize.css (eb5e3ed178ef3b678cb520f1366a737a32aafeca)
    js/foundation.min.js (eb5e3ed178ef3b678cb520f1366a737a32aafeca)
GROUP Build

GITHUB
  remote: SignalR/bower-signalr
  specs:
    jquery.signalR.js (26092205231972de4b3db966e5689ddbee971ef9)
"""

    let after = """source https://www.nuget.org/api/v2

nuget FAKE

github zurb/bower-foundation css/normalize.css
github zurb/bower-foundation js/foundation.min.js"""

    let cfg = DependenciesFile.FromCode(after)
    let lockFile = LockFile.Parse("",toLines lockFileData)
    let changedDependencies = DependencyChangeDetection.findRemoteFileChangesInDependenciesFile(cfg,lockFile)
    changedDependencies.Count |> shouldEqual 1
    changedDependencies |> Set.filter (fun (g,_) -> g = GroupName "Build") |> Set.count |> shouldEqual 1