module Paket.LockFile.GeneratorSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.ModuleResolver
open Paket.Domain

let config1 = """
source "http://www.nuget.org/api/v2"

nuget "Castle.Windsor-log4net" "~> 3.2"
nuget "Rx-Main" "~> 2.0" """

let graph = 
  OfSimpleGraph [
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
    "FAKE","4.0",[]
  ]

[<Test>]
let ``should generate lock file for packages``() = 
    let expected = """NUGET
  remote: http://www.nuget.org/api/v2
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

    let cfg = DependenciesFile.FromSource(config1)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected)

let configWithRestrictions = """
source "http://www.nuget.org/api/v2"

nuget "Castle.Windsor-log4net" ~> 3.2 framework: net35
nuget "Rx-Main" "~> 2.0" framework: >= net40 """

[<Test>]
let ``should generate lock file with framework restrictions for packages``() = 
    let expected = """NUGET
  remote: http://www.nuget.org/api/v2
    Castle.Windsor (2.1) - restriction: == net35
    Castle.Windsor-log4net (3.3) - restriction: == net35
      Castle.Windsor (>= 2.0)
      log4net (>= 1.0)
    log (1.2) - restriction: == net35
    log4net (1.1) - restriction: == net35
      log (>= 1.0)
    Rx-Core (2.1) - restriction: >= net40
    Rx-Main (2.0) - restriction: >= net40
      Rx-Core (>= 2.1)"""

    let cfg = DependenciesFile.FromSource(configWithRestrictions)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected)


let configWithNoImport = """
source "D:\code\temp with space"

nuget "Castle.Windsor-log4net" ~> 3.2 import_targets: false, framework: net35
nuget "Rx-Main" "~> 2.0" framework: >= net40 """

[<Test>]
let ``should generate lock file with no targets import for packages``() = 
    let expected = """NUGET
  remote: "D:\code\temp with space"
    Castle.Windsor (2.1) - import_targets: false, restriction: == net35
    Castle.Windsor-log4net (3.3) - import_targets: false, restriction: == net35
      Castle.Windsor (>= 2.0)
      log4net (>= 1.0)
    log (1.2) - import_targets: false, restriction: == net35
    log4net (1.1) - import_targets: false, restriction: == net35
      log (>= 1.0)
    Rx-Core (2.1) - restriction: >= net40
    Rx-Main (2.0) - restriction: >= net40
      Rx-Core (>= 2.1)"""

    let cfg = DependenciesFile.FromSource(configWithNoImport)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected)

let configWithCopyLocal = """
source "http://www.nuget.org/api/v2"

nuget "Castle.Windsor-log4net" ~> 3.2 copy_local: false, import_targets: false, framework: net35
nuget "Rx-Main" "~> 2.0" framework: >= net40 """

[<Test>]
let ``should generate lock file with no copy local for packages``() = 
    let expected = """NUGET
  remote: http://www.nuget.org/api/v2
    Castle.Windsor (2.1) - copy_local: false, import_targets: false, restriction: == net35
    Castle.Windsor-log4net (3.3) - copy_local: false, import_targets: false, restriction: == net35
      Castle.Windsor (>= 2.0)
      log4net (>= 1.0)
    log (1.2) - copy_local: false, import_targets: false, restriction: == net35
    log4net (1.1) - copy_local: false, import_targets: false, restriction: == net35
      log (>= 1.0)
    Rx-Core (2.1) - restriction: >= net40
    Rx-Main (2.0) - restriction: >= net40
      Rx-Core (>= 2.1)"""
    let cfg = DependenciesFile.FromSource(configWithCopyLocal)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected)

let configWithSpecificVersion = """
source "http://www.nuget.org/api/v2"

nuget "Castle.Windsor-log4net" ~> 3.2 specific_version: false, import_targets: false, framework: net35
nuget "Rx-Main" "~> 2.0" framework: >= net40 """

[<Test>]
let ``should generate lock file with no specific version for packages``() = 
    let expected = """NUGET
  remote: http://www.nuget.org/api/v2
    Castle.Windsor (2.1) - specific_version: false, import_targets: false, restriction: == net35
    Castle.Windsor-log4net (3.3) - specific_version: false, import_targets: false, restriction: == net35
      Castle.Windsor (>= 2.0)
      log4net (>= 1.0)
    log (1.2) - specific_version: false, import_targets: false, restriction: == net35
    log4net (1.1) - specific_version: false, import_targets: false, restriction: == net35
      log (>= 1.0)
    Rx-Core (2.1) - restriction: >= net40
    Rx-Main (2.0) - restriction: >= net40
      Rx-Core (>= 2.1)"""
    let cfg = DependenciesFile.FromSource(configWithSpecificVersion)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected)

let configWithDisabledContent = """
source "http://www.nuget.org/api/v2"

