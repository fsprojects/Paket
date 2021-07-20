module Paket.LockFile.ParserSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.ModuleResolver
open Paket.Requirements
open Paket.PackageSources

let lockFile = """COPY-LOCAL: FALSE
NUGET
  remote: https://www.nuget.org/api/v2
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
    src/app/FAKE/Cli.fs (7699e40e335f3cc54ab382a8969253fecc1e08a9) gitHubAuth
    src/app/Fake.Deploy.Lib/FakeDeployAgentHelper.fs (Globbing)
"""

[<Test>]
let ``should parse lock file``() = 
    let lockFile = LockFileParser.Parse(toLines lockFile) |> List.head
    let packages = List.rev lockFile.Packages
    packages.Length |> shouldEqual 6
    lockFile.Options.Strict |> shouldEqual false
    lockFile.Options.Settings.CopyLocal |> shouldEqual (Some false)
    lockFile.Options.Settings.ImportTargets |> shouldEqual None

    packages.[0].Source |> shouldEqual PackageSources.DefaultNuGetSource
    packages.[0].Name |> shouldEqual (PackageName "Castle.Windsor")
    packages.[0].Version |> shouldEqual (SemVer.Parse "2.1")
    packages.[0].Dependencies |> shouldEqual Set.empty

    packages.[1].Source |> shouldEqual PackageSources.DefaultNuGetSource
    packages.[1].Name |> shouldEqual (PackageName "Castle.Windsor-log4net")
    packages.[1].Version |> shouldEqual (SemVer.Parse "3.3")
    packages.[1].Dependencies |> shouldEqual (Set.ofList [PackageName "Castle.Windsor", VersionRequirement(Minimum(SemVer.Parse "2.0"), PreReleaseStatus.No), makeOrList []; PackageName "log4net", VersionRequirement(Minimum(SemVer.Parse "1.0"), PreReleaseStatus.No), makeOrList []])
    
    packages.[5].Source |> shouldEqual PackageSources.DefaultNuGetSource
    packages.[5].Name |> shouldEqual (PackageName "log4net")
    packages.[5].Version |> shouldEqual (SemVer.Parse "1.1")
    packages.[5].Dependencies |> shouldEqual (Set.ofList [PackageName "log", VersionRequirement(Minimum(SemVer.Parse "1.0"), PreReleaseStatus.No), makeOrList []])

    let sourceFiles = List.rev lockFile.SourceFiles
    sourceFiles|> shouldEqual
        [ { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/Cli.fs"
            Origin = ModuleResolver.Origin.GitHubLink
            Dependencies = Set.empty
            Commit = "7699e40e335f3cc54ab382a8969253fecc1e08a9"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            AuthKey = Some "gitHubAuth" }
          { Owner = "fsharp"
            Project = "FAKE"
            Dependencies = Set.empty
            Name = "src/app/Fake.Deploy.Lib/FakeDeployAgentHelper.fs"
            Origin = ModuleResolver.Origin.GitHubLink
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            Commit = "Globbing"
            AuthKey = None } ]
    
    sourceFiles.[0].Commit |> shouldEqual "7699e40e335f3cc54ab382a8969253fecc1e08a9"
    sourceFiles.[0].Name |> shouldEqual "src/app/FAKE/Cli.fs"
    sourceFiles.[0].ToString() |> shouldEqual "fsharp/FAKE:7699e40e335f3cc54ab382a8969253fecc1e08a9 src/app/FAKE/Cli.fs"

let strictLockFile = """REFERENCES: STRICT
IMPORT-TARGETS: FALSE
NUGET
  remote: https://www.nuget.org/api/v2
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
    let lockFile = LockFileParser.Parse(toLines strictLockFile) |> List.head
    let packages = List.rev lockFile.Packages
    packages.Length |> shouldEqual 6
    lockFile.Options.Strict |> shouldEqual true
    lockFile.Options.Redirects |> shouldEqual None
    lockFile.Options.Settings.ImportTargets |> shouldEqual (Some false)
    lockFile.Options.Settings.CopyLocal |> shouldEqual None

    packages.[5].Source |> shouldEqual PackageSources.DefaultNuGetSource
    packages.[5].Name |> shouldEqual (PackageName "log4net")
    packages.[5].Version |> shouldEqual (SemVer.Parse "1.1")
    packages.[5].Dependencies |> shouldEqual (Set.ofList [PackageName "log", VersionRequirement(Minimum(SemVer.Parse "1.0"), PreReleaseStatus.No), makeOrList []])

let redirectsLockFile = """REDIRECTS: ON
IMPORT-TARGETS: TRUE
COPY-LOCAL: TRUE
NUGET
  remote: "D:\code\temp with space"
  specs:
    Castle.Windsor (2.1)

GROUP Test
NUGET
  remote: "D:\code\temp with space"
  specs:
    xUnit (2.0.0)

GROUP Build
REDIRECTS: OFF
NUGET
  remote: "D:\code\temp with space"
  specs:
    FAKE (4.0.0)
"""

[<Test>]
let ``should parse redirects lock file``() = 
    let lockFile = LockFileParser.Parse(toLines redirectsLockFile)

    let main = lockFile.Tail.Tail.Head
    main.Packages.Length |> shouldEqual 1
    main.Options.Strict |> shouldEqual false
    main.Options.Redirects |> shouldEqual (Some BindingRedirectsSettings.On)
    main.Options.Settings.ImportTargets |> shouldEqual (Some true)
    main.Options.Settings.CopyLocal |> shouldEqual (Some true)

    let test = lockFile.Tail.Head
    test.Packages.Length |> shouldEqual 1
    test.Options.Strict |> shouldEqual false
    test.Options.Redirects |> shouldEqual None
    test.Options.Settings.ImportTargets |> shouldEqual None
    test.Options.Settings.CopyLocal |> shouldEqual None

    let build = lockFile.Head
    build.Packages.Length |> shouldEqual 1
    build.Options.Strict |> shouldEqual false
    build.Options.Redirects |> shouldEqual (Some BindingRedirectsSettings.Off)
    build.Options.Settings.ImportTargets |> shouldEqual None
    build.Options.Settings.CopyLocal |> shouldEqual None

let lockFileWithFrameworkRestrictions = """FRAMEWORK: >= NET45
IMPORT-TARGETS: TRUE
NUGET
  remote: https://www.nuget.org/api/v2
  specs:
    Castle.Windsor (2.1)
