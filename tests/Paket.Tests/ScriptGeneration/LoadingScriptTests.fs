[<NUnit.Framework.TestFixture>]
[<NUnit.Framework.Category "Script Generation">]
module Paket.LoadingScriptTests

open System.IO
open Pri.LongPath
open Paket.LoadingScripts
open Paket.LoadingScripts.ScriptGeneration
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket
open Paket.Domain

let testData =
    [ { PackageResolver.ResolvedPackage.Name = PackageName "Test1"
        PackageResolver.ResolvedPackage.Version = SemVer.Parse "1.0.0"
        PackageResolver.ResolvedPackage.Dependencies =
          Set.empty
          |> Set.add(
              PackageName "other", 
              VersionRequirement(VersionRange.Specific (SemVer.Parse "1.0.0"), PreReleaseStatus.No),
              Paket.Requirements.FrameworkRestrictions.AutoDetectFramework)
        PackageResolver.ResolvedPackage.Unlisted = false
        PackageResolver.ResolvedPackage.IsCliTool = false
        PackageResolver.ResolvedPackage.Settings = Requirements.InstallSettings.Default
        PackageResolver.ResolvedPackage.Source = PackageSources.PackageSource.NuGetV2 { Url = ""; Authentication = None }
        PackageResolver.IsRuntimeDependency = false }
      { Name = PackageName "other"
        Version = SemVer.Parse "1.0.0"
        Dependencies = Set.empty
        Unlisted = false
        Settings = Requirements.InstallSettings.Default
        IsCliTool = false
        Source = PackageSources.PackageSource.NuGetV2 { Url = ""; Authentication = None }
        IsRuntimeDependency = false }
    ]
    
//[<Test>]
//let ``can re-order simple dependency``() = 
//    PackageAndAssemblyResolution.getPackageOrderResolvedPackage testData
//    |> List.map (fun p -> p.Name)
//    |> shouldEqual
//        [ PackageName "other"
//          PackageName "Test1" ]

//[<Test>]
//let ``can keep order simple dependency``() = 
//    PackageAndAssemblyResolution.getPackageOrderResolvedPackage (testData |> List.rev)
//    |> List.map (fun p -> p.Name)
//    |> shouldEqual
//        [ PackageName "other"
//          PackageName "Test1" ]

let scriptGenInputWithNoDendency = {
    PackageName                  = Paket.Domain.PackageName "foo"
    IncludeScriptsRootFolder     = DirectoryInfo "b"
    DependentScripts             = List.empty
    FrameworkReferences          = List.empty
    OrderedDllReferences         = List.empty
}

[<Test>]
let ``generateFSharpScript returns DoNotGenerate given empty dependency set``() =
    let output = ScriptGeneration.generateScript ScriptType.FSharp scriptGenInputWithNoDendency

    match output with
    | Generate _ -> Assert.Fail("F# script with no dependency was supposed to return DoNotGenerate")
    | DoNotGenerate -> ()

[<Test>]
let ``generateCSharpScript returns DoNotGenerate given empty dependency set``() =
    let output = ScriptGeneration.generateScript ScriptType.CSharp scriptGenInputWithNoDendency

    match output with
    | Generate _ -> Assert.Fail("C# script with no dependency was supposed to return DoNotGenerate")
    | DoNotGenerate -> ()


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

