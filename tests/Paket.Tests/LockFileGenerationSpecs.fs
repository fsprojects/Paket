module Paket.LockFileGenerationSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

let config1 = """
source "http://nuget.org/api/v2"

nuget "Castle.Windsor-log4net" "~> 3.2"
nuget "Rx-Main" "~> 2.0"
"""

let graph = [
    "Castle.Windsor-log4net","3.2",[]
    "Castle.Windsor-log4net","3.3",["Castle.Windsor",VersionRange.AtLeast "2.0";"log4net",VersionRange.AtLeast "1.0"]
    "Castle.Windsor","2.0",[]
    "Castle.Windsor","2.1",[]
    "Rx-Main","2.0",["Rx-Core",VersionRange.AtLeast "2.1"]
    "Rx-Core","2.0",[]
    "Rx-Core","2.1",[]
    "log4net","1.0",["log",VersionRange.AtLeast "1.0"]
    "log4net","1.1",["log",VersionRange.AtLeast "1.0"]
    "log","1.0",[]
    "log","1.2",[]
]

let expected = """NUGET
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
      log (>= 1.0)"""

[<Test>]
let ``should generate lock file``() = 
    let cfg = Config.FromCode config1
    cfg.Resolve(true, DictionaryDiscovery graph)
    |> LockFile.format
    |> shouldEqual (normalizeLineEndings expected)