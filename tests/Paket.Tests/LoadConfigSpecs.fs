module Paket.LoadConfigSpecs

open Paket
open NUnit.Framework
open FsUnit

let config1 = """
source "http://nuget.org/api/v2"

nuget "Castle.Windsor-log4net" "~> 3.2"
nuget "Rx-Main" "~> 2.0"
nuget "FAKE" "= 1.1"
nuget "SignalR" "= 3.3.2"
"""

[<Test>]
let ``should read simple config``() = 
    let cfg = Config.FromCode config1
    cfg.DirectDependencies.["Rx-Main"] |> shouldEqual (VersionRange.Between("2.0", "3.0"))
    cfg.DirectDependencies.["Castle.Windsor-log4net"] |> shouldEqual (VersionRange.Between("3.2", "4.0"))
    cfg.DirectDependencies.["FAKE"] |> shouldEqual (VersionRange.Exactly "1.1")
    cfg.DirectDependencies.["SignalR"] |> shouldEqual (VersionRange.Exactly "3.3.2")

let config2 = """
source "http://nuget.org/api/v2"

printfn "hello world from config"

nuget "FAKE" "~> 3.0"
nuget "Rx-Main" "~> 2.2"
nuget "MinPackage" "1.1.3"
"""

[<Test>]
let ``should read simple config with additional F# code``() = 
    let cfg = Config.FromCode config2
    cfg.DirectDependencies.["Rx-Main"] |> shouldEqual (VersionRange.Between("2.2", "3.0"))
    cfg.DirectDependencies.["FAKE"] |> shouldEqual (VersionRange.Between("3.0", "4.0"))
    cfg.DirectDependencies.["MinPackage"] |> shouldEqual (VersionRange.Exactly "1.1.3")

let config3 = """
source "http://nuget.org/api/v2" // here we are

nuget "FAKE" "~> 3.0" // born to rule
nuget "Rx-Main" "~> 2.2"
nuget "MinPackage" "1.1.3"
"""

[<Test>]
let ``should read simple config with comments``() = 
    let cfg = Config.FromCode config3
    cfg.DirectDependencies.["Rx-Main"] |> shouldEqual (VersionRange.Between("2.2", "3.0"))
    (cfg.Packages |> List.find (fun p -> p.Name = "Rx-Main")).Source |> shouldEqual (Nuget "http://nuget.org/api/v2")
    cfg.DirectDependencies.["FAKE"] |> shouldEqual (VersionRange.Between("3.0", "4.0"))
    (cfg.Packages |> List.find (fun p -> p.Name = "FAKE")).Source |> shouldEqual (Nuget "http://nuget.org/api/v2")