"""

[<Test>]
let ``should parse lock file with framework restrictions``() = 
    let lockFile = LockFileParser.Parse(toLines lockFileWithFrameworkRestrictions) |> List.head
    let packages = List.rev lockFile.Packages
    packages.Length |> shouldEqual 1
    lockFile.Options.Strict |> shouldEqual false
    lockFile.Options.Redirects |> shouldEqual None
    lockFile.Options.Settings.ImportTargets |> shouldEqual (Some true)
    lockFile.Options.Settings.CopyLocal |> shouldEqual None

let dogfood = """NUGET
  remote: https://www.nuget.org/api/v2
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
    let lockFile = LockFileParser.Parse(toLines dogfood) |> List.head
    let packages = List.rev lockFile.Packages
    packages.Length |> shouldEqual 16
    lockFile.Options.Strict |> shouldEqual false

    packages.[1].Source |> shouldEqual PackageSources.DefaultNuGetSource
    packages.[1].Name |> shouldEqual (PackageName "FAKE")
    packages.[1].Version |> shouldEqual (SemVer.Parse "3.5.5")
    packages.[1].Settings.FrameworkRestrictions |> shouldEqual (makeOrList [])

    lockFile.SourceFiles.[0].Name |> shouldEqual "modules/Octokit/Octokit.fsx"

let dogfood2 = """NUGET
  remote: https://www.nuget.org/api/v2
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
      Microsoft.Net.Http
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
      Octokit"""

[<Test>]
let ``should parse own lock file2``() = 
    let lockFile = LockFileParser.Parse(toLines dogfood2) |> List.head
    let packages = List.rev lockFile.Packages
    packages.Length |> shouldEqual 16
    lockFile.Options.Strict |> shouldEqual false

    packages.[1].Source |> shouldEqual PackageSources.DefaultNuGetSource
    packages.[1].Name |> shouldEqual (PackageName "FAKE")
    packages.[1].Version |> shouldEqual (SemVer.Parse "3.5.5")
    packages.[3].Settings.FrameworkRestrictions |> shouldEqual (makeOrList [])

    lockFile.SourceFiles.[0].Name |> shouldEqual "modules/Octokit/Octokit.fsx"


let frameworkRestricted = """NUGET
  remote: https://www.nuget.org/api/v2
  specs:
    Fleece (0.4.0)
      FSharpPlus (>= 0.0.4)
      ReadOnlyCollectionExtensions (>= 1.2.0)
      ReadOnlyCollectionInterfaces (1.0.0) - >= net40
      System.Json (>= 4.0.20126.16343)
    FsControl (1.0.9)
    FSharpPlus (0.0.4)
      FsControl (>= 1.0.9)
    LinqBridge (1.3.0) - >= net20 < net35
    ReadOnlyCollectionExtensions (1.2.0)
      LinqBridge (>= 1.3.0) - >= net20 < net35
      ReadOnlyCollectionInterfaces (1.0.0) - net20, net35, >= net40
    ReadOnlyCollectionInterfaces (1.0.0) - net20, net35, >= net40
    System.Json (4.0.20126.16343)
"""

[<Test>]
let ``should parse framework restricted lock file``() = 
    let lockFile = LockFileParser.Parse(toLines frameworkRestricted) |> List.head
    let packages = List.rev lockFile.Packages
    packages.Length |> shouldEqual 7

    packages.[0].Dependencies |> Set.toList |> List.map (fun (_, _, r) -> r)
    |> List.item 2
    |> getExplicitRestriction
    |> shouldEqual (FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4)))

    packages.[3].Source |> shouldEqual PackageSources.DefaultNuGetSource
    packages.[3].Name |> shouldEqual (PackageName "LinqBridge")
    packages.[3].Version |> shouldEqual (SemVer.Parse "1.3.0")
    packages.[3].Settings.FrameworkRestrictions 
    |> getExplicitRestriction
    |> shouldEqual (FrameworkRestriction.Between(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V2),FrameworkIdentifier.DotNetFramework(FrameworkVersion.V3_5)))
    packages.[3].Settings.ImportTargets |> shouldEqual None

    let dependencies4 =
        packages.[4].Dependencies |> Set.toList |> List.map (fun (_, _, r) -> r)

    dependencies4.Head
    |> getExplicitRestriction
    |> shouldEqual (FrameworkRestriction.Between(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V2), FrameworkIdentifier.DotNetFramework(FrameworkVersion.V3_5)))
    dependencies4.Tail.Head
    |> getExplicitRestriction
    |> shouldEqual ([FrameworkRestriction.Exactly(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V2))
                     FrameworkRestriction.Exactly(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V3_5))
                     FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4))] |> makeOrList |> getExplicitRestriction)

    packages.[5].Source |> shouldEqual PackageSources.DefaultNuGetSource
    packages.[5].Name |> shouldEqual (PackageName "ReadOnlyCollectionInterfaces")
    packages.[5].Version |> shouldEqual (SemVer.Parse "1.0.0")
    packages.[5].Settings.FrameworkRestrictions
    |> getExplicitRestriction
    |> shouldEqual ([FrameworkRestriction.Exactly(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V2))
                     FrameworkRestriction.Exactly(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V3_5))
                     FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4))] |> makeOrList |> getExplicitRestriction)

