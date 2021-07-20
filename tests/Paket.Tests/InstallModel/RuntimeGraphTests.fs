module Paket.RuntimeGraphTests

open Paket
open NUnit.Framework
open FsUnit
open Paket.Domain
open Paket.TestHelpers

let supportAndDeps = """
{
  "runtimes": {
    "win": {
      "Microsoft.Win32.Primitives": {
        "runtime.win.Microsoft.Win32.Primitives": "4.3.0"
      },
      "System.Runtime.Extensions": {
        "runtime.win.System.Runtime.Extensions": "4.3.0"
      }
    }
  },
  "supports": {
    "uwp.10.0.app": {
      "uap10.0": [
        "win10-x86",
        "win10-x86-aot",
        "win10-arm",
        "win10-arm-aot"
      ]
    },
    "net45.app": {
      "net45": [
        "",
        "win-x86",
        "win-x64"
      ]
    }
  }
}"""


let rids = """
{
    "runtimes": {
        "base": {
        },
        "any": {
            "#import": [ "base" ]
        },
        "win": {
            "#import": [ "any" ]
        },
        "win-x86": {
            "#import": [ "win" ]
        }
   }
}
"""

let runtimeJsonMsNetCorePlatforms2_2_1 = System.IO.File.ReadAllText (System.IO.Path.Combine(__SOURCE_DIRECTORY__, "runtimeJsonMsNetCorePlatforms2_2_1.json"))

let runtimeJsonMsNetCoreTargets2_1_0 = System.IO.File.ReadAllText (System.IO.Path.Combine( __SOURCE_DIRECTORY__, "runtimeJsonMsNetCoreTargets2_1_0.json"))

let runtimeGraphMsNetCorePlatforms2_2_1 = RuntimeGraphParser.readRuntimeGraph runtimeJsonMsNetCorePlatforms2_2_1
let runtimeGraphMsNetCoreTargets2_1_0 = RuntimeGraphParser.readRuntimeGraph runtimeJsonMsNetCoreTargets2_1_0



[<Test>]
let ``Check if we can parse runtime support and runtime dependencies``() =
    let runtimeGraph = RuntimeGraphParser.readRuntimeGraph supportAndDeps

    runtimeGraph
    |> shouldEqual
        { Runtimes =
            [ { Rid = Rid.Of "win"; InheritedRids = [ ]
                RuntimeDependencies =
                  [ PackageName "Microsoft.Win32.Primitives", [ PackageName "runtime.win.Microsoft.Win32.Primitives", VersionRequirement.VersionRequirement (VersionRange.Minimum (SemVer.Parse "4.3.0"), PreReleaseStatus.No) ]
                    PackageName "System.Runtime.Extensions", [ PackageName "runtime.win.System.Runtime.Extensions", VersionRequirement.VersionRequirement (VersionRange.Minimum (SemVer.Parse "4.3.0"), PreReleaseStatus.No) ]
                  ]
                  |> Map.ofSeq } ]
            |> Seq.map (fun r -> r.Rid, r)
            |> Map.ofSeq
          Supports =
            [ { Name = "net45.app"
                Supported =
                  [ FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_5, [ Rid.Of ""; Rid.Of "win-x86"; Rid.Of "win-x64" ]]
                  |> Map.ofSeq }
              { Name = "uwp.10.0.app"
                Supported =
                  [ FrameworkIdentifier.UAP UAPVersion.V10, [ Rid.Of "win10-x86"; Rid.Of "win10-x86-aot"; Rid.Of "win10-arm"; Rid.Of "win10-arm-aot" ]]
                  |> Map.ofSeq } ]
            |> Seq.map (fun c -> c.Name, c)
            |> Map.ofSeq
        }

[<Test>]
let ``Check if we can parse runtime rids``() =
    let runtimeGraph = RuntimeGraphParser.readRuntimeGraph rids

    runtimeGraph
    |> shouldEqual
        { Runtimes =
            [ { Rid = Rid.Of "base"; InheritedRids = [ ]; RuntimeDependencies = Map.empty }
              { Rid = Rid.Of "any"; InheritedRids = [ Rid.Of "base" ]; RuntimeDependencies = Map.empty }
              { Rid = Rid.Of "win"; InheritedRids = [ Rid.Of "any" ]; RuntimeDependencies = Map.empty }
              { Rid = Rid.Of "win-x86"; InheritedRids = [ Rid.Of "win" ]; RuntimeDependencies = Map.empty } ]
            |> Seq.map (fun r -> r.Rid, r)
            |> Map.ofSeq
          Supports =
            []
            |> Map.ofSeq
        }

