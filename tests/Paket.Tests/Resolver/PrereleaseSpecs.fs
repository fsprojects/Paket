module Paket.PrereleaseSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain

let graph = 
  OfSimpleGraph [
    "packageA","1.0.11250",["packageB", VersionRequirement(VersionRange.Between("1.0", "2.0"),PreReleaseStatus.No)]
    "packageB","1.0.11203",[]
    "packageB","1.0.11204-custom",[]
    "packageB","2.0.0",[]
  ]
  
let graphWithTransitiveDependencies = 
  OfSimpleGraph [
    "packageA","1.0.11250",["packageB", VersionRequirement(VersionRange.Between("1.0", "2.0"),PreReleaseStatus.No)]
    "packageB","1.0.11203",["packageC", VersionRequirement(VersionRange.Between("1.0", "2.0"),PreReleaseStatus.No)]
    "packageB","1.0.11204-custom",["packageC", VersionRequirement(VersionRange.Between("1.0", "2.0"),PreReleaseStatus.No)]
    "packageB","2.0.0",["packageC", VersionRequirement(VersionRange.Between("2.0", "3.0"),PreReleaseStatus.No)]
    "packageC","1.0.1",[]
    "packageC","1.0.2-pre",[]
  ]
  
let config1 = """
source "https://www.nuget.org/api/v2"

nuget PackageA
nuget PackageB
"""


[<Test>]
let ``should resolve config1``() = 
    let cfg = DependenciesFile.FromSource(config1)
    let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "packageA"] |> shouldEqual "1.0.11250"
    getVersion resolved.[PackageName "packageB"] |> shouldEqual "1.0.11203"

    
let config2 = """
source "https://www.nuget.org/api/v2"

nuget PackageA
nuget PackageB prerelease
"""

[<Test>]
let ``should resolve prerelease config2``() = 
    let cfg = DependenciesFile.FromSource(config2)
    let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "packageA"] |> shouldEqual "1.0.11250"
    getVersion resolved.[PackageName "packageB"] |> shouldEqual "1.0.11204-custom"
    
let config3 = """
source "https://www.nuget.org/api/v2"

nuget PackageA
nuget PackageB 1.0.11204-custom
"""

[<Test>]
let ``should resolve pinned config3``() = 
    let cfg = DependenciesFile.FromSource(config3)
    let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "packageA"] |> shouldEqual "1.0.11250"
    getVersion resolved.[PackageName "packageB"] |> shouldEqual "1.0.11204-custom"
    
let config4 = """
source "https://www.nuget.org/api/v2"

nuget PackageA
nuget PackageB == 2.0
"""

[<Test>]
let ``should resolve overwritten config4``() = 
    let cfg = DependenciesFile.FromSource(config4)
    let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "packageA"] |> shouldEqual "1.0.11250"
    getVersion resolved.[PackageName "packageB"] |> shouldEqual "2.0.0"

let config5 = """
source "https://www.nuget.org/api/v2"
nuget PackageA prerelease
nuget PackageB prerelease
"""

[<Test>]
let ``should resolve prerelease config5``() = 
    let cfg = DependenciesFile.FromSource(config5)
    let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "packageA"] |> shouldEqual "1.0.11250"
    getVersion resolved.[PackageName "packageB"] |> shouldEqual "1.0.11204-custom"
    
[<Test>]
let ``should resolve prerelease config2 but no prerelease for transitive deps``() = 
    let cfg = DependenciesFile.FromSource(config2)
    let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graphWithTransitiveDependencies, PackageDetailsFromGraph graphWithTransitiveDependencies).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "packageA"] |> shouldEqual "1.0.11250"
    getVersion resolved.[PackageName "packageB"] |> shouldEqual "1.0.11204-custom"
    getVersion resolved.[PackageName "packageC"] |> shouldEqual "1.0.1"


