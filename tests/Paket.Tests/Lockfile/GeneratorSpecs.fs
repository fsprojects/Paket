module Paket.LockFile.GeneratorSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.ModuleResolver

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

[<Test>]
let ``should generate lock file for packages``() = 
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
    let cfg = DependenciesFile.FromCode(config1)
    cfg.Resolve(noSha1,VersionsFromGraph graph, PackageDetailsFromGraph graph).ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Options
    |> shouldEqual (normalizeLineEndings expected)

let configWithRestrictions = """
source "http://nuget.org/api/v2"

nuget "Castle.Windsor-log4net" ~> 3.2 framework: net35
nuget "Rx-Main" "~> 2.0" framework: >= net40 """

[<Test>]
let ``should generate lock file with framework restrictions for packages``() = 
    let expected = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Windsor (2.1)
    Castle.Windsor-log4net (3.3) - framework: net35
      Castle.Windsor (>= 2.0)
      log4net (>= 1.0)
    log (1.2)
    log4net (1.1)
      log (>= 1.0)
    Rx-Core (2.1)
    Rx-Main (2.0) - framework: >= net40
      Rx-Core (>= 2.1)"""
    let cfg = DependenciesFile.FromCode(configWithRestrictions)
    cfg.Resolve(noSha1,VersionsFromGraph graph, PackageDetailsFromGraph graph).ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Options
    |> shouldEqual (normalizeLineEndings expected)


let configWithNoImport = """
source "D:\code\temp with space"

nuget "Castle.Windsor-log4net" ~> 3.2 import_targets: false, framework: net35
nuget "Rx-Main" "~> 2.0" framework: >= net40 """

[<Test>]
let ``should generate lock file with no targets import for packages``() = 
    let expected = """NUGET
  remote: "D:\code\temp with space"
  specs:
    Castle.Windsor (2.1) - import_targets: false
    Castle.Windsor-log4net (3.3) - import_targets: false, framework: net35
      Castle.Windsor (>= 2.0)
      log4net (>= 1.0)
    log (1.2) - import_targets: false
    log4net (1.1) - import_targets: false
      log (>= 1.0)
    Rx-Core (2.1)
    Rx-Main (2.0) - framework: >= net40
      Rx-Core (>= 2.1)"""
    let cfg = DependenciesFile.FromCode(configWithNoImport)
    cfg.Resolve(noSha1,VersionsFromGraph graph, PackageDetailsFromGraph graph).ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Options
    |> shouldEqual (normalizeLineEndings expected)

let configWithCopyLocal = """
source "http://nuget.org/api/v2"

nuget "Castle.Windsor-log4net" ~> 3.2 copy_local: false, import_targets: false, framework: net35
nuget "Rx-Main" "~> 2.0" framework: >= net40 """

[<Test>]
let ``should generate lock file with no copy local for packages``() = 
    let expected = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Windsor (2.1) - copy_local: false, import_targets: false
    Castle.Windsor-log4net (3.3) - copy_local: false, import_targets: false, framework: net35
      Castle.Windsor (>= 2.0)
      log4net (>= 1.0)
    log (1.2) - copy_local: false, import_targets: false
    log4net (1.1) - copy_local: false, import_targets: false
      log (>= 1.0)
    Rx-Core (2.1)
    Rx-Main (2.0) - framework: >= net40
      Rx-Core (>= 2.1)"""
    let cfg = DependenciesFile.FromCode(configWithCopyLocal)
    cfg.Resolve(noSha1,VersionsFromGraph graph, PackageDetailsFromGraph graph).ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Options
    |> shouldEqual (normalizeLineEndings expected)


let configWithDisabledContent = """
source "http://nuget.org/api/v2"

nuget "Castle.Windsor-log4net" ~> 3.2 framework: net35
nuget "Rx-Main" "~> 2.0" content: none, framework: >= net40 """

