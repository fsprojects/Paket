module Paket.TemplateFile.Test

open System.IO
open Pri.LongPath
open Paket
open Chessie.ErrorHandling
open FsUnit
open NUnit.Framework
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

[<Literal>]
let FileBasedShortDesc = """type file
id My.Thing
version 1.0
authors Bob McBob
description A short description
"""

[<Literal>]
let FileBasedLongDesc = """type file
id My.Thing
version 1.0
authors Bob McBob
description
    A longer description
    on two lines.
"""

[<Literal>]
let FileBasedLongDesc2 = """type file
id My.Thing
authors Bob McBob
description
    A longer description
    on two lines.
version 1.0
"""

[<Literal>]
let FileBasedLongDesc3 = """type file
ID My.Thing
authors Bob McBob
DESCRIPTION
    A longer description
    on two lines.
version
    1.0
"""

[<Literal>]
let FileBasedLongDesc4 = """type file
id My.Thing
authors Bob McBob
description
    description starting with description
version
    1.0
"""

let v1 = Paket.SemVer.Parse "1.0"

let strToStream (str : string) =
    let mem = new MemoryStream()
    let writer = new StreamWriter(mem)
    writer.Write(str)
    writer.Flush()
    mem.Seek(0L, SeekOrigin.Begin) |> ignore
    mem

[<TestCase(FileBasedShortDesc, "A short description")>]
[<TestCase(FileBasedLongDesc, "A longer description\non two lines.")>]
[<TestCase(FileBasedLongDesc2, "A longer description\non two lines.")>]
[<TestCase(FileBasedLongDesc3, "A longer description\non two lines.")>]
[<TestCase(FileBasedLongDesc4, "description starting with description")>]
let ``Parsing minimal file based packages works`` (fileContent, desc) =
    let result =
        TemplateFile.Parse("file1.template",LockFile.Parse("",[||]), None, Map.empty, strToStream fileContent)
        |> returnOrFail

    match result with
    | ProjectInfo _ ->
        Assert.Fail("File package detected as project package")
    | CompleteInfo (core, _) ->
        core.Id |> shouldEqual "My.Thing"
        core.Version |> shouldEqual (Some v1)
        core.Authors |> shouldEqual ["Bob McBob"]
        core.Description |> normalizeLineEndings |> shouldEqual (normalizeLineEndings desc)

[<Literal>]
let Invalid1 = """type fil
id My.Thing
version 1.0
authors Bob McBob
description A short description
"""

[<Literal>]
let Invalid3 = """type file
id My.Thing
version 1.0
description A short description
"""

[<TestCase(Invalid1)>]
[<TestCase(Invalid3)>]
let ``Invalid file input recognised as invalid`` (fileContent : string) =
    TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), None, Map.empty, strToStream fileContent)
    |> failed
    |> shouldEqual true

[<Literal>]
let ValidWithoutVersion = """type file
id My.Thing
authors Bob McBob
description A short description
"""

[<Literal>]
let RealTest = """type project
owners
    Thomas Petricek, David Thomas, Ryan Riley, Steffen Forkmann
authors
    Thomas Petricek, David Thomas, Ryan Riley, Steffen Forkmann
projectUrl
    http://fsprojects.github.io/FSharpx.Async/
iconUrl
    http://fsprojects.github.io/FSharpx.Async/img/logo.png
licenseUrl
    http://fsprojects.github.io/FSharpx.Async/license.html
requireLicenseAcceptance
    false
copyright
    Copyright 2015
tags
    F#, async, fsharpx
summary
    Async extensions for F#
description
    Async extensions for F#

"""

[<Literal>]
let FullTest = """type project
title Chessie.Rop
owners
    Steffen Forkmann, Max Malook, Tomasz Heimowski
authors
    Steffen Forkmann, Max Malook, Tomasz Heimowski
projectUrl
    http://github.com/fsprojects/Chessie
iconUrl
    https://raw.githubusercontent.com/fsprojects/Chessie/master/docs/files/img/logo.png
licenseUrl
    http://github.com/fsprojects/Chessie/blob/master/LICENSE.txt
requireLicenseAcceptance
    false
copyright
    Copyright 2015
LANGUAGE
    en-gb
tags
    rop, fsharp F#
summary
    Railway-oriented programming for .NET
dependencies
     FSharp.Core 4.3.1
     My.OtherThing
excludeddependencies
      Newtonsoft.Json
      Chessie
excludedgroups
      build
description
    Railway-oriented programming for .NET"""

