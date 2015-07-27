module Paket.UpdateProcess.Test

open Paket
open Paket.Domain
open Paket.PackageSources
open Paket.PackageResolver
open Paket.TestHelpers
open NUnit.Framework
open FsUnit

let lockFileData = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Core (3.2.0)
    Castle.Core-log4net (3.2.0)
      Castle.Core (>= 3.2.0)
      log4net (1.2.10)
    FAKE (4.0.0)
    log4net (1.2.10)
"""

let graph = 
    [ "Castle.Core-log4net", "3.2.0", 
      [ "Castle.Core", VersionRequirement(VersionRange.AtLeast "3.2.0",PreReleaseStatus.No)
        "log4net", VersionRequirement(VersionRange.AtLeast "1.2.10",PreReleaseStatus.No) ]
      "Castle.Core-log4net", "3.3.3", 
      [ "Castle.Core", VersionRequirement(VersionRange.AtLeast "3.3.3",PreReleaseStatus.No)
        "log4net", VersionRequirement(VersionRange.AtLeast "1.2.10",PreReleaseStatus.No) ]
      "Castle.Core-log4net", "4.0.0", 
      [ "Castle.Core", VersionRequirement(VersionRange.AtLeast "4.0.0",PreReleaseStatus.No) ]
      "Castle.Core", "3.2.0", []
      "Castle.Core", "3.3.3", []
      "Castle.Core", "4.0.0", []
      "FAKE", "4.0.0", []
      "FAKE", "4.0.1", []
      "log4net", "1.2.10", [] ]

let getVersions = VersionsFromGraph graph
let getPackageDetails = PackageDetailsFromGraph graph

let lockFile = LockFile.Parse("",toLines lockFileData)
let resolve (dependenciesFile : DependenciesFile) packages = dependenciesFile.Resolve(noSha1, getVersions, getPackageDetails, packages)

[<Test>]
let ``SelectiveUpdate does not update any package when it is neither updating all nor selective updating``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget FAKE""")
    
    let updateAll = false    
    let lockFile = selectiveUpdate resolve lockFile dependenciesFile updateAll None
    
    let result = 
        lockFile.ResolvedPackages
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","3.2.0");
        ("Castle.Core","3.2.0");
        ("FAKE","4.0.0");
        ("log4net","1.2.10")]
        |> Seq.sortBy (fun (key,_) -> key)

    result
    |> Seq.sortBy (fun (key,_) -> key)
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate updates all packages not constraining version``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget FAKE""")

    let updateAll = true
    let lockFile = selectiveUpdate resolve lockFile dependenciesFile updateAll None
    
    let result = 
        lockFile.ResolvedPackages
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","3.3.3");
        ("Castle.Core","4.0.0");
        ("FAKE","4.0.1");
        ("log4net","1.2.10")]
        |> Seq.sortBy (fun (key,_) -> key)

    result
    |> Seq.sortBy (fun (key,_) -> key)
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate updates all packages constraining version``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net < 4.0
    nuget Castle.Core ~> 3.2
    nuget FAKE = 4.0.0""")

    let updateAll = true
    let lockFile = selectiveUpdate resolve lockFile dependenciesFile updateAll None
    
    let result = 
        lockFile.ResolvedPackages
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","3.3.3");
        ("Castle.Core","3.3.3");
        ("FAKE","4.0.0");
        ("log4net","1.2.10")]
        |> Seq.sortBy (fun (key,_) -> key)

    result
    |> Seq.sortBy (fun (key,_) -> key)
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate removes a dependency when it is updated to a version that does not depend on a library``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget FAKE""")

    let updateAll = true
    let lockFile = selectiveUpdate resolve lockFile dependenciesFile updateAll None
    
    let result = 
        lockFile.ResolvedPackages
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","4.0.0");
        ("Castle.Core","4.0.0");
        ("FAKE","4.0.1")]
        |> Seq.sortBy (fun (key,_) -> key)

    result
    |> Seq.sortBy (fun (key,_) -> key)
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate updates a single package``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget FAKE""")

    let updateAll = false
    let lockFile = 
        Some(NormalizedPackageName(PackageName "FAKE"))
        |> selectiveUpdate resolve lockFile dependenciesFile updateAll
    
    let result = 
        lockFile.ResolvedPackages
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","3.2.0");
        ("Castle.Core","3.2.0");
        ("FAKE","4.0.1");
        ("log4net","1.2.10")]
        |> Seq.sortBy (fun (key,_) -> key)

    result
    |> Seq.sortBy (fun (key,_) -> key)
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate updates a single constrained package``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget FAKE""")

    let updateAll = false
    let lockFile = 
        Some(NormalizedPackageName(PackageName "Castle.Core-log4net"))
        |> selectiveUpdate resolve lockFile dependenciesFile updateAll
    
    let result = 
        lockFile.ResolvedPackages
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","3.3.3");
        ("Castle.Core","4.0.0");
        ("FAKE","4.0.0");
        ("log4net","1.2.10")]
        |> Seq.sortBy (fun (key,_) -> key)

    result
    |> Seq.sortBy (fun (key,_) -> key)
    |> shouldEqual expected
     
[<Test>]
let ``SelectiveUpdate updates a single package with constrained dependency in dependencies file``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget Castle.Core ~> 3.2
    nuget FAKE""")

    let updateAll = false
    let lockFile = 
        Some(NormalizedPackageName(PackageName "Castle.Core-log4net"))
        |> selectiveUpdate resolve lockFile dependenciesFile updateAll
    
    let result = 
        lockFile.ResolvedPackages
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","3.3.3");
        ("Castle.Core","3.3.3");
        ("FAKE","4.0.0");
        ("log4net","1.2.10")]
        |> Seq.sortBy (fun (key,_) -> key)

    result
    |> Seq.sortBy (fun (key,_) -> key)
    |> shouldEqual expected
    