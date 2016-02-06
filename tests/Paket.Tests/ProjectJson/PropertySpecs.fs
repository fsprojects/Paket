module Paket.ProjectJson.PropertySpecs

open Paket.ProjectJson
open NUnit.Framework
open FsUnit
open System.Collections.Generic

let freshProject =
        """{
    "version": "1.0.0-*",
    "compilationOptions": {
        "emitEntryPoint": true
    },

    "dependencies": {
        "NETStandard.Library": "1.0.0-rc2-23727"
    },

    "frameworks": {
        "dnxcore50": { }
    }
}"""

let empty = """{ }"""

let removed =
        """{
    "version": "1.0.0-*",
    "compilationOptions": {
        "emitEntryPoint": true
    },

    "dependencies": { },

    "frameworks": {
        "dnxcore50": { }
    }
}"""


[<Test>]
let ``can remove dependencies from fresh project.json``() = 

    let doc = ProjectJsonProject("",freshProject)
    let doc' = doc.WithDependencies []
    doc'.ToString() |> shouldEqual removed
    

[<Test>]
let ``can add simple dependency to fresh project.json``() = 



    let doc = ProjectJsonProject("",removed)
    let doc' = doc.WithDependencies ["NETStandard.Library", "1.0.0-rc2-23727"]
    doc'.ToString() |> shouldEqual freshProject
    
[<Test>]
let ``can add simple dependencies to fresh project.json``() = 

    let expected =
        """{
    "version": "1.0.0-*",
    "compilationOptions": {
        "emitEntryPoint": true
    },

    "dependencies": {
        "a": "1.0.0",
        "NETStandard.Library": "1.0.0-rc2-23727"
    },

    "frameworks": {
        "dnxcore50": { }
    }
}"""

    let doc = ProjectJsonProject("",removed)
    let doc' = doc.WithDependencies ["NETStandard.Library", "1.0.0-rc2-23727"; "a", "1.0.0"]
    doc'.ToString() |> shouldEqual expected
    
[<Test>]
let ``can add simple dependencies to empty project.json``() = 

    let expected =
        """{ "dependencies": {
     "a": "1.0.0",
     "NETStandard.Library": "1.0.0-rc2-23727"
 }}"""

    let doc = ProjectJsonProject("",empty)
    let doc' = doc.WithDependencies ["NETStandard.Library", "1.0.0-rc2-23727"; "a", "1.0.0"]
    doc'.ToString() |> shouldEqual expected
    