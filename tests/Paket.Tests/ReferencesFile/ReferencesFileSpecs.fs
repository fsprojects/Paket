module Paket.ReferencesFileSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain

let refFileContent = """
Castle.Windsor
Newtonsoft.Json
jQuery
File:FsUnit.fs
"""

[<Test>]
let ``should parse lines correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileContent)
    refFile.NugetPackages.Length |> shouldEqual 3
    refFile.NugetPackages.Head.Name |> shouldEqual (PackageName "Castle.Windsor")
    refFile.NugetPackages.Tail.Tail.Head.Name |> shouldEqual (PackageName "jQuery")
    refFile.RemoteFiles.Length |> shouldEqual 1
    refFile.RemoteFiles.Head.Name |> shouldEqual "FsUnit.fs"
    refFile.RemoteFiles.Head.Link |> shouldEqual ReferencesFile.DefaultLink

[<Test>]
let ``should serialize itself correctly``() = 
    let refFile = {FileName = ""; NugetPackages = [ PackageInstallSettings.Default("A"); PackageInstallSettings.Default("B")]; RemoteFiles = [{Name = "FromGithub.fs"; Link = ReferencesFile.DefaultLink}]}
    let expected = [|"A"; "B"; "File:FromGithub.fs"|]

    refFile.ToString() |> toLines |> shouldEqual expected

let refFileWithCustomPath = """
File:FsUnit.fs Tests\Common
"""

[<Test>]
let ``should parse custom path correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileWithCustomPath)
    refFile.NugetPackages.Length |> shouldEqual 0
    refFile.RemoteFiles.Length |> shouldEqual 1
    refFile.RemoteFiles.Head.Name |> shouldEqual "FsUnit.fs"
    refFile.RemoteFiles.Head.Link |> shouldEqual "Tests\Common"

[<Test>]
let ``should serialize customPath correctly``() = 
    let refFile = {FileName = ""; NugetPackages = []; RemoteFiles = [{Name = "FromGithub.fs"; Link = "CustomPath\Dir"}]}
    let expected = [|"File:FromGithub.fs CustomPath\Dir"|]

    refFile.ToString() |> toLines |> shouldEqual expected

let refFileWithTrailingWhitespace = """
Castle.Windsor  
Newtonsoft.Json 
"""

[<Test>]
let ``should parse lines with trailing whitspace correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileWithTrailingWhitespace)
    refFile.NugetPackages.Length |> shouldEqual 2
    refFile.NugetPackages.Head.Name |> shouldEqual (PackageName "Castle.Windsor")
    refFile.NugetPackages.Tail.Head.Name |> shouldEqual (PackageName "Newtonsoft.Json")

[<Test>]
let ``should add nuget package``() = 
    let empty = ReferencesFile.New("file.txt")
    empty.NugetPackages.Length |> shouldEqual 0
    empty.RemoteFiles.Length |> shouldEqual 0
    empty.FileName |> shouldEqual "file.txt"

    let refFile = empty.AddNuGetReference(PackageName "NUnit")
    refFile.NugetPackages.Length |> shouldEqual 1
    refFile.NugetPackages.Head.Name |> shouldEqual (PackageName "NUnit")

    let refFile' = refFile.AddNuGetReference(PackageName "xUnit")
    refFile'.NugetPackages.Length |> shouldEqual 2
    refFile'.NugetPackages.Head.Name |> shouldEqual (PackageName "NUnit")
    refFile'.NugetPackages.Tail.Head.Name |> shouldEqual (PackageName "xUnit")


[<Test>]
let ``should not add nuget package twice``() = 
    let refFile = 
        ReferencesFile.New("file.txt")
          .AddNuGetReference(PackageName "NUnit")
          .AddNuGetReference(PackageName "NUnit")
          .AddNuGetReference(PackageName "NUnit")

    refFile.NugetPackages.Length |> shouldEqual 1
    refFile.NugetPackages.Head.Name |> shouldEqual (PackageName "NUnit")

let refFileContentWithCopyLocalFalse = """Castle.Windsor copy_local : false
Newtonsoft.Json"""

