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
  remote: http://www.nuget.org/api/v2
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
      [ "Castle.Core", VersionRequirement(VersionRange.AtLeast "4.0.0",PreReleaseStatus.No) 
        "log4net", VersionRequirement(VersionRange.Exactly "1.2.10",PreReleaseStatus.No) ]
      "Castle.Core", "3.2.0", []
      "Castle.Core", "3.3.3", []
      "Castle.Core", "4.0.0", []
      "FAKE", "4.0.0", []
      "FAKE", "4.0.1", []
      "log4net", "1.2.10", []
      "log4net", "2.0.0", []
      "Newtonsoft.Json", "7.0.1", []
      "Newtonsoft.Json", "6.0.8", [] ]
    |> OfSimpleGraph

let getLockFile lockFileData = LockFile.Parse("",toLines lockFileData)
let lockFile = lockFileData |> getLockFile

let selectiveUpdateFromGraph graph force lockFile depsFile updateMode restriction =
    selectiveUpdate force noSha1 (VersionsFromGraph graph) (PackageDetailsFromGraph graph) (GetRuntimeGraphFromGraph graph) lockFile depsFile updateMode restriction

[<Test>]
let ``SelectiveUpdate does not update any package when it is neither updating all nor selective updating``() = 

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget FAKE""")
    
    let lockFile,_ = selectiveUpdateFromGraph graph true lockFile dependenciesFile PackageResolver.UpdateMode.Install SemVerUpdateMode.NoRestriction
    
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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget FAKE""")

    let lockFile,_ = selectiveUpdateFromGraph graph true lockFile dependenciesFile PackageResolver.UpdateMode.UpdateAll SemVerUpdateMode.NoRestriction
    
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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net < 4.0
    nuget Castle.Core ~> 3.2
    nuget FAKE = 4.0.0""")

    let lockFile,_ = selectiveUpdateFromGraph graph true lockFile dependenciesFile PackageResolver.UpdateMode.UpdateAll SemVerUpdateMode.NoRestriction
    
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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget FAKE""")

    let lockFile,_ = selectiveUpdateFromGraph graph true lockFile dependenciesFile PackageResolver.UpdateMode.UpdateAll SemVerUpdateMode.NoRestriction
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","4.0.0");
        ("Castle.Core","4.0.0");
        ("FAKE","4.0.1");
        ("log4net","1.2.10")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate updates a single package``() = 

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget FAKE""")

    let lockFile,_ = 
        selectiveUpdateFromGraph graph true lockFile dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, PackageName "FAKE" |> PackageFilter.ofName)) SemVerUpdateMode.NoRestriction

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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget FAKE""")

    let lockFile,_ = 
        selectiveUpdateFromGraph graph true lockFile dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, PackageName "Castle.Core-log4net" |> PackageFilter.ofName)) SemVerUpdateMode.NoRestriction

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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget Castle.Core ~> 3.2
    nuget FAKE""")

    let lockFile,_ = 
        selectiveUpdateFromGraph graph true lockFile dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, PackageName "Castle.Core-log4net" |> PackageFilter.ofName)) SemVerUpdateMode.NoRestriction

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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget FAKE
    nuget Newtonsoft.Json""")

    let lockFile,_ = selectiveUpdateFromGraph graph true lockFile dependenciesFile PackageResolver.UpdateMode.Install SemVerUpdateMode.NoRestriction
    
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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget FAKE""")

    let lockFile,_ = 
        selectiveUpdateFromGraph graph true lockFile dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, PackageName "Castle.Core-log4net" |> PackageFilter.ofName)) SemVerUpdateMode.NoRestriction

    let result = 
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version))

    let expected = 
        [("Castle.Core-log4net","4.0.0");
        ("Castle.Core","4.0.0");
        ("FAKE","4.0.0");
        ("log4net","1.2.10")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate does not update when a dependency constrain is not met``() = 

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget Castle.Core = 3.2.0
    nuget FAKE""")

    let lockFile,_ = 
        selectiveUpdateFromGraph graph true lockFile dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, PackageName "Castle.Core-log4net" |> PackageFilter.ofName)) SemVerUpdateMode.NoRestriction
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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget castle.core = 3.2.0
    nuget FAKE""")

    let lockFile,_ = 
        selectiveUpdateFromGraph graph true lockFile dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, PackageName "Castle.Core-log4net" |> PackageFilter.ofName)) SemVerUpdateMode.NoRestriction

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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget Castle.Core = 3.2.0
    nuget log4net > 1.2.10
    nuget FAKE""")

    (fun () ->
    selectiveUpdateFromGraph graph true lockFile dependenciesFile
        (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, PackageName "Castle.Core-log4net" |> PackageFilter.ofName)) SemVerUpdateMode.NoRestriction
    |> ignore)
    |> shouldFail

