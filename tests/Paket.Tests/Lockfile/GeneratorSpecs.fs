module Paket.LockFile.GeneratorSpecs

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
    log (1.2)
    log4net (1.1)
      log (>= 1.0)
    Rx-Core (2.1)
    Rx-Main (2.0)
      Rx-Core (>= 2.1)"""

[<Test>]
let ``should generate lock file for packages``() = 
    let cfg = DependenciesFile.FromCode(fakeSha1,config1)
    cfg.Resolve(VersionsFromGraph graph, PackageDetailsFromGraph graph)|> UpdateProcess.getResolvedPackagesOrFail
    |> LockFileSerializer.serializePackages cfg.Strict
    |> shouldEqual (normalizeLineEndings expected)


let expectedWithGitHub = """GITHUB
  remote: owner/project1
  specs:
    folder/file.fs (master)
    folder/file1.fs (commit1)
  remote: owner/project2
  specs:
    folder/file.fs (commit2)"""
    
[<Test>]
let ``should generate lock file for source files``() = 
    let config = """github "owner:project1:master" "folder/file.fs"
github "owner/project1:commit1" "folder/file1.fs"
github "owner:project2:commit2" "folder/file.fs" """ 

    let cfg =DependenciesFile.FromCode(fakeSha1,config)
    
    cfg.RemoteFiles
    |> LockFileSerializer.serializeSourceFiles
    |> shouldEqual (normalizeLineEndings expectedWithGitHub)