[<Test>]
let ``should parse lines with CopyLocal settings correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileContentWithCopyLocalFalse)
    refFile.NugetPackages.Length |> shouldEqual 2
    refFile.NugetPackages.Head.Name |> shouldEqual (PackageName "Castle.Windsor")
    refFile.NugetPackages.Head.Settings.CopyLocal |> shouldEqual false
    refFile.NugetPackages.Tail.Head.Name |> shouldEqual (PackageName "Newtonsoft.Json")
    refFile.NugetPackages.Tail.Head.Settings.CopyLocal |> shouldEqual true

[<Test>]
let ``should serialize CopyLocal correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileContentWithCopyLocalFalse)
    let expected = """Castle.Windsor copy_local: false
Newtonsoft.Json"""

    refFile.ToString()
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings expected)

let refFileContentWithNoTargetsImport = """Castle.Windsor import_targets: false
Newtonsoft.Json"""

[<Test>]
let ``should parse lines with import_targets settings correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileContentWithNoTargetsImport)
    refFile.NugetPackages.Length |> shouldEqual 2
    refFile.NugetPackages.Head.Name |> shouldEqual (PackageName "Castle.Windsor")
    refFile.NugetPackages.Head.Settings.CopyLocal |> shouldEqual true
    refFile.NugetPackages.Head.Settings.ImportTargets |> shouldEqual false
    refFile.NugetPackages.Tail.Head.Name |> shouldEqual (PackageName "Newtonsoft.Json")
    refFile.NugetPackages.Tail.Head.Settings.CopyLocal |> shouldEqual true
    refFile.NugetPackages.Tail.Head.Settings.ImportTargets |> shouldEqual true

let refFileContentWithCopyLocalFalseAndNoTargetsImport = """Castle.Windsor copy_local : false, import_targets: false
Newtonsoft.Json
xUnit import_targets:false"""

[<Test>]
let ``should parse lines with CopyLocal and import_targets settings correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileContentWithCopyLocalFalseAndNoTargetsImport)
    refFile.NugetPackages.Length |> shouldEqual 3
    refFile.NugetPackages.Head.Name |> shouldEqual (PackageName "Castle.Windsor")
    refFile.NugetPackages.Head.Settings.CopyLocal |> shouldEqual false
    refFile.NugetPackages.Head.Settings.ImportTargets |> shouldEqual false
    refFile.NugetPackages.Tail.Head.Name |> shouldEqual (PackageName "Newtonsoft.Json")
    refFile.NugetPackages.Tail.Head.Settings.CopyLocal |> shouldEqual true
    refFile.NugetPackages.Tail.Head.Settings.ImportTargets |> shouldEqual true

[<Test>]
let ``should serialize import_targets correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileContentWithCopyLocalFalseAndNoTargetsImport)
    let expected = """Castle.Windsor copy_local: false, import_targets: false
Newtonsoft.Json
xUnit import_targets: false"""

    refFile.ToString()
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings expected)


let refFileContentWithMultipleSettings = """Castle.Windsor copy_local: false, import_targets: false, framework: net35, >= net40
Newtonsoft.Json content: none, framework: net40
xUnit import_targets: false"""

[<Test>]
let ``should parse and serialize lines with multiple settings settings correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileContentWithMultipleSettings)
    refFile.NugetPackages.Length |> shouldEqual 3
    refFile.NugetPackages.Head.Name |> shouldEqual (PackageName "Castle.Windsor")
    refFile.NugetPackages.Head.Settings.CopyLocal |> shouldEqual false
    refFile.NugetPackages.Head.Settings.ImportTargets |> shouldEqual false

    refFile.NugetPackages.Tail.Head.Name |> shouldEqual (PackageName "Newtonsoft.Json")
    refFile.NugetPackages.Tail.Head.Settings.CopyLocal |> shouldEqual true
    refFile.NugetPackages.Tail.Head.Settings.ImportTargets |> shouldEqual true
    refFile.NugetPackages.Tail.Head.Settings.OmitContent |> shouldEqual true

    refFile.NugetPackages.Tail.Tail.Head.Name |> shouldEqual (PackageName "xUnit")
    refFile.NugetPackages.Tail.Tail.Head.Settings.CopyLocal |> shouldEqual true
    refFile.NugetPackages.Tail.Tail.Head.Settings.ImportTargets |> shouldEqual false
    refFile.NugetPackages.Tail.Tail.Head.Settings.OmitContent |> shouldEqual false

    refFile.ToString()
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings refFileContentWithMultipleSettings)