module Paket.ReferencesFileSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.Requirements

let refFileContent = """
Castle.Windsor
Newtonsoft.Json
jQuery
File:FsUnit.fs
"""

[<Test>]
let ``should parse lines correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileContent).Groups.[NormalizedGroupName Constants.MainDependencyGroup]
    refFile.NugetPackages.Length |> shouldEqual 3
    refFile.NugetPackages.Head.Name |> shouldEqual (PackageName "Castle.Windsor")
    refFile.NugetPackages.Tail.Tail.Head.Name |> shouldEqual (PackageName "jQuery")
    refFile.RemoteFiles.Length |> shouldEqual 1
    refFile.RemoteFiles.Head.Name |> shouldEqual "FsUnit.fs"
    refFile.RemoteFiles.Head.Link |> shouldEqual ReferencesFile.DefaultLink

[<Test>]
let ``should serialize itself correctly``() = 
    let refFile = 
        {FileName = ""; 
         Groups = 
            [NormalizedGroupName Constants.MainDependencyGroup, 
             { Name = Constants.MainDependencyGroup; 
               NugetPackages = [ PackageInstallSettings.Default("A"); PackageInstallSettings.Default("B")]; 
               RemoteFiles = [{Name = "FromGithub.fs"; Link = ReferencesFile.DefaultLink; Settings = RemoteFileInstallSettings.Default }]} ] |> Map.ofSeq 
        }
    let expected = [|"A"; "B"; "File:FromGithub.fs"|]

    refFile.ToString() |> toLines |> shouldEqual expected

let refFileWithCustomPath = """
File:FsUnit.fs Tests\Common
"""

[<Test>]
let ``should parse custom path correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileWithCustomPath).Groups.[NormalizedGroupName Constants.MainDependencyGroup]
    refFile.NugetPackages.Length |> shouldEqual 0
    refFile.RemoteFiles.Length |> shouldEqual 1
    refFile.RemoteFiles.Head.Name |> shouldEqual "FsUnit.fs"
    refFile.RemoteFiles.Head.Link |> shouldEqual "Tests\Common"

[<Test>]
let ``should serialize customPath correctly``() = 
    let refFile = 
        {FileName = ""; 
         Groups = 
            [NormalizedGroupName Constants.MainDependencyGroup, 
             { Name = Constants.MainDependencyGroup; 
               NugetPackages = [ ]; 
               RemoteFiles = [{Name = "FromGithub.fs"; Link = "CustomPath\Dir"; Settings = RemoteFileInstallSettings.Default }]} ] |> Map.ofSeq 
        }
    let expected = [|"File:FromGithub.fs CustomPath\Dir"|]

    refFile.ToString() |> toLines |> shouldEqual expected

let refFileWithTrailingWhitespace = """
Castle.Windsor  
Newtonsoft.Json 
"""

[<Test>]
let ``should parse lines with trailing whitspace correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileWithTrailingWhitespace).Groups.[NormalizedGroupName Constants.MainDependencyGroup]
    refFile.NugetPackages.Length |> shouldEqual 2
    refFile.NugetPackages.Head.Name |> shouldEqual (PackageName "Castle.Windsor")
    refFile.NugetPackages.Tail.Head.Name |> shouldEqual (PackageName "Newtonsoft.Json")

[<Test>]
let ``should add nuget package``() = 
    let empty = ReferencesFile.New("file.txt")
    empty.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Length |> shouldEqual 0
    empty.Groups.[NormalizedGroupName Constants.MainDependencyGroup].RemoteFiles.Length |> shouldEqual 0
    empty.FileName |> shouldEqual "file.txt"

    let refFile = empty.AddNuGetReference(PackageName "NUnit")
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Length |> shouldEqual 1
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Head.Name |> shouldEqual (PackageName "NUnit")

    let refFile' = refFile.AddNuGetReference(PackageName "xUnit")
    refFile'.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Length |> shouldEqual 2
    refFile'.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Head.Name |> shouldEqual (PackageName "NUnit")
    refFile'.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Tail.Head.Name |> shouldEqual (PackageName "xUnit")


[<Test>]
let ``should not add nuget package twice``() = 
    let refFile = 
        ReferencesFile.New("file.txt")
          .AddNuGetReference(PackageName "NUnit")
          .AddNuGetReference(PackageName "NUnit")
          .AddNuGetReference(PackageName "NUnit")

    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Length |> shouldEqual 1
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Head.Name |> shouldEqual (PackageName "NUnit")

let refFileContentWithCopyLocalFalse = """Castle.Windsor copy_local : false
Newtonsoft.Json"""

[<Test>]
let ``should parse lines with CopyLocal settings correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileContentWithCopyLocalFalse)
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Length |> shouldEqual 2
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Head.Name |> shouldEqual (PackageName "Castle.Windsor")
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Head.Settings.CopyLocal |> shouldEqual (Some false)
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Tail.Head.Name |> shouldEqual (PackageName "Newtonsoft.Json")
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Tail.Head.Settings.CopyLocal |> shouldEqual None

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
    let refFile = ReferencesFile.FromLines(toLines refFileContentWithNoTargetsImport).Groups.[NormalizedGroupName Constants.MainDependencyGroup]
    refFile.NugetPackages.Length |> shouldEqual 2
    refFile.NugetPackages.Head.Name |> shouldEqual (PackageName "Castle.Windsor")
    refFile.NugetPackages.Head.Settings.CopyLocal |> shouldEqual None
    refFile.NugetPackages.Head.Settings.ImportTargets |> shouldEqual (Some false)
    refFile.NugetPackages.Tail.Head.Name |> shouldEqual (PackageName "Newtonsoft.Json")
    refFile.NugetPackages.Tail.Head.Settings.CopyLocal |> shouldEqual None
    refFile.NugetPackages.Tail.Head.Settings.ImportTargets |> shouldEqual None

