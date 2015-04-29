module paket.dependenciesFile.AddPackageSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain

[<Test>]
let ``should add new packages to the end``() = 
    let config = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget SignalR = 3.3.2"""

    let cfg = DependenciesFile.FromCode(config).Add(PackageName "xunit","")
    
    let expected = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget SignalR = 3.3.2
nuget xunit"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new packages to alphabetical position``() = 
    let config = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget FAKE = 1.1
nuget Rx-Main ~> 2.0
nuget SignalR = 3.3.2"""

    let cfg = DependenciesFile.FromCode(config).Add(PackageName "Rz","")
    
    let expected = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget FAKE = 1.1
nuget Rx-Main ~> 2.0
nuget Rz
nuget SignalR = 3.3.2"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new packages before github files``() = 
    let config = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget NuGet.CommandLine

github forki/FsUnit FsUnit.fs"""

    let cfg = DependenciesFile.FromCode(config).Add(PackageName "xunit","")
    
    let expected = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget NuGet.CommandLine
nuget xunit

github forki/FsUnit FsUnit.fs"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)


[<Test>]
let ``should add new packages with ~> version if we give it``() = 
    let config = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2

github forki/FsUnit FsUnit.fs"""

    let cfg = DependenciesFile.FromCode(config).Add(PackageName "FAKE","~> 1.2")
    
    let expected = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget FAKE ~> 1.2

github forki/FsUnit FsUnit.fs"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new packages with specific version if we give it``() = 
    let config = """source http://nuget.org/api/v2
nuget Castle.Windsor-log4net ~> 3.2"""

    let cfg = DependenciesFile.FromCode(config).Add(PackageName "FAKE","1.2")
    
    let expected = """source http://nuget.org/api/v2
nuget Castle.Windsor-log4net ~> 3.2
nuget FAKE 1.2"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new packages even to empty package section``() = 
    let config = """github forki/FsUnit FsUnit.fs"""

    let cfg = DependenciesFile.FromCode(config).Add(PackageName "FAKE","~> 1.2")
    
    let expected = """source https://nuget.org/api/v2

nuget FAKE ~> 1.2

github forki/FsUnit FsUnit.fs"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new packages with nuget package resolution strategy``() = 
    let config = ""

    let cfg = DependenciesFile.FromCode(config).Add(PackageName "FAKE","!~> 1.2")
    
    let expected = """source https://nuget.org/api/v2

nuget FAKE !~> 1.2
"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)


[<Test>]
let ``should not fail if package already exists``() = 
    let config = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget SignalR = 3.3.2"""

    DependenciesFile.FromCode(config).Add(PackageName "FAKE","") |> ignore
    
[<Test>]
let ``should not fail if package already exists - case insensitive``() = 
    let config = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget SignalR = 3.3.2"""

    DependenciesFile.FromCode(config).Add(PackageName "fAKe","") |> ignore

[<Test>]
let ``should keep sources stable``() = 
    let before = """source https://www.nuget.org/api/v2

nuget quicksilver
nuget FsCheck

source https://www.nuget.org/api/v3

nuget NUnit"""

    let expected = """source https://www.nuget.org/api/v2

nuget quicksilver
nuget FsCheck

source https://www.nuget.org/api/v3

nuget NUnit
nuget FAKE"""

    DependenciesFile.FromCode(before)
      .Add(PackageName "FAKE","")
      .ToString()
    |> shouldEqual (normalizeLineEndings expected)