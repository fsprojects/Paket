module Paket.LockFile.GenerateWithOptionsSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

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
    cfg.Resolve(noSha1,VersionsFromGraph graph1, PackageDetailsFromGraph graph1).ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Options
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
    cfg.Resolve(noSha1,VersionsFromGraph graph2, PackageDetailsFromGraph graph2).ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Options
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
    cfg.Resolve(noSha1,VersionsFromGraph graph3, PackageDetailsFromGraph graph3).ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Options
    |> shouldEqual (normalizeLineEndings expected3)