[<Test>]
let ``SelectiveUpdate generates paket.lock correctly``() = 

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget Castle.Core
    nuget FAKE""")

    let lockFile,_ = 
        selectiveUpdateFromGraph graph true lockFile dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, PackageName "Castle.Core" |> PackageFilter.ofName)) SemVerUpdateMode.NoRestriction
    
    let result = 
            String.Join
                (Environment.NewLine,
                    LockFileSerializer.serializePackages InstallOptions.Default lockFile.Groups.[Constants.MainDependencyGroup].Resolution, 
                    LockFileSerializer.serializeSourceFiles lockFile.Groups.[Constants.MainDependencyGroup].RemoteFiles)


    let expected = """NUGET
  remote: http://www.nuget.org/api/v2
    Castle.Core (4.0)
    Castle.Core-log4net (3.2)
      Castle.Core (>= 3.2)
      log4net (1.2.10)
    FAKE (4.0)
    log4net (1.2.10)
"""

    result
    |> shouldEqual (normalizeLineEndings expected)    

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
    |> OfSimpleGraph
      
let lockFileData2 = """NUGET
  remote: http://www.nuget.org/api/v2
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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget log4f
    nuget Ninject.Extensions.Logging.Log4net""")
    
    let packageFilter = PackageName "log4f" |> PackageFilter.ofName

    let lockFile,_ = 
        selectiveUpdateFromGraph graph2 true lockFile2 dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, packageFilter)) SemVerUpdateMode.NoRestriction
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Map.toSeq
        |> Seq.map (fun (_,r) -> (string r.Name, string r.Version))

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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget log4f
    nuget Ninject.Extensions.Logging.Log4net""")
    
    let packageFilter = PackageName "Ninject.Extensions.Logging.Log4net" |> PackageFilter.ofName

    let lockFile,_ = 
        selectiveUpdateFromGraph graph2 true lockFile2 dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, packageFilter)) SemVerUpdateMode.NoRestriction
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Map.toSeq
        |> Seq.map (fun (_,r) -> (string r.Name, string r.Version))

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
    ([ "Ninject.Extensions.Interception", "2.2.1.2", [ "Ninject", VersionRequirement(VersionRange.Between("2.2.0.0","2.3.0.0"),PreReleaseStatus.No) ]
       "Ninject.Extensions.Interception", "2.2.1.3", [ "Ninject", VersionRequirement(VersionRange.Between("2.2.0.0","2.3.0.0"),PreReleaseStatus.No) ]
       "Ninject.Extensions.Interception", "3.2.0", [ "Ninject", VersionRequirement(VersionRange.Between("3.2.0.0","3.3.0.0"),PreReleaseStatus.No) ] ]
     |> OfSimpleGraph)

      
