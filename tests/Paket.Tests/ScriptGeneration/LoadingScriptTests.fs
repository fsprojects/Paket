module Paket.LoadingScriptTests

open System.IO
open Paket.LoadingScripts
open Paket.LoadingScripts.ScriptGeneration
open NUnit.Framework
open FsUnit
open Paket
open Paket.Domain

let testData =
    [ { PackageResolver.ResolvedPackage.Name = PackageName("Test1")
        PackageResolver.ResolvedPackage.Version = SemVer.Parse "1.0.0"
        PackageResolver.ResolvedPackage.Dependencies =
          Set.empty
          |> Set.add(
              PackageName("other"), 
              VersionRequirement(VersionRange.Specific (SemVer.Parse "1.0.0"), PreReleaseStatus.No),
              Paket.Requirements.FrameworkRestrictions.AutoDetectFramework)
        PackageResolver.ResolvedPackage.Unlisted = false
        PackageResolver.ResolvedPackage.Settings = Requirements.InstallSettings.Default
        PackageResolver.ResolvedPackage.Source = PackageSources.PackageSource.NuGetV2 { Url = ""; Authentication = None } }
      { Name = PackageName("other")
        Version = SemVer.Parse "1.0.0"
        Dependencies = Set.empty
        Unlisted = false
        Settings = Requirements.InstallSettings.Default
        Source = PackageSources.PackageSource.NuGetV2 { Url = ""; Authentication = None } }
    ]
    
[<Test>]
let ``can re-order simple dependency``() = 
    PackageAndAssemblyResolution.getPackageOrderResolvedPackage testData
    |> List.map (fun p -> p.Name)
    |> shouldEqual
        [ PackageName("other")
          PackageName("Test1") ]

[<Test>]
let ``can keep order simple dependency``() = 
    PackageAndAssemblyResolution.getPackageOrderResolvedPackage (testData |> List.rev)
    |> List.map (fun p -> p.Name)
    |> shouldEqual
        [ PackageName("other")
          PackageName("Test1") ]

let scriptGenInputWithNoDendency = {
    PackageName                  = Paket.Domain.PackageName "foo"
    Framework                    = FrameworkIdentifier.DotNetFramework FrameworkVersion.V4
    PackagesOrGroupFolder        = "a" |> DirectoryInfo
    IncludeScriptsRootFolder     = "b" |> DirectoryInfo
    DependentScripts             = List.empty
    FrameworkReferences          = List.empty
    OrderedRelativeDllReferences = List.empty
}

[<Test>]
let ``generateFSharpScript returns DoNotGenerate given empty dependency set``() =
    let output = ScriptGeneration.generateFSharpScript scriptGenInputWithNoDendency

    match output with
    | Generate _ -> Assert.Fail()
    | DoNotGenerate -> ()

[<Test>]
let ``generateCSharpScript returns DoNotGenerate given empty dependency set``() =
    let output = ScriptGeneration.generateCSharpScript scriptGenInputWithNoDendency

    match output with
    | Generate _ -> Assert.Fail()
    | DoNotGenerate -> ()