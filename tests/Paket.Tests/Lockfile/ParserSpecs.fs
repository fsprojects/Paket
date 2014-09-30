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
    let lockFile = LockFileParser.Parse(toLines lockFile)
    let packages = List.rev lockFile.Packages
    packages.Length |> shouldEqual 6
    lockFile.Strict |> shouldEqual false

    packages.[0].Source |> shouldEqual PackageSources.DefaultNugetSource
    packages.[0].Name |> shouldEqual "Castle.Windsor"
    packages.[0].Version |> shouldEqual (SemVer.parse "2.1")
    packages.[0].Dependencies |> shouldEqual Set.empty

    packages.[1].Source |> shouldEqual PackageSources.DefaultNugetSource
    packages.[1].Name |> shouldEqual "Castle.Windsor-log4net"
    packages.[1].Version |> shouldEqual (SemVer.parse "3.3")
    packages.[1].Dependencies |> shouldEqual (Set.ofList ["Castle.Windsor", VersionRequirement.AllReleases; "log4net", VersionRequirement.AllReleases])
    
    packages.[5].Source |> shouldEqual PackageSources.DefaultNugetSource
    packages.[5].Name |> shouldEqual "log4net"
    packages.[5].Version |> shouldEqual (SemVer.parse "1.1")
    packages.[5].Dependencies |> shouldEqual (Set.ofList ["log", VersionRequirement.AllReleases])

    let sourceFiles = List.rev lockFile.SourceFiles
    sourceFiles|> shouldEqual
        [ { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/Cli.fs"
            Dependencies = []
            Commit = "7699e40e335f3cc54ab382a8969253fecc1e08a9" }
          { Owner = "fsharp"
            Project = "FAKE"
            Dependencies = []
            Name = "src/app/Fake.Deploy.Lib/FakeDeployAgentHelper.fs"
            Commit = "Globbing" } ]
    
    sourceFiles.[0].Commit |> shouldEqual "7699e40e335f3cc54ab382a8969253fecc1e08a9"
    sourceFiles.[0].Name |> shouldEqual "src/app/FAKE/Cli.fs"
    sourceFiles.[0].ToString() |> shouldEqual "fsharp/FAKE:7699e40e335f3cc54ab382a8969253fecc1e08a9 src/app/FAKE/Cli.fs"

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
    let lockFile = LockFileParser.Parse(toLines strictLockFile)
    let packages = List.rev lockFile.Packages
    packages.Length |> shouldEqual 6
    lockFile.Strict |> shouldEqual true

    packages.[5].Source |> shouldEqual PackageSources.DefaultNugetSource
    packages.[5].Name |> shouldEqual "log4net"
    packages.[5].Version |> shouldEqual (SemVer.parse "1.1")
    packages.[5].Dependencies |> shouldEqual (Set.ofList ["log", VersionRequirement.AllReleases])

let dogfood = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    DotNetZip (1.9.3)
    FAKE (3.5.5)
    FSharp.Compiler.Service (0.0.62)
    FSharp.Formatting (2.4.25)
      Microsoft.AspNet.Razor (2.0.30506.0)
      RazorEngine (3.3.0)
      FSharp.Compiler.Service (>= 0.0.59)
    Microsoft.AspNet.Razor (2.0.30506.0)
    Microsoft.Bcl (1.1.9)
      Microsoft.Bcl.Build (>= 1.0.14)
    Microsoft.Bcl.Build (1.0.21)
    Microsoft.Net.Http (2.2.28)
      Microsoft.Bcl (>= 1.1.9)
      Microsoft.Bcl.Build (>= 1.0.14)
    Newtonsoft.Json (6.0.5)
    NuGet.CommandLine (2.8.2)
    NUnit (2.6.3)
    NUnit.Runners (2.6.3)
    Octokit (0.4.1)
      Microsoft.Net.Http (>= 0)
    RazorEngine (3.3.0)
      Microsoft.AspNet.Razor (>= 2.0.30506.0)
    SourceLink.Fake (0.3.4)
    UnionArgParser (0.8.0)
GITHUB
  remote: forki/FsUnit
  specs:
    FsUnit.fs (7623fc13439f0e60bd05c1ed3b5f6dcb937fe468)
  remote: fsharp/FAKE
  specs:
    modules/Octokit/Octokit.fsx (a25c2f256a99242c1106b5a3478aae6bb68c7a93)
      Octokit (>= 0)"""

[<Test>]
let ``should parse own lock file``() = 
    let lockFile = LockFileParser.Parse(toLines dogfood)
    let packages = List.rev lockFile.Packages
    packages.Length |> shouldEqual 16
    lockFile.Strict |> shouldEqual false

    packages.[1].Source |> shouldEqual PackageSources.DefaultNugetSource
    packages.[1].Name |> shouldEqual "FAKE"
    packages.[1].Version |> shouldEqual (SemVer.parse "3.5.5")

    lockFile.SourceFiles.[0].Name |> shouldEqual "modules/Octokit/Octokit.fsx"