[<TestCase(ValidWithoutVersion)>]
[<TestCase(RealTest)>]
[<TestCase(FullTest)>]
let ``Valid file input recognised as valid`` (fileContent : string) =
   TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), None, Map.empty, strToStream fileContent)
    |> failed
    |> shouldEqual false

[<TestCase(FullTest)>]
let ``Optional fields are read`` (fileContent : string) =
    let sut =
        TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), None, Map.empty, strToStream fileContent)
        |> returnOrFail
        |> function
           | CompleteInfo (_, opt)
           | ProjectInfo (_, opt) -> opt
    sut.Title |> shouldEqual (Some "Chessie.Rop")
    sut.Copyright |> shouldEqual (Some "Copyright 2015")
    sut.Summary |> shouldEqual (Some "Railway-oriented programming for .NET")
    sut.IconUrl |> shouldEqual (Some "https://raw.githubusercontent.com/fsprojects/Chessie/master/docs/files/img/logo.png")
    sut.LicenseUrl |> shouldEqual (Some "http://github.com/fsprojects/Chessie/blob/master/LICENSE.txt")
    sut.ProjectUrl |> shouldEqual (Some "http://github.com/fsprojects/Chessie")
    sut.Tags |> shouldEqual ["rop";"fsharp";"F#"]
    sut.Owners |> shouldEqual ["Steffen Forkmann";"Max Malook";"Tomasz Heimowski"]
    sut.RequireLicenseAcceptance |> shouldEqual false
    sut.DevelopmentDependency |> shouldEqual false
    sut.Language |> shouldEqual (Some "en-gb")
    sut.DependencyGroups |> shouldContain ({ Framework = None; Dependencies =
        [PackageName "FSharp.Core",VersionRequirement.Parse("[4.3.1]")
         PackageName "My.OtherThing",VersionRequirement.AllReleases] })
    sut.ExcludedDependencies |> shouldContain (PackageName "Newtonsoft.Json")
    sut.ExcludedDependencies |> shouldContain (PackageName "Chessie")
    sut.ExcludedGroups |> shouldContain (GroupName "build")

[<Literal>]
let Dependency1 = """type file
id My.Thing
authors Bob McBob
description
    A longer description
    on two lines.
version
    1.0
dependencies
     FSharp.Core 4.3.1
     My.OtherThing
"""

[<TestCase(Dependency1)>]
let ``Detect dependencies correctly`` fileContent =
    let sut =
        TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), None, Map.empty, strToStream fileContent)
        |> returnOrFail
        |> function
           | CompleteInfo (_, opt)
           | ProjectInfo (_, opt) -> opt
    
    sut.DependencyGroups.Length |> shouldEqual 1
    match sut.DependencyGroups.Head.Dependencies with
    | [name1,range1;name2,range2] ->
        name1 |> shouldEqual (PackageName "FSharp.Core")
        range1.Range |> shouldEqual (Specific (SemVer.Parse "4.3.1"))
        name2 |> shouldEqual (PackageName "My.OtherThing")
        range2.Range |> shouldEqual (Minimum (SemVer.Parse "0"))
    | _ -> Assert.Fail()

