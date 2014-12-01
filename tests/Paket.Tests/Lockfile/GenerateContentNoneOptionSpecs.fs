module Paket.LockFile.GenerateContentNoneOptionSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

let config1 = """
content none
source "http://nuget.org/api/v2"

nuget "Microsoft.SqlServer.Types"
"""

let graph = [
    "Microsoft.SqlServer.Types","1.0",[]
]

let expected = """CONTENT: NONE
NUGET
  remote: http://nuget.org/api/v2
  specs:
    Microsoft.SqlServer.Types (1.0)"""

[<Test>]
let ``should generate content none lock file``() = 
    let cfg = DependenciesFile.FromCode(config1)
    cfg.Resolve(noSha1,VersionsFromGraph graph, PackageDetailsFromGraph graph).ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Options
    |> shouldEqual (normalizeLineEndings expected)