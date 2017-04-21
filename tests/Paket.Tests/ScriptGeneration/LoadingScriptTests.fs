[<NUnit.Framework.TestFixture>]
[<NUnit.Framework.Category "Script Generation">]
module Paket.LoadingScriptTests

open System
open System.IO
open Paket.LoadingScripts
open Paket.LoadingScripts.ScriptGeneration
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket
open Paket.Domain
open Paket.ModuleResolver
open Paket.LoadingScripts.PackageAndAssemblyResolution

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

let cfg = DependenciesFile.FromCode("/root/",config1)

let lockFile = 
    ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    |> LockFileSerializer.serializePackages cfg.Groups.[Constants.MainDependencyGroup].Options
    |> fun x -> LockFile.Parse("/root/paket.lock",String.getLines x)



[<Test>]
let ``Can determine package order from lock file``() =
 
    getPackageOrderFromLockFile lockFile
    |> Seq.collect (fun x ->  x.Value |> Seq.map (fun y -> x.Key, y.Name ))
    |> fun  pkgs -> 
        pkgs |> shouldContain (GroupName "Main", PackageName "Rx-Core" )
        pkgs |> shouldContain (GroupName "Main", PackageName "log4net" )
        pkgs |> shouldContain (GroupName "Main", PackageName "Castle.Windsor" )
    
let rootFolder = "/.paket/"


[<Test>]
let ``Can generate script contents from lockfile info``() =
    let framework = FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_5
    let packagesFolder = "packages"
    let loadScriptsRootFolder = DirectoryInfo "load"
    let scriptType = ScriptType.FSharp
    let folderForDefaultFramework = false
    let fst' (a,_,_) = a

    let generated = 
        getPackageOrderFromLockFile lockFile
        //|> Seq.collect (fun x ->  x.Value |> Seq.map (fun y -> x.Key, y))
        |> Map.map (fun  groupName packages -> 
            // fold over a map constructing load scripts to ensure shared packages don't have their scripts duplicated
            ((Map.empty,[]),packages)
            ||> Seq.fold (fun ((knownIncludeScripts,scriptFiles): Map<_,FileInfo>*_) (package: PackageResolver.ResolvedPackage) ->

                let packagesOrGroupFolder =
                    if groupName = Constants.MainDependencyGroup then DirectoryInfo packagesFolder
                    else let x = packagesFolder </> groupName in DirectoryInfo x

                let scriptFolder  =
                    let group = if groupName = Constants.MainDependencyGroup then String.Empty else (string groupName)
                    let framework = if folderForDefaultFramework then String.Empty else string framework
                    DirectoryInfo (rootFolder </> framework </> packagesOrGroupFolder </> package.Name)

                let scriptFile =
                    FileInfo <| sprintf "%s.%s"  scriptFolder.FullName ScriptType.FSharp.Extension
                    
                let dependencies = 
                    package.Dependencies |> Seq.map fst' 
                    |> Seq.choose knownIncludeScripts.TryFind |> List.ofSeq

                let installModel = 
                    (QualifiedPackageName.FromStrings(Some groupName.Name, package.Name.ToString()))
                    |> lockFile.GetInstalledPackageModel
            
                let dllFiles =
                    installModel
                    |> InstallModel.getLegacyReferences (SinglePlatform framework)
                    |> Seq.map (fun l -> FileInfo l.Path) |> List.ofSeq

                let scriptInfo = {
                    PackageName                  = package.Name
                    PackagesOrGroupFolder        = packagesOrGroupFolder
                    IncludeScriptsRootFolder     = loadScriptsRootFolder 
                    FrameworkReferences          = getFrameworkReferencesWithinPackage framework installModel |> List.map (fun ref -> ref.Name)
                    OrderedDllReferences         = dllFiles
                    DependentScripts             = dependencies
                }

                match generateScript scriptType scriptInfo with
                | DoNotGenerate -> 
                    (knownIncludeScripts,scriptFiles)
                | Generate pieces -> 
                    let knownScripts = knownIncludeScripts |> Map.add package.Name scriptFile
                    let rendered = (renderScript scriptType scriptFile pieces)::scriptFiles
                    (knownScripts, rendered)
            ) |> fun (_,sfs) -> sfs 
        ) |> Seq.collect (fun x -> x.Value)

    printSqs generated 
    





