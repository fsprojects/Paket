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
    getVersion resolved.[PackageName "packageB"] |> shouldEqual "2.0"

    
[<Test>]
let ``should resolve prerelease config2 but no prerelease for transitive deps``() = 
    let cfg = DependenciesFile.FromSource(config2)
    let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graphWithTransitiveDependencies, PackageDetailsFromGraph graphWithTransitiveDependencies).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "packageA"] |> shouldEqual "1.0.11250"
    getVersion resolved.[PackageName "packageB"] |> shouldEqual "1.0.11204-custom"
    getVersion resolved.[PackageName "packageC"] |> shouldEqual "1.0.1"
