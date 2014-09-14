module paket.lockFile.ParserSpecs

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
    src/app/FAKE/Cli.fs
    src/app/Fake.Deploy.Lib/FakeDeployAgentHelper.fs (Globbing)
"""   

[<Test>]
let ``should parse lock file``() = 
    let packages, sourceFiles = LockFile.Parse(toLines lockFile)
    packages.Length |> shouldEqual 6

    packages.[0].Source |> shouldEqual (Nuget "http://nuget.org/api/v2")
    packages.[0].Name |> shouldEqual "Castle.Windsor"
    packages.[0].Version |> shouldEqual (SemVer.parse "2.1")
    packages.[0].DirectDependencies |> shouldEqual []

    packages.[1].Source |> shouldEqual (Nuget "http://nuget.org/api/v2")
    packages.[1].Name |> shouldEqual "Castle.Windsor-log4net"
    packages.[1].Version |> shouldEqual (SemVer.parse "3.3")
    packages.[1].DirectDependencies |> shouldEqual ["Castle.Windsor", Latest; "log4net", Latest]
    
    packages.[5].Source |> shouldEqual (Nuget "http://nuget.org/api/v2")
    packages.[5].Name |> shouldEqual "log4net"
    packages.[5].Version |> shouldEqual (SemVer.parse "1.1")
    packages.[5].DirectDependencies |> shouldEqual ["log", Latest]

    sourceFiles |> shouldEqual
        [ { Owner = "fsharp"
            Project = "FAKE"
            Path = "src/app/FAKE/Cli.fs"
            Commit = None }
          { Owner = "fsharp"
            Project = "FAKE"
            Path = "src/app/Fake.Deploy.Lib/FakeDeployAgentHelper.fs"
            Commit = Some "Globbing" } ]
    
    sourceFiles.[0].CommitWithDefault |> shouldEqual "master"
    sourceFiles.[0].FilePath |> shouldEqual "src\\app\\FAKE\\Cli.fs"
    sourceFiles.[0].ToString() |> shouldEqual "(fsharp:FAKE:master) src/app/FAKE/Cli.fs"