nuget "Castle.Windsor-log4net" ~> 3.2 framework: net35
nuget "Rx-Main" "~> 2.0" content: none, framework: >= net40 """

[<Test>]
let ``should generate lock file with disabled content for packages``() = 
    let expected = """NUGET
  remote: http://www.nuget.org/api/v2
    Castle.Windsor (2.1) - restriction: == net35
    Castle.Windsor-log4net (3.3) - restriction: == net35
      Castle.Windsor (>= 2.0)
      log4net (>= 1.0)
    log (1.2) - restriction: == net35
    log4net (1.1) - restriction: == net35
      log (>= 1.0)
    Rx-Core (2.1) - content: none, restriction: >= net40
    Rx-Main (2.0) - content: none, restriction: >= net40
      Rx-Core (>= 2.1)"""
    let cfg = DependenciesFile.FromSource(configWithDisabledContent)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should generate lock file for github source files``() =
    let expectedWithGitHub = "GITHUB
  remote: owner/project0
    \"folder/file 9.fs\" (feature/branch)
    folder/file.fs (master)
    folder/file3.fs (feature/branch)
    folder/file4.fs (feature/branch)
    folder/file5.fs (feature/branch)
    folder/file6.fs (feature/branch)
    folder/file7.fs (feature/branch)
    folder/file8.fs (feature/branch)
  remote: owner/project1
    \"folder/file 2.fs\" (commit1)
    folder/file.fs (master)
    folder/file1.fs (commit1)
    folder/file3.fs (commit0)
    folder/file4.fs (commit1)
    folder/file5.fs (commit1)
  remote: owner/project2
    folder/file.fs (commit2)
    folder/file3.fs (commit3) githubAuth
  remote: owner/project3
    FULLPROJECT (master)"

    let config = "github \"owner:project0:master\" \"folder/file.fs\"
github \"owner:project0:feature/branch\" \"folder/file3.fs\"
github \"owner/project0:feature/branch\" \"folder/file4.fs\"
github owner:project0:feature/branch \"folder/file5.fs\"
github owner/project0:feature/branch \"folder/file6.fs\"
github owner:project0:feature/branch folder/file7.fs
github owner/project0:feature/branch folder/file8.fs
github owner/project0:feature/branch \"folder/file 9.fs\"
github \"owner:project1:master\" \"folder/file.fs\"
github \"owner/project1:commit1\" \"folder/file1.fs\"
github \"owner/project1:commit1\" \"folder/file 2.fs\"
github \"owner/project1:commit0\" folder/file3.fs
github owner/project1:commit1 folder/file4.fs
github owner/project1:commit1 \"folder/file5.fs\"
github \"owner:project2:commit2\" \"folder/file.fs\"
github \"owner:project2:commit3\" \"folder/file3.fs\" githubAuth
github \"owner:project3:master\""

    let cfg = DependenciesFile.FromSource(config)
    
    cfg.Groups.[Constants.MainDependencyGroup].RemoteFiles
    |> List.map (fun f -> 
        match f.Version with
        | VersionRestriction.Concrete commit -> 
            { Commit = commit
              Owner = f.Owner
              Origin = ModuleResolver.Origin.GitHubLink
              Project = f.Project
              Command = None
              OperatingSystemRestriction = None
              PackagePath = None
              Dependencies = Set.empty
              Name = f.Name
              AuthKey = f.AuthKey } : ModuleResolver.ResolvedSourceFile
        | _ -> failwith "error")
    |> LockFileSerializer.serializeSourceFiles
    |> shouldEqual (normalizeLineEndings expectedWithGitHub)


let config2 = """
source https://www.myget.org/F/ravendb3/

nuget RavenDB.Client == 3.0.3498-Unstable
 """

let graph2 =
    OfSimpleGraph [
        "RavenDB.Client","3.0.3498-Unstable",[]
    ]

let expected2 = """NUGET
  remote: https://www.myget.org/F/ravendb3
    RavenDB.Client (3.0.3498-Unstable)"""

[<Test>]
let ``should generate lock file for RavenDB.Client``() = 
    let cfg = DependenciesFile.FromSource(config2)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph2, PackageDetailsFromGraph graph2).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected2)

let config3 = """
source "http://www.nuget.org/api/v2"