let frameworkRestricted' = """NUGET
  remote: https://www.nuget.org/api/v2
  specs:
    Fleece (0.4.0) - license_download: true
      FSharpPlus (>= 0.0.4)
      ReadOnlyCollectionExtensions (>= 1.2.0)
      ReadOnlyCollectionInterfaces (1.0.0) - framework: >= net40
      System.Json (>= 4.0.20126.16343)
    FsControl (1.0.9)
    FSharpPlus (0.0.4)
      FsControl (>= 1.0.9)
    LinqBridge (1.3.0) - import_targets: false, content: none, version_in_path: true, framework: >= net20 < net35, copy_content_to_output_dir: never
    ReadOnlyCollectionExtensions (1.2.0)
      LinqBridge (>= 1.3.0) - framework: >= net20 < net35
      ReadOnlyCollectionInterfaces (1.0.0) - framework: net20, net35, >= net40
    ReadOnlyCollectionInterfaces (1.0.0) - copy_local: false, specific_version: true, import_targets: false, framework: net20, net35, >= net40
    System.Json (4.0.20126.16343)
"""

[<Test>]
let ``should parse framework restricted lock file in new syntax``() = 
    let lockFile = LockFileParser.Parse(toLines frameworkRestricted') |> List.head
    let packages = List.rev lockFile.Packages
    packages.Length |> shouldEqual 7

    packages.[0].Dependencies |> Set.toList |> List.map (fun (_, _, r) -> r)
    |> List.item 2
    |> getExplicitRestriction
    |> shouldEqual (FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4)))

    packages.[0].Settings.LicenseDownload |> shouldEqual (Some true)

    packages.[3].Source |> shouldEqual PackageSources.DefaultNuGetSource
    packages.[3].Name |> shouldEqual (PackageName "LinqBridge")
    packages.[3].Version |> shouldEqual (SemVer.Parse "1.3.0")
    packages.[3].Settings.CopyContentToOutputDirectory |> shouldEqual (Some CopyToOutputDirectorySettings.Never)
    packages.[3].Settings.FrameworkRestrictions
    |> getExplicitRestriction 
    |> shouldEqual (FrameworkRestriction.Between(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V2),FrameworkIdentifier.DotNetFramework(FrameworkVersion.V3_5)))
    packages.[3].Settings.CopyLocal |> shouldEqual None
    packages.[3].Settings.SpecificVersion |> shouldEqual None
    packages.[3].Settings.ImportTargets |> shouldEqual (Some false)
    packages.[3].Settings.IncludeVersionInPath |> shouldEqual (Some true)
    packages.[3].Settings.LicenseDownload |> shouldEqual None
    packages.[3].Settings.OmitContent |> shouldEqual (Some ContentCopySettings.Omit)

    let dependencies4 =
        packages.[4].Dependencies |> Set.toList |> List.map (fun (_, _, r) -> r)

    dependencies4.Head
    |> getExplicitRestriction
    |> shouldEqual (FrameworkRestriction.Between(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V2), FrameworkIdentifier.DotNetFramework(FrameworkVersion.V3_5)))
    dependencies4.Tail.Head
    |> getExplicitRestriction
    |> shouldEqual ([FrameworkRestriction.Exactly(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V2))
                     FrameworkRestriction.Exactly(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V3_5))
                     FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4))] |> makeOrList |> getExplicitRestriction)

    packages.[5].Source |> shouldEqual PackageSources.DefaultNuGetSource
    packages.[5].Name |> shouldEqual (PackageName "ReadOnlyCollectionInterfaces")
    packages.[5].Version |> shouldEqual (SemVer.Parse "1.0.0")
    packages.[5].Settings.ImportTargets |> shouldEqual (Some false)
    packages.[5].Settings.CopyLocal |> shouldEqual (Some false)
    packages.[5].Settings.SpecificVersion |> shouldEqual (Some true)
    packages.[5].Settings.OmitContent |> shouldEqual None
    packages.[5].Settings.IncludeVersionInPath |> shouldEqual None
    packages.[5].Settings.FrameworkRestrictions 
    |> getExplicitRestriction
    |> shouldEqual ([FrameworkRestriction.Exactly(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V2))
                     FrameworkRestriction.Exactly(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V3_5))
                     FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4))] |> makeOrList |> getExplicitRestriction)

let simpleHTTP = """
HTTP
  remote: http://www.frijters.net/ikvmbin-8.0.5449.0.zip
  specs:
    ikvmbin-8.0.5449.0.zip
"""

[<Test>]
let ``should parse simple http reference``() = 
    let lockFile = LockFileParser.Parse(toLines simpleHTTP) |> List.head
    let references = lockFile.SourceFiles

    references.[0].Name |> shouldEqual "ikvmbin-8.0.5449.0.zip"
    references.[0].Origin |> shouldEqual (Origin.HttpLink("http://www.frijters.net/ikvmbin-8.0.5449.0.zip"))


let lockFileForStanfordNLPdotNET = """HTTP
  remote: http://www.frijters.net
  specs:
    ikvmbin-8.0.5449.0.zip (/ikvmbin-8.0.5449.0.zip)
  remote: http://nlp.stanford.edu
  specs:
    stanford-corenlp-full-2014-10-31.zip (/software/stanford-corenlp-full-2014-10-31.zip)
    stanford-ner-2014-10-26.zip (/software/stanford-ner-2014-10-26.zip)
    stanford-parser-full-2014-10-31.zip (/software/stanford-parser-full-2014-10-31.zip)
    stanford-postagger-full-2014-10-26.zip (/software/stanford-postagger-full-2014-10-26.zip)
    stanford-segmenter-2014-10-26.zip (/software/stanford-segmenter-2014-10-26.zip)"""

[<Test>]
let ``should parse lock file for http Stanford.NLP.NET project``() =
    let lockFile = LockFileParser.Parse(toLines lockFileForStanfordNLPdotNET) |> List.head
    let references = lockFile.SourceFiles

    references.Length |> shouldEqual 6

    references.[0].Origin |> shouldEqual (Origin.HttpLink("http://nlp.stanford.edu"))
    references.[0].Commit |> shouldEqual "/software/stanford-segmenter-2014-10-26.zip"  // That's strange
    references.[0].Project |> shouldEqual ""
    references.[0].Name |> shouldEqual "stanford-segmenter-2014-10-26.zip"

let portableLockFile = """NUGET
  remote: https://www.nuget.org/api/v2
  specs:
    FSharp.Data (2.0.14)
      Zlib.Portable (>= 1.10.0) - framework: portable-net40+sl50+wp80+win80
    Zlib.Portable (1.10.0) - framework: portable-net40+sl50+wp80+win80