[<Test>]
let ``Detect dependencies with targetFramework correctly`` () =
    let fileContent = """type file
id My.Thing
authors Bob McBob
description
    A longer description
    on two lines.
version
    1.0
dependencies
    xunit 2.0.0
    framework: net461
    framework: net45
        FSharp.Core 4.3.1
        My.OtherThing
    framework: netstandard11
        FSharp.Core 4.3.1
"""

    let sut =
        TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), None, Map.empty, strToStream fileContent)
        |> returnOrFail
        |> function
           | CompleteInfo (_, opt)
           | ProjectInfo (_, opt) -> opt
    
    sut.DependencyGroups |> List.length |> shouldEqual 4
    match sut.DependencyGroups with
    | [ g1; g2; g3; g4 ] ->
        g1.Framework |> shouldEqual None
        match g1.Dependencies with
        | [ name, range ] ->
            name |> shouldEqual (PackageName "xunit")
            range.Range |> shouldEqual (Specific (SemVer.Parse "2.0.0"))
        | _ -> Assert.Fail()

        g2.Framework |> shouldEqual (Some(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4_6_1)))
        g2.Dependencies |> List.length |> shouldEqual 0

        g3.Framework |> shouldEqual (Some(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4_5)))
        match g3.Dependencies with
        | [ name1, range1; name2, range2 ] ->
            name1 |> shouldEqual (PackageName "FSharp.Core")
            range1.Range |> shouldEqual (Specific (SemVer.Parse "4.3.1"))
            name2 |> shouldEqual (PackageName "My.OtherThing")
            range2.Range |> shouldEqual (Minimum (SemVer.Parse "0"))
        | _ -> Assert.Fail()

        g4.Framework |> shouldEqual (Some(FrameworkIdentifier.DotNetStandard(DotNetStandardVersion.V1_1)))
        match g4.Dependencies with
        | [name, range] ->
            name |> shouldEqual (PackageName "FSharp.Core")
            range.Range |> shouldEqual (Specific (SemVer.Parse "4.3.1"))            
        | _ -> Assert.Fail()
    | _ -> Assert.Fail()


[<Test>]
let ``Detect dependencies with CURRENTVERSION correctly`` () =
    let fileContent = """type file
id My.Thing
authors Bob McBob
description
    A longer description
    on two lines.
version
    1.0
dependencies
     FSharp.Core 4.3.1
     My.OtherThing CURRENTVERSION
"""

    let sut =
        TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), Some(SemVer.Parse "2.1"), Map.empty, strToStream fileContent)
        |> returnOrFail
        |> function
           | CompleteInfo (_, opt)
           | ProjectInfo (_, opt) -> opt
    match sut.DependencyGroups.Head.Dependencies with
    | [name1,range1;name2,range2] ->
        name1 |> shouldEqual (PackageName "FSharp.Core")
        range1.Range |> shouldEqual (Specific (SemVer.Parse "4.3.1"))
        name2 |> shouldEqual (PackageName "My.OtherThing")
        range2.Range |> shouldEqual (Specific (SemVer.Parse "2.1"))
    | _ -> Assert.Fail()

[<Test>]
let ``Should resolve custom versions correctly`` () =
    let fileContent = """type file
id Project.C
authors Bob McBob
description
    A longer description
    on two lines.
version
    1.0
dependencies
     FSharp.Core 4.3.1
     Project.A CURRENTVERSION
     Project.B CURRENTVERSION
"""

    let globalVersion = SemVer.Parse "1.0"
    let specificVersion = SemVer.Parse "2.0"
    let customVersions = Map.ofList [("Project.C", specificVersion); ("Project.B", specificVersion)]
    let version,sut =
        TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), Some(globalVersion), customVersions, strToStream fileContent)
        |> returnOrFail
        |> function
           | CompleteInfo (info, opt) -> info.Version, opt
           | ProjectInfo (info, opt) -> info.Version, opt

    version |> shouldEqual (Some specificVersion)

    match sut.DependencyGroups.Head.Dependencies with
    | [name1,range1;name2,range2;name3,range3] ->
        name1 |> shouldEqual (PackageName "FSharp.Core")
        range1.Range |> shouldEqual (Specific (SemVer.Parse "4.3.1"))
        name2 |> shouldEqual (PackageName "Project.A")
        range2.Range |> shouldEqual (Specific globalVersion)
        name3 |> shouldEqual (PackageName "Project.B")
        range3.Range |> shouldEqual (Specific specificVersion)
    | _ -> Assert.Fail()

