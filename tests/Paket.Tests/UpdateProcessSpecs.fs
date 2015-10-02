module Paket.UpdateProcess.Test

open Paket
open Paket.Domain
open Paket.PackageSources
open Paket.PackageResolver
open Paket.Requirements
open Paket.TestHelpers
open NUnit.Framework
open FsUnit
open System

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
        "log4net", VersionRequirement(VersionRange.Exactly "1.2.10",PreReleaseStatus.No) ]
      "Castle.Core-log4net", "3.3.3", 
      [ "Castle.Core", VersionRequirement(VersionRange.AtLeast "3.3.3",PreReleaseStatus.No)
        "log4net", VersionRequirement(VersionRange.Exactly "1.2.10",PreReleaseStatus.No) ]
      "Castle.Core-log4net", "4.0.0", 
      [ "Castle.Core", VersionRequirement(VersionRange.AtLeast "4.0.0",PreReleaseStatus.No) ]
      "Castle.Core", "3.2.0", []
      "Castle.Core", "3.3.3", []
      "Castle.Core", "4.0.0", []
      "FAKE", "4.0.0", []
      "FAKE", "4.0.1", []
      "log4net", "1.2.10", []
      "log4net", "2.0.0", []
      "Newtonsoft.Json", "7.0.1", []
      "Newtonsoft.Json", "6.0.8", [] ]

let getLockFile lockFileData = LockFile.Parse("",toLines lockFileData)
let lockFile = lockFileData |> getLockFile
let resolve' graph (dependenciesFile : DependenciesFile) groups = 
    dependenciesFile.Resolve(true,noSha1, VersionsFromGraph graph, PackageDetailsFromGraph graph, groups)

let resolve = resolve' graph