"""

[<Test>]
let ``should parse portable lockfile``() =
    let lockFile = LockFileParser.Parse(toLines portableLockFile) |> List.head
    let references = lockFile.SourceFiles

    references.Length |> shouldEqual 0

    let packages = List.rev lockFile.Packages
    packages.Length |> shouldEqual 2
    
    packages.[1].Name |> shouldEqual (PackageName "Zlib.Portable")
    packages.[1].Version |> shouldEqual (SemVer.Parse "1.10.0")
    (packages.[1].Settings.FrameworkRestrictions |> getExplicitRestriction).ToString() |> shouldEqual ">= portable-net40+sl5+win8+wp8"

let reactiveuiLockFile = """NUGET
  remote: https://www.nuget.org/api/v2
  specs:
    reactiveui (5.5.1)
      reactiveui-core (5.5.1)
      reactiveui-platforms (5.5.1)
    reactiveui-core (5.5.1)
      Rx-Main (>= 2.1.30214.0) - framework: portable-net45+win+wp80
      Rx-WindowStoreApps (>= 2.1.30214.0) - framework: winv4.5
    reactiveui-platforms (5.5.1)
      Rx-Xaml (>= 2.1.30214.0) - framework: winv4.5, wpv8.0, >= net45
      reactiveui-core (5.5.1) - framework: monoandroid, monotouch, monomac, winv4.5, wpv8.0, >= net45
    Rx-Core (2.2.5)
      Rx-Interfaces (>= 2.2.5)
    Rx-Interfaces (2.2.5)
    Rx-Linq (2.2.5)
      Rx-Core (>= 2.2.5)
      Rx-Interfaces (>= 2.2.5)
    Rx-Main (2.2.5) - framework: portable-net45+win+wp80
      Rx-Core (>= 2.2.5)
      Rx-Interfaces (>= 2.2.5)
      Rx-Linq (>= 2.2.5)
      Rx-PlatformServices (>= 2.2.5)
    Rx-PlatformServices (2.2.5)
      Rx-Core (>= 2.2.5)
      Rx-Interfaces (>= 2.2.5)
    Rx-WindowStoreApps (2.2.5) - framework: winv4.5
      Rx-Main (>= 2.2.5)
      Rx-WinRT (>= 2.2.5)
    Rx-WinRT (2.2.5)
      Rx-Main (>= 2.2.5)
    Rx-Xaml (2.2.5) - framework: winv4.5, wpv8.0, >= net45
      Rx-Main (>= 2.2.5)"""

[<Test>]
let ``should parse reactiveui lockfile``() =
    let lockFile = LockFileParser.Parse(toLines reactiveuiLockFile) |> List.head
    let references = lockFile.SourceFiles

    references.Length |> shouldEqual 0

    let packages = List.rev lockFile.Packages
    
    packages.[8].Name |> shouldEqual (PackageName "Rx-WindowStoreApps")
    packages.[8].Version |> shouldEqual (SemVer.Parse "2.2.5")
    (packages.[8].Settings.FrameworkRestrictions |> getExplicitRestriction).ToString() |> shouldEqual "== win8"

    packages.[10].Name |> shouldEqual (PackageName "Rx-Xaml")
    packages.[10].Version |> shouldEqual (SemVer.Parse "2.2.5")
    (packages.[10].Settings.FrameworkRestrictions |> getExplicitRestriction).ToString() |> shouldEqual "|| (== win8) (== wp8) (>= net45)"

let multipleFeedLockFileLegacy = """NUGET
  remote: http://internalfeed/NugetWebFeed/nuget
    Internal_1 (1.2.10)
      Newtonsoft.Json (>= 6.0 < 6.1)
    log4net (1.2.10)
    Newtonsoft.Json (6.0.6)
  remote: https://www.nuget.org/api/v2
    Microsoft.AspNet.WebApi (5.2.3)
      Microsoft.AspNet.WebApi.WebHost (>= 5.2.3 < 5.3)
    Microsoft.AspNet.WebApi.Client (5.2.3)
      Microsoft.Net.Http (>= 2.2.22) - framework: portable-wp80+win+net45+wp81+wpa81
      Newtonsoft.Json (>= 6.0.4) - framework: >= net45, portable-wp80+win+net45+wp81+wpa81
    Microsoft.AspNet.WebApi.Core (5.2.3)
      Microsoft.AspNet.WebApi.Client (>= 5.2.3)
    Microsoft.AspNet.WebApi.WebHost (5.2.3)
      Microsoft.AspNet.WebApi.Core (>= 5.2.3 < 5.3)
"""

let multipleFeedLockFile = """NUGET
  remote: http://internalfeed/NugetWebFeed/nuget
    Internal_1 (1.2.10)
      Newtonsoft.Json (>= 6.0 < 6.1)
    log4net (1.2.10)
    Newtonsoft.Json (6.0.6)
  remote: https://www.nuget.org/api/v2
    Microsoft.AspNet.WebApi (5.2.3)
      Microsoft.AspNet.WebApi.WebHost (>= 5.2.3 < 5.3)
    Microsoft.AspNet.WebApi.Client (5.2.3)
      Microsoft.Net.Http (>= 2.2.22) - restriction: >= portable-net45+win8+wp8+wp81+wpa81
      Newtonsoft.Json (>= 6.0.4) - restriction: >= portable-net45+win8+wp8+wp81+wpa81
    Microsoft.AspNet.WebApi.Core (5.2.3)
      Microsoft.AspNet.WebApi.Client (>= 5.2.3)
    Microsoft.AspNet.WebApi.WebHost (5.2.3)
      Microsoft.AspNet.WebApi.Core (>= 5.2.3 < 5.3)