[<Test>]
let ``Check if we can merge two graphs``() =
    let r1 = RuntimeGraphParser.readRuntimeGraph rids
    let r2 = RuntimeGraphParser.readRuntimeGraph supportAndDeps
    let merged = RuntimeGraph.merge r1 r2
    let win = merged.Runtimes.[Rid.Of "win"]
    win.InheritedRids
        |> shouldEqual [ Rid.Of "any" ]
    win.RuntimeDependencies
        |> shouldEqual
             ([ PackageName "Microsoft.Win32.Primitives", [ PackageName "runtime.win.Microsoft.Win32.Primitives", VersionRequirement.VersionRequirement (VersionRange.Minimum (SemVer.Parse "4.3.0"), PreReleaseStatus.No) ]
                PackageName "System.Runtime.Extensions", [ PackageName "runtime.win.System.Runtime.Extensions", VersionRequirement.VersionRequirement (VersionRange.Minimum (SemVer.Parse "4.3.0"), PreReleaseStatus.No) ]
              ] |> Map.ofSeq)

[<Test>]
let ``Check that runtime dependencies are saved as such in the lockfile`` () =
    let lockFileData = """ """
    let getLockFile lockFileData = LockFile.Parse("",toLines lockFileData)
    let lockFile = lockFileData |> getLockFile

    let graph =
        [ "MyDependency", "3.2.0", [], RuntimeGraph.Empty
          "MyDependency", "3.3.3", [], RuntimeGraph.Empty
          "MyDependency", "4.0.0", [], RuntimeGraphParser.readRuntimeGraph """{
  "runtimes": {
    "win": {
      "MyDependency": {
        "MyRuntimeDependency": "4.0.0"
      }
    }
  }
}"""
          "MyRuntimeDependency", "4.0.0", [], RuntimeGraph.Empty
          "MyRuntimeDependency", "4.0.1", [], RuntimeGraph.Empty ]
        |> OfGraphWithRuntimeDeps

    let expectedLockFile = """NUGET
  remote: http://www.nuget.org/api/v2
    MyDependency (4.0)
    MyRuntimeDependency (4.0.1) - isRuntimeDependency: true"""

    let depsFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2
nuget MyDependency""")
    let lockFile, resolution =
        UpdateProcess.selectiveUpdate true noSha1 (VersionsFromGraph graph) (PackageDetailsFromGraph graph) (GetRuntimeGraphFromGraph graph) lockFile depsFile PackageResolver.UpdateMode.Install SemVerUpdateMode.NoRestriction

    let result =
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version, resolved.IsRuntimeDependency))

    let expected =
        [("MyDependency","4.0.0", false);
        ("MyRuntimeDependency","4.0.1", true)]
        |> Seq.sortBy (fun (t,_,_) ->t)

    result
    |> Seq.sortBy (fun (t,_,_) ->t)
    |> shouldEqual expected

    lockFile.GetGroup(Constants.MainDependencyGroup).Resolution
    |> LockFileSerializer.serializePackages depsFile.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expectedLockFile)

[<Test>]
let ``Check that runtime dependencies we don't use are ignored`` () =
    let lockFileData = """ """
    let getLockFile lockFileData = LockFile.Parse("",toLines lockFileData)
    let lockFile = lockFileData |> getLockFile

    let graph =
        [ "MyDependency", "3.2.0", [], RuntimeGraph.Empty
          "MyDependency", "3.3.3", [], RuntimeGraph.Empty
          "MyDependency", "4.0.0", [], RuntimeGraphParser.readRuntimeGraph """{
  "runtimes": {
    "win": {
      "SomePackage": {
        "MyRuntimeDependency": "4.0.0"
      }
    }
  }
}"""
          "MyRuntimeDependency", "4.0.0", [], RuntimeGraph.Empty
          "MyRuntimeDependency", "4.0.1", [], RuntimeGraph.Empty ]
        |> OfGraphWithRuntimeDeps

    let depsFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2
nuget MyDependency""")
    let lockFile, resolution =
        UpdateProcess.selectiveUpdate true noSha1 (VersionsFromGraph graph) (PackageDetailsFromGraph graph) (GetRuntimeGraphFromGraph graph) lockFile depsFile PackageResolver.UpdateMode.Install SemVerUpdateMode.NoRestriction

    let result =
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version, resolved.IsRuntimeDependency))

    let expected =
        [("MyDependency","4.0.0", false)]
        |> Seq.sortBy (fun (t,_,_) ->t)

    result
    |> Seq.sortBy (fun (t,_,_) ->t)
    |> shouldEqual expected