[<Test>]
let ``SelectiveUpdate does not update any package when it is neither updating all nor selective updating``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget FAKE""")
    
    let updateAll = false    
    let lockFile = selectiveUpdate resolve lockFile dependenciesFile updateAll None
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","3.2.0");
        ("Castle.Core","3.2.0");
        ("FAKE","4.0.0");
        ("log4net","1.2.10")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate updates all packages not constraining version``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget FAKE""")

    let updateAll = true
    let lockFile = selectiveUpdate resolve lockFile dependenciesFile updateAll None
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","3.3.3");
        ("Castle.Core","4.0.0");
        ("FAKE","4.0.1");
        ("log4net","1.2.10")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
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
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","3.3.3");
        ("Castle.Core","3.3.3");
        ("FAKE","4.0.0");
        ("log4net","1.2.10")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate removes a dependency when it is updated to a version that does not depend on a library``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget FAKE""")

    let updateAll = true
    let lockFile = selectiveUpdate resolve lockFile dependenciesFile updateAll None
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","4.0.0");
        ("Castle.Core","4.0.0");
        ("FAKE","4.0.1")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate updates a single package``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget FAKE""")

    let updateAll = false
    let lockFile = 
        Some(Constants.MainDependencyGroup, PackageName "FAKE")
        |> selectiveUpdate resolve lockFile dependenciesFile updateAll
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","3.2.0");
        ("Castle.Core","3.2.0");
        ("FAKE","4.0.1");
        ("log4net","1.2.10")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate updates a single constrained package``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget FAKE""")

    let updateAll = false
    let lockFile = 
        Some(Constants.MainDependencyGroup, PackageName "Castle.Core-log4net")
        |> selectiveUpdate resolve lockFile dependenciesFile updateAll
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","3.3.3");
        ("Castle.Core","4.0.0");
        ("FAKE","4.0.0");
        ("log4net","1.2.10")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
     
[<Test>]
let ``SelectiveUpdate updates a single package with constrained dependency in dependencies file``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget Castle.Core ~> 3.2
    nuget FAKE""")

    let updateAll = false
    let lockFile = 
        Some(Constants.MainDependencyGroup, PackageName "Castle.Core-log4net")
        |> selectiveUpdate resolve lockFile dependenciesFile updateAll
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","3.3.3");
        ("Castle.Core","3.3.3");
        ("FAKE","4.0.0");
        ("log4net","1.2.10")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate installs new packages``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget FAKE
    nuget Newtonsoft.Json""")

    let updateAll = false
    let lockFile = selectiveUpdate resolve lockFile dependenciesFile updateAll None
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","3.2.0");
        ("Castle.Core","3.2.0");
        ("FAKE","4.0.0");
        ("log4net", "1.2.10");
        ("Newtonsoft.Json", "7.0.1")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate removes a dependency when it updates a single package and it is updated to a version that does not depend on a library``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget FAKE""")

    let updateAll = false
    let lockFile = 
        Some(Constants.MainDependencyGroup, PackageName "Castle.Core-log4net")
        |> selectiveUpdate resolve lockFile dependenciesFile updateAll

    let result = 
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","4.0.0");
        ("Castle.Core","4.0.0");
        ("FAKE","4.0.0")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate does not update when a dependency constrain is not met``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget Castle.Core = 3.2.0
    nuget FAKE""")

    let updateAll = false
    let lockFile = 
        Some(Constants.MainDependencyGroup, PackageName "Castle.Core-log4net")
        |> selectiveUpdate resolve lockFile dependenciesFile updateAll

    let result = 
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","3.2.0");
        ("Castle.Core","3.2.0");
        ("FAKE","4.0.0");
        ("log4net","1.2.10")]
        |> Seq.sortBy fst
        
    result
    |> Seq.sortBy fst
    |> shouldEqual expected
   
[<Test>]
let ``SelectiveUpdate considers package name case difference``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget castle.core = 3.2.0
    nuget FAKE""")

    let updateAll = false
    let lockFile = 
        Some(Constants.MainDependencyGroup, PackageName "Castle.Core-log4net")
        |> selectiveUpdate resolve lockFile dependenciesFile updateAll

    let result = 
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","3.2.0");
        ("Castle.Core","3.2.0");
        ("FAKE","4.0.0");
        ("log4net","1.2.10")]
        |> Seq.sortBy fst
        
    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate conflicts when a dependency is contrained``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget Castle.Core = 3.2.0
    nuget log4net > 1.2.10
    nuget FAKE""")

    let updateAll = false

    (fun () ->
    Some(Constants.MainDependencyGroup, PackageName "Castle.Core-log4net")
    |> selectiveUpdate resolve lockFile dependenciesFile updateAll
    |> ignore)
    |> shouldFail

[<Test>]
let ``SelectiveUpdate does not update any package when package does not exist``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget FAKE""")

    let updateAll = false
    let lockFile = 
        Some(Constants.MainDependencyGroup, PackageName "package")
        |> selectiveUpdate resolve lockFile dependenciesFile updateAll
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","3.2.0");
        ("Castle.Core","3.2.0");
        ("FAKE","4.0.0");
        ("log4net","1.2.10")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
     
[<Test>]
let ``SelectiveUpdate generates paket.lock correctly``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget Castle.Core
    nuget FAKE""")

    let updateAll = false
    let lockFile = 
        Some(Constants.MainDependencyGroup, PackageName "Castle.Core")
        |> selectiveUpdate resolve lockFile dependenciesFile updateAll
    
    let result = 
            String.Join
                (Environment.NewLine,
                    LockFileSerializer.serializePackages InstallOptions.Default lockFile.Groups.[Constants.MainDependencyGroup].Resolution, 
                    LockFileSerializer.serializeSourceFiles lockFile.Groups.[Constants.MainDependencyGroup].RemoteFiles)


    let expected = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Core (4.0.0)
    Castle.Core-log4net (3.2.0)
      Castle.Core (>= 3.2.0)
      log4net (1.2.10)
    FAKE (4.0.0)
    log4net (1.2.10)
"""

    result
    |> shouldEqual (normalizeLineEndings expected)
     