[<Test>]
let ``should resolve no prerelease``() =
    let graph =
      OfSimpleGraph [
        "packageA","1.0",["packageB", VersionRequirement(VersionRange.Between("1.0", "2.0"),PreReleaseStatus.No)]
        "packageA","1.1-alpha",["packageB", VersionRequirement(VersionRange.Between("1.0", "2.0"),PreReleaseStatus.No)]
        "packageB","1.0",[]
        "packageB","1.1-alpha",[]
      ]
    let config = """
source "https://www.nuget.org/api/v2"

nuget PackageA prerelease
"""
    let cfg = DependenciesFile.FromSource(config)
    let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "packageA"] |> shouldEqual "1.1-alpha"
    // We can resolve without prerelease
    getVersion resolved.[PackageName "packageB"] |> shouldEqual "1.0"



[<Test>]
let ``should resolve prerelease when required``() =
    let graph =
      OfSimpleGraph [
        "packageA","1.1-alpha",["packageB", VersionRequirement(VersionRange.AtLeast("1.1-alpha"),PreReleaseStatus.All)]
        "packageB","1.0",[]
        "packageB","1.1-alpha",[]
        "packageC","1.0",["packageB", VersionRequirement(VersionRange.Between("1.0", "2.0"),PreReleaseStatus.No)]
        "packageC","1.1-alpha",["packageB", VersionRequirement(VersionRange.Between("1.0", "2.0"),PreReleaseStatus.No)]
      ]
    let config = """
source "https://www.nuget.org/api/v2"

nuget PackageA prerelease
nuget PackageC prerelease
"""
    let cfg = DependenciesFile.FromSource(config)
    let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "packageA"] |> shouldEqual "1.1-alpha"
    // Required and allowed because of packageA
    getVersion resolved.[PackageName "packageB"] |> shouldEqual "1.1-alpha"
    getVersion resolved.[PackageName "packageC"] |> shouldEqual "1.1-alpha"

[<Test>]
let ``should resolve prerelease when forbidden``() =
    let graph =
      OfSimpleGraph [
        "packageA","1.1-alpha",["packageB", VersionRequirement(VersionRange.AtLeast("1.1-alpha"),PreReleaseStatus.All)]
        "packageB","1.0",[]
        "packageB","1.1-alpha",[]
        "packageC","1.0",["packageB", VersionRequirement(VersionRange.Between("1.0", "2.0"),PreReleaseStatus.No)]
        "packageC","1.1-alpha",["packageB", VersionRequirement(VersionRange.Between("1.0", "2.0"),PreReleaseStatus.No)]
      ]
    let config = """
source "https://www.nuget.org/api/v2"

nuget PackageA prerelease
nuget PackageC 
"""
    try
        let cfg = DependenciesFile.FromSource(config)
        let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
        Assert.Fail (sprintf "Expected resolution to fail but got %A" resolved)
    with
    | :? NUnit.Framework.AssertionException -> reraise()
    | :? System.AggregateException as agg -> ()




[<Test>]
let ``should prefer stable when possible``() =
    let graph =
      OfSimpleGraph [
        "packageA","1.1-alpha",["packageB", VersionRequirement(VersionRange.AtLeast("1.1-alpha"),PreReleaseStatus.All)]
        "packageB","1.0",[]
        "packageB","1.1-alpha",[]
        "packageB","1.1",[]
        "packageB","1.2-alpha",[]
        "packageC","1.0",["packageB", VersionRequirement(VersionRange.Between("1.0", "2.0"),PreReleaseStatus.No)]
        "packageC","1.1-alpha",["packageB", VersionRequirement(VersionRange.Between("1.0", "2.0"),PreReleaseStatus.No)]
      ]

    let config = """
source "https://www.nuget.org/api/v2"

nuget PackageA prerelease
nuget PackageC
"""
    let cfg = DependenciesFile.FromSource(config)
    let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "packageA"] |> shouldEqual "1.1-alpha"
    // Stable is prefered
    getVersion resolved.[PackageName "packageB"] |> shouldEqual "1.1"
    getVersion resolved.[PackageName "packageC"] |> shouldEqual "1.0"

