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

let scriptGenInputWithNoDendency = {
    PackageName                  = Paket.Domain.PackageName "foo"
    DependentScripts             = List.empty
    FrameworkReferences          = List.empty
    OrderedDllReferences         = List.empty
    PackageLoadScripts           = List.empty
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

let scriptGenInputWithLoadScript = {
    PackageName                  = Paket.Domain.PackageName "foo"
    DependentScripts             = List.empty
    FrameworkReferences          = List.empty
    OrderedDllReferences         = List.empty
    PackageLoadScripts           = ["foo.fsx"]
}

[<Test>]
let ``generateFSharpScript generates load script``() =
    let output = ScriptGeneration.generateScript ScriptType.FSharp scriptGenInputWithLoadScript

    match output with
    | Generate [ ReferenceType.LoadScript _ ] -> ()
    | _ -> Assert.Fail("generated script was expected to be a single load script")



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