[<Test>]
let ``Detect dependencies with LOCKEDVERSION correctly`` () =
    let fileContent = """type file
id My.Thing
authors Bob McBob
description
    A longer description
    on two lines.
version
    1.0
dependencies
     FSharp.Core 4.3.1
     My.OtherThing LOCKEDVERSION
"""

    let lockFile = """NUGET
  remote: https://www.nuget.org/api/v2
  specs:
    Argu (1.1.2)
    FSharp.Core (4.0.0.1) - redirects: on
    Newtonsoft.Json (7.0.1) - redirects: on
    My.OtherThing (1.2.3.0) - redirects: on
GITHUB
  remote: fsharp/FAKE
  specs:
    src/app/FakeLib/Globbing/Globbing.fs (494c549c61dc15ab798b7b92cb4ac6e981267f49)
  remote: fsprojects/Chessie
  specs:
    src/Chessie/ErrorHandling.fs (1f23b1caeb1f87e750abc96a25109376771dd090)
GROUP Build
NUGET
  remote: https://www.nuget.org/api/v2
  specs:
    FAKE (4.7.2)
    FSharp.Compiler.Service (1.4.0.6)
    FSharp.Formatting (2.12.0)
      FSharp.Compiler.Service (1.4.0.6)
      FSharpVSPowerTools.Core (2.1.0)
    FSharpVSPowerTools.Core (2.1.0)
      FSharp.Compiler.Service (>= 1.4.0.6)
    ILRepack (2.0.8)
    Microsoft.Bcl (1.1.10)
      Microsoft.Bcl.Build (>= 1.0.14)
    Microsoft.Bcl.Build (1.0.21) - import_targets: false
    Microsoft.Net.Http (2.2.29)
      Microsoft.Bcl (>= 1.1.10)
      Microsoft.Bcl.Build (>= 1.0.14)
    Octokit (0.16.0)
      Microsoft.Net.Http
GITHUB
  remote: fsharp/FAKE
  specs:
    modules/Octokit/Octokit.fsx (494c549c61dc15ab798b7b92cb4ac6e981267f49)
      Octokit"""

    let sut =
        TemplateFile.Parse("file1.template", LockFile.Parse("",toLines lockFile), Some(SemVer.Parse "2.1"), Map.empty, strToStream fileContent)
        |> returnOrFail
        |> function
           | CompleteInfo (_, opt)
           | ProjectInfo (_, opt) -> opt
    match sut.DependencyGroups.Head.Dependencies with
    | [name1,range1;name2,range2] ->
        name1 |> shouldEqual (PackageName "FSharp.Core")
        range1.Range |> shouldEqual (Specific (SemVer.Parse "4.3.1"))
        name2 |> shouldEqual (PackageName "My.OtherThing")
        range2.Range.ToString() |> shouldEqual "1.2.3"
        range2.FormatInNuGetSyntax() |> shouldEqual "[1.2.3.0]"

    | _ -> Assert.Fail()

[<Test>]
let ``Detect single file correctly``() =
    let text = """type file
id My.Thing
authors Bob McBob
description
    A longer description
    on two lines.
version
    1.0
files
    someDir ==> lib
"""
    let sut =
        TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), None, Map.empty, strToStream text)
        |> returnOrFail
        |> function
           | CompleteInfo (_, opt)
           | ProjectInfo (_, opt) -> opt
    match sut.Files with
    | [from,to'] ->
        from |> shouldEqual "someDir"
        to' |> shouldEqual "lib"
    | _ ->  Assert.Fail()

[<Test>]
let ``Detect references correctly``() =
    let text = """type file
id My.Thing
authors Bob McBob
description
    A longer description
    on two lines.
references
    somefile
    someOtherFile.dll
version
    1.0
"""
    let sut =
        TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), None, Map.empty, strToStream text)
        |> returnOrFail
        |> function
           | CompleteInfo (_, opt)
           | ProjectInfo (_, opt) -> opt

    match sut.References with
    | reference1::reference2::[] ->
        reference1 |> shouldEqual "somefile"
        reference2 |> shouldEqual "someOtherFile.dll"
    | _ ->  Assert.Fail()


[<Test>]
let ``Detect framework references correctly``() =
    let text = """type file
id My.Thing
authors Bob McBob
description
    A longer description
    on two lines.
frameworkAssemblies
    somefile
    someOtherFile.dll
version
    1.0
"""
    let sut =
        TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), None, Map.empty, strToStream text)
        |> returnOrFail
        |> function
           | CompleteInfo (_, opt)
           | ProjectInfo (_, opt) -> opt

    match sut.FrameworkAssemblyReferences with
    | reference1::reference2::[] ->
        reference1 |> shouldEqual "somefile"
        reference2 |> shouldEqual "someOtherFile.dll"
    | _ ->  Assert.Fail()