[<Test>]
let ``should prefer stable when possible (2)``() =
    let graph =
      OfSimpleGraph [
        "packageA","1.1-alpha",
            ["packageB", VersionRequirement(VersionRange.AtLeast("1.1-alpha"),PreReleaseStatus.All)
             "packageC", VersionRequirement(VersionRange.Between("1.0", "2.0"),PreReleaseStatus.No)]
        "packageB","1.2-alpha",[]
        "packageB","1.1",[]
        "packageB","1.1-alpha",[]
        "packageB","1.0",[]
        "packageC","1.1-alpha",["packageB", VersionRequirement(VersionRange.Between("1.0", "2.0"),PreReleaseStatus.No)]
        "packageC","1.0",["packageB", VersionRequirement(VersionRange.Between("1.0", "2.0"),PreReleaseStatus.No)]
      ]

    let config = """
source "https://www.nuget.org/api/v2"

nuget PackageA prerelease
"""
    let cfg = DependenciesFile.FromSource(config)
    let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "packageA"] |> shouldEqual "1.1-alpha"
    // Stable is prefered
    getVersion resolved.[PackageName "packageB"] |> shouldEqual "1.1"
    getVersion resolved.[PackageName "packageC"] |> shouldEqual "1.0"


[<Test>]
let ``should take alpha if stable is not a valid resolution``() =
    let graph =
      OfSimpleGraph [
        "packageA","1.1-alpha",["packageB", VersionRequirement(VersionRange.AtLeast("1.1-alpha"),PreReleaseStatus.All)]
        "packageB","1.0",[]
        "packageB","1.1-alpha",["packageC", VersionRequirement(VersionRange.Between("1.0", "2.0"),PreReleaseStatus.All)]
        "packageB","1.1",["packageC", VersionRequirement(VersionRange.Between("1.1", "2.0"),PreReleaseStatus.No)]
        "packageC","1.0",[]
      ]

    let config = """
source "https://www.nuget.org/api/v2"

nuget PackageA prerelease
"""
    let cfg = DependenciesFile.FromSource(config)
    let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "packageA"] |> shouldEqual "1.1-alpha"
    // we need to take the alpha because there is no C in 1.1
    getVersion resolved.[PackageName "packageB"] |> shouldEqual "1.1-alpha"
    getVersion resolved.[PackageName "packageC"] |> shouldEqual "1.0"

[<Test>]
let ``should fail resolution when only prerelease is available in transitives``() =
    let graph =
      OfSimpleGraph [
        "packageA","1.1-alpha",["packageB", VersionRequirement(VersionRange.AtLeast("1.1-alpha"),PreReleaseStatus.All)]
        "packageB","1.0",[]
        "packageB","1.1",["packageC", VersionRequirement(VersionRange.Between("1.1", "2.0"),PreReleaseStatus.No)]
        "packageC","1.0",[]
        "packageC","1.1-alpha",[]
      ]

    let config = """
source "https://www.nuget.org/api/v2"

nuget PackageA prerelease
"""
    let cfg = DependenciesFile.FromSource(config)
    try
        let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()        
        Assert.Fail (sprintf "Expected resolution to fail but got %A" resolved)
    with
    | :? NUnit.Framework.AssertionException -> reraise()
    | :? System.AggregateException as agg -> ()

[<Test>]
let ``should not take prerelease when they have no resolution``() =
    let graph =
      OfSimpleGraph [
        "packageA","1.0",["packageB", VersionRequirement(VersionRange.AtLeast("1.0"),PreReleaseStatus.No)]
        "packageA","1.1-alpha",["packageB", VersionRequirement(VersionRange.AtLeast("1.1-alpha"),PreReleaseStatus.All)]
        "packageB","1.0",[]
        "packageB","1.1",["packageC", VersionRequirement(VersionRange.Between("1.1", "2.0"),PreReleaseStatus.No)]
        "packageC","1.1-alpha",[]
        "packageC","1.0",[]
      ]

    let config = """
source "https://www.nuget.org/api/v2"

nuget PackageA prerelease
"""
    let cfg = DependenciesFile.FromSource(config)
    let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "packageA"] |> shouldEqual "1.0"
    // we need to take the stable as the alpha package has no resolution
    getVersion resolved.[PackageName "packageB"] |> shouldEqual "1.0"