[<Test>]
let ``Check that runtime dependencies are loaded from the lockfile`` () =
    let lockFile = """NUGET
  remote: http://www.nuget.org/api/v2
    MyDependency (4.0)
    MyRuntimeDependency (4.0.1) - isRuntimeDependency: true"""

    let lockFile = LockFileParser.Parse(toLines lockFile) |> List.head
    let packages = List.rev lockFile.Packages

    let expected =
        [("MyDependency","4.0", false);
        ("MyRuntimeDependency","4.0.1", true)]
        |> List.sortBy (fun (t,_,_) ->t)

    packages
    |> List.map (fun r -> string r.Name, string r.Version, r.IsRuntimeDependency)
    |> List.sortBy (fun (t,_,_) ->t)
    |> shouldEqual expected

    
[<Test>]
let ``Check that runtime inheritance works`` () =
    let runtimeGraph = RuntimeGraphParser.readRuntimeGraph rids
    let content =
        { NuGet.NuGetPackageContent.Path = "/c/test/blub";
          NuGet.NuGetPackageContent.Spec = Nuspec.All
          NuGet.NuGetPackageContent.Content =
            NuGet.ofFiles [
              "lib/netstandard1.1/testpackage.xml"
              "lib/netstandard1.1/testpackage.dll"
              "runtimes/win/lib/netstandard1.1/testpackage.xml"
              "runtimes/win/lib/netstandard1.1/testpackage.dll"
            ]}
    let model =
        InstallModel.EmptyModel (PackageName "testpackage", SemVer.Parse "1.0.0")
        |> InstallModel.addNuGetFiles content
        
    let targetProfile = Paket.TargetProfile.SinglePlatform(Paket.FrameworkIdentifier.DotNetStandard Paket.DotNetStandardVersion.V1_6)
    model.GetRuntimeAssemblies runtimeGraph (Rid.Of "win-x86") targetProfile
    |> Seq.map (fun fi -> fi.Library.PathWithinPackage)
    |> Seq.toList
    |> shouldEqual [ "runtimes/win/lib/netstandard1.1/testpackage.dll" ]
    
[<Test>]
let ``Check that runtime inheritance works (2)`` () =
    let runtimeGraph = runtimeGraphMsNetCorePlatforms2_2_1
    let content =
        { NuGet.NuGetPackageContent.Path = "~/.nuget/packages/System.Runtime.InteropServices.RuntimeInformation";
          NuGet.NuGetPackageContent.Spec = Nuspec.All
          NuGet.NuGetPackageContent.Content =
            NuGet.ofFiles [
              "lib/MonoTouch10/_._"
              "lib/net45/System.Runtime.InteropServices.RuntimeInformation.dll"
              "lib/netstandard1.1/System.Runtime.InteropServices.RuntimeInformation.dll"
              "ref/MonoTouch10/_._"
              "ref/netstandard1.1/System.Runtime.InteropServices.RuntimeInformation.dll"
              "runtimes/aot/lib/netcore50/System.Runtime.InteropServices.RuntimeInformation.dll"
              "runtimes/unix/lib/netstandard1.1/System.Runtime.InteropServices.RuntimeInformation.dll"
              "runtimes/win/lib/net45/System.Runtime.InteropServices.RuntimeInformation.dll"
              "runtimes/win/lib/netcore50/System.Runtime.InteropServices.RuntimeInformation.dll"
              "runtimes/win/lib/netstandard1.1/System.Runtime.InteropServices.RuntimeInformation.dll"
            ]}
    let model =
        InstallModel.EmptyModel (PackageName "System.Runtime.InteropServices.RuntimeInformation", SemVer.Parse "4.3.0")
        |> InstallModel.addNuGetFiles content
        
    let targetProfile = Paket.TargetProfile.SinglePlatform(Paket.FrameworkIdentifier.DotNetStandard Paket.DotNetStandardVersion.V1_6)
    model.GetRuntimeAssemblies runtimeGraph (Rid.Of "win10-x86") targetProfile
    |> Seq.map (fun fi -> fi.Library.PathWithinPackage)
    |> Seq.toList
    |> shouldEqual [ "runtimes/win/lib/netstandard1.1/System.Runtime.InteropServices.RuntimeInformation.dll" ]

[<Test>]
let ``Check correct inheritance list`` () =
    let runtimeGraph = RuntimeGraphParser.readRuntimeGraph rids
    RuntimeGraph.getInheritanceList (Rid.Of "win-x86") runtimeGraph
        |> shouldEqual [ Rid.Of "win-x86"; Rid.Of "win"; Rid.Of "any"; Rid.Of "base"]
    