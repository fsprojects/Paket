module paket.dependenciesFile.ParserSpecs

open Paket
open Paket.PackageSources
open NUnit.Framework
open FsUnit
open TestHelpers
open System

[<Test>]
let ``should read emptx config``() = 
    let cfg = DependenciesFile.FromCode("")
    cfg.Options.Strict |> shouldEqual false

    cfg.Packages.Length |> shouldEqual 0
    cfg.RemoteFiles.Length |> shouldEqual 0

let config1 = """
source "http://nuget.org/api/v2"

nuget "Castle.Windsor-log4net" "~> 3.2"
nuget "Rx-Main" "~> 2.0"
nuget "FAKE" "= 1.1"
nuget "SignalR" "= 3.3.2"
"""

[<Test>]
let ``should read simple config``() = 
    let cfg = DependenciesFile.FromCode(config1)
    cfg.Options.Strict |> shouldEqual false

    cfg.DirectDependencies.["Rx-Main"].Range |> shouldEqual (VersionRange.Between("2.0", "3.0"))
    cfg.DirectDependencies.["Castle.Windsor-log4net"].Range |> shouldEqual (VersionRange.Between("3.2", "4.0"))
    cfg.DirectDependencies.["FAKE"].Range |> shouldEqual (VersionRange.Exactly "1.1")
    cfg.DirectDependencies.["SignalR"].Range |> shouldEqual (VersionRange.Exactly "3.3.2")

let config2 = """
source "http://nuget.org/api/v2"

printfn "hello world from config"

nuget "FAKE" "~> 3.0"
nuget "Rx-Main" "~> 2.2"
nuget "MinPackage" "1.1.3"
"""

[<Test>]
let ``should read simple config with additional F# code``() = 
    let cfg = DependenciesFile.FromCode(config2)
    cfg.DirectDependencies.["Rx-Main"].Range |> shouldEqual (VersionRange.Between("2.2", "3.0"))
    cfg.DirectDependencies.["FAKE"].Range |> shouldEqual (VersionRange.Between("3.0", "4.0"))
    cfg.DirectDependencies.["MinPackage"].Range |> shouldEqual (VersionRange.Exactly "1.1.3")

let config3 = """
source "http://nuget.org/api/v2" // here we are

nuget "FAKE" "~> 3.0" // born to rule
nuget "Rx-Main" "~> 2.2"
nuget "MinPackage" "1.1.3"
"""

[<Test>]
let ``should read simple config with comments``() = 
    let cfg = DependenciesFile.FromCode(config3)
    cfg.DirectDependencies.["Rx-Main"].Range |> shouldEqual (VersionRange.Between("2.2", "3.0"))
    (cfg.Packages |> List.find (fun p -> p.Name = "Rx-Main")).Sources |> List.head |> shouldEqual PackageSources.DefaultNugetSource
    cfg.DirectDependencies.["FAKE"].Range |> shouldEqual (VersionRange.Between("3.0", "4.0"))
    (cfg.Packages |> List.find (fun p -> p.Name = "FAKE")).Sources |> List.head  |> shouldEqual PackageSources.DefaultNugetSource

let config4 = """
source "http://nuget.org/api/v2" // first source

nuget "FAKE" "~> 3.0" 
source "http://nuget.org/api/v3" // second
nuget "Rx-Main" "~> 2.2"
nuget "MinPackage" "1.1.3"
"""

[<Test>]
let ``should read config with multiple sources``() = 
    let cfg = DependenciesFile.FromCode(config4)
    cfg.Options.Strict |> shouldEqual false

    (cfg.Packages |> List.find (fun p -> p.Name = "Rx-Main")).Sources |> shouldEqual [PackageSource.NugetSource "http://nuget.org/api/v3"; PackageSources.DefaultNugetSource]
    (cfg.Packages |> List.find (fun p -> p.Name = "MinPackage")).Sources |> shouldEqual [PackageSource.NugetSource "http://nuget.org/api/v3"; PackageSources.DefaultNugetSource]
    (cfg.Packages |> List.find (fun p -> p.Name = "FAKE")).Sources |> shouldEqual [PackageSources.DefaultNugetSource]

[<Test>]
let ``should read source file from config``() =
    let config = """github "fsharp/FAKE:master" "src/app/FAKE/Cli.fs"
                    github "fsharp/FAKE:bla123zxc" "src/app/FAKE/FileWithCommit.fs" """
    let dependencies = DependenciesFile.FromCode(config)
    dependencies.RemoteFiles
    |> shouldEqual
        [ { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/Cli.fs"
            Commit = Some "master" }
          { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/FileWithCommit.fs"
            Commit = Some "bla123zxc" } ]

let strictConfig = """
references strict
source "http://nuget.org/api/v2" // first source

nuget "FAKE" "~> 3.0"
"""

[<Test>]
let ``should read strict config``() = 
    let cfg = DependenciesFile.FromCode(strictConfig)
    cfg.Options.Strict |> shouldEqual true

    (cfg.Packages |> List.find (fun p -> p.Name = "FAKE")).Sources |> shouldEqual [PackageSource.NugetSource "http://nuget.org/api/v2"]

let noneContentConfig = """
content none
source "http://nuget.org/api/v2" // first source

nuget "Microsoft.SqlServer.Types"
"""

[<Test>]
let ``should read content none config``() = 
    let cfg = DependenciesFile.FromCode(noneContentConfig)
    cfg.Options.OmitContent |> shouldEqual true

    (cfg.Packages |> List.find (fun p -> p.Name = "Microsoft.SqlServer.Types")).Sources |> shouldEqual [PackageSource.NugetSource "http://nuget.org/api/v2"]

