module paket.dependenciesFile.RemovePackageSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain

[<Test>]
let ``should remove the right package``() = 
    let config = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget SignalR = 3.3.2"""

    let cfg = DependenciesFile.FromCode(config).Remove(Constants.MainDependencyGroup, PackageName "FAKE")
    
    let expected = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget SignalR = 3.3.2"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should remove only the correct package``() = 
    let config = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Castle.Windsor ~> 3.2"""

    let cfg = DependenciesFile.FromCode(config).Remove(Constants.MainDependencyGroup, PackageName "Castle.Windsor")
    
    let expected = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should remove only the correct package from the correct group``() = 
    let config = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Castle.Windsor ~> 3.2

group Test
nuget Castle.Windsor-log4net ~> 3.2
nuget Castle.Windsor ~> 3.2"""

    let cfg = DependenciesFile.FromCode(config).Remove(GroupName "Test", PackageName "Castle.Windsor")
    
    let expected = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Castle.Windsor ~> 3.2

group Test
nuget Castle.Windsor-log4net ~> 3.2"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should keep stable if package doesn't exist``() = 
    let config = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2"""

    let cfg = DependenciesFile.FromCode(config).Remove(Constants.MainDependencyGroup, PackageName "Castle.Windsor")

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings config)

[<Test>]
let ``should keep stable if group doesn't exist``() = 
    let config = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
group Build
nuget xUnit"""

    let cfg = DependenciesFile.FromCode(config).Remove(GroupName "Test", PackageName "Castle.Windsor")

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings config)