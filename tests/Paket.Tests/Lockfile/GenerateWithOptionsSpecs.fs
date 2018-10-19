module Paket.LockFile.GenerateWithOptionsSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.Requirements

let config1 = """
references strict
framework: >= net45
copy_local false
specific_version true
source "http://www.nuget.org/api/v2"

nuget "Castle.Windsor-log4net" "~> 3.2"
"""

let graph1 =
    OfSimpleGraph [
        "Castle.Windsor-log4net","3.2",[]
    ]

let expected1 = """REFERENCES: STRICT
COPY-LOCAL: FALSE
SPECIFIC-VERSION: TRUE
RESTRICTION: >= net45
NUGET
  remote: http://www.nuget.org/api/v2
    Castle.Windsor-log4net (3.2)"""

[<Test>]
let ``should generate strict lock file``() = 
    let cfg = DependenciesFile.FromSource(config1)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph1, PackageDetailsFromGraph graph1).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected1)


let configWithContent = """
content none
import_targets false
source "http://www.nuget.org/api/v2"

nuget "Microsoft.SqlServer.Types"
"""

let graph2 =
    OfSimpleGraph [
        "Microsoft.SqlServer.Types","1.0",[]
    ]

let expected2 = """IMPORT-TARGETS: FALSE
CONTENT: NONE
NUGET
  remote: http://www.nuget.org/api/v2
    Microsoft.SqlServer.Types (1.0)"""

[<Test>]
let ``should generate content none lock file``() = 
    let cfg = DependenciesFile.FromSource(configWithContent)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph2, PackageDetailsFromGraph graph2).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected2)

let configWithRedirects = """
redirects on
source "http://www.nuget.org/api/v2"

nuget "Microsoft.SqlServer.Types"
"""

let graph3 = 
    OfSimpleGraph [
        "Microsoft.SqlServer.Types","1.0",[]
    ]

let expected3 = """REDIRECTS: ON
NUGET
  remote: http://www.nuget.org/api/v2
    Microsoft.SqlServer.Types (1.0)"""

[<Test>]
let ``should generate redirects lock file``() = 
    let cfg = DependenciesFile.FromSource(configWithRedirects)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph3, PackageDetailsFromGraph graph3).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected3)

[<Test>]
let ``should generate strategy min lock file``() = 
    let config = """
    strategy min
    source "http://www.nuget.org/api/v2"

    nuget "Microsoft.SqlServer.Types"
    """

    let expected = """STRATEGY: MIN
NUGET
  remote: http://www.nuget.org/api/v2
    Microsoft.SqlServer.Types (1.0)"""

    let cfg = DependenciesFile.FromSource(config)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph3, PackageDetailsFromGraph graph3).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should generate strategy max lock file``() = 
    let config = """
    strategy max
    source "http://www.nuget.org/api/v2"

    nuget "Microsoft.SqlServer.Types"
    """

    let expected = """STRATEGY: MAX
NUGET
  remote: http://www.nuget.org/api/v2
    Microsoft.SqlServer.Types (1.0)"""

    let cfg = DependenciesFile.FromSource(config)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph3, PackageDetailsFromGraph graph3).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should generate lowest_matching true lock file``() = 
    let config = """
    lowest_matching true
    source "http://www.nuget.org/api/v2"

    nuget "Microsoft.SqlServer.Types"
    """

    let expected = """LOWEST_MATCHING: TRUE
NUGET
  remote: http://www.nuget.org/api/v2
    Microsoft.SqlServer.Types (1.0)"""

    let cfg = DependenciesFile.FromSource(config)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph3, PackageDetailsFromGraph graph3).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should generate lowest_matching false lock file``() = 
    let config = """
    lowest_matching false
    source "http://www.nuget.org/api/v2"

    nuget "Microsoft.SqlServer.Types"
    """

    let expected = """LOWEST_MATCHING: FALSE
NUGET
  remote: http://www.nuget.org/api/v2
    Microsoft.SqlServer.Types (1.0)"""

    let cfg = DependenciesFile.FromSource(config)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph3, PackageDetailsFromGraph graph3).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected)


[<Test>]
let ``should resolve config with global framework restrictions``() = 

    let config = """framework: >= net40

source https://www.nuget.org/api/v2

nuget NLog framework: net40
nuget NLog.Contrib
"""

    let graph =
      OfSimpleGraph [
        "NLog","1.0.0",[]
        "NLog","1.0.1",[]
        "NLog.Contrib","1.0.0",["NLog",DependenciesFileParser.parseVersionRequirement ">= 1.0.1"]
      ]

    let expected = """RESTRICTION: >= net40
NUGET
  remote: https://www.nuget.org/api/v2
    NLog (1.0.1) - restriction: == net40
    NLog.Contrib (1.0)
      NLog (>= 1.0.1)"""

    let cfg = DependenciesFile.FromSource(config)
    let group = cfg.Groups.[Constants.MainDependencyGroup]
    group.Packages.Head.Settings.FrameworkRestrictions 
    |> getExplicitRestriction
    |> shouldEqual (FrameworkRestriction.Exactly(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4)))

    let resolved = ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "NLog"] |> shouldEqual "1.0.1"
    resolved.[PackageName "NLog"].Settings.FrameworkRestrictions 
    |> getExplicitRestriction
    |> shouldEqual (FrameworkRestriction.Exactly(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4)))

    resolved
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected)