"""

[<Test>]
let ``should parse lockfile with multiple feeds``() =
    for lockFileText in [multipleFeedLockFileLegacy;multipleFeedLockFile] do
        let lockFile = LockFileParser.Parse(toLines lockFileText) |> List.head
        let references = lockFile.SourceFiles

        references.Length |> shouldEqual 0

        let packages = List.rev lockFile.Packages
        packages.Length |> shouldEqual 7
    
        packages.[3].Name |> shouldEqual (PackageName "Microsoft.AspNet.WebApi")
        packages.[3].Version |> shouldEqual (SemVer.Parse "5.2.3")
        packages.[3].Source.ToString() |> shouldEqual "https://www.nuget.org/api/v2"

[<Test>]
let ``should parse and serialise multiple feed lockfile``() =
    for lockFileText in [multipleFeedLockFileLegacy;multipleFeedLockFile] do
        let lockFile = LockFile.Parse("",toLines lockFileText)
        let lockFile' = lockFile.ToString()

        normalizeLineEndings lockFile' 
        |> shouldEqual (normalizeLineEndings multipleFeedLockFile)


let groupsLockFile = """REDIRECTS: ON
COPY-LOCAL: TRUE
IMPORT-TARGETS: TRUE
LICENSE-DOWNLOAD: TRUE
NUGET
  remote: "D:\code\temp with space"
    Castle.Windsor (2.1)

GROUP Build
REDIRECTS: ON
COPY-LOCAL: TRUE
CONDITION: LEGACY
NUGET
  remote: "D:\code\temp with space"
    FAKE (4.0) - redirects: on
"""

[<Test>]
let ``should parse lock file with groups``() = 
    let lockFile1 = LockFileParser.Parse(toLines groupsLockFile) |> List.skip 1 |> List.head
    lockFile1.GroupName |> shouldEqual Constants.MainDependencyGroup
    let packages1 = List.rev lockFile1.Packages
    
    packages1.Length |> shouldEqual 1
    lockFile1.Options.Strict |> shouldEqual false
    lockFile1.Options.Redirects |> shouldEqual (Some BindingRedirectsSettings.On)
    lockFile1.Options.Settings.ImportTargets |> shouldEqual (Some true)
    lockFile1.Options.Settings.LicenseDownload |> shouldEqual (Some true)
    lockFile1.Options.Settings.CopyLocal |> shouldEqual (Some true)
    lockFile1.Options.Settings.ReferenceCondition |> shouldEqual None

    packages1.Head.Source.Url |> shouldEqual "D:\code\\temp with space"
    packages1.[0].Name |> shouldEqual (PackageName "Castle.Windsor")

    let lockFile2 = LockFileParser.Parse(toLines groupsLockFile) |> List.head
    lockFile2.GroupName.ToString() |> shouldEqual "Build"
    let packages2 = List.rev lockFile2.Packages
    
    packages2.Length |> shouldEqual 1
    lockFile2.Options.Strict |> shouldEqual false
    lockFile2.Options.Redirects |> shouldEqual (Some BindingRedirectsSettings.On)
    lockFile2.Options.Settings.ImportTargets |> shouldEqual None
    lockFile2.Options.Settings.LicenseDownload |> shouldEqual None
    lockFile2.Options.Settings.CopyLocal |> shouldEqual (Some true)
    lockFile2.Options.Settings.ReferenceCondition |> shouldEqual (Some "LEGACY")

    packages2.Head.Source.Url |> shouldEqual "D:\code\\temp with space"
    packages2.[0].Name |> shouldEqual (PackageName "FAKE")
    packages2.[0].Settings.CreateBindingRedirects |> shouldEqual (Some BindingRedirectsSettings.On)


[<Test>]
let ``should parse and serialise groups lockfile``() =
    let lockFile = LockFile.Parse("",toLines groupsLockFile)
    let lockFile' = lockFile.ToString()

    normalizeLineEndings lockFile' 
    |> shouldEqual (normalizeLineEndings groupsLockFile)

[<Test>]
let ``should parse strategy min lock file``() = 
    let lockFile = """STRATEGY: MIN
NUGET
  remote: "D:\code\temp with space"
    Castle.Windsor (2.1)
"""
    let lockFile = LockFileParser.Parse(toLines lockFile) |> List.head
    let packages = List.rev lockFile.Packages
    
    packages.Length |> shouldEqual 1
    lockFile.Options.ResolverStrategyForTransitives |> shouldEqual (Some ResolverStrategy.Min)
    
[<Test>]
let ``should parse strategy max lock file``() = 
    let lockFile = """STRATEGY: MAX
NUGET
  remote: "D:\code\temp with space"
  specs:
    Castle.Windsor (2.1)
"""
    let lockFile = LockFileParser.Parse(toLines lockFile) |> List.head
    let packages = List.rev lockFile.Packages
    
    packages.Length |> shouldEqual 1
    lockFile.Options.ResolverStrategyForTransitives |> shouldEqual (Some ResolverStrategy.Max)

[<Test>]
let ``should parse no strategy lock file``() = 
    let lockFile = """NUGET
  remote: "D:\code\temp with space"
  specs:
    Castle.Windsor (2.1)
"""
    let lockFile = LockFileParser.Parse(toLines lockFile) |> List.head
    let packages = List.rev lockFile.Packages
    
    packages.Length |> shouldEqual 1
    lockFile.Options.ResolverStrategyForTransitives |> shouldEqual None
    
let packageRedirectsLockFile = """REDIRECTS: ON
NUGET
  remote: "D:\code\temp with space"
    Castle.Windsor (2.1)
    DotNetZip (1.9.3) - redirects: on
    FAKE (3.5.5) - redirects: off
    FSharp.Compiler.Service (0.0.62) - redirects: force

