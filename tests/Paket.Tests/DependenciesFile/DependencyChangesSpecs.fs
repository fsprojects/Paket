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
   
    let changedDependencies = DependencyChangeDetection.findChangesInDependenciesFile(cfg,lockFile)
    let newDependencies = DependencyChangeDetection.PinUnchangedDependencies cfg lockFile changedDependencies
    newDependencies.GetDependenciesInGroup(Constants.MainDependencyGroup)
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
    let changedDependencies = DependencyChangeDetection.findChangesInDependenciesFile(cfg,lockFile)
   
    let newDependencies = DependencyChangeDetection.PinUnchangedDependencies cfg lockFile changedDependencies
    let expected =
        Map.ofList
            ([(PackageName "Castle.Core", VersionRequirement (Specific(SemVer.Parse "3.3.3"),No));
              (PackageName "Castle.Core-log4net", VersionRequirement (Specific(SemVer.Parse "3.3.3"),No));
              (PackageName "Castle.LoggingFacility", VersionRequirement (Specific(SemVer.Parse "3.3.0"),No));
              (PackageName "Castle.Windsor", VersionRequirement (Specific(SemVer.Parse "3.3.0"),No));
              (PackageName "Castle.Windsor-log4net", VersionRequirement (Specific(SemVer.Parse "3.3.0"),No));
              (PackageName "NUnit", VersionRequirement (Minimum(SemVer.Parse "0"),No));
              (PackageName "log4net", VersionRequirement (Specific(SemVer.Parse "1.2.10"),No))])

    
    newDependencies.GetDependenciesInGroup(Constants.MainDependencyGroup)
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
    let changedDependencies = DependencyChangeDetection.findChangesInDependenciesFile(cfg,lockFile)
   
    let newDependencies = DependencyChangeDetection.PinUnchangedDependencies cfg lockFile changedDependencies
    let expected =
        Map.ofList
            ([(PackageName "Castle.Core", VersionRequirement (Specific(SemVer.Parse "3.3.3"),No));
              (PackageName "Castle.Core-log4net", VersionRequirement (Specific(SemVer.Parse "3.3.3"),No));
              (PackageName "Castle.LoggingFacility", VersionRequirement (Specific(SemVer.Parse "3.3.0"),No));
              (PackageName "Castle.Windsor", VersionRequirement (Specific(SemVer.Parse "3.3.0"),No));
              (PackageName "Castle.Windsor-log4net", VersionRequirement (Specific(SemVer.Parse "3.3.0"),No));
              (PackageName "log4net", VersionRequirement (Specific(SemVer.Parse "1.2.10"),No))])

    
    newDependencies.GetDependenciesInGroup(Constants.MainDependencyGroup)
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
    let changedDependencies = DependencyChangeDetection.findChangesInDependenciesFile(cfg,lockFile)
   
    let newDependencies = DependencyChangeDetection.PinUnchangedDependencies cfg lockFile changedDependencies
    let expected =
        Map.ofList
            ([(PackageName "Castle.Windsor-log4net", VersionRequirement (Minimum(SemVer.Parse "3.4.0"),No));])

    
    newDependencies.GetDependenciesInGroup(Constants.MainDependencyGroup)
    |> shouldEqual expected

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
    let changedDependencies = DependencyChangeDetection.findChangesInDependenciesFile(cfg,lockFile)
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
    let changedDependencies = DependencyChangeDetection.findChangesInDependenciesFile(cfg,lockFile)
    changedDependencies.Count |> shouldEqual 1
    (changedDependencies |> Seq.head) |> shouldEqual (NormalizedPackageName (PackageName "Caliburn.Micro"))