[<Test>]
let ``SelectiveUpdate does not update when package conflicts with a transitive dependency``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget FAKE
    nuget log4net""")

    let updateAll = false
    let packageName = PackageName "log4net"
    let resolve = resolve' graph

    let lockFile = 
        Some(Constants.MainDependencyGroup, packageName)
        |> selectiveUpdate resolve lockFile dependenciesFile updateAll
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.map (fun r -> (string r.Name, string r.Version))

    let expected = 
        [("Castle.Core-log4net","3.2.0");
        ("Castle.Core","3.2.0");
        ("FAKE","4.0.0");
        ("log4net", "1.2.10")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected


let graph2 = 
    [ "Ninject", "2.2.1.4", []
      "Ninject", "2.2.1.5", []
      "Ninject", "2.3.1.4", []
      "Ninject", "3.2.0", []
      "Ninject.Extensions.Logging.Log4net", "2.2.0.4",
      [ "Ninject.Extensions.Logging", VersionRequirement(VersionRange.Between("2.2.0.0","2.3.0.0"),PreReleaseStatus.No)
        "log4net", VersionRequirement(VersionRange.AtLeast "1.0.4",PreReleaseStatus.No) ]
      "Ninject.Extensions.Logging.Log4net", "2.2.0.5",
      [ "Ninject.Extensions.Logging", VersionRequirement(VersionRange.Between("2.2.0.0","2.3.0.0"),PreReleaseStatus.No)
        "log4net", VersionRequirement(VersionRange.AtLeast "1.0.4",PreReleaseStatus.No) ]
      "Ninject.Extensions.Logging.Log4net", "3.2.3",
      [ "Ninject.Extensions.Logging", VersionRequirement(VersionRange.Between("3.2.0.0","3.3.0.0"),PreReleaseStatus.No)
        "log4net", VersionRequirement(VersionRange.AtLeast "1.2.11",PreReleaseStatus.No) ]
      "Ninject.Extensions.Logging", "2.2.0.4", [ "Ninject", VersionRequirement(VersionRange.Between("2.2.0.0","2.3.0.0"),PreReleaseStatus.No) ]
      "Ninject.Extensions.Logging", "2.2.0.5", [ "Ninject", VersionRequirement(VersionRange.Between("2.2.0.0","2.3.0.0"),PreReleaseStatus.No) ]
      "Ninject.Extensions.Logging", "3.2.3", [ "Ninject", VersionRequirement(VersionRange.Between("3.2.0.0","3.3.0.0"),PreReleaseStatus.No) ]
      "log4f", "0.4.0", [ "log4net", VersionRequirement(VersionRange.Between("1.2.10","2.0.0"),PreReleaseStatus.No) ]
      "log4f", "0.5.0", [ "log4net", VersionRequirement(VersionRange.AtLeast "1.2.10",PreReleaseStatus.No) ]
      "log4net", "1.0.4", []
      "log4net", "1.2.10", []
      "log4net", "1.2.11", []
      "log4net", "2.0.3", [] ]
      
let lockFileData2 = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    log4f (0.4.0)
      log4net (>= 1.2.10 < 2.0.0)
    log4net (1.0.4)
    Ninject (2.2.1.4)
    Ninject.Extensions.Logging (2.2.0.4)
      Ninject (>= 2.2.0.0 < 2.3.0.0)
    Ninject.Extensions.Logging.Log4net (2.2.0.4)
      Ninject.Extensions.Logging (>= 2.2.0.0 < 2.3.0.0)
      log4net (>= 1.0.4)
"""

let lockFile2 = lockFileData2 |> getLockFile

