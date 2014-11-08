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
    "Castle.Windsor-log4net","3.3",["Castle.Windsor",VersionRequirement(VersionRange.AtLeast "2.0",PreReleaseStatus.No);"log4net",VersionRequirement(VersionRange.AtLeast "1.0",PreReleaseStatus.No)]
    "Castle.Windsor","2.0",[]
    "Castle.Windsor","2.1",[]
    "Rx-Main","2.0",["Rx-Core",VersionRequirement(VersionRange.AtLeast "2.1",PreReleaseStatus.No)]
    "Rx-Core","2.0",[]
    "Rx-Core","2.1",[]
    "log4net","1.0",["log",VersionRequirement(VersionRange.AtLeast "1.0",PreReleaseStatus.No)]
    "log4net","1.1",["log",VersionRequirement(VersionRange.AtLeast "1.0",PreReleaseStatus.No)]
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
    let cfg = DependenciesFile.FromCode(config1)
    cfg.Resolve(noSha1,VersionsFromGraph graph, PackageDetailsFromGraph graph).ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Options
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

    let cfg = DependenciesFile.FromCode(config)
    
    cfg.RemoteFiles
    |> List.map (fun f -> 
        match f.Commit with
        | Some commit ->  { Commit = commit
                            Owner = f.Owner
                            Origin = Paket.ModuleResolver.SourceFileOrigin.GitHubLink
                            Project = f.Project
                            Dependencies = Set.empty
                            Name = f.Name } : ModuleResolver.ResolvedSourceFile
        | _ -> failwith "error")
    |> LockFileSerializer.serializeSourceFiles
    |> shouldEqual (normalizeLineEndings expectedWithGitHub)


let config2 = """
source https://www.myget.org/F/ravendb3/

nuget RavenDB.Client == 3.0.3498-Unstable
 """

let graph2 = [
    "RavenDB.Client","3.0.3498-Unstable",[]
]

let expected2 = """NUGET
  remote: https://www.myget.org/F/ravendb3
  specs:
    RavenDB.Client (3.0.3498-Unstable)"""

[<Test>]
let ``should generate lock file for RavenDB.Client``() = 
    let cfg = DependenciesFile.FromCode(config2)
    cfg.Resolve(noSha1,VersionsFromGraph graph2, PackageDetailsFromGraph graph2).ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Options
    |> shouldEqual (normalizeLineEndings expected2)

let config3 = """
source "http://nuget.org/api/v2"

nuget "OtherVersionRanges.Package" "~> 1.0" """

let graph3 = [
    "OtherVersionRanges.Package","1.0", ["LessThan.Package", VersionRequirement(VersionRange.LessThan(SemVer.Parse "2.0"), PreReleaseStatus.No)]
    "LessThan.Package","1.9",["GreaterThan.Package", VersionRequirement(VersionRange.GreaterThan(SemVer.Parse "2.0"), PreReleaseStatus.No)]
    "GreaterThan.Package","2.1",["Maximum.Package", VersionRequirement(VersionRange.Maximum(SemVer.Parse "3.0"), PreReleaseStatus.No)]
    "Maximum.Package","2.9",[]
]

let expected3 = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    GreaterThan.Package (2.1)
      Maximum.Package (<= 3.0)
    LessThan.Package (1.9)
      GreaterThan.Package (> 2.0)
    Maximum.Package (2.9)
    OtherVersionRanges.Package (1.0)
      LessThan.Package (< 2.0)"""

[<Test>]
let ``should generate other version ranges for packages``() = 
    let cfg = DependenciesFile.FromCode(config3)
    cfg.Resolve(noSha1,VersionsFromGraph graph3, PackageDetailsFromGraph graph3).ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Options
    |> shouldEqual (normalizeLineEndings expected3)

let expectedWithHttp = """HTTP
  remote: LINK
  specs:
    http://www.fssnip.net/raw/1M"""
    
[<Test>]
let ``should generate lock file for http source files``() = 
    let config = """http "http://www.fssnip.net/raw/1M" """ 

    let cfg = DependenciesFile.FromCode(config)
    
    cfg.RemoteFiles
    |> List.map (fun f -> 
          { Commit = ""
            Owner = f.Owner
            Origin = Paket.ModuleResolver.SourceFileOrigin.HttpLink
            Project = f.Project
            Dependencies = Set.empty
            Name = f.Name } : ModuleResolver.ResolvedSourceFile)
    |> LockFileSerializer.serializeSourceFiles
    |> shouldEqual (normalizeLineEndings expectedWithHttp)

