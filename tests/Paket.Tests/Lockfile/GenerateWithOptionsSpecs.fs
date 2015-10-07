module Paket.LockFile.GenerateWithOptionsSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain

let config1 = """
references strict
framework: >= net45
copy_local false
source "http://nuget.org/api/v2"

nuget "Castle.Windsor-log4net" "~> 3.2"
"""

let graph1 = [
    "Castle.Windsor-log4net","3.2",[]
]

let expected1 = """REFERENCES: STRICT
COPY-LOCAL: FALSE
FRAMEWORK: >= NET45
NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Windsor-log4net (3.2)"""

[<Test>]
let ``should generate strict lock file``() = 
    let cfg = DependenciesFile.FromCode(config1)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph1, PackageDetailsFromGraph graph1).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected1)


let configWithContent = """
content none
import_targets false
source "http://nuget.org/api/v2"

nuget "Microsoft.SqlServer.Types"
"""

let graph2 = [
    "Microsoft.SqlServer.Types","1.0",[]
]

let expected2 = """IMPORT-TARGETS: FALSE
CONTENT: NONE
NUGET
  remote: http://nuget.org/api/v2
  specs:
    Microsoft.SqlServer.Types (1.0)"""

[<Test>]
let ``should generate content none lock file``() = 
    let cfg = DependenciesFile.FromCode(configWithContent)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph2, PackageDetailsFromGraph graph2).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected2)

let configWithRedirects = """
redirects on
source "http://nuget.org/api/v2"

nuget "Microsoft.SqlServer.Types"
"""

let graph3 = [
    "Microsoft.SqlServer.Types","1.0",[]
]

let expected3 = """REDIRECTS: ON
NUGET
  remote: http://nuget.org/api/v2
  specs:
    Microsoft.SqlServer.Types (1.0)"""

[<Test>]
let ``should generate redirects lock file``() = 
    let cfg = DependenciesFile.FromCode(configWithRedirects)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph3, PackageDetailsFromGraph graph3).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected3)