[<Test>]
let ``Detect multiple files correctly``() =
    let text = """type file
id My.Thing
authors Bob McBob
description
    A longer description
    on two lines.
version
    1.0
files
    someDir
    anotherDir ==> someLib
"""
    let sut =
        TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), None, Map.empty, strToStream text)
        |> returnOrFail
        |> function
           | CompleteInfo (_, opt)
           | ProjectInfo (_, opt) -> opt
    match sut.Files with
    | [from1,to1;from2,to2] ->
        from1 |> shouldEqual "someDir"
        to1 |> shouldEqual "lib"
        from2 |> shouldEqual "anotherDir"
        to2 |> shouldEqual "someLib"
    | _ ->  Assert.Fail()

[<Test>]
let ``Detect exclude files correctly``() =
    let text = """type file
id My.Thing
authors Bob McBob
description
    A longer description
    on two lines.
version
    1.0
files
    someDir
    anotherDir ==> someLib
    !dontWantThis.txt
"""
    let sut =
        TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), None, Map.empty, strToStream text)
        |> returnOrFail
        |> function
           | CompleteInfo (_, opt)
           | ProjectInfo (_, opt) -> opt
    match sut.FilesExcluded with
    | [x] -> x |> shouldEqual "dontWantThis.txt"
    | _ ->  Assert.Fail()

[<Test>]
let ``Detect mutliple exclude files correctly``() =
    let text = """type file
id My.Thing
authors Bob McBob
description
    A longer description
    on two lines.
version
    1.0
files
    someDir
    anotherDir ==> someLib
    !dontWantThis.txt
    !dontWantThat.txt
"""
    let sut =
        TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), None, Map.empty, strToStream text)
        |> returnOrFail
        |> function
           | CompleteInfo (_, opt)
           | ProjectInfo (_, opt) -> opt
    match sut.FilesExcluded with
    | [x;y] -> x |> shouldEqual "dontWantThis.txt"
               y |> shouldEqual "dontWantThat.txt"
    | _ ->  Assert.Fail()

[<Test>]
let ``disallow the space to avoid ambiguity in exclusion file pattern``() =
    let text = """type file
id My.Thing
authors Bob McBob
description
    A longer description
    on two lines.
version
    1.0
files
    someDir
    anotherDir ==> someLib
    ! excludeDir
"""
    let sut =
        TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), None, Map.empty, strToStream text)
        |> returnOrFail
        |> function
           | CompleteInfo (_, opt)
           | ProjectInfo (_, opt) -> opt
    match sut.FilesExcluded with
    | [] -> Assert.Pass()
    | _ ->  Assert.Fail()

[<Literal>]
let ProjectType1 = """type project
"""

[<TestCase(ProjectType1)>]
let ``Parsing minimal project based packages works`` (fileContent) =
    let result =
        TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), None, Map.empty, strToStream fileContent)
        |> returnOrFail

    match result with
    | CompleteInfo _ ->
        Assert.Fail("Project package detected as file package")
    | ProjectInfo (core, _) ->
        core.Id |> shouldEqual None
        core.Version |> shouldEqual None
        core.Authors |> shouldEqual None
        core.Description |> shouldEqual None

