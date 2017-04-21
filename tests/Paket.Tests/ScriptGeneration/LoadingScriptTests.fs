[<NUnit.Framework.TestFixture>]
[<NUnit.Framework.Category "Script Generation">]
module Paket.LoadingScriptTests

open System.IO
open Paket.LoadingScripts
open Paket.LoadingScripts.ScriptGeneration
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket
open Paket.Domain
open Paket.ModuleResolver

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
        PackageResolver.ResolvedPackage.Settings = Requirements.InstallSettings.Default
        PackageResolver.ResolvedPackage.Source = PackageSources.PackageSource.NuGetV2 { Url = ""; Authentication = None }
        PackageResolver.IsRuntimeDependency = false }
      { Name = PackageName "other"
        Version = SemVer.Parse "1.0.0"
        Dependencies = Set.empty
        Unlisted = false
        Settings = Requirements.InstallSettings.Default
        Source = PackageSources.PackageSource.NuGetV2 { Url = ""; Authentication = None }
        IsRuntimeDependency = false }
    ]
    
[<Test>]
let ``can re-order simple dependency``() = 
    PackageAndAssemblyResolution.getPackageOrderResolvedPackage testData
    |> List.map (fun p -> p.Name)
    |> shouldEqual
        [ PackageName "other"
          PackageName "Test1" ]

[<Test>]
let ``can keep order simple dependency``() = 
    PackageAndAssemblyResolution.getPackageOrderResolvedPackage (testData |> List.rev)
    |> List.map (fun p -> p.Name)
    |> shouldEqual
        [ PackageName "other"
          PackageName "Test1" ]

let scriptGenInputWithNoDendency = {
    PackageName                  = Paket.Domain.PackageName "foo"
    PackagesOrGroupFolder        = DirectoryInfo "a"
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
let ``generateScriptData ``() =
    
    let cfg = DependenciesFile.FromCode("/root/",config1)

    let lockFile = 
        ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
        |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
        |> fun x -> LockFile.Parse("/root/paket.lock",String.getLines x)


    //let lockFile = 
    //    ResolveWithGraph(depsFile,noSha1,VersionsFromGraphAsSeq graph1, PackageDetailsFromGraph graph1).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    //    |> LockFileSerializer.serializePackages depsFile.Groups.[Constants.MainDependencyGroup].Options
    //    |> fun x -> LockFile.Parse("paket.lock", String.getLines x)
    
    //lockFile.
    cfg.Groups
    |> Map.toSeq 
    |> Seq.iter (fun (g,ds)-> 
        printfn "--| %A |--" g
        ds.Packages|> List.iter(fun x -> printfn " -- %A" x))

    let scriptData = ScriptGeneration.constructScriptsFromData [] cfg  lockFile [] [ScriptType.FSharp.Extension]
    scriptData |> Seq.iter(fun sd -> 
        printfn "\n-- %s --\n\n%s\n" sd.Path.FullName sd.Text
    )