let lockFileData3 = """NUGET
  remote: http://www.nuget.org/api/v2
  specs:
    log4f (0.4.0)
      log4net (>= 1.2.10 < 2.0.0)
    log4net (1.2.10)
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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget log4f
    nuget Ninject.Extensions.Logging.Log4net
    nuget Ninject.Extensions.Interception""")
    
    let packageFilter = PackageName "Ninject.Extensions.Logging.Log4net" |> PackageFilter.ofName

    let lockFile,_ = 
        selectiveUpdateFromGraph graph3 true lockFile3 dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, packageFilter)) SemVerUpdateMode.NoRestriction
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Map.toSeq
        |> Seq.map (fun (_,r) -> (string r.Name, string r.Version))

    let expected = 
        [("Ninject.Extensions.Logging.Log4net","3.2.3")
         ("Ninject.Extensions.Logging","3.2.3")
         ("Ninject.Extensions.Interception","3.2.0")
         ("Ninject", "3.2.0")
         ("log4f", "0.4.0")
         ("log4net", "1.2.11")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate does not conflict with a transitive dependency of another package when paket.dependencies requirement has changed``() = 

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Ninject ~> 3.0
    nuget Ninject.Extensions.Logging.Log4net
    nuget Ninject.Extensions.Interception""")
    
    let packageFilter = PackageName "Ninject" |> PackageFilter.ofName

    let lockFile,_ = 
        selectiveUpdateFromGraph graph3 true lockFile3 dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, packageFilter)) SemVerUpdateMode.NoRestriction

    let result = 
        lockFile.GetGroupedResolution()
        |> Map.toSeq
        |> Seq.map (fun (_,r) -> (string r.Name, string r.Version))

    let expected = 
        [("Ninject.Extensions.Logging.Log4net","3.2.3");
        ("Ninject.Extensions.Logging","3.2.3");
        ("Ninject.Extensions.Interception","3.2.0");
        ("Ninject", "3.2.0");
        ("log4net", "2.0.3")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
[<Test>]
let ``SelectiveUpdate updates package that conflicts with a deep transitive dependency of another package to correct version``() = 

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget log4f
    nuget Ninject.Extensions.Logging.Log4net
    nuget Ninject.Extensions.Interception""")
    
    let packageFilter = PackageName "Ninject.Extensions.Interception" |> PackageFilter.ofName

    let lockFile,_ = 
        selectiveUpdateFromGraph graph3 true lockFile3 dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, packageFilter)) SemVerUpdateMode.NoRestriction
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Map.toSeq
        |> Seq.map (fun (_,r) -> (string r.Name, string r.Version))

    let expected = 
        [("Ninject.Extensions.Logging.Log4net","3.2.3");
        ("Ninject.Extensions.Logging","3.2.3");
        ("Ninject.Extensions.Interception","3.2.0");
        ("Ninject", "3.2.0");
        ("log4f", "0.4.0");
        ("log4net", "1.2.11")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected
    
let graph4 =
    graph2 @
     ([ "Ninject.Extensions.Logging.Log4net.Deep", "2.2.0.4", [ "Ninject.Extensions.Logging.Log4net", VersionRequirement(VersionRange.Between("2.2.0.0","2.3.0.0"),PreReleaseStatus.No) ]
        "Ninject.Extensions.Logging.Log4net.Deep", "2.2.0.5", [ "Ninject.Extensions.Logging.Log4net", VersionRequirement(VersionRange.Between("2.2.0.0","2.3.0.0"),PreReleaseStatus.No) ]
        "Ninject.Extensions.Logging.Log4net.Deep", "3.2.3", [ "Ninject.Extensions.Logging.Log4net", VersionRequirement(VersionRange.Between("3.2.0.0","3.3.0.0"),PreReleaseStatus.No) ] ]
      |> OfSimpleGraph)

let lockFileData4 = """NUGET
  remote: http://www.nuget.org/api/v2
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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Ninject.Extensions.Logging.Log4net.Deep""")
    
    let packageFilter = PackageName "Ninject.Extensions.Logging.Log4net.Deep" |> PackageFilter.ofName

    let lockFile,_ = 
        selectiveUpdateFromGraph graph4 true lockFile4 dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, packageFilter)) SemVerUpdateMode.NoRestriction
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Map.toSeq
        |> Seq.map (fun (_,r) -> (string r.Name, string r.Version))

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
      |> OfSimpleGraph

let lockFileData5 = """NUGET
  remote: http://www.nuget.org/api/v2
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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Ninject.Extensions.Interception
    nuget Ninject.Extensions.Logging""")
    
    let packageFilter = PackageName "Ninject.Extensions.Logging" |> PackageFilter.ofName

    let lockFile,_ = 
        selectiveUpdateFromGraph graph5 true lockFile5 dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, packageFilter)) SemVerUpdateMode.NoRestriction
    
    let result = 
        lockFile.GetGroupedResolution()
        |> Map.toSeq
        |> Seq.map (fun (_,r) -> (string r.Name, string r.Version))

    let expected = 
        [("Ninject.Extensions.Interception","0.0.2-alpha001");
        ("Ninject.Extensions.Logging","0.0.3");
        ("Ninject", "0.0.2-alpha001")]
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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget FAKE
    
    group Group
        source http://www.nuget.org/api/v2

        nuget Castle.Core-log4net ~> 4.0""")

    let lockFile,_ = selectiveUpdateFromGraph graph true lockFile dependenciesFile PackageResolver.UpdateMode.UpdateAll SemVerUpdateMode.NoRestriction
    
    let result = groupMap lockFile

    let expected = 
        [("Group","Castle.Core-log4net","4.0.0");
        ("Group","Castle.Core","4.0.0");
        ("Group","log4net","1.2.10");
        (mainGroup,"Castle.Core-log4net","3.3.3");
        (mainGroup,"Castle.Core","4.0.0");
        (mainGroup,"FAKE","4.0.1");
        (mainGroup,"log4net","1.2.10")]
        |> Seq.sortBy gfst

    result
    |> Seq.sortBy gfst
    |> shouldEqual expected

let groupedLockFileData = """NUGET
  remote: http://www.nuget.org/api/v2
  specs:
    Castle.Core (3.2.0)
    Castle.Core-log4net (3.2.0)
      Castle.Core (>= 3.2.0)
      log4net (1.2.10)
    FAKE (4.0.1)
    log4net (1.2.10)

