module paket.lockFile.GenerateAuthModeSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

let config1 = """
source "http://nuget.org/api/v2"  username: "user" password: "pass"

nuget "Castle.Windsor-log4net" "~> 3.2"
"""

let graph = [
    "Castle.Windsor-log4net","3.2",[]
]

let expected = """NUGET
  remote: http://nuget.org/api/v2 username: "user" password: "pass"
  specs:
    Castle.Windsor-log4net (3.2)"""

[<Test>]
let ``should generate strict lock file``() = 
    let cfg = DependenciesFile.FromCode(noSha1,config1)
    cfg.Resolve(VersionsFromGraph graph, PackageDetailsFromGraph graph) |> UpdateProcess.getResolvedPackagesOrFail
    |> LockFileSerializer.serializePackages cfg.Strict
    |> shouldEqual (normalizeLineEndings expected)