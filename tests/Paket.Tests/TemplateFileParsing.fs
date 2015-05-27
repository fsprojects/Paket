module Paket.TemplateFile.Test

open System.IO
open Paket
open Chessie.ErrorHandling
open FsUnit
open NUnit.Framework
open Paket.TestHelpers

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
        TemplateFile.Parse("file1.template", strToStream fileContent)
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
    TemplateFile.Parse("file1.template", strToStream fileContent)
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
description
    Railway-oriented programming for .NET"""

[<TestCase(ValidWithoutVersion)>]
[<TestCase(RealTest)>]
[<TestCase(FullTest)>]
let ``Valid file input recognised as valid`` (fileContent : string) =
   TemplateFile.Parse("file1.template", strToStream fileContent)
    |> failed
    |> shouldEqual false

[<TestCase(FullTest)>]
let ``Optional fields are read`` (fileContent : string) =
    let sut =
        TemplateFile.Parse("file1.template", strToStream fileContent)
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
        TemplateFile.Parse("file1.template", strToStream fileContent)
        |> returnOrFail
        |> function
           | CompleteInfo (_, opt)
           | ProjectInfo (_, opt) -> opt
    match sut.Dependencies with
    | [name1,range1;name2,range2] ->
        name1 |> shouldEqual "FSharp.Core"
        range1.Range |> shouldEqual (Specific (SemVer.Parse "4.3.1"))
        name2 |> shouldEqual "My.OtherThing"
        range2.Range |> shouldEqual (Minimum (SemVer.Parse "0"))
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
        TemplateFile.Parse("file1.template", strToStream text)
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
        TemplateFile.Parse("file1.template", strToStream text)
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

[<Literal>]
let ProjectType1 = """type project
"""

[<TestCase(ProjectType1)>]
let ``Parsing minimal project based packages works`` (fileContent) =
    let result =
        TemplateFile.Parse("file1.template", strToStream fileContent)
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
        TemplateFile.Parse("file1.template", strToStream text)
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