GROUP Group
NUGET
  remote: http://www.nuget.org/api/v2
  specs:
    Castle.Core (3.2.0)
    Castle.Core-log4net (3.2.0)
      Castle.Core (>= 3.2.0)"""

let groupedLockFile = groupedLockFileData |> getLockFile

[<Test>]
let ``SelectiveUpdate updates only packages from specific group if group is specified``() = 

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget FAKE
    
    group Group
        source http://www.nuget.org/api/v2

        nuget Castle.Core-log4net""")

    let lockFile,_ = selectiveUpdateFromGraph graph true groupedLockFile dependenciesFile (PackageResolver.UpdateMode.UpdateGroup Constants.MainDependencyGroup) SemVerUpdateMode.NoRestriction
    
    let result = groupMap lockFile |> Seq.toList

    let expected = 
        [("Group","Castle.Core-log4net","3.2.0");
        ("Group","Castle.Core","3.2.0");
        (mainGroup,"Castle.Core-log4net","4.0.0");
        (mainGroup,"Castle.Core","4.0.0");
        (mainGroup,"FAKE","4.0.1");
        (mainGroup,"log4net","1.2.10")]
        |> Seq.sortBy gfst
        |> Seq.toList

    result
    |> Seq.sortBy gfst
    |> Seq.toList
    |> shouldEqual expected

[<Test>]
let ``SelectiveUpdate updates only packages from specified group``() = 

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net
    nuget FAKE
    
    group Group
        source http://www.nuget.org/api/v2

        nuget Castle.Core-log4net""")

    let lockFile,_ = selectiveUpdateFromGraph graph true groupedLockFile dependenciesFile (PackageResolver.UpdateMode.UpdateGroup(GroupName "Group")) SemVerUpdateMode.NoRestriction
    
    let result = groupMap lockFile

    let expected = 
        [("Group","Castle.Core-log4net","4.0.0");
        ("Group","Castle.Core","4.0.0");
        ("Group","log4net","1.2.10");
        (mainGroup,"Castle.Core-log4net","3.2.0");
        (mainGroup,"Castle.Core","3.2.0");
        (mainGroup,"FAKE","4.0.1");
        (mainGroup,"log4net","1.2.10")]
        |> Seq.sortBy gfst

    result
    |> Seq.sortBy gfst
    |> shouldEqual expected
    
let lockFileData6 = """NUGET
  remote: http://www.nuget.org/api/v2
  specs:
    Castle.Core (3.2.0)
    Castle.Core-log4net (3.2.0)
      Castle.Core (>= 3.2.0)
      log4net (1.2.10)
    FAKE (4.0.1)
    log4net (1.2.10)

GROUP Group
NUGET
  remote: http://www.nuget.org/api/v2
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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget FAKE
    
    group Group
        source http://www.nuget.org/api/v2

        nuget Castle.Core-log4net
        nuget FAKE""")

    let lockFile,_ =
        selectiveUpdateFromGraph graph true lockFile6 dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(GroupName "Group", PackageName "Castle.Core-log4net" |> PackageFilter.ofName)) SemVerUpdateMode.NoRestriction
    
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
let ``SelectiveUpdate does not remove a dependency from group when it is a top-level dependency in that group``() = 

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.0
    nuget FAKE
    
    group Group
        source http://www.nuget.org/api/v2

        nuget Castle.Core-log4net
        nuget FAKE
        nuget log4net""")

    let lockFile,_ =
        selectiveUpdateFromGraph graph true lockFile6 dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(GroupName "Group", PackageName "Castle.Core-log4net" |> PackageFilter.ofName)) SemVerUpdateMode.NoRestriction
    
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

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget FAKE
    
    group Group
        source http://www.nuget.org/api/v2

        nuget Castle.Core-log4net
        nuget FAKE""")

    let lockFile,_ =
        selectiveUpdateFromGraph graph true lockFile6 dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, PackageName "Castle.Core-log4net" |> PackageFilter.ofName)) SemVerUpdateMode.NoRestriction
    
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
    
let lockFileData7 = """NUGET
  remote: http://www.nuget.org/api/v2
  specs:
    Newtonsoft.Json (6.0.8)
    Package (3.2.0)
