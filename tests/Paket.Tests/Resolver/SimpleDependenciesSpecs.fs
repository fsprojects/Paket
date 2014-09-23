module Paket.Resolver.SimpleDependenciesSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

let config1 = """
source "http://nuget.org/api/v2"

nuget "Castle.Windsor-log4net" "~> 3.2"
nuget "Rx-Main" "~> 2.0"
"""

let graph = [
    "Castle.Windsor-log4net","3.2",[]
    "Castle.Windsor-log4net","3.3",["Castle.Windsor",VersionRange.AtLeast "2.0";"log4net",VersionRange.AtLeast "1.0"]
    "Castle.Windsor","2.0",[]
    "Castle.Windsor","2.1",[]
    "Rx-Main","2.0",["Rx-Core",VersionRange.AtLeast "2.1"]
    "Rx-Core","2.0",[]
    "Rx-Core","2.1",[]
    "log4net","1.0",["log",VersionRange.AtLeast "1.0"]
    "log4net","1.1",["log",VersionRange.AtLeast "1.0"]
    "log","1.0",[]
    "log","1.2",[]
]

[<Test>]
let ``should resolve simple config1``() = 
    let cfg = DependenciesFile.FromCode(fakeSha1,config1)
    let resolved = cfg.Resolve(VersionsFromGraph graph, PackageDetailsFromGraph graph) |> UpdateProcess.getResolvedPackagesOrFail
    getVersion resolved.["Rx-Main"] |> shouldEqual "2.0"
    getVersion resolved.["Rx-Core"] |> shouldEqual "2.1"
    getVersion resolved.["Castle.Windsor-log4net"] |> shouldEqual "3.3"
    getVersion resolved.["Castle.Windsor"] |> shouldEqual "2.1"
    getVersion resolved.["log4net"] |> shouldEqual "1.1"
    getVersion resolved.["log"] |> shouldEqual "1.2"
    getSource resolved.["log"] |> shouldEqual (Nuget Constants.DefaultNugetStream)


let config2 = """
source "http://nuget.org/api/v2"

nuget NUnit ~> 2.6
nuget FsUnit ~> 1.3
"""

let graph2 = [
    "FsUnit","1.3.1",["NUnit",DependenciesFileParser.parseVersionRange ">= 2.6.3"]
    "NUnit","2.6.2",[]
    "NUnit","2.6.3",[]    
]

[<Test>]
let ``should resolve simple config2``() = 
    let cfg = DependenciesFile.FromCode(fakeSha1,config2)
    let resolved = cfg.Resolve(VersionsFromGraph graph2, PackageDetailsFromGraph graph2) |> UpdateProcess.getResolvedPackagesOrFail
    getVersion resolved.["FsUnit"] |> shouldEqual "1.3.1"
    getVersion resolved.["NUnit"] |> shouldEqual "2.6.3"

let config3 = """
source "http://nuget.org/api/v2"

nuget "Castle.Core" "= 3.2.0"
nuget "Castle.Windsor-log4net" "= 3.2.0.1"
"""

let graph3 = [
    "Castle.Core","3.2.0",[]
    "Castle.Core","3.3.0",[]
    "Castle.Core","3.3.1",[]
    "Castle.Windsor-log4net","3.2.0.1",["Castle.Core-log4net",DependenciesFileParser.parseVersionRange ">= 3.2.0"]
    "Castle.Core-log4net","3.2.0",["Castle.Core",DependenciesFileParser.parseVersionRange ">= 3.2.0"]
    "Castle.Core-log4net","3.3.0",["Castle.Core",DependenciesFileParser.parseVersionRange ">= 3.3.0"]
    "Castle.Core-log4net","3.3.1",["Castle.Core",DependenciesFileParser.parseVersionRange ">= 3.3.1"]
]

[<Test>]
let ``should resolve fixed config``() = 
    let cfg = DependenciesFile.FromCode(fakeSha1,config3)
    let resolved = cfg.Resolve(VersionsFromGraph graph3, PackageDetailsFromGraph graph3) |> UpdateProcess.getResolvedPackagesOrFail
    getVersion resolved.["Castle.Core"] |> shouldEqual "3.2.0"
    getVersion resolved.["Castle.Windsor-log4net"] |> shouldEqual "3.2.0.1"
    getVersion resolved.["Castle.Core-log4net"] |> shouldEqual "3.2.0"


let config4 = """
source "http://nuget.org/api/v2"

nuget "Castle.Core" "= 3.2.0"
nuget "Castle.Windsor-log4net" "~> 3.2"
"""

[<Test>]
let ``should resolve fixed config4``() = 
    let cfg = DependenciesFile.FromCode(fakeSha1,config4)
    let resolved = cfg.Resolve(VersionsFromGraph graph3, PackageDetailsFromGraph graph3) |> UpdateProcess.getResolvedPackagesOrFail
    getVersion resolved.["Castle.Core"] |> shouldEqual "3.2.0"
    getVersion resolved.["Castle.Windsor-log4net"] |> shouldEqual "3.2.0.1"
    getVersion resolved.["Castle.Core-log4net"] |> shouldEqual "3.2.0"