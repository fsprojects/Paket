module paket.dependenciesFile.ParserSpecs

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
    let cfg = DependenciesFile.FromCode config1
    cfg.Strict |> shouldEqual false

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
    let cfg = DependenciesFile.FromCode config2
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
    let cfg = DependenciesFile.FromCode config3
    cfg.DirectDependencies.["Rx-Main"] |> shouldEqual (VersionRange.Between("2.2", "3.0"))
    (cfg.Packages |> List.find (fun p -> p.Name = "Rx-Main")).Sources |> List.head |> shouldEqual (Nuget Constants.DefaultNugetStream)
    cfg.DirectDependencies.["FAKE"] |> shouldEqual (VersionRange.Between("3.0", "4.0"))
    (cfg.Packages |> List.find (fun p -> p.Name = "FAKE")).Sources |> List.head  |> shouldEqual (Nuget Constants.DefaultNugetStream)

let config4 = """
source "http://nuget.org/api/v2" // first source

nuget "FAKE" "~> 3.0" 
source "http://nuget.org/api/v3" // second
nuget "Rx-Main" "~> 2.2"
nuget "MinPackage" "1.1.3"
"""

[<Test>]
let ``should read config with multiple sources``() = 
    let cfg = DependenciesFile.FromCode config4
    cfg.Strict |> shouldEqual false

    (cfg.Packages |> List.find (fun p -> p.Name = "Rx-Main")).Sources |> shouldEqual [Nuget "http://nuget.org/api/v3"; Nuget Constants.DefaultNugetStream]
    (cfg.Packages |> List.find (fun p -> p.Name = "MinPackage")).Sources |> shouldEqual [Nuget "http://nuget.org/api/v3"; Nuget Constants.DefaultNugetStream]
    (cfg.Packages |> List.find (fun p -> p.Name = "FAKE")).Sources |> shouldEqual [Nuget Constants.DefaultNugetStream]

[<Test>]
let ``should read source file from config``() =
    let config = """github "fsharp/FAKE:master" "src/app/FAKE/Cli.fs"
                    github "fsharp/FAKE:bla123zxc" "src/app/FAKE/FileWithCommit.fs" """
    let dependencies = DependenciesFile.FromCode config
    dependencies.RemoteFiles
    |> shouldEqual
        [ { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/Cli.fs"
            CommitSpecified = true
            Commit = "master" }
          { Owner = "fsharp"
            Project = "FAKE"
            CommitSpecified = true
            Name = "src/app/FAKE/FileWithCommit.fs"
            Commit = "bla123zxc" } ]

let strictConfig = """
references strict
source "http://nuget.org/api/v2" // first source

nuget "FAKE" "~> 3.0"
"""

[<Test>]
let ``should read strict config``() = 
    let cfg = DependenciesFile.FromCode strictConfig
    cfg.Strict |> shouldEqual true

    (cfg.Packages |> List.find (fun p -> p.Name = "FAKE")).Sources |> shouldEqual [Nuget "http://nuget.org/api/v2"]


let configWithoutQuotes = """
source http://nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget SignalR = 3.3.2
"""

[<Test>]
let ``should read config without quotes``() = 
    let cfg = DependenciesFile.FromCode configWithoutQuotes
    cfg.Strict |> shouldEqual false
    cfg.DirectDependencies.Count |> shouldEqual 4

    cfg.DirectDependencies.["Rx-Main"] |> shouldEqual (VersionRange.Between("2.0", "3.0"))
    cfg.DirectDependencies.["Castle.Windsor-log4net"] |> shouldEqual (VersionRange.Between("3.2", "4.0"))
    cfg.DirectDependencies.["FAKE"] |> shouldEqual (VersionRange.Exactly "1.1")
    cfg.DirectDependencies.["SignalR"] |> shouldEqual (VersionRange.Exactly "3.3.2")

let configWithoutQuotesButLotsOfWhiteSpace = """
source      http://nuget.org/api/v2

nuget   Castle.Windsor-log4net   ~>     3.2
nuget Rx-Main ~> 2.0
nuget FAKE =    1.1
nuget SignalR    = 3.3.2
"""

[<Test>]
let ``should read config without quotes but lots of whitespace``() = 
    let cfg = DependenciesFile.FromCode configWithoutQuotes
    cfg.Strict |> shouldEqual false
    cfg.DirectDependencies.Count |> shouldEqual 4

    cfg.DirectDependencies.["Rx-Main"] |> shouldEqual (VersionRange.Between("2.0", "3.0"))
    cfg.DirectDependencies.["Castle.Windsor-log4net"] |> shouldEqual (VersionRange.Between("3.2", "4.0"))
    cfg.DirectDependencies.["FAKE"] |> shouldEqual (VersionRange.Exactly "1.1")
    cfg.DirectDependencies.["SignalR"] |> shouldEqual (VersionRange.Exactly "3.3.2")


[<Test>]
let ``should read github source file from config without quotes``() =
    let config = """github fsharp/FAKE:master   src/app/FAKE/Cli.fs
                    github    fsharp/FAKE:bla123zxc src/app/FAKE/FileWithCommit.fs """
    let dependencies = DependenciesFile.FromCode config
    dependencies.RemoteFiles
    |> shouldEqual
        [ { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/Cli.fs"
            CommitSpecified = true
            Commit = "master" }
          { Owner = "fsharp"
            Project = "FAKE"
            CommitSpecified = true
            Name = "src/app/FAKE/FileWithCommit.fs"
            Commit = "bla123zxc" } ]

let configWithoutVersions = """
source "http://nuget.org/api/v2"

nuget Castle.Windsor-log4net
nuget Rx-Main
nuget "FAKE"
"""

[<Test>]
let ``should read config without versions``() = 
    let cfg = DependenciesFile.FromCode configWithoutVersions

    cfg.DirectDependencies.["Rx-Main"] |> shouldEqual (VersionRange.AtLeast "0")
    cfg.DirectDependencies.["Castle.Windsor-log4net"] |> shouldEqual (VersionRange.AtLeast "0")
    cfg.DirectDependencies.["FAKE"] |> shouldEqual (VersionRange.AtLeast "0")