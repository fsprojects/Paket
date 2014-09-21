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
    let lockFile = LockFile.Parse(toLines lockFile)
    let packages = List.rev lockFile.Packages
    packages.Length |> shouldEqual 6
    lockFile.Strict |> shouldEqual false

    packages.[0].Source |> shouldEqual (Nuget Constants.DefaultNugetStream)
    packages.[0].Name |> shouldEqual "Castle.Windsor"
    packages.[0].Version |> shouldEqual (SemVer.parse "2.1")
    packages.[0].DirectDependencies |> shouldEqual []

    packages.[1].Source |> shouldEqual (Nuget Constants.DefaultNugetStream)
    packages.[1].Name |> shouldEqual "Castle.Windsor-log4net"
    packages.[1].Version |> shouldEqual (SemVer.parse "3.3")
    packages.[1].DirectDependencies |> shouldEqual ["Castle.Windsor", VersionRange.NoRestriction; "log4net", VersionRange.NoRestriction]
    
    packages.[5].Source |> shouldEqual (Nuget Constants.DefaultNugetStream)
    packages.[5].Name |> shouldEqual "log4net"
    packages.[5].Version |> shouldEqual (SemVer.parse "1.1")
    packages.[5].DirectDependencies |> shouldEqual ["log", VersionRange.NoRestriction]

    let sourceFiles = List.rev lockFile.SourceFiles
    sourceFiles|> shouldEqual
        [ { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/Cli.fs"
            Commit = "7699e40e335f3cc54ab382a8969253fecc1e08a9" }
          { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/Fake.Deploy.Lib/FakeDeployAgentHelper.fs"
            Commit = "Globbing" } ]
    
    sourceFiles.[0].Commit |> shouldEqual "7699e40e335f3cc54ab382a8969253fecc1e08a9"
    sourceFiles.[0].Name |> shouldEqual "src/app/FAKE/Cli.fs"
    sourceFiles.[0].ToString() |> shouldEqual "(fsharp:FAKE:7699e40e335f3cc54ab382a8969253fecc1e08a9) src/app/FAKE/Cli.fs"

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
    let lockFile = LockFile.Parse(toLines strictLockFile)
    let packages = List.rev lockFile.Packages
    packages.Length |> shouldEqual 6
    lockFile.Strict |> shouldEqual true

    packages.[5].Source |> shouldEqual (Nuget Constants.DefaultNugetStream)
    packages.[5].Name |> shouldEqual "log4net"
    packages.[5].Version |> shouldEqual (SemVer.parse "1.1")
    packages.[5].DirectDependencies |> shouldEqual ["log", VersionRange.NoRestriction]