GROUP Build
NUGET
  remote: "D:\code\temp with space"
    FAKE (4.0) - redirects: on

GROUP Test
REDIRECTS: OFF
NUGET
  remote: "D:\code\temp with space"
    xUnit (2.0.0)
"""

[<Test>]
let ``should parse redirects lock file and packages``() = 
    let lockFile = LockFileParser.Parse(toLines packageRedirectsLockFile)
    let main = lockFile.Tail.Tail.Head
    let packages = List.rev main.Packages
    
    packages.Length |> shouldEqual 4
    main.Options.Redirects |> shouldEqual (Some BindingRedirectsSettings.On)

    packages.Head.Settings.CreateBindingRedirects |> shouldEqual None
    packages.Tail.Head.Settings.CreateBindingRedirects |> shouldEqual (Some BindingRedirectsSettings.On)
    packages.Tail.Tail.Head.Settings.CreateBindingRedirects |> shouldEqual (Some BindingRedirectsSettings.Off)
    packages.Tail.Tail.Tail.Head.Settings.CreateBindingRedirects |> shouldEqual (Some BindingRedirectsSettings.Force)
    
    let build = lockFile.Tail.Head
    let packages = List.rev build.Packages
    
    packages.Length |> shouldEqual 1
    build.Options.Redirects |> shouldEqual None

    packages.Head.Settings.CreateBindingRedirects |> shouldEqual (Some BindingRedirectsSettings.On)

    let test = lockFile.Head
    let packages = List.rev test.Packages
    
    packages.Length |> shouldEqual 1
    test.Options.Redirects |> shouldEqual (Some BindingRedirectsSettings.Off)

    packages.Head.Settings.CreateBindingRedirects |> shouldEqual None

[<Test>]
let ``should parse and serialize redirects lockfile``() =
    let lockFile = LockFile.Parse("",toLines packageRedirectsLockFile)
    let lockFile' = lockFile.ToString()

    normalizeLineEndings lockFile' 
    |> shouldEqual (normalizeLineEndings packageRedirectsLockFile)

let autodetectLockFile = """REDIRECTS: ON
FRAMEWORK: NET452, NET452
NUGET
  remote: http://api.nuget.org/v3/index.json
  specs:
    Autofac (3.5.2) - framework: net452
    Autofac.Extras.ServiceStack (2.0.2) - framework: net452
      Autofac  - framework: net452
      ServiceStack (>= 4.0.0) - framework: net452
    ServiceStack (4.0.54) - framework: net452
      ServiceStack.Client (>= 4.0.54) - framework: net452
      ServiceStack.Common (>= 4.0.54) - framework: net452
    ServiceStack.Client (4.0.54) - framework: net452
      ServiceStack.Interfaces (>= 4.0.54) - framework: net452
      ServiceStack.Text (>= 4.0.54) - framework: net452
    ServiceStack.Common (4.0.54) - framework: net452
      ServiceStack.Interfaces (>= 4.0.54) - framework: net452
      ServiceStack.Text (>= 4.0.54) - framework: net452
    ServiceStack.Interfaces (4.0.54) - framework: net452
    ServiceStack.Text (4.0.54) - framework: net452
  remote: https://www.myget.org/F/paket-framework-problem-repro
  specs:
    DependsOnAutofac (1.2.0)
      Autofac  - framework: net452
      Autofac.Extras.ServiceStack  - framework: net452
"""

[<Test>]
let ``should parse lock file from auto-detect settings``() = 
    let lockFile = LockFileParser.Parse(toLines autodetectLockFile)
    let main = lockFile.Head
    let packages = List.rev main.Packages
    
    packages.Length |> shouldEqual 8

    packages.Head.Name |> shouldEqual (PackageName "Autofac")
    packages.Tail.Head.Name |> shouldEqual (PackageName "Autofac.Extras.ServiceStack")
    let deps = packages.Tail.Head.Dependencies |> Seq.toList |> List.map (fun (n,_,_) -> n)
    deps.Head |> shouldEqual (PackageName "Autofac")

let lockFileWithManyFrameworksLegacy = """NUGET
  remote: https://www.nuget.org/api/v2
    CommonServiceLocator (1.3) - framework: >= net40, monoandroid, portable-net45+wp80+wpa81+win+monoandroid10+xamarinios10, xamarinios, winv4.5, winv4.5.1, wpv8.0, wpv8.1, sl50
    MvvmLightLibs (5.2)
      CommonServiceLocator (>= 1.0) - framework: net35, sl40
      CommonServiceLocator (>= 1.3) - framework: >= net40, monoandroid, portable-net45+wp80+wpa81+win+monoandroid10+xamarinios10, xamarinios, winv4.5, winv4.5.1, wpv8.0, wpv8.1, sl50"""

let lockFileWithManyFrameworks = """NUGET
  remote: https://www.nuget.org/api/v2
    CommonServiceLocator (1.3) - restriction: || (== sl5) (>= net40) (>= portable-net45+win8+wp8+wpa81)
    MvvmLightLibs (5.2)
      CommonServiceLocator (>= 1.0) - restriction: || (== net35) (== sl4)
      CommonServiceLocator (>= 1.3) - restriction: || (== sl5) (>= net40) (>= portable-net45+win8+wp8+wpa81)"""

[<Test>]
let ``should parse lock file many frameworks``() = 
    for lockFile in [lockFileWithManyFrameworksLegacy;lockFileWithManyFrameworks] do
        let lockFile = LockFileParser.Parse(toLines lockFile)
        let main = lockFile.Head
        let packages = List.rev main.Packages
    
        packages.Length |> shouldEqual 2

        packages.Head.Name |> shouldEqual (PackageName "CommonServiceLocator")
        packages.Tail.Head.Name |> shouldEqual (PackageName "MvvmLightLibs")
        LockFileSerializer.serializePackages main.Options (main.Packages |> List.map (fun p -> p.Name,p) |> Map.ofList)
        |> normalizeLineEndings
        |> shouldEqual (normalizeLineEndings lockFileWithManyFrameworks)

let lockFileWithDependencies = """NUGET
  remote: https://www.nuget.org/api/v2
    Argu (2.1)
    Chessie (0.4)
      FSharp.Core
    FSharp.Core (4.0.0.1) - redirects: force
    Newtonsoft.Json (8.0.3) - redirects: force"""

[<Test>]
let ``should parse lock file with depdencies``() = 
    let lockFile = LockFileParser.Parse(toLines lockFileWithDependencies)
    let main = lockFile.Head
    let packages = List.rev main.Packages
    
    LockFileSerializer.serializePackages main.Options (main.Packages |> List.map (fun p -> p.Name,p) |> Map.ofList)
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings lockFileWithDependencies)

let lockFileWithGreaterZeroDependency = """NUGET
  remote: https://www.nuget.org/api/v2
  specs:
    Argu (2.1)
    Chessie (0.4)
      FSharp.Core (>= 0.0)
    FSharp.Core (4.0.0.1) - redirects: force
    Newtonsoft.Json (8.0.3) - redirects: force"""

[<Test>]
let ``should parse lock file with greater zero dependency``() = 
    let lockFile = LockFileParser.Parse(toLines lockFileWithGreaterZeroDependency)
    let main = lockFile.Head
    let packages = List.rev main.Packages
    
    LockFileSerializer.serializePackages main.Options (main.Packages |> List.map (fun p -> p.Name,p) |> Map.ofList)
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings lockFileWithDependencies)

let fullGitLockFile = """
GIT
  remote: git@github.com:fsprojects/Paket.git
  specs:
     (528024723f314aa1011499a122258167b53699f7)