nuget "OtherVersionRanges.Package" "~> 1.0" """

let graph3 =
  OfSimpleGraph [
    "OtherVersionRanges.Package","1.0", ["LessThan.Package", VersionRequirement(VersionRange.LessThan(SemVer.Parse "2.0"), PreReleaseStatus.No)]
    "LessThan.Package","1.9",["GreaterThan.Package", VersionRequirement(VersionRange.GreaterThan(SemVer.Parse "2.0"), PreReleaseStatus.No)]
    "GreaterThan.Package","2.1",["Maximum.Package", VersionRequirement(VersionRange.Maximum(SemVer.Parse "3.0"), PreReleaseStatus.No)]
    "Maximum.Package","2.9",[]
  ]

let expected3 = """NUGET
  remote: http://www.nuget.org/api/v2
    GreaterThan.Package (2.1)
      Maximum.Package (<= 3.0)
    LessThan.Package (1.9)
      GreaterThan.Package (> 2.0)
    Maximum.Package (2.9)
    OtherVersionRanges.Package (1.0)
      LessThan.Package (< 2.0)"""

[<Test>]
let ``should generate other version ranges for packages``() = 
    let cfg = DependenciesFile.FromSource(config3)
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph3, PackageDetailsFromGraph graph3).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expected3)

let trivialResolve (f:ModuleResolver.UnresolvedSource) =
    { Commit =
        match f.Version with
        | VersionRestriction.Concrete(v) -> v
        | _ -> ""
      Owner = f.Owner
      Origin = f.Origin
      Project = f.Project
      Dependencies = Set.empty
      Command = None
      OperatingSystemRestriction = None
      PackagePath = None
      Name = f.Name
      AuthKey = f.AuthKey } : ModuleResolver.ResolvedSourceFile

let expectedWithHttp = """HTTP
  remote: http://www.fssnip.net
    test.fs (/raw/1M)"""
    
[<Test>]
let ``should generate lock file for http source files``() = 
    let config = """http "http://www.fssnip.net/raw/1M" "test.fs" """ 

    let cfg = DependenciesFile.FromSource(config)
    
    cfg.Groups.[Constants.MainDependencyGroup].RemoteFiles
    |> List.map trivialResolve
    |> LockFileSerializer.serializeSourceFiles
    |> shouldEqual (normalizeLineEndings expectedWithHttp)

let expectedMultiple = """HTTP
  remote: http://www.fssnip.net
    myFile.fs (/raw/1M)
    myFile2.fs (/raw/32)
    myFile3.fs (/raw/15)
    myFile5.fs (/raw/34) httpAuth
GIST
  remote: Thorium/1972308
    gistfile1.fs
  remote: Thorium/6088882
    FULLPROJECT"""
    
[<Test>]
let ``should generate lock file for http and gist source files``() = 
    let config = """source "http://www.nuget.org/api/v2

http http://www.fssnip.net/raw/32 myFile2.fs
http http://www.fssnip.net/raw/34 myFile5.fs httpAuth

gist Thorium/1972308 gistfile1.fs
gist Thorium/6088882 

http http://www.fssnip.net/raw/1M myFile.fs
http http://www.fssnip.net/raw/15 myFile3.fs """ 

    let cfg = DependenciesFile.FromSource(config)
    
    let actual = 
        cfg.Groups.[Constants.MainDependencyGroup].RemoteFiles
        |> List.map trivialResolve
        |> LockFileSerializer.serializeSourceFiles
    actual |> shouldEqual (normalizeLineEndings expectedMultiple)


let expectedForStanfordNLPdotNET = """HTTP
  remote: http://www.frijters.net
    ikvmbin-8.0.5449.0.zip (/ikvmbin-8.0.5449.0.zip)
  remote: http://nlp.stanford.edu
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

    let cfg = DependenciesFile.FromSource(config)

    let references =
        cfg.Groups.[Constants.MainDependencyGroup].RemoteFiles
        |> List.map trivialResolve
    
    references.Length |> shouldEqual 6

    references.[5].Origin |> shouldEqual (Origin.HttpLink("http://nlp.stanford.edu"))
    references.[5].Commit |> shouldEqual "/software/stanford-segmenter-2014-10-26.zip"  // That's strange
    references.[5].Name |> shouldEqual "stanford-segmenter-2014-10-26.zip"  

    references
    |> LockFileSerializer.serializeSourceFiles
    |> shouldEqual (normalizeLineEndings expectedForStanfordNLPdotNET)

[<Test>]
let ``should parse and regenerate http Stanford.NLP.NET project``() =
    let lockFile = LockFileParser.Parse(toLines expectedForStanfordNLPdotNET) |> List.head
    
    lockFile.SourceFiles
    |> List.rev
    |> LockFileSerializer.serializeSourceFiles
    |> shouldEqual (normalizeLineEndings expectedForStanfordNLPdotNET)

[<Test>]
let ``should generate lock file with second group``() = 
    let expected = """NUGET
  remote: http://www.nuget.org/api/v2
    Castle.Windsor (2.1) - copy_content_to_output_dir: preserve_newest
    Castle.Windsor-log4net (3.3) - restriction: == net35
      Castle.Windsor (>= 2.0)
      log4net (>= 1.0)
    log (1.2)
    log4net (1.1) - copy_content_to_output_dir: never
      log (>= 1.0)
    Rx-Core (2.1) - content: none
    Rx-Main (2.0) - content: none, restriction: >= net40
      Rx-Core (>= 2.1)

GROUP Build
COPY-LOCAL: TRUE
COPY-CONTENT-TO-OUTPUT-DIR: ALWAYS
CONDITION: LEGACY
NUGET
  remote: http://www.nuget.org/api/v2
    FAKE (4.0)
"""
    let lockFile = LockFile.Parse("Test",toLines expected)
    lockFile.ToString() |> normalizeLineEndings |> shouldEqual (normalizeLineEndings expected)