[<Test>]
let ``skip empty lines correctly``() =
    let text = """type file
id GROSSWEBER.Angebot.Contracts
version 1.0

title
  grossweber.com Angebot contracts

description
  Contracts to talk to the Angebot service

authors
  GROSSWEBER

owners
  GROSSWEBER

projectUrl
  http://grossweber.com/

iconUrl
  http://grossweber.com/favicon.ico

copyright
  Copyright GROSSWEBER. All rights reserved.

files
  ../../build/bin/Angebot.Contracts.dll ==> lib
  ../../build/bin/Angebot.Contracts.pdb ==> lib
"""
    let sut =
        TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), None, Map.empty, strToStream text)
        |> returnOrFail
        |> function
           | CompleteInfo (_, opt)
           | ProjectInfo (_, opt) -> opt

    sut.Title |> shouldEqual (Some "grossweber.com Angebot contracts")
    sut.Copyright |> shouldEqual (Some "Copyright GROSSWEBER. All rights reserved.")
    sut.Summary |> shouldEqual None
    sut.IconUrl |> shouldEqual (Some "http://grossweber.com/favicon.ico")
    sut.LicenseUrl |> shouldEqual None
    sut.ProjectUrl |> shouldEqual (Some "http://grossweber.com/")
    sut.Tags |> shouldEqual []
    sut.Owners |> shouldEqual ["GROSSWEBER"]
    sut.RequireLicenseAcceptance |> shouldEqual false
    sut.DevelopmentDependency |> shouldEqual false
    sut.Language |> shouldEqual None

    match sut.Files with
    | [from1,to1;from2,to2] ->
        from1 |> shouldEqual "../../build/bin/Angebot.Contracts.dll"
        to1 |> shouldEqual "lib"
        from2 |> shouldEqual "../../build/bin/Angebot.Contracts.pdb"
        to2 |> shouldEqual "lib"
    | _ ->  Assert.Fail()


[<Test>]
let ``skip comment lines``() =
    let text = """type file
# comment here
# comment here
// a comment with slashes
id GROSSWEBER.Angebot.Contracts
version 1.0
# comment here
title
  grossweber.com Angebot contracts

description
  Contracts to talk to the Angebot service

# another comment here

authors
  GROSSWEBER

owners
  GROSSWEBER

projectUrl
  http://grossweber.com/

iconUrl
  http://grossweber.com/favicon.ico

copyright
  Copyright GROSSWEBER. All rights reserved.

files
    # another comment here
    // a comment with slashes
  ../../build/bin/Angebot.Contracts.dll ==> lib
    # another comment here
  ../../build/bin/Angebot.Contracts.pdb ==> lib
    # another comment here
  !../../build/bin/Angebot.Contracts.xml
    # another comment here

"""
    let sut =
        TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), None, Map.empty, strToStream text)
        |> returnOrFail
        |> function
           | CompleteInfo (_, opt)
           | ProjectInfo (_, opt) -> opt

    sut.Title |> shouldEqual (Some "grossweber.com Angebot contracts")
    sut.Copyright |> shouldEqual (Some "Copyright GROSSWEBER. All rights reserved.")
    sut.Summary |> shouldEqual None
    sut.IconUrl |> shouldEqual (Some "http://grossweber.com/favicon.ico")
    sut.LicenseUrl |> shouldEqual None
    sut.ProjectUrl |> shouldEqual (Some "http://grossweber.com/")
    sut.Tags |> shouldEqual []
    sut.Owners |> shouldEqual ["GROSSWEBER"]
    sut.RequireLicenseAcceptance |> shouldEqual false
    sut.DevelopmentDependency |> shouldEqual false
    sut.Language |> shouldEqual None

    match sut.Files with
    | [from1,to1;from2,to2] ->
        from1 |> shouldEqual "../../build/bin/Angebot.Contracts.dll"
        to1 |> shouldEqual "lib"
        from2 |> shouldEqual "../../build/bin/Angebot.Contracts.pdb"
        to2 |> shouldEqual "lib"
    | _ ->  Assert.Fail()

    Assert.AreEqual(1, sut.FilesExcluded.Length)
    Assert.AreEqual("../../build/bin/Angebot.Contracts.xml", sut.FilesExcluded.[0])


[<Test>]
let ``parse real world template``() =
    let text = """﻿
type project
title Gu.SiemensCommunication

files
    .\lib\*.* ==> lib\net45

excludeddependencies
  JetBrains.Annotations
  StyleCop.Analyzers
  Microsoft.Net.Compilers"""

    let sut =
        TemplateFile.Parse("file1.template", LockFile.Parse("",[||]), None, Map.empty, strToStream text)
        |> returnOrFail
        |> function
           | CompleteInfo (_, opt)
           | ProjectInfo (_, opt) -> opt

    sut.ExcludedDependencies.Count |> shouldEqual 3