[<Test>]
let ``should generate lock file with disabled content for packages``() = 
    let expected = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Windsor (2.1)
    Castle.Windsor-log4net (3.3) - framework: net35
      Castle.Windsor (>= 2.0)
      log4net (>= 1.0)
    log (1.2)
    log4net (1.1)
      log (>= 1.0)
    Rx-Core (2.1) - content: none
    Rx-Main (2.0) - content: none, framework: >= net40
      Rx-Core (>= 2.1)"""
    let cfg = DependenciesFile.FromCode(configWithDisabledContent)
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
                            Origin = ModuleResolver.SingleSourceFileOrigin.GitHubLink
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

let trivialResolve (f:ModuleResolver.UnresolvedSourceFile) =
    { Commit =
        match f.Commit with
        | Some(v) -> v
        | None -> ""
      Owner = f.Owner
      Origin = f.Origin
      Project = f.Project
      Dependencies = Set.empty
      Name = f.Name } : ModuleResolver.ResolvedSourceFile

let expectedWithHttp = """HTTP
  remote: http://www.fssnip.net
  specs:
    test.fs (/raw/1M)"""
    
[<Test>]
let ``should generate lock file for http source files``() = 
    let config = """http "http://www.fssnip.net/raw/1M" "test.fs" """ 

    let cfg = DependenciesFile.FromCode(config)
    
    cfg.RemoteFiles
    |> List.map trivialResolve
    |> LockFileSerializer.serializeSourceFiles
    |> shouldEqual (normalizeLineEndings expectedWithHttp)

let expectedMultiple = """HTTP
  remote: http://www.fssnip.net
  specs:
    myFile.fs (/raw/1M)
    myFile2.fs (/raw/32)
    myFile3.fs (/raw/15)
GIST
  remote: Thorium/1972308
  specs:
    gistfile1.fs
  remote: Thorium/6088882
  specs:
    FULLPROJECT"""
    
[<Test>]
let ``should generate lock file for http and gist source files``() = 
    let config = """source "http://nuget.org/api/v2

http http://www.fssnip.net/raw/32 myFile2.fs

gist Thorium/1972308 gistfile1.fs
gist Thorium/6088882 

http http://www.fssnip.net/raw/1M myFile.fs
http http://www.fssnip.net/raw/15 myFile3.fs """ 

    let cfg = DependenciesFile.FromCode(config)
    
    cfg.RemoteFiles
    |> List.map trivialResolve
    |> LockFileSerializer.serializeSourceFiles
    |> shouldEqual (normalizeLineEndings expectedMultiple)


let expectedForStanfordNLPdotNET = """HTTP
  remote: http://www.frijters.net
  specs:
    ikvmbin-8.0.5449.0.zip (/ikvmbin-8.0.5449.0.zip)
  remote: http://nlp.stanford.edu
  specs:
    stanford-corenlp-full-2014-10-31.zip (/software/stanford-corenlp-full-2014-10-31.zip)
    stanford-ner-2014-10-26.zip (/software/stanford-ner-2014-10-26.zip)
    stanford-parser-full-2014-10-31.zip (/software/stanford-parser-full-2014-10-31.zip)
    stanford-postagger-full-2014-10-26.zip (/software/stanford-postagger-full-2014-10-26.zip)
    stanford-segmenter-2014-10-26.zip (/software/stanford-segmenter-2014-10-26.zip)"""

[<Test>]
let ``should generate lock file for http Stanford.NLP.NET project``() =
    let config = """http http://www.frijters.net/ikvmbin-8.0.5449.0.zip
http http://nlp.stanford.edu/software/stanford-corenlp-full-2014-10-31.zip
http http://nlp.stanford.edu/software/stanford-ner-2014-10-26.zip
http http://nlp.stanford.edu/software/stanford-parser-full-2014-10-31.zip
http http://nlp.stanford.edu/software/stanford-postagger-full-2014-10-26.zip
http http://nlp.stanford.edu/software/stanford-segmenter-2014-10-26.zip"""

    let cfg = DependenciesFile.FromCode(config)

    let references =
        cfg.RemoteFiles
        |> List.map trivialResolve
    
    references.Length |> shouldEqual 6

    references.[5].Origin |> shouldEqual (SingleSourceFileOrigin.HttpLink("http://nlp.stanford.edu"))
    references.[5].Commit |> shouldEqual ("/software/stanford-segmenter-2014-10-26.zip")  // That's strange
    references.[5].Name |> shouldEqual "stanford-segmenter-2014-10-26.zip"  

    references
    |> LockFileSerializer.serializeSourceFiles
    |> shouldEqual (normalizeLineEndings expectedForStanfordNLPdotNET)

[<Test>]
let ``should parse and regenerate http Stanford.NLP.NET project``() =
    let lockFile = LockFileParser.Parse(toLines expectedForStanfordNLPdotNET)
    
    lockFile.SourceFiles
    |> List.rev
    |> LockFileSerializer.serializeSourceFiles
    |> shouldEqual (normalizeLineEndings expectedForStanfordNLPdotNET)