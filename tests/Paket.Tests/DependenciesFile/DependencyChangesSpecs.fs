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
   
    let newDependencies = DependencyChangeDetection.fixOldDependencies cfg lockFile
    newDependencies.DirectDependencies
    |> shouldEqual Map.empty

[<Test>]
let ``should detect add of single nuget package``() = 
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
   
    let newDependencies = DependencyChangeDetection.fixOldDependencies cfg lockFile
    let expected =
        Map.ofList
            ([(PackageName "Castle.Core", VersionRequirement (Specific(SemVer.Parse "3.3.3"),No));
              (PackageName "Castle.Core-log4net", VersionRequirement (Specific(SemVer.Parse "3.3.3"),No));
              (PackageName "Castle.LoggingFacility", VersionRequirement (Specific(SemVer.Parse "3.3.0"),No));
              (PackageName "Castle.Windsor", VersionRequirement (Specific(SemVer.Parse "3.3.0"),No));
              (PackageName "Castle.Windsor-log4net", VersionRequirement (Specific(SemVer.Parse "3.3.0"),No));
              (PackageName "NUnit", VersionRequirement (Minimum(SemVer.Parse "0"),No));
              (PackageName "log4net", VersionRequirement (Specific(SemVer.Parse "1.2.10"),No))])

    
    newDependencies.DirectDependencies
    |> shouldEqual expected
