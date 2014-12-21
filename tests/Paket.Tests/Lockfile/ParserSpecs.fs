module Paket.LockFile.ParserSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.ModuleResolver
open Paket.Requirements

let lockFile = """NUGET
  remote: https://nuget.org/api/v2
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
    lockFile.Options.Strict |> shouldEqual false

    packages.[0].Source |> shouldEqual PackageSources.DefaultNugetSource
    packages.[0].Name |> shouldEqual (PackageName "Castle.Windsor")
    packages.[0].Version |> shouldEqual (SemVer.Parse "2.1")
    packages.[0].Dependencies |> shouldEqual Set.empty

    packages.[1].Source |> shouldEqual PackageSources.DefaultNugetSource
    packages.[1].Name |> shouldEqual (PackageName "Castle.Windsor-log4net")
    packages.[1].Version |> shouldEqual (SemVer.Parse "3.3")
    packages.[1].Dependencies |> shouldEqual (Set.ofList [PackageName "Castle.Windsor", VersionRequirement.AllReleases, []; PackageName "log4net", VersionRequirement.AllReleases, []])
    
    packages.[5].Source |> shouldEqual PackageSources.DefaultNugetSource
    packages.[5].Name |> shouldEqual (PackageName "log4net")
    packages.[5].Version |> shouldEqual (SemVer.Parse "1.1")
    packages.[5].Dependencies |> shouldEqual (Set.ofList [PackageName "log", VersionRequirement.AllReleases, []])

    let sourceFiles = List.rev lockFile.SourceFiles
    sourceFiles|> shouldEqual
        [ { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/Cli.fs"
            Origin = ModuleResolver.SingleSourceFileOrigin.GitHubLink
            Dependencies = Set.empty
            Commit = "7699e40e335f3cc54ab382a8969253fecc1e08a9" }
          { Owner = "fsharp"
            Project = "FAKE"
            Dependencies = Set.empty
            Name = "src/app/Fake.Deploy.Lib/FakeDeployAgentHelper.fs"
            Origin = ModuleResolver.SingleSourceFileOrigin.GitHubLink
            Commit = "Globbing" } ]
    
    sourceFiles.[0].Commit |> shouldEqual "7699e40e335f3cc54ab382a8969253fecc1e08a9"
    sourceFiles.[0].Name |> shouldEqual "src/app/FAKE/Cli.fs"
    sourceFiles.[0].ToString() |> shouldEqual "fsharp/FAKE:7699e40e335f3cc54ab382a8969253fecc1e08a9 src/app/FAKE/Cli.fs"

let strictLockFile = """REFERENCES: STRICT
NUGET
  remote: https://nuget.org/api/v2
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
    lockFile.Options.Strict |> shouldEqual true
    lockFile.Options.Redirects |> shouldEqual false

    packages.[5].Source |> shouldEqual PackageSources.DefaultNugetSource
    packages.[5].Name |> shouldEqual (PackageName "log4net")
    packages.[5].Version |> shouldEqual (SemVer.Parse "1.1")
    packages.[5].Dependencies |> shouldEqual (Set.ofList [PackageName "log", VersionRequirement.AllReleases, []])

let redirectsLockFile = """REDIRECTS: ON
NUGET
  remote: https://nuget.org/api/v2
  specs:
    Castle.Windsor (2.1)
"""   

[<Test>]
let ``should parse redirects lock file``() = 
    let lockFile = LockFileParser.Parse(toLines redirectsLockFile)
    let packages = List.rev lockFile.Packages
    packages.Length |> shouldEqual 1
    lockFile.Options.Strict |> shouldEqual false
    lockFile.Options.Redirects |> shouldEqual true

let dogfood = """NUGET
  remote: https://nuget.org/api/v2
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
    lockFile.Options.Strict |> shouldEqual false

    packages.[1].Source |> shouldEqual PackageSources.DefaultNugetSource
    packages.[1].Name |> shouldEqual (PackageName "FAKE")
    packages.[1].Version |> shouldEqual (SemVer.Parse "3.5.5")
    packages.[3].FrameworkRestriction |> shouldEqual []

    lockFile.SourceFiles.[0].Name |> shouldEqual "modules/Octokit/Octokit.fsx"


let frameworkRestricted = """NUGET
  remote: https://nuget.org/api/v2
  specs:
    Fleece (0.4.0)
      FSharpPlus (>= 0.0.4)
      ReadOnlyCollectionExtensions (>= 1.2.0)
      ReadOnlyCollectionInterfaces (1.0.0) - >= net40
      System.Json (>= 4.0.20126.16343)
    FsControl (1.0.9)
    FSharpPlus (0.0.4)
      FsControl (>= 1.0.9)
    LinqBridge (1.3.0) - net20
    ReadOnlyCollectionExtensions (1.2.0)
      LinqBridge (>= 1.3.0) - net20
      ReadOnlyCollectionInterfaces (1.0.0) - net20
      ReadOnlyCollectionInterfaces (1.0.0) - net35
      ReadOnlyCollectionInterfaces (1.0.0) - >= net40
    ReadOnlyCollectionInterfaces (1.0.0) - >= net40
    System.Json (4.0.20126.16343)
"""

[<Test>]
let ``should parse framework restricted lock file``() = 
    let lockFile = LockFileParser.Parse(toLines frameworkRestricted)
    let packages = List.rev lockFile.Packages
    packages.Length |> shouldEqual 7

    packages.[3].Source |> shouldEqual PackageSources.DefaultNugetSource
    packages.[3].Name |> shouldEqual (PackageName "LinqBridge")
    packages.[3].Version |> shouldEqual (SemVer.Parse "1.3.0")
    packages.[3].FrameworkRestriction |> shouldEqual ([FrameworkRestriction.Exactly(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V2))])

    packages.[5].Source |> shouldEqual PackageSources.DefaultNugetSource
    packages.[5].Name |> shouldEqual (PackageName "ReadOnlyCollectionInterfaces")
    packages.[5].Version |> shouldEqual (SemVer.Parse "1.0.0")
    packages.[5].FrameworkRestriction |> shouldEqual ([FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4_Client))])

let simpleHTTP = """
HTTP
  remote: http://www.frijters.net/ikvmbin-8.0.5449.0.zip
  specs:
    ikvmbin-8.0.5449.0.zip
"""

[<Test>]
let ``should parse simple http reference``() = 
    let lockFile = LockFileParser.Parse(toLines simpleHTTP)
    let references = lockFile.SourceFiles

    references.[0].Name |> shouldEqual "ikvmbin-8.0.5449.0.zip"  
    references.[0].Origin |> shouldEqual (SingleSourceFileOrigin.HttpLink("http://www.frijters.net/ikvmbin-8.0.5449.0.zip"))