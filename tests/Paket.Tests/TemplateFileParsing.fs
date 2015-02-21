module Paket.TemplateFile.Test

open System.IO
open Paket
open Paket.Rop
open FsUnit
open NUnit.Framework

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
        TemplateFile.Parse (strToStream fileContent)
        |> returnOrFail

    match result with
    | ProjectInfo _ ->
        Assert.Fail("File package detected as project package")
    | CompleteInfo (core, _) ->
        core.Id |> shouldEqual "My.Thing"
        core.Version |> shouldEqual v1
        core.Authors |> shouldEqual ["Bob McBob"]
        core.Description |> shouldEqual desc

[<Literal>]
let Invalid1 = """type fil
id My.Thing
version 1.0
authors Bob McBob
description A short description
"""

[<Literal>]
let Invalid2 = """type file
id My.Thing
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
[<TestCase(Invalid2)>]
[<TestCase(Invalid3)>]
let ``Invalid file input recognised as invalid`` (fileContent : string) =
    fileContent |> strToStream |> TemplateFile.Parse |> (function | Failure _ -> true | Success _ -> false)
    |> shouldEqual true

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
        fileContent |> strToStream |> TemplateFile.Parse
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
        text |> strToStream |> TemplateFile.Parse
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
        text |> strToStream |> TemplateFile.Parse
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
        TemplateFile.Parse (strToStream fileContent)
        |> returnOrFail

    match result with
    | CompleteInfo _ ->
        Assert.Fail("Project package detected as file package")
    | ProjectInfo (core, _) ->
        core.Id |> shouldEqual None
        core.Version |> shouldEqual None
        core.Authors |> shouldEqual None
        core.Description |> shouldEqual None
