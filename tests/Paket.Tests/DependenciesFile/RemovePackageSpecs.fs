module Paket.DependenciesFile.RemovePackageSpecs

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

    let cfg = DependenciesFile.FromSource(config).Remove(Constants.MainDependencyGroup, PackageName "FAKE")
    
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

    let cfg = DependenciesFile.FromSource(config).Remove(Constants.MainDependencyGroup, PackageName "Castle.Windsor")
    
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

    let cfg = DependenciesFile.FromSource(config).Remove(GroupName "Test", PackageName "Castle.Windsor")
    
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

    let cfg = DependenciesFile.FromSource(config).Remove(Constants.MainDependencyGroup, PackageName "Castle.Windsor")

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings config)

[<Test>]
let ``should keep stable if group doesn't exist``() = 
    let config = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
group Build
nuget xUnit"""

    let cfg = DependenciesFile.FromSource(config).Remove(GroupName "Test", PackageName "Castle.Windsor")

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings config)

[<Test>]
let ``should also remove group if it is empty afterwards``() = 
    let config = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Castle.Windsor ~> 3.2

group Test
nuget Castle.Windsor ~> 3.2"""

    let cfg = DependenciesFile.FromSource(config).Remove(GroupName "Test", PackageName "Castle.Windsor")
    
    let expected = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Castle.Windsor ~> 3.2
"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should remove the package and keep the main group empty``() = 
    let config = """source http://www.nuget.org/api/v2

nuget Castle.Windsor ~> 3.2"""

    let cfg = DependenciesFile.FromSource(config).Remove(Constants.MainDependencyGroup, PackageName "Castle.Windsor")
    
    let expected = """source http://www.nuget.org/api/v2
"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should not remove group if only contains remote files``() = 
    let config = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Castle.Windsor ~> 3.2

group Test
http http://www.fssnip.net/1n decrypt.fs
nuget Castle.Windsor ~> 3.2"""

    let cfg = DependenciesFile.FromSource(config).Remove(GroupName "Test", PackageName "Castle.Windsor")
    
    let expected = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Castle.Windsor ~> 3.2

group Test
http http://www.fssnip.net/1n decrypt.fs"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)