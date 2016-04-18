module Paket.LoadingScriptTests

open Paket.LoadingScripts
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
    LoadingScriptsGenerator.getPackageOrderResolvedPackage testData
    |> List.map (fun p -> p.Name)
    |> shouldEqual
        [ PackageName("other")
          PackageName("Test1") ]

[<Test>]
let ``can keep order simple dependency``() = 
    LoadingScriptsGenerator.getPackageOrderResolvedPackage (testData |> List.rev)
    |> List.map (fun p -> p.Name)
    |> shouldEqual
        [ PackageName("other")
          PackageName("Test1") ]