let configWithoutQuotes = """
source http://nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget SignalR = 3.3.2
"""

[<Test>]
let ``should read config without quotes``() = 
    let cfg = DependenciesFile.FromCode(configWithoutQuotes)
    cfg.Options.Strict |> shouldEqual false
    cfg.DirectDependencies.Count |> shouldEqual 4

    cfg.DirectDependencies.["Rx-Main"].Range |> shouldEqual (VersionRange.Between("2.0", "3.0"))
    cfg.DirectDependencies.["Castle.Windsor-log4net"].Range |> shouldEqual (VersionRange.Between("3.2", "4.0"))
    cfg.DirectDependencies.["FAKE"].Range |> shouldEqual (VersionRange.Exactly "1.1")
    cfg.DirectDependencies.["SignalR"].Range |> shouldEqual (VersionRange.Exactly "3.3.2")

let configWithoutQuotesButLotsOfWhiteSpace = """
source      http://nuget.org/api/v2

nuget   Castle.Windsor-log4net   ~>     3.2
nuget Rx-Main ~> 2.0
nuget FAKE =    1.1
nuget SignalR    = 3.3.2
"""

[<Test>]
let ``should read config without quotes but lots of whitespace``() = 
    let cfg = DependenciesFile.FromCode(configWithoutQuotes)
    cfg.Options.Strict |> shouldEqual false
    cfg.DirectDependencies.Count |> shouldEqual 4

    cfg.DirectDependencies.["Rx-Main"].Range |> shouldEqual (VersionRange.Between("2.0", "3.0"))
    cfg.DirectDependencies.["Castle.Windsor-log4net"].Range |> shouldEqual (VersionRange.Between("3.2", "4.0"))
    cfg.DirectDependencies.["FAKE"].Range |> shouldEqual (VersionRange.Exactly "1.1")
    cfg.DirectDependencies.["SignalR"].Range |> shouldEqual (VersionRange.Exactly "3.3.2")


[<Test>]
let ``should read github source file from config without quotes``() =
    let config = """github fsharp/FAKE:master   src/app/FAKE/Cli.fs
                    github    fsharp/FAKE:bla123zxc src/app/FAKE/FileWithCommit.fs """
    let dependencies = DependenciesFile.FromCode(config)
    dependencies.RemoteFiles
    |> shouldEqual
        [ { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/Cli.fs"
            Commit = Some "master" }
          { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/FileWithCommit.fs"
            Commit = Some "bla123zxc" } ]

[<Test>]
let ``should read github source files withou sha1``() =
    let config = """github fsharp/FAKE  src/app/FAKE/Cli.fs
                    github    fsharp/FAKE:bla123zxc src/app/FAKE/FileWithCommit.fs """
    let dependencies = DependenciesFile.FromCode(config)
    dependencies.RemoteFiles
    |> shouldEqual
        [ { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/Cli.fs"
            Commit = None }
          { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/FileWithCommit.fs"
            Commit = Some "bla123zxc" } ]

let configWithoutVersions = """
source "http://nuget.org/api/v2"

nuget Castle.Windsor-log4net
nuget Rx-Main
nuget "FAKE"
"""

[<Test>]
let ``should read config without versions``() = 
    let cfg = DependenciesFile.FromCode(configWithoutVersions)

    cfg.DirectDependencies.["Rx-Main"] .Range|> shouldEqual (VersionRange.AtLeast "0")
    cfg.DirectDependencies.["Castle.Windsor-log4net"].Range |> shouldEqual (VersionRange.AtLeast "0")
    cfg.DirectDependencies.["FAKE"].Range |> shouldEqual (VersionRange.AtLeast "0")


let configWithPassword = """
source http://nuget.org/api/v2 username: "tatü tata" password: "you got hacked!"
nuget Rx-Main
"""

[<Test>]
let ``should read config with encapsulated password source``() = 
    let cfg = DependenciesFile.FromCode( configWithPassword)
    
    (cfg.Packages |> List.find (fun p -> p.Name = "Rx-Main")).Sources 
    |> shouldEqual [ PackageSource.Nuget { Url = "http://nuget.org/api/v2"
                                           Auth = 
                                               Some { Username = "tatü tata"
                                                      Password = "you got hacked!" } } ]

let configWithPasswordInSingleQuotes = """
source http://nuget.org/api/v2 username: 'tatü tata' password: 'you got hacked!'
nuget Rx-Main
"""

[<Test>]
let ``should read config with single-quoted password source``() = 
    try
        DependenciesFile.FromCode( configWithPasswordInSingleQuotes) |> ignore
        failwith "Expected error"
    with
    | exn when exn.Message <> "Expected error" -> ()

let configWithPasswordInEnvVariable = """
source http://nuget.org/api/v2 username: "%FEED_USERNAME%" password: "%FEED_PASSWORD%"
nuget Rx-Main
"""

[<Test>]
let ``should read config with password in env variable``() = 
    Environment.SetEnvironmentVariable("FEED_USERNAME", "user XYZ", EnvironmentVariableTarget.Process)
    Environment.SetEnvironmentVariable("FEED_PASSWORD", "pw Love", EnvironmentVariableTarget.Process)
    let cfg = DependenciesFile.FromCode( configWithPasswordInEnvVariable)
    
    (cfg.Packages |> List.find (fun p -> p.Name = "Rx-Main")).Sources 
    |> shouldEqual [ PackageSource.Nuget { Url = "http://nuget.org/api/v2"
                                           Auth = 
                                               Some { Username = "user XYZ"
                                                      Password = "pw Love" } } ]