[<Test>]
let ``SelectiveUpdate updates package that conflicts with a transitive dependency with correct version``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget log4f
    nuget Ninject.Extensions.Logging.Log4net""")
    
    let updateAll = false
    let packageName = PackageName "log4f"
    let resolve = resolve' graph2

    let lockFile = 
        Some(Constants.MainDependencyGroup, packageName)
        |> selectiveUpdate resolve lockFile2 dependenciesFile updateAll
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.map (fun r -> (string r.Name, string r.Version))

    let expected = 
        [("Ninject.Extensions.Logging.Log4net","2.2.0.4");
        ("Ninject.Extensions.Logging","2.2.0.4");
        ("Ninject", "2.2.1.4");
        ("log4f", "0.5.0");
        ("log4net", "2.0.3")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate updates package that conflicts with a transitive dependency in its own graph with correct version``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget log4f
    nuget Ninject.Extensions.Logging.Log4net""")
    
    let updateAll = false
    let packageName = PackageName "Ninject.Extensions.Logging.Log4net"
    let resolve = resolve' graph2

    let lockFile = 
        Some(Constants.MainDependencyGroup, packageName)
        |> selectiveUpdate resolve lockFile2 dependenciesFile updateAll
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.map (fun r -> (string r.Name, string r.Version))

    let expected = 
        [("Ninject.Extensions.Logging.Log4net","3.2.3");
        ("Ninject.Extensions.Logging","3.2.3");
        ("Ninject", "3.2.0");
        ("log4f", "0.4.0");
        ("log4net", "1.2.11")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    

let graph3 = 
    graph2 @
    [ "Ninject.Extensions.Interception", "2.2.1.2", [ "Ninject", VersionRequirement(VersionRange.Between("2.2.0.0","2.3.0.0"),PreReleaseStatus.No) ]
      "Ninject.Extensions.Interception", "2.2.1.3", [ "Ninject", VersionRequirement(VersionRange.Between("2.2.0.0","2.3.0.0"),PreReleaseStatus.No) ]
      "Ninject.Extensions.Interception", "3.2.0", [ "Ninject", VersionRequirement(VersionRange.Between("3.2.0.0","3.3.0.0"),PreReleaseStatus.No) ] ]
      
let lockFileData3 = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    log4f (0.4.0)
      log4net (>= 1.2.10 < 2.0.0)
    log4net (1.0.4)
    Ninject (2.2.1.4)
    Ninject.Extensions.Logging (2.2.0.4)
      Ninject (>= 2.2.0.0 < 2.3.0.0)
    Ninject.Extensions.Logging.Log4net (2.2.0.4)
      Ninject.Extensions.Logging (>= 2.2.0.0 < 2.3.0.0)
      log4net (>= 1.0.4)
    Ninject.Extensions.Interception (2.2.1.2)
      Ninject (>= 2.2.0.0 < 2.3.0.0)
"""
let lockFile3 = lockFileData3 |> getLockFile

[<Test>]
let ``SelectiveUpdate updates package that conflicts with a transitive dependency of another package with correct version``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget log4f
    nuget Ninject.Extensions.Logging.Log4net
    nuget Ninject.Extensions.Interception""")
    
    let updateAll = false
    let packageName = PackageName "Ninject.Extensions.Logging.Log4net"
    let resolve = resolve' graph3

    let lockFile = 
        Some(Constants.MainDependencyGroup, packageName)
        |> selectiveUpdate resolve lockFile3 dependenciesFile updateAll
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.map (fun r -> (string r.Name, string r.Version))

    let expected = 
        [("Ninject.Extensions.Logging.Log4net","2.2.0.5");
        ("Ninject.Extensions.Logging","2.2.0.5");
        ("Ninject.Extensions.Interception","2.2.1.2");
        ("Ninject", "2.2.1.5");
        ("log4f", "0.4.0");
        ("log4net", "1.2.11")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate conflicts with a transitive dependency of another package when paket.dependencies requirement has changed``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Ninject ~> 3.0
    nuget Ninject.Extensions.Logging.Log4net
    nuget Ninject.Extensions.Interception""")
    
    let updateAll = false
    let packageName = PackageName "Ninject"
    let resolve = resolve' graph3

    (fun () ->
    Some(Constants.MainDependencyGroup, packageName)
    |> selectiveUpdate resolve lockFile3 dependenciesFile updateAll
    |> ignore)
    |> shouldFail
    
[<Test>]
let ``SelectiveUpdate updates package that conflicts with a deep transitive dependency of another package to correct version``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget log4f
    nuget Ninject.Extensions.Logging.Log4net
    nuget Ninject.Extensions.Interception""")
    
    let updateAll = false
    let packageName = PackageName "Ninject.Extensions.Interception"
    let resolve = resolve' graph3

    let lockFile = 
        Some(Constants.MainDependencyGroup, packageName)
        |> selectiveUpdate resolve lockFile3 dependenciesFile updateAll
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.map (fun r -> (string r.Name, string r.Version))

    let expected = 
        [("Ninject.Extensions.Logging.Log4net","2.2.0.4");
        ("Ninject.Extensions.Logging","2.2.0.4");
        ("Ninject.Extensions.Interception","2.2.1.3");
        ("Ninject", "2.2.1.5");
        ("log4f", "0.4.0");
        ("log4net", "1.0.4")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
let graph4 =
    graph2 @
      [ "Ninject.Extensions.Logging.Log4net.Deep", "2.2.0.4", [ "Ninject.Extensions.Logging.Log4net", VersionRequirement(VersionRange.Between("2.2.0.0","2.3.0.0"),PreReleaseStatus.No) ]
        "Ninject.Extensions.Logging.Log4net.Deep", "2.2.0.5", [ "Ninject.Extensions.Logging.Log4net", VersionRequirement(VersionRange.Between("2.2.0.0","2.3.0.0"),PreReleaseStatus.No) ]
        "Ninject.Extensions.Logging.Log4net.Deep", "3.2.3", [ "Ninject.Extensions.Logging.Log4net", VersionRequirement(VersionRange.Between("3.2.0.0","3.3.0.0"),PreReleaseStatus.No) ] ]

let lockFileData4 = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    log4net (1.0.4)
    Ninject (2.2.1.4)
    Ninject.Extensions.Logging (2.2.0.4)
      Ninject (>= 2.2.0.0 < 2.3.0.0)
    Ninject.Extensions.Logging.Log4net (2.2.0.4)
      Ninject.Extensions.Logging (>= 2.2.0.0 < 2.3.0.0)
      log4net (>= 1.0.4)
    Ninject.Extensions.Logging.Log4net.Deep (2.2.0.4)
      Ninject.Extensions.Logging.Log4net (2.2.0.4)
"""
let lockFile4 = lockFileData4 |> getLockFile

[<Test>]
let ``SelectiveUpdate updates package that conflicts with a deep transitive dependency in its own graph with correct version``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Ninject.Extensions.Logging.Log4net.Deep""")
    
    let updateAll = false
    let packageName = PackageName "Ninject.Extensions.Logging.Log4net.Deep"
    let resolve = resolve' graph4

    let lockFile = 
        Some(Constants.MainDependencyGroup, packageName)
        |> selectiveUpdate resolve lockFile4 dependenciesFile updateAll
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.map (fun r -> (string r.Name, string r.Version))

    let expected = 
        [("Ninject.Extensions.Logging.Log4net.Deep","3.2.3");
        ("Ninject.Extensions.Logging.Log4net","3.2.3");
        ("Ninject.Extensions.Logging","3.2.3");
        ("Ninject", "3.2.0");
        ("log4net", "2.0.3")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
let graph5 =
      [ "Ninject.Extensions.Interception", "0.0.2-alpha001", [ "Ninject", VersionRequirement(VersionRange.Between("0.0.1","0.0.3"),PreReleaseStatus.No) ]
        "Ninject.Extensions.Logging", "0.0.2-alpha001", [ "Ninject", VersionRequirement(VersionRange.Between("0.0.1","0.0.3"),PreReleaseStatus.No) ]
        "Ninject.Extensions.Logging", "0.0.3", [ "Ninject", VersionRequirement(VersionRange.Between("0.0.2","1.0.0"),PreReleaseStatus.No) ]
        "Ninject", "0.0.2-alpha001", []
        "Ninject", "0.0.3-alpha001", []
        "Ninject", "0.0.4-alpha001", [] ]

let lockFileData5 = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    Ninject (0.0.2-alpha001)
    Ninject.Extensions.Interception (0.0.2-alpha001)
      Ninject (>= 0.0.1 < 0.0.3)
    Ninject.Extensions.Logging (0.0.2-alpha001)
      Ninject (>= 0.0.1 < 0.0.3)
"""
let lockFile5 = lockFileData5 |> getLockFile

[<Test>]
let ``SelectiveUpdate updates package that conflicts with transitive dependency with correct prerelease version``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Ninject.Extensions.Interception
    nuget Ninject.Extensions.Logging""")
    
    let updateAll = false
    let packageName = PackageName "Ninject.Extensions.Logging"
    let resolve = resolve' graph5

    let lockFile = 
        Some(Constants.MainDependencyGroup, packageName)
        |> selectiveUpdate resolve lockFile5 dependenciesFile updateAll
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.map (fun r -> (string r.Name, string r.Version))

    let expected = 
        [("Ninject.Extensions.Interception","0.0.2-alpha001");
        ("Ninject.Extensions.Logging","0.0.3");
        ("Ninject", "0.0.3-alpha001")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected

let gfst (g, p, _) = sprintf "%s.%s" g p
let mainGroup = string Constants.MainDependencyGroup
let groupMap (lockFile : LockFile) =
    lockFile.GetGroupedResolution()
    |> Seq.map (fun (KeyValue ((g,_),resolved)) ->
        (string g,string resolved.Name, string resolved.Version))

[<Test>]
let ``SelectiveUpdate updates all packages from all groups if no group is specified``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget FAKE
    
    group Group
        source http://nuget.org/api/v2

        nuget Castle.Core-log4net ~> 4.0""")

    let updateAll = true
    let lockFile = selectiveUpdate resolve lockFile dependenciesFile updateAll None
    
    let result = groupMap lockFile

    let expected = 
        [("Group","Castle.Core-log4net","4.0.0");
        ("Group","Castle.Core","4.0.0");
        (mainGroup,"Castle.Core-log4net","3.3.3");
        (mainGroup,"Castle.Core","4.0.0");
        (mainGroup,"FAKE","4.0.1");
        (mainGroup,"log4net","1.2.10")]
        |> Seq.sortBy gfst

    result
    |> Seq.sortBy gfst
    |> shouldEqual expected
    
let lockFileData6 = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Core (3.2.0)
    Castle.Core-log4net (3.2.0)
      Castle.Core (>= 3.2.0)
      log4net (1.2.10)
    FAKE (4.0.1)
    log4net (1.2.10)

GROUP Group
NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Core (3.2.0)
    Castle.Core-log4net (3.2.0)
      Castle.Core (>= 3.2.0)
      log4net (1.2.10)
    FAKE (4.0.0)
    log4net (1.2.10)
"""
let lockFile6 = lockFileData6 |> getLockFile

[<Test>]
let ``SelectiveUpdate updates package from a specific group``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget FAKE
    
    group Group
        source http://nuget.org/api/v2

        nuget Castle.Core-log4net
        nuget FAKE""")

    let updateAll = false
    let lockFile =
        Some(GroupName "Group", PackageName "Castle.Core-log4net")
        |> selectiveUpdate resolve lockFile6 dependenciesFile updateAll
    
    let result = groupMap lockFile

    let expected = 
        [("Group","Castle.Core-log4net","4.0.0");
        ("Group","Castle.Core","4.0.0");
        ("Group","FAKE","4.0.0");
        (mainGroup,"Castle.Core-log4net","3.2.0");
        (mainGroup,"Castle.Core","3.2.0");
        (mainGroup,"FAKE","4.0.1");
        (mainGroup,"log4net","1.2.10")]
        |> Seq.sortBy gfst

    result
    |> Seq.sortBy gfst
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate does not remove a dependency from group when it is a top-level dependency in that group``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.0
    nuget FAKE
    
    group Group
        source http://nuget.org/api/v2

        nuget Castle.Core-log4net
        nuget FAKE
        nuget log4net""")

    let updateAll = false
    let lockFile =
        Some(GroupName "Group", PackageName "Castle.Core-log4net")
        |> selectiveUpdate resolve lockFile6 dependenciesFile updateAll
    
    let result = groupMap lockFile

    let expected = 
        [("Group","Castle.Core-log4net","4.0.0");
        ("Group","Castle.Core","4.0.0");
        ("Group","FAKE","4.0.0");
        ("Group","log4net","1.2.10");
        (mainGroup,"Castle.Core-log4net","3.2.0");
        (mainGroup,"Castle.Core","3.2.0");
        (mainGroup,"FAKE","4.0.1");
        (mainGroup,"log4net","1.2.10")]
        |> Seq.sortBy gfst

    result
    |> Seq.sortBy gfst
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate updates package from main group``() = 

    let dependenciesFile = DependenciesFile.FromCode("""source http://nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget FAKE
    
    group Group
        source http://nuget.org/api/v2

        nuget Castle.Core-log4net
        nuget FAKE""")

    let updateAll = false
    let lockFile =
        Some(Constants.MainDependencyGroup, PackageName "Castle.Core-log4net")
        |> selectiveUpdate resolve lockFile6 dependenciesFile updateAll
    
    let result = groupMap lockFile

    let expected = 
        [("Group","Castle.Core-log4net","3.2.0");
        ("Group","Castle.Core","3.2.0");
        ("Group","FAKE","4.0.0");
        ("Group","log4net","1.2.10");
        (mainGroup,"Castle.Core-log4net","3.3.3");
        (mainGroup,"Castle.Core","4.0.0");
        (mainGroup,"FAKE","4.0.1");
        (mainGroup,"log4net","1.2.10")]
        |> Seq.sortBy gfst

    result
    |> Seq.sortBy gfst
    |> shouldEqual expected
    