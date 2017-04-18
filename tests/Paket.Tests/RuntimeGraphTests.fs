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
        }
   }
}
"""

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
              { Rid = Rid.Of "win"; InheritedRids = [ Rid.Of "any" ]; RuntimeDependencies = Map.empty } ]
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

    let depsFile = DependenciesFile.FromCode("""source http://www.nuget.org/api/v2
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

    let depsFile = DependenciesFile.FromCode("""source http://www.nuget.org/api/v2
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