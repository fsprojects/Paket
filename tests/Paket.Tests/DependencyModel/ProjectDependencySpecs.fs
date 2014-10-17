module Paket.ProjectDependencySpecs

open Paket
open NUnit.Framework
open FsUnit

let config1 = """
source http://nuget.org/api/v2  username: "user" password: "pass"

nuget Castle.Windsor-log4net ~> 3.2
"""

[<Test>]
let ``project references for empty project should be empty``() = 
    let cfg = DependenciesFile.FromCode(config1)
    DependencyModel.CalcDependencies(cfg,[])
    |> shouldEqual Map.empty

[<Test>]
let ``project reference for single dependency should be found``() = 
    let cfg = DependenciesFile.FromCode(config1)
    let model = DependencyModel.CalcDependencies(cfg,["Castle.Windsor-log4net"])
    model.["Castle.Windsor-log4net"].Range |> shouldEqual (VersionRange.Between("3.2", "4.0"))

let config2 = """
source "http://nuget.org/api/v2

nuget Castle.Windsor-log4net == 3.2.1
nuget Rx-Main ~> 2.0"
nuget FAKE = 1.1
nuget SignalR = 3.3.2
"""

[<Test>]
let ``project reference for dependencies simple config should be found``() = 
    let cfg = DependenciesFile.FromCode(config2)
    let model = DependencyModel.CalcDependencies(cfg,["Castle.Windsor-log4net"; "FAKE"])

    
    model.["Castle.Windsor-log4net"].Range |> shouldEqual (VersionRange.OverrideAll(SemVer.Parse "3.2.1"))
    model.["FAKE"].Range |> shouldEqual (VersionRange.Exactly "1.1")
    model.ContainsKey("SignalR") |> shouldEqual false
    model.ContainsKey("Rx-Main") |> shouldEqual false