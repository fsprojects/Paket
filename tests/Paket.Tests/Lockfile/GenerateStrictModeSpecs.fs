module paket.lockFile.GenerateStrictModeSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

let config1 = """
references strict
source "http://nuget.org/api/v2"

nuget "Castle.Windsor-log4net" "~> 3.2"
"""

let graph = [
    "Castle.Windsor-log4net","3.2",[]
]

let expected = """REFERENCES: STRICT
NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Windsor-log4net (3.2)"""

[<Test>]
let ``should generate strict lock file``() = 
    let cfg = DependenciesFile.FromCode(config1)
    cfg.Resolve(noSha1,VersionsFromGraph graph, PackageDetailsFromGraph graph).ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Strict
    |> shouldEqual (normalizeLineEndings expected)