"""

[<Test>]
let ``should parse full git lock file``() = 
    let lockFile = LockFileParser.Parse(toLines fullGitLockFile)
    lockFile.Head.RemoteUrl |> shouldEqual (Some "git@github.com:fsprojects/Paket.git")
    lockFile.Head.SourceFiles.Head.Commit |> shouldEqual "528024723f314aa1011499a122258167b53699f7"
    lockFile.Head.SourceFiles.Head.Project |> shouldEqual "Paket"

let localGitLockFile = """
GIT
  remote: file:///c:/code/Paket.VisualStudio
  specs:
     (528024723f314aa1011499a122258167b53699f7)
"""

[<Test>]
let ``should parse local git lock file``() = 
    let lockFile = LockFileParser.Parse(toLines localGitLockFile)
    lockFile.Head.RemoteUrl |> shouldEqual (Some "file:///c:/code/Paket.VisualStudio")
    lockFile.Head.SourceFiles.Head.Commit |> shouldEqual "528024723f314aa1011499a122258167b53699f7"
    lockFile.Head.SourceFiles.Head.Project |> shouldEqual "Paket.VisualStudio"
    lockFile.Head.SourceFiles.Head.Command |> shouldEqual None


let localGitLockFileWithBuild = """
NUGET
  remote: paket-files/github.com/nupkgtest/source
  specs:
    Argu (1.1.3)
GIT
  remote: https://github.com/forki/nupkgtest.git
  specs:
     (2942d23fcb13a2574b635194203aed7610b21903)
      build: build.cmd Test
"""

[<Test>]
let ``should parse local git lock file with build``() = 
    let lockFile = LockFileParser.Parse(toLines localGitLockFileWithBuild)
    lockFile.Head.RemoteUrl |> shouldEqual (Some "https://github.com/forki/nupkgtest.git")
    lockFile.Head.SourceFiles.Head.Commit |> shouldEqual "2942d23fcb13a2574b635194203aed7610b21903"
    lockFile.Head.SourceFiles.Head.Project |> shouldEqual "nupkgtest"
    lockFile.Head.SourceFiles.Head.Command |> shouldEqual (Some "build.cmd Test")


let localGitLockFileWithBuildAndNoSpecs = """
NUGET
  remote: paket-files/github.com/nupkgtest/source
  specs:
    Argu (1.1.3)
GIT
  remote: https://github.com/forki/nupkgtest.git
     (2942d23fcb13a2574b635194203aed7610b21903)
      build: build.cmd Test
