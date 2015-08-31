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

    let cfg = DependenciesFile.FromCode(config).Add(Constants.MainDependencyGroup, PackageName "xunit","")
    
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

    let cfg = DependenciesFile.FromCode(config).Add(Constants.MainDependencyGroup, PackageName "Rz","")
    
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

    let cfg = DependenciesFile.FromCode(config).Add(Constants.MainDependencyGroup, PackageName "xunit","")
    
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

    let cfg = DependenciesFile.FromCode(config).Add(Constants.MainDependencyGroup, PackageName "FAKE","~> 1.2")
    
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

    let cfg = DependenciesFile.FromCode(config).Add(Constants.MainDependencyGroup, PackageName "FAKE","1.2")
    
    let expected = """source http://nuget.org/api/v2
nuget Castle.Windsor-log4net ~> 3.2
nuget FAKE 1.2"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new packages even to empty package section``() = 
    let config = """github forki/FsUnit FsUnit.fs"""

    let cfg = DependenciesFile.FromCode(config).Add(Constants.MainDependencyGroup, PackageName "FAKE","~> 1.2")
    
    let expected = """source https://nuget.org/api/v2

nuget FAKE ~> 1.2

github forki/FsUnit FsUnit.fs"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new packages with nuget package resolution strategy``() = 
    let config = ""

    let cfg = DependenciesFile.FromCode(config).Add(Constants.MainDependencyGroup, PackageName "FAKE","!~> 1.2")
    
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

    DependenciesFile.FromCode(config).Add(Constants.MainDependencyGroup, PackageName "FAKE","") |> ignore
    
[<Test>]
let ``should not fail if package already exists - case insensitive``() = 
    let config = """source http://nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget SignalR = 3.3.2"""

    DependenciesFile.FromCode(config).Add(Constants.MainDependencyGroup, PackageName "fAKe","") |> ignore

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

nuget FAKE
nuget NUnit"""

    DependenciesFile.FromCode(before)
      .Add(Constants.MainDependencyGroup, PackageName "FAKE","")
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

    let cfg = DependenciesFile.FromCode(config).Add(Constants.MainDependencyGroup, PackageName "FsCheck","")
    
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

    let cfg = DependenciesFile.FromCode(config).Add(Constants.MainDependencyGroup, PackageName "Microsoft.AspNet.WebApi","")
    
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

    let cfg = DependenciesFile.FromCode(config).Add(Constants.MainDependencyGroup, PackageName "Microsoft.AspNet.WebApi","5.2.3")
    
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

    let cfg = DependenciesFile.FromCode(config).AddFixedPackage(Constants.MainDependencyGroup, PackageName "Microsoft.AspNet.WebApi.Client","5.2.3")
    
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

    let cfg = DependenciesFile.FromCode(config).Add(Constants.MainDependencyGroup, PackageName "Microsoft.AspNet.WebApi","5.2.3")
    
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

[<Test>]
let ``should pin down version requirement during add``() = 
    let config = """source https://nuget.org/api/v2
nuget Microsoft.AspNet.WebApi ~> 1.0"""

    let cfg = DependenciesFile.FromCode(config).AddFixedPackage(Constants.MainDependencyGroup, PackageName "Microsoft.AspNet.WebApi","1.0.071.9432")
    
    let expected = """source https://nuget.org/api/v2
nuget Microsoft.AspNet.WebApi 1.0.071.9432"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add Moq to second feed``() = 
    let config = """source http://internalfeed/NugetWebFeed/nuget

nuget log4net
nuget Microsoft.AspNet.WebApi.Client 5.2.3
nuget Microsoft.AspNet.WebApi.Core 5.2.3
nuget Microsoft.AspNet.WebApi.WebHost 5.2.3

source https://nuget.org/api/v2
nuget log4net 1.2.10
nuget Microsoft.AspNet.WebApi 5.2.1
"""

    let cfg = DependenciesFile.FromCode(config).Add(Constants.MainDependencyGroup, PackageName "Moq","")
    
    let expected = """source http://internalfeed/NugetWebFeed/nuget

nuget log4net
nuget Microsoft.AspNet.WebApi.Client 5.2.3
nuget Microsoft.AspNet.WebApi.Core 5.2.3
nuget Microsoft.AspNet.WebApi.WebHost 5.2.3

source https://nuget.org/api/v2
nuget log4net 1.2.10
nuget Microsoft.AspNet.WebApi 5.2.1
nuget Moq
"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add Microsoft.AspNet.WebApi package in first group``() = 
    let config = """source https://nuget.org/api/v2

group Build
nuget Moq"""

    let cfg = DependenciesFile.FromCode(config).Add(Constants.MainDependencyGroup, PackageName "Microsoft.AspNet.WebApi","")
    
    let expected = """source https://nuget.org/api/v2

nuget Microsoft.AspNet.WebApi

group Build
nuget Moq"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add Microsoft.AspNet.WebApi package in second group``() = 
    let config = """source https://nuget.org/api/v2

group Build
nuget Moq"""

    let cfg = DependenciesFile.FromCode(config).Add(GroupName "Build", PackageName "Microsoft.AspNet.WebApi","")
    
    let expected = """source https://nuget.org/api/v2

group Build
nuget Microsoft.AspNet.WebApi
nuget Moq"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add Microsoft.AspNet.WebApi package in first group in alphabetical pos``() = 
    let config = """source https://nuget.org/api/v2

nuget A
nuget Z

group Build
nuget Moq"""

    let cfg = DependenciesFile.FromCode(config).Add(Constants.MainDependencyGroup, PackageName "Microsoft.AspNet.WebApi","")
    
    let expected = """source https://nuget.org/api/v2

nuget A
nuget Microsoft.AspNet.WebApi
nuget Z

group Build
nuget Moq"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)


[<Test>]
let ``should add Microsoft.AspNet.WebApi package in second group in alphabetical pos``() = 
    let config = """source https://nuget.org/api/v2

nuget NUnit

group Build
nuget A
nuget Z"""

    let cfg = DependenciesFile.FromCode(config).Add(GroupName "Build", PackageName "Microsoft.AspNet.WebApi","")
    
    let expected = """source https://nuget.org/api/v2

nuget NUnit

group Build
nuget A
nuget Microsoft.AspNet.WebApi
nuget Z"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)