let refFileContentWithCopyLocalFalseAndNoTargetsImport = """Castle.Windsor copy_local : false, import_targets: false
Newtonsoft.Json
xUnit import_targets:false"""

[<Test>]
let ``should parse lines with CopyLocal and import_targets settings correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileContentWithCopyLocalFalseAndNoTargetsImport).Groups.[NormalizedGroupName Constants.MainDependencyGroup]
    refFile.NugetPackages.Length |> shouldEqual 3
    refFile.NugetPackages.Head.Name |> shouldEqual (PackageName "Castle.Windsor")
    refFile.NugetPackages.Head.Settings.CopyLocal |> shouldEqual (Some false)
    refFile.NugetPackages.Head.Settings.ImportTargets |> shouldEqual (Some false)
    refFile.NugetPackages.Tail.Head.Name |> shouldEqual (PackageName "Newtonsoft.Json")
    refFile.NugetPackages.Tail.Head.Settings.CopyLocal |> shouldEqual None
    refFile.NugetPackages.Tail.Head.Settings.ImportTargets |> shouldEqual None

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
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Length |> shouldEqual 3
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Head.Name |> shouldEqual (PackageName "Castle.Windsor")
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Head.Settings.CopyLocal |> shouldEqual (Some false)
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Head.Settings.ImportTargets |> shouldEqual (Some false)

    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Tail.Head.Name |> shouldEqual (PackageName "Newtonsoft.Json")
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Tail.Head.Settings.CopyLocal |> shouldEqual None
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Tail.Head.Settings.ImportTargets |> shouldEqual None
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Tail.Head.Settings.OmitContent |> shouldEqual (Some true)

    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Tail.Tail.Head.Name |> shouldEqual (PackageName "xUnit")
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Tail.Tail.Head.Settings.CopyLocal |> shouldEqual None
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Tail.Tail.Head.Settings.ImportTargets |> shouldEqual (Some false)
    refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup].NugetPackages.Tail.Tail.Head.Settings.OmitContent |> shouldEqual None

    refFile.ToString()
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings refFileContentWithMultipleSettings)


[<Test>]
let ``should parse link:false correctly``() = 

    let refFile = """
File:FsUnit.fs Tests\Common link:true
File:FsUnit1.fs link:false
"""
    let refFile = ReferencesFile.FromLines(toLines refFile).Groups.[NormalizedGroupName Constants.MainDependencyGroup]
    refFile.NugetPackages.Length |> shouldEqual 0
    refFile.RemoteFiles.Length |> shouldEqual 2
    
    refFile.RemoteFiles.Head.Name |> shouldEqual "FsUnit.fs"
    refFile.RemoteFiles.Head.Link |> shouldEqual "Tests\Common"
    refFile.RemoteFiles.Head.Settings.Link |> shouldEqual (Some true)

    refFile.RemoteFiles.Tail.Head.Name |> shouldEqual "FsUnit1.fs"
    refFile.RemoteFiles.Tail.Head.Link |> shouldEqual ReferencesFile.DefaultLink
    refFile.RemoteFiles.Tail.Head.Settings.Link |> shouldEqual (Some false)
    

let refFileWithSecondGroup = """
Castle.Windsor  
Newtonsoft.Json 
group Test
NUnit
"""

[<Test>]
let ``should parse reffiles with groups``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileWithSecondGroup)
    let mainGroup = refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup]
    mainGroup.NugetPackages.Length |> shouldEqual 2
    mainGroup.NugetPackages.Head.Name |> shouldEqual (PackageName "Castle.Windsor")
    mainGroup.NugetPackages.Tail.Head.Name |> shouldEqual (PackageName "Newtonsoft.Json")
    
    let testGroup = refFile.Groups.[NormalizedGroupName (GroupName "Test")]
    testGroup.NugetPackages.Length |> shouldEqual 1
    testGroup.NugetPackages.Head.Name |> shouldEqual (PackageName "NUnit")

let refFileWithSecondNestedGroup = """
Castle.Windsor  
Newtonsoft.Json

group Test
  NUnit
"""

[<Test>]
let ``should parse reffiles with nested groups``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileWithSecondNestedGroup)
    let mainGroup = refFile.Groups.[NormalizedGroupName Constants.MainDependencyGroup]
    mainGroup.NugetPackages.Length |> shouldEqual 2
    mainGroup.NugetPackages.Head.Name |> shouldEqual (PackageName "Castle.Windsor")
    mainGroup.NugetPackages.Tail.Head.Name |> shouldEqual (PackageName "Newtonsoft.Json")
    
    let testGroup = refFile.Groups.[NormalizedGroupName (GroupName "Test")]
    testGroup.NugetPackages.Length |> shouldEqual 1
    testGroup.NugetPackages.Head.Name |> shouldEqual (PackageName "NUnit")