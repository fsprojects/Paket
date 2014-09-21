module Paket.LockFile.ParserSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

let lockFile = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Windsor (2.1)
    Castle.Windsor-log4net (3.3)
      Castle.Windsor (>= 2.0)
      log4net (>= 1.0)
    Rx-Core (2.1)
    Rx-Main (2.0)
      Rx-Core (>= 2.1)
    log (1.2)
    log4net (1.1)
      log (>= 1.0)
GITHUB
  remote: fsharp/FAKE
  specs:
    src/app/FAKE/Cli.fs (7699e40e335f3cc54ab382a8969253fecc1e08a9)
    src/app/Fake.Deploy.Lib/FakeDeployAgentHelper.fs (Globbing)
"""   

[<Test>]
let ``should parse lock file``() = 
    let lockFile = LockFile.Parse("",toLines lockFile)
    lockFile.ResolvedPackages.Length |> shouldEqual 6
    lockFile.Strict |> shouldEqual false

    lockFile.ResolvedPackages.[0].Source |> shouldEqual (Nuget Constants.DefaultNugetStream)
    lockFile.ResolvedPackages.[0].Name |> shouldEqual "Castle.Windsor"
    lockFile.ResolvedPackages.[0].Version |> shouldEqual (SemVer.parse "2.1")
    lockFile.ResolvedPackages.[0].DirectDependencies |> shouldEqual []

    lockFile.ResolvedPackages.[1].Source |> shouldEqual (Nuget Constants.DefaultNugetStream)
    lockFile.ResolvedPackages.[1].Name |> shouldEqual "Castle.Windsor-log4net"
    lockFile.ResolvedPackages.[1].Version |> shouldEqual (SemVer.parse "3.3")
    lockFile.ResolvedPackages.[1].DirectDependencies |> shouldEqual ["Castle.Windsor", VersionRange.NoRestriction; "log4net", VersionRange.NoRestriction]
    
    lockFile.ResolvedPackages.[5].Source |> shouldEqual (Nuget Constants.DefaultNugetStream)
    lockFile.ResolvedPackages.[5].Name |> shouldEqual "log4net"
    lockFile.ResolvedPackages.[5].Version |> shouldEqual (SemVer.parse "1.1")
    lockFile.ResolvedPackages.[5].DirectDependencies |> shouldEqual ["log", VersionRange.NoRestriction]

    lockFile.SourceFiles |> shouldEqual
        [ { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/Cli.fs"
            Commit = "7699e40e335f3cc54ab382a8969253fecc1e08a9" }
          { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/Fake.Deploy.Lib/FakeDeployAgentHelper.fs"
            Commit = "Globbing" } ]
    
    lockFile.SourceFiles.[0].Commit |> shouldEqual "7699e40e335f3cc54ab382a8969253fecc1e08a9"
    lockFile.SourceFiles.[0].Name |> shouldEqual "src/app/FAKE/Cli.fs"
    lockFile.SourceFiles.[0].ToString() |> shouldEqual "(fsharp:FAKE:7699e40e335f3cc54ab382a8969253fecc1e08a9) src/app/FAKE/Cli.fs"

let strictLockFile = """REFERENCES: STRICT
NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Windsor (2.1)
    Castle.Windsor-log4net (3.3)
      Castle.Windsor (>= 2.0)
      log4net (>= 1.0)
    Rx-Core (2.1)
    Rx-Main (2.0)
      Rx-Core (>= 2.1)
    log (1.2)
    log4net (1.1)
      log (>= 1.0)
"""   

[<Test>]
let ``should parse strict lock file``() = 
    let lockFile = LockFile.Parse("",toLines strictLockFile) 
    lockFile.ResolvedPackages.Length |> shouldEqual 6
    lockFile.Strict |> shouldEqual true

    lockFile.ResolvedPackages.[5].Source |> shouldEqual (Nuget Constants.DefaultNugetStream)
    lockFile.ResolvedPackages.[5].Name |> shouldEqual "log4net"
    lockFile.ResolvedPackages.[5].Version |> shouldEqual (SemVer.parse "1.1")
    lockFile.ResolvedPackages.[5].DirectDependencies |> shouldEqual ["log", VersionRange.NoRestriction]

