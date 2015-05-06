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

nuget FAKE
nuget quicksilver
nuget FsCheck

source https://www.nuget.org/api/v3

nuget NUnit"""

    DependenciesFile.FromCode(before)
      .Add(PackageName "FAKE","")
      .ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should update packages with new version``() = 
    let config = """source https://nuget.org/api/v2

nuget FAKE 1.1
"""

    let cfg = DependenciesFile.FromCode(config).UpdatePackageVersion(PackageName "FAKE","1.2")
    
    let expected = """source https://nuget.org/api/v2

nuget FAKE 1.2
"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should update packages with nuget package resolution strategy``() = 
    let config = """source https://nuget.org/api/v2

nuget FAKE ~> 1.1
"""

    let cfg = DependenciesFile.FromCode(config).UpdatePackageVersion(PackageName "FAKE","!~> 1.2")
    
    let expected = """source https://nuget.org/api/v2

nuget FAKE !~> 1.2
"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)


[<Test>]
let ``should add FsCheck package in first position (if smaller than first)``() = 
    let config = """source https://nuget.org/api/v2

nuget Newtonsoft.Json
nuget UnionArgParser
nuget NUnit.Runners
nuget NUnit
nuget FAKE
nuget FSharp.Formatting
nuget FSharp.Core

github forki/FsUnit FsUnit.fs
github fsharp/FAKE modules/Octokit/Octokit.fsx
github fsharp/FAKE src/app/FakeLib/Globbing/Globbing.fs
github fsprojects/Chessie src/Chessie/ErrorHandling.fs"""

    let cfg = DependenciesFile.FromCode(config).Add(PackageName "FsCheck","")
    
    let expected = """source https://nuget.org/api/v2

nuget FsCheck
nuget Newtonsoft.Json
nuget UnionArgParser
nuget NUnit.Runners
nuget NUnit
nuget FAKE
nuget FSharp.Formatting
nuget FSharp.Core

github forki/FsUnit FsUnit.fs
github fsharp/FAKE modules/Octokit/Octokit.fsx
github fsharp/FAKE src/app/FakeLib/Globbing/Globbing.fs
github fsprojects/Chessie src/Chessie/ErrorHandling.fs"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add Microsoft.AspNet.WebApi package in first position if only source is given``() = 
    let config = """source https://nuget.org/api/v2"""

    let cfg = DependenciesFile.FromCode(config).Add(PackageName "Microsoft.AspNet.WebApi","")
    
    let expected = """source https://nuget.org/api/v2

nuget Microsoft.AspNet.WebApi"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add Microsoft.AspNet.WebApi package in correct position if package is already given``() = 
    let config = """source http://internalfeed/NugetWebFeed/nuget

nuget Microsoft.AspNet.WebApi.Client 5.2.3
nuget Microsoft.AspNet.WebApi.Core 5.2.3
nuget Microsoft.AspNet.WebApi.WebHost 5.2.3
nuget log4net

source https://nuget.org/api/v2
nuget Microsoft.AspNet.WebApi
nuget log4net 1.2.10"""

    let cfg = DependenciesFile.FromCode(config).Add(PackageName "Microsoft.AspNet.WebApi","5.2.3")
    
    let expected = """source http://internalfeed/NugetWebFeed/nuget

nuget Microsoft.AspNet.WebApi.Client 5.2.3
nuget Microsoft.AspNet.WebApi.Core 5.2.3
nuget Microsoft.AspNet.WebApi.WebHost 5.2.3
nuget log4net

source https://nuget.org/api/v2
nuget Microsoft.AspNet.WebApi 5.2.3
nuget log4net 1.2.10"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should pin Microsoft.AspNet.WebApi.Client package in correct position``() = 
    let config = """source http://internalfeed/NugetWebFeed/nuget

nuget Microsoft.AspNet.WebApi.Core 5.2.3
nuget Microsoft.AspNet.WebApi.WebHost 5.2.3
nuget log4net

source https://nuget.org/api/v2
nuget Microsoft.AspNet.WebApi
nuget log4net 1.2.10"""

    let cfg = DependenciesFile.FromCode(config).AddFixedPackage(PackageName "Microsoft.AspNet.WebApi.Client","5.2.3")
    
    let expected = """source http://internalfeed/NugetWebFeed/nuget

nuget Microsoft.AspNet.WebApi.Core 5.2.3
nuget Microsoft.AspNet.WebApi.WebHost 5.2.3
nuget log4net

source https://nuget.org/api/v2
nuget Microsoft.AspNet.WebApi
nuget log4net 1.2.10
nuget Microsoft.AspNet.WebApi.Client 5.2.3"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)


[<Test>]
let ``should add Microsoft.AspNet.WebApi package in correct position if package is already given with version``() = 
    let config = """source http://internalfeed/NugetWebFeed/nuget

nuget Microsoft.AspNet.WebApi.Client 5.2.3
nuget Microsoft.AspNet.WebApi.Core 5.2.3
nuget Microsoft.AspNet.WebApi.WebHost 5.2.3
nuget log4net

source https://nuget.org/api/v2
nuget Microsoft.AspNet.WebApi 5.2.1
nuget log4net 1.2.10"""

    let cfg = DependenciesFile.FromCode(config).Add(PackageName "Microsoft.AspNet.WebApi","5.2.3")
    
    let expected = """source http://internalfeed/NugetWebFeed/nuget

nuget Microsoft.AspNet.WebApi.Client 5.2.3
nuget Microsoft.AspNet.WebApi.Core 5.2.3
nuget Microsoft.AspNet.WebApi.WebHost 5.2.3
nuget log4net

source https://nuget.org/api/v2
nuget Microsoft.AspNet.WebApi 5.2.1
nuget Microsoft.AspNet.WebApi 5.2.3
nuget log4net 1.2.10"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)