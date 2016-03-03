module Paket.ProjectJson.PropertySpecs

open Paket.ProjectJson
open NUnit.Framework
open FsUnit
open System.Collections.Generic
open Paket

let freshProject =
        """{
    "version": "1.0.0-*",
    "compilationOptions": {
        "emitEntryPoint": true
    },

    "dependencies": {
        "NETStandard.Library": "[1.0.0-rc2-23727]"
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

let TestApp =
        """{
    "version": "1.0.0-*",
    "compilationOptions": {
        "emitEntryPoint": true,
        "preserveCompilationContext": true
    },

    "dependencies": {
        "TestLibrary": { "target":"project", "version":"1.0.0-*" },

        "NETStandard.Library": "1.0.0-rc2-23811"
    },

    "frameworks": {
        "dnxcore50": { }
    }
}"""



[<Test>]
let ``can remove dependencies from fresh project.json``() = 
    let doc = ProjectJsonFile("",freshProject)
    let doc' = doc.WithDependencies []
    doc'.ToString() |> shouldEqual removed
    

[<Test>]
let ``can add simple dependency to fresh project.json``() = 
    let doc = ProjectJsonFile("",removed)
    let doc' = doc.WithDependencies ["NETStandard.Library", "1.0.0-rc2-23727"]
    doc'.ToString() |> normalizeLineEndings |> shouldEqual (freshProject |> normalizeLineEndings)
    
[<Test>]
let ``can add simple dependencies to fresh project.json``() = 

    let expected =
        """{
    "version": "1.0.0-*",
    "compilationOptions": {
        "emitEntryPoint": true
    },

    "dependencies": {
        "a": "[1.0.0]",
        "NETStandard.Library": "[1.0.0-rc2-23727]"
    },

    "frameworks": {
        "dnxcore50": { }
    }
}"""

    let doc = ProjectJsonFile("",removed)
    let doc' = doc.WithDependencies ["NETStandard.Library", "1.0.0-rc2-23727"; "a", "1.0.0"]
    doc'.ToString() |> normalizeLineEndings |> shouldEqual (expected |> normalizeLineEndings)
    
[<Test>]
let ``can add simple dependencies to empty project.json``() = 

    let expected =
        """{
    "dependencies": {
        "a": "[1.0.0]",
        "NETStandard.Library": "[1.0.0-rc2-23727]"
    }
}"""

    let doc = ProjectJsonFile("",empty)
    let doc' = doc.WithDependencies ["NETStandard.Library", "1.0.0-rc2-23727"; "a", "1.0.0"]
    doc'.ToString() |> normalizeLineEndings |> shouldEqual (expected |> normalizeLineEndings)

    
[<Test>]
let ``can extract dependencies from empty``() = 

    let doc = ProjectJsonFile("",empty)
    let deps = doc.GetGlobalDependencies()
    deps 
    |> shouldEqual []

[<Test>]
let ``can extract dependencies``() = 

    let original =
        """{
    "dependencies": {
        "a": "[1.0.0]",
        "NETStandard.Library": "[1.0.0-rc2-23727]"
    }
}"""

    let doc = ProjectJsonFile("",original)
    let deps = doc.GetGlobalDependencies()
    deps 
    |> List.map (fun (n,v) ->n.ToString(),v.ToString())
    |> shouldEqual ["a", "1.0.0"; "NETStandard.Library", "1.0.0-rc2-23727"]


[<Test>]
let ``can extract dependencies from TestApp``() = 

    let doc = ProjectJsonFile("",TestApp)
    let deps = doc.GetGlobalDependencies()
    deps 
    |> List.map (fun (n,v) ->n.ToString(),v.ToString())
    |> shouldEqual ["NETStandard.Library", ">= 1.0.0-rc2-23811"]

    let deps = doc.GetGlobalInterProjectDependencies()
    deps 
    |> List.map (fun x -> x.ToString())
    |> shouldEqual [""""TestLibrary": {"target":"project","version":"1.0.0-*"}"""]

[<Test>]
let ``can add simple dependencies to TestApp``() = 

    let expected =
        """{
    "version": "1.0.0-*",
    "compilationOptions": {
        "emitEntryPoint": true,
        "preserveCompilationContext": true
    },

    "dependencies": {
        "TestLibrary": {"target":"project","version":"1.0.0-*"},

        "a": "[1.0.0]",
        "NETStandard.Library": "[1.0.0-rc2-23727]"
    },

    "frameworks": {
        "dnxcore50": { }
    }
}"""

    let doc = ProjectJsonFile("",TestApp)
    let doc' = doc.WithDependencies ["NETStandard.Library", "1.0.0-rc2-23727"; "a", "1.0.0"]
    doc'.ToString() |> normalizeLineEndings |> shouldEqual (expected |> normalizeLineEndings)

[<Test>]
let ``can add simple dependencies to project.json without deps``() = 

    let original = """{
    "version": "1.0.0-*",
    "compilationOptions": {
        "emitEntryPoint": true
    },

    "frameworks": {
        "dnxcore50": { }
    }
}
"""

    let expected =
        """{
    "version": "1.0.0-*",
    "compilationOptions": {
        "emitEntryPoint": true
    },

    "frameworks": {
        "dnxcore50": { }
    },

    "dependencies": {
        "a": "[1.0.0]",
        "NETStandard.Library": "[1.0.0-rc2-23727]"
    }
}
"""

    let doc = ProjectJsonFile("",original)
    let doc' = doc.WithDependencies ["NETStandard.Library", "1.0.0-rc2-23727"; "a", "1.0.0"]
    doc'.ToString() |> normalizeLineEndings |> shouldEqual (expected |> normalizeLineEndings)

[<Test>]
let ``can extract dependencies from Argu``() = 

    let Argu =
        """{
    "version": "1.0.0-*",
    "compilationOptions": {
        "emitEntryPoint": true
    },

    "compilerName": "fsc",
    "compileFiles": [
        "Program.fs"
    ],

    "frameworks": {
        "dnxcore50" : { 
            "dependencies": {
                "Argu":  { "version": "1.0.0", "type": "build" }
            },
        },
        "net46" : { 
            "dependencies": {
                "Argu":  { "version": "1.0.0", "type": "build" }
            },
            "frameworkAssemblies": {
                "System": "",
                "System.Core": "",
            }
        }
    }
}
"""

    let doc = ProjectJsonFile("",Argu)
    let deps = doc.GetDependencies()
    deps.["dnxcore50"]
    |> shouldEqual []

    deps.["net46"]
    |> shouldEqual []

    let deps = doc.GetInterProjectDependencies()
    deps.["dnxcore50"]
    |> List.map (fun x -> x.ToString())
    |> shouldEqual [""""Argu": {"version":"1.0.0","type":"build"}"""]

    deps.["net46"]
    |> List.map (fun x -> x.ToString())
    |> shouldEqual [""""Argu": {"version":"1.0.0","type":"build"}"""]
    
    
[<Test>]
let ``does not replace frameworks in argu``() = 

    let original = """{
    "version": "1.0.0-*",
    "compilationOptions": {
        "emitEntryPoint": true
    },

    "compilerName": "fsc",
    "compileFiles": [
        "Program.fs"
    ],

    "frameworks": {
        "dnxcore50" : { 
            "dependencies": {
                "Argu":  { "version": "1.0.0", "type": "build" }
            },
        },
        "net46" : { 
            "dependencies": {
                "Argu":  { "version": "1.0.0", "type": "build" }
            },
            "frameworkAssemblies": {
                "System": "",
                "System.Core": "",
            }
        }
    }
}
"""

    let expected = """{
    "version": "1.0.0-*",
    "compilationOptions": {
        "emitEntryPoint": true
    },

    "compilerName": "fsc",
    "compileFiles": [
        "Program.fs"
    ],

    "frameworks": {
        "dnxcore50" : { 
            "dependencies": {
                "Argu":  { "version": "1.0.0", "type": "build" }
            },
        },
        "net46" : { 
            "dependencies": {
                "Argu":  { "version": "1.0.0", "type": "build" }
            },
            "frameworkAssemblies": {
                "System": "",
                "System.Core": "",
            }
        }
    },

    "dependencies": { }
}
"""

    let doc = ProjectJsonFile("",original)
    let doc' = doc.WithDependencies []
    doc'.ToString() |> normalizeLineEndings |> shouldEqual (expected |> normalizeLineEndings)