"""

[<Test>]
let ``should parse local git lock file with build and no specs``() = 
    let lockFile = LockFileParser.Parse(toLines localGitLockFileWithBuildAndNoSpecs)
    lockFile.Head.RemoteUrl |> shouldEqual (Some "https://github.com/forki/nupkgtest.git")
    lockFile.Head.SourceFiles.Head.Commit |> shouldEqual "2942d23fcb13a2574b635194203aed7610b21903"
    lockFile.Head.SourceFiles.Head.Project |> shouldEqual "nupkgtest"
    lockFile.Head.SourceFiles.Head.Command |> shouldEqual (Some "build.cmd Test")

let lockFileWithFilesContainingSpaces = """
GITHUB
  remote: owner/repo
  specs:
    "file 1.fs" (7623fc13439f0e60bd05c1ed3b5f6dcb937fe468)
    "file 2.fs" (7623fc13439f0e60bd05c1ed3b5f6dcb937fe468) secret"""

[<Test>]
let ``should parse lock file with spaces in file names``() =
    let lockFile = LockFileParser.Parse (toLines lockFileWithFilesContainingSpaces)
    let sourceFiles = List.rev lockFile.Head.SourceFiles
    sourceFiles|> shouldEqual
        [ { Owner = "owner"
            Project = "repo"
            Name = "file 1.fs"
            Origin = ModuleResolver.Origin.GitHubLink
            Dependencies = Set.empty
            Commit = "7623fc13439f0e60bd05c1ed3b5f6dcb937fe468"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            AuthKey = None }
          { Owner = "owner"
            Project = "repo"
            Name = "file 2.fs"
            Origin = ModuleResolver.Origin.GitHubLink
            Dependencies = Set.empty
            Commit = "7623fc13439f0e60bd05c1ed3b5f6dcb937fe468"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            AuthKey = Some "secret" } ]


let lockFileWithNewRestrictions = """NUGET
  remote: http://www.nuget.org/api/v2
    MathNet.Numerics (3.2.3)
      TaskParallelLibrary (>= 1.0.2856) - restriction: && (>= net35) (< net40)
    MathNet.Numerics.FSharp (3.2.3)
      MathNet.Numerics (3.2.3)
    TaskParallelLibrary (1.0.2856) - restriction: && (>= net35) (< net40)"""

[<Test>]
let ``should parse new restrictions && (>= net35) (< net40)``() =
    let lockFile = LockFileParser.Parse (toLines lockFileWithNewRestrictions)
    let main = lockFile.Head
    let packages = lockFile.Tail
    
    LockFileSerializer.serializePackages main.Options (main.Packages |> List.map (fun p -> p.Name,p) |> Map.ofList)
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings lockFileWithNewRestrictions)

let lockFileWithNewComplexRestrictions = """NUGET
  remote: http://www.nuget.org/api/v2
    AWSSDK.Core (3.1.5.3)
      Microsoft.Net.Http (>= 2.2.29) - restriction: && (< net45) (>= portable-net45+win8+wp8+wpa81)
      PCLStorage (>= 1.0.2) - restriction: && (< net45) (>= portable-net45+win8+wp8+wpa81)
    Microsoft.Bcl (1.1.10) - restriction: && (< net45) (>= portable-net45+win8+wp8+wpa81)
      Microsoft.Bcl.Build (>= 1.0.14)
    Microsoft.Bcl.Async (1.0.168) - restriction: false
      Microsoft.Bcl (>= 1.1.8)
    Microsoft.Bcl.Build (1.0.21) - import_targets: false, restriction: && (< net45) (>= portable-net45+win8+wp8+wpa81)
    Microsoft.Net.Http (2.2.29) - restriction: && (< net45) (>= portable-net45+win8+wp8+wpa81)
      Microsoft.Bcl (>= 1.1.10)
      Microsoft.Bcl.Build (>= 1.0.14)
    PCLStorage (1.0.2) - restriction: && (< net45) (>= portable-net45+win8+wp8+wpa81)
      Microsoft.Bcl (>= 1.1.6) - restriction: < portable-net45+win8+wp8+wpa81
      Microsoft.Bcl.Async (>= 1.0.165) - restriction: < portable-net45+win8+wp8+wpa81"""

[<Test>]
let ``should parse new restrictions || (&& (< net45) (>= portable-net45+win8+wp8+wpa81)) (&& (< portable-net45+monoandroid+monotouch+xamarinios+xamarinmac+win8+wp8+wpa81) (>= portable-net45+win8+wp8+wpa81))``() =
    let lockFile = LockFileParser.Parse (toLines lockFileWithNewComplexRestrictions)
    let main = lockFile.Head
    let packages = lockFile.Tail
    
    LockFileSerializer.serializePackages main.Options (main.Packages |> List.map (fun p -> p.Name,p) |> Map.ofList)
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings lockFileWithNewComplexRestrictions)

let lockFileWithMissingVersion = """NUGET
  remote: https://www.nuget.org/api/v2
    Microsoft.Bcl (1.1.10) - restriction: || (== net10) (== net11) (== net20) (== net30) (== net35) (== net40)
      Microsoft.Bcl.Build (>= 1.0.14)
    Microsoft.Bcl.Build (1.0.21) - import_targets: false, restriction: || (== net10) (== net11) (== net20) (== net30) (== net35) (== net40)
    Microsoft.Net.Http (2.2.29) - restriction: || (== net10) (== net11) (== net20) (== net30) (== net35) (== net40)
      Microsoft.Bcl (>= 1.1.10)
      Microsoft.Bcl.Build (>= 1.0.14)
    Octokit (0.19)
      Microsoft.Net.Http  - restriction: || (== net10) (== net11) (== net20) (== net30) (== net35) (== net40)"""

[<Test>]
let ``should parse lockfile with missing version``() =
    let lockFile = LockFileParser.Parse (toLines lockFileWithMissingVersion)
    let main = lockFile.Head
    let packages = lockFile.Tail
    
    LockFileSerializer.serializePackages main.Options (main.Packages |> List.map (fun p -> p.Name,p) |> Map.ofList)
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings lockFileWithMissingVersion)

let lockFileWithCLiTool = """NUGET
  remote: https://www.nuget.org/api/v2
    Argu (2.1)
    Chessie (0.4)
      FSharp.Core
    dotnet-fable (1.1.7) - clitool: true
    FSharp.Core (4.0.0.1) - redirects: force
    Newtonsoft.Json (8.0.3) - redirects: force"""

[<Test>]
let ``should parse lock file with cli tool``() = 
    let lockFile = LockFileParser.Parse(toLines lockFileWithCLiTool)
    let main = lockFile.Head
    let packages = List.rev main.Packages
    
    packages
    |> List.find (fun p -> p.Name = PackageName "dotnet-fable")
    |> fun p -> p.Kind |> shouldEqual Paket.PackageResolver.ResolvedPackageKind.DotnetCliTool

    packages
    |> List.find (fun p -> p.Name = PackageName "Argu")
    |> fun p -> p.Kind |> shouldEqual Paket.PackageResolver.ResolvedPackageKind.Package

    LockFileSerializer.serializePackages main.Options (main.Packages |> List.map (fun p -> p.Name,p) |> Map.ofList)
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings lockFileWithCLiTool)
