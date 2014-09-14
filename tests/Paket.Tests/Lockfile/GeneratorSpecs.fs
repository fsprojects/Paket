module paket.lockFile.GeneratorSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

let config1 = """
source "http://nuget.org/api/v2"

nuget "Castle.Windsor-log4net" "~> 3.2"
nuget "Rx-Main" "~> 2.0" """

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
let ``should generate lock file for packages``() = 
    let cfg = DependenciesFile.FromCode config1
    cfg.Resolve(true, DictionaryDiscovery graph)
    |> LockFile.serializePackages
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should generate lock file for source files``() = 
    let cfg = """github "owner:project1" "folder/file.fs"
github "owner:project1:commit1" "folder/file1.fs"
github "owner:project2:commit2" "folder/file.fs" """ |> DependenciesFile.FromCode
    
    cfg.RemoteFiles
    |> LockFile.serializeSourceFiles
    |> shouldEqual """GITHUB
  remote: owner/project1
  specs:
    folder/file.fs
    folder/file1.fs (commit1)
  remote: owner/project2
  specs:
    folder/file.fs (commit2)"""