"""
let lockFile7 = lockFileData7 |> getLockFile

let graph7 = 
    [ "Package", "3.2.0", []
      "Package", "4.0.0", ["Newtonsoft.Json", VersionRequirement(VersionRange.AtLeast "7.0.0",PreReleaseStatus.No)]
      "APackage", "3.2.0", []
      "APackage", "4.0.0", ["Newtonsoft.Json", VersionRequirement(VersionRange.AtLeast "7.0.0",PreReleaseStatus.No)]
      "Newtonsoft.Json", "7.0.1", []
      "Newtonsoft.Json", "6.0.8", [] ]
    |> OfSimpleGraph

[<Test>]
let ``SelectiveUpdate updates package that has a new dependent package that also is a direct dependency``() = 

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Newtonsoft.Json
    nuget Package""")

    let lockFile,_ = 
        selectiveUpdateFromGraph graph7 true lockFile7 dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, PackageName "Package" |> PackageFilter.ofName)) SemVerUpdateMode.NoRestriction

    let result = 
        lockFile.GetGroupedResolution()
        |> Map.toSeq
        |> Seq.map (fun (_,r) -> (string r.Name, string r.Version))

    let expected = 
        [("Package","4.0.0");
        ("Newtonsoft.Json","7.0.1")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected

let lockFileData8 = """NUGET
  remote: http://www.nuget.org/api/v2
  specs:
    APackage (3.2.0)
    Newtonsoft.Json (6.0.8)
"""
let lockFile8 = lockFileData8 |> getLockFile

[<Test>]
let ``SelectiveUpdate updates early package that has a new dependent package that also is a direct dependency``() = 

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Newtonsoft.Json
    nuget APackage""")

    let lockFile,_ = 
        selectiveUpdateFromGraph graph7 true lockFile8 dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, PackageName "APackage" |> PackageFilter.ofName)) SemVerUpdateMode.NoRestriction

    let result = 
        lockFile.GetGroupedResolution()
        |> Map.toSeq
        |> Seq.map (fun (_,r) -> (string r.Name, string r.Version))

    let expected = 
        [("APackage","4.0.0");
        ("Newtonsoft.Json","7.0.1")]
        |> Seq.sortBy fst

    result
    |> Seq.sortBy fst
    |> shouldEqual expected

[<Test>]
let ``SelectiveUpdate with SemVerUpdateMode.Minor updates package from a specific group in minor version``() = 

    let dependenciesFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.2
    nuget FAKE
    
    group Group
        source http://www.nuget.org/api/v2

        nuget Castle.Core-log4net
        nuget FAKE""")

    let lockFile,_ =
        selectiveUpdateFromGraph graph true lockFile6 dependenciesFile
            (PackageResolver.UpdateMode.UpdateFiltered(GroupName "Group", PackageName "Castle.Core-log4net" |> PackageFilter.ofName)) SemVerUpdateMode.KeepMinor
    
    let result = groupMap lockFile

    let expected = 
        [("Group","Castle.Core-log4net","3.2.0");
        ("Group","Castle.Core","3.2.0");
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
let ``adding new group to lockfile should not crash``() =
    let update deps lock = selectiveUpdateFromGraph graph true lock deps UpdateMode.Install SemVerUpdateMode.NoRestriction
    
    let initialDepsText = 
        """source http://www.nuget.org/api/v2
        nuget Castle.Core-log4net ~> 3.2"""
    let emptyLock = LockFile.Parse("test", [||])

    let addGroupDepsText = 
        initialDepsText + """
        group build
            source http://www.nuget.org/api/v2
            nuget FAKE"""
    let deps = DependenciesFile.FromSource(initialDepsText)
    
    let installlock,_ = update deps emptyLock
    installlock.Groups.Count |> shouldEqual 1

    let deps' = DependenciesFile.FromSource(addGroupDepsText)
    let updatelock,_ = update deps' installlock
    
    updatelock.Groups.Count |> shouldEqual 2

    let group = updatelock.Groups.TryFind (GroupName "build")
    group |> shouldNotEqual None
    group.Value.Resolution.ContainsKey (PackageName "FAKE") |> shouldEqual true
    
