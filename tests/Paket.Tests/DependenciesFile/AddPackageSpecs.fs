module Paket.DependenciesFile.AddPackageSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.Requirements

[<Test>]
let ``should add new packages to the end``() = 
    let config = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget SignalR = 3.3.2"""

    let cfg = DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "xunit","")
    
    let expected = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget SignalR = 3.3.2
nuget xunit"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new packages to alphabetical position``() = 
    let config = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget FAKE = 1.1
nuget Rx-Main ~> 2.0
nuget SignalR = 3.3.2"""

    let cfg = DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "Rz","")
    
    let expected = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget FAKE = 1.1
nuget Rx-Main ~> 2.0
nuget Rz
nuget SignalR = 3.3.2"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new packages before github files``() = 
    let config = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget NuGet.CommandLine

github forki/FsUnit FsUnit.fs"""

    let cfg = DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "xunit","")
    
    let expected = """source http://www.nuget.org/api/v2

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
    let config = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2

github forki/FsUnit FsUnit.fs"""

    let cfg = DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "FAKE","~> 1.2")
    
    let expected = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget FAKE ~> 1.2

github forki/FsUnit FsUnit.fs"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new packages with specific version if we give it``() = 
    let config = """source http://www.nuget.org/api/v2
nuget Castle.Windsor-log4net ~> 3.2"""

    let cfg = DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "FAKE","1.2")
    
    let expected = """source http://www.nuget.org/api/v2
nuget Castle.Windsor-log4net ~> 3.2
nuget FAKE 1.2"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new packages even to empty package section``() = 
    let config = """github forki/FsUnit FsUnit.fs"""

    let cfg = DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "FAKE","~> 1.2")
    
    let expected = """source https://www.nuget.org/api/v2

nuget FAKE ~> 1.2

github forki/FsUnit FsUnit.fs"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new packages with nuget package resolution strategy``() = 
    let config = ""

    let cfg = DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "FAKE","!~> 1.2")
    
    let expected = """source https://www.nuget.org/api/v2

nuget FAKE !~> 1.2
"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)


[<Test>]
let ``should not fail if package already exists``() = 
    let config = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget SignalR = 3.3.2"""

    DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "FAKE","") |> ignore
    
[<Test>]
let ``should not fail if package already exists - case insensitive``() = 
    let config = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget SignalR = 3.3.2"""

    DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "fAKe","") |> ignore

[<Test>]
let ``should update packages with new version``() = 
    let config = """source https://www.nuget.org/api/v2

nuget FAKE >= 1.1
"""

    let cfg = DependenciesFile.FromSource(config).UpdatePackageVersion(Constants.MainDependencyGroup, PackageName "FAKE","1.2")
    
    let expected = """source https://www.nuget.org/api/v2

nuget FAKE 1.2
"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)


[<Test>]
let ``should not update packages with new version if out of bounds``() = 
    let config = """source https://www.nuget.org/api/v2

nuget FAKE 1.1
"""

    try
        DependenciesFile.FromSource(config).UpdatePackageVersion(Constants.MainDependencyGroup, PackageName "FAKE","1.2")
        |> ignore

        failwith "expected error"
     with
     | exn when exn.Message.Contains "doesn't match the version requirement" -> ()


[<Test>]
let ``should update packages with nuget package resolution strategy``() = 
    let config = """source https://www.nuget.org/api/v2

nuget FAKE ~> 1.1
"""

    let cfg = DependenciesFile.FromSource(config).UpdatePackageVersion(Constants.MainDependencyGroup, PackageName "FAKE","!~> 1.2")
    
    let expected = """source https://www.nuget.org/api/v2

nuget FAKE !~> 1.2
"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)


[<Test>]
let ``should add FsCheck package in first position (if smaller than first)``() = 
    let config = """source https://www.nuget.org/api/v2

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

    let cfg = DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "FsCheck","")
    
    let expected = """source https://www.nuget.org/api/v2

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
    let config = """source https://www.nuget.org/api/v2"""

    let cfg = DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "Microsoft.AspNet.WebApi","")
    
    let expected = """source https://www.nuget.org/api/v2
nuget Microsoft.AspNet.WebApi"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add Microsoft.AspNet.WebApi package in correct position if package is already given``() = 
    let config = """source http://internalfeed/NugetWebFeed/nuget

nuget Microsoft.AspNet.WebApi.Client 5.2.3
nuget Microsoft.AspNet.WebApi.Core 5.2.3
nuget Microsoft.AspNet.WebApi.WebHost 5.2.3
nuget log3net

source https://www.nuget.org/api/v2
nuget Microsoft.AspNet.WebApi
nuget log4net 1.2.10"""

    let cfg = DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "Microsoft.AspNet.WebApi","5.2.3")
    
    let expected = """source http://internalfeed/NugetWebFeed/nuget

nuget Microsoft.AspNet.WebApi.Client 5.2.3
nuget Microsoft.AspNet.WebApi.Core 5.2.3
nuget Microsoft.AspNet.WebApi.WebHost 5.2.3
nuget log3net

source https://www.nuget.org/api/v2
nuget Microsoft.AspNet.WebApi 5.2.3
nuget log4net 1.2.10"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should pin Microsoft.AspNet.WebApi.Client package in correct position``() = 
    let config = """source http://internalfeed/NugetWebFeed/nuget

nuget Microsoft.AspNet.WebApi.Core 5.2.3
nuget Microsoft.AspNet.WebApi.WebHost 5.2.3
nuget log3net

source https://www.nuget.org/api/v2
nuget Microsoft.AspNet.WebApi
nuget log4net 1.2.10"""

    let cfg = DependenciesFile.FromSource(config).AddFixedPackage(Constants.MainDependencyGroup, PackageName "Microsoft.AspNet.WebApi.Client","5.2.3")
    
    let expected = """source http://internalfeed/NugetWebFeed/nuget

nuget Microsoft.AspNet.WebApi.Core 5.2.3
nuget Microsoft.AspNet.WebApi.WebHost 5.2.3
nuget log3net

source https://www.nuget.org/api/v2
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
nuget log3net

source https://www.nuget.org/api/v2
nuget Microsoft.AspNet.WebApi 5.2.1
nuget log4net 1.2.10"""

    let cfg = DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "Microsoft.AspNet.WebApi","5.2.3")
    
    let expected = """source http://internalfeed/NugetWebFeed/nuget

nuget Microsoft.AspNet.WebApi.Client 5.2.3
nuget Microsoft.AspNet.WebApi.Core 5.2.3
nuget Microsoft.AspNet.WebApi.WebHost 5.2.3
nuget log3net

source https://www.nuget.org/api/v2
nuget Microsoft.AspNet.WebApi 5.2.1
nuget Microsoft.AspNet.WebApi 5.2.3
nuget log4net 1.2.10"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should pin down version requirement during add``() = 
    let config = """source https://www.nuget.org/api/v2
nuget Microsoft.AspNet.WebApi ~> 1.0"""

    let cfg = DependenciesFile.FromSource(config).AddFixedPackage(Constants.MainDependencyGroup, PackageName "Microsoft.AspNet.WebApi","1.0.071.9432")
    
    let expected = """source https://www.nuget.org/api/v2
nuget Microsoft.AspNet.WebApi 1.0.071.9432"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add Moq to second feed``() = 
    let config = """source http://internalfeed/NugetWebFeed/nuget

nuget log3net
nuget Microsoft.AspNet.WebApi.Client 5.2.3
nuget Microsoft.AspNet.WebApi.Core 5.2.3
nuget Microsoft.AspNet.WebApi.WebHost 5.2.3

source https://www.nuget.org/api/v2
nuget log4net 1.2.10
nuget Microsoft.AspNet.WebApi 5.2.1
"""

    let cfg = DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "Moq","")
    
    let expected = """source http://internalfeed/NugetWebFeed/nuget

nuget log3net
nuget Microsoft.AspNet.WebApi.Client 5.2.3
nuget Microsoft.AspNet.WebApi.Core 5.2.3
nuget Microsoft.AspNet.WebApi.WebHost 5.2.3

source https://www.nuget.org/api/v2
nuget log4net 1.2.10
nuget Microsoft.AspNet.WebApi 5.2.1

nuget Moq"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add Microsoft.AspNet.WebApi package in first group``() = 
    let config = """source https://www.nuget.org/api/v2

group Build
nuget Moq"""

    let cfg = DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "Microsoft.AspNet.WebApi","")
    
    let expected = """source https://www.nuget.org/api/v2
nuget Microsoft.AspNet.WebApi

group Build
nuget Moq"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add Microsoft.AspNet.WebApi package in second group``() = 
    let config = """source https://www.nuget.org/api/v2

group Build
nuget Moq"""

    let cfg = DependenciesFile.FromSource(config).Add(GroupName "Build", PackageName "Microsoft.AspNet.WebApi","")
    
    let expected = """source https://www.nuget.org/api/v2

group Build
nuget Microsoft.AspNet.WebApi
nuget Moq"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add Microsoft.AspNet.WebApi package in first group in alphabetical pos``() = 
    let config = """source https://www.nuget.org/api/v2

nuget A
nuget Z

group Build
nuget Moq"""

    let cfg = DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "Microsoft.AspNet.WebApi","")
    
    let expected = """source https://www.nuget.org/api/v2

nuget A
nuget Microsoft.AspNet.WebApi
nuget Z

group Build
nuget Moq"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)


[<Test>]
let ``should add Microsoft.AspNet.WebApi package in second group in alphabetical pos``() = 
    let config = """source https://www.nuget.org/api/v2

nuget NUnit

group Build
nuget A
nuget Z"""

    let cfg = DependenciesFile.FromSource(config).Add(GroupName "Build", PackageName "Microsoft.AspNet.WebApi","")
    
    let expected = """source https://www.nuget.org/api/v2

nuget NUnit

group Build
nuget A
nuget Microsoft.AspNet.WebApi
nuget Z"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add Microsoft.AspNet.WebApi package to third group in alphabetical pos``() = 
    let config = """source https://www.nuget.org/api/v2

nuget NUnit

group Build
nuget A
nuget Z

group Test
nuget A
nuget Z"""

    let cfg = DependenciesFile.FromSource(config).Add(GroupName "Test", PackageName "Microsoft.AspNet.WebApi","")
    
    let expected = """source https://www.nuget.org/api/v2

nuget NUnit

group Build
nuget A
nuget Z

group Test
nuget A
nuget Microsoft.AspNet.WebApi
nuget Z"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add pinned package version to third group``() = 
    let config = """source https://www.nuget.org/api/v2

nuget Newtonsoft.Json
nuget Argu
nuget FSharp.Core

github fsharp/FAKE src/app/FakeLib/Globbing/Globbing.fs
github fsprojects/Chessie src/Chessie/ErrorHandling.fs

group Build

  source https://www.nuget.org/api/v2
  
  nuget FAKE
  nuget FSharp.Formatting
  nuget ILRepack

  github fsharp/FAKE modules/Octokit/Octokit.fsx

group Test

  source https://www.nuget.org/api/v2

  nuget NUnit.Runners.Net4
  nuget NUnit
  github forki/FsUnit FsUnit.fs"""

    let cfg = DependenciesFile.FromSource(config)
                .AddFixedPackage(
                    GroupName "Build",
                    PackageName "FSharp.Compiler.Service",
                    "= 1.4.0.1",
                    Paket.Requirements.InstallSettings.Default)
    
    let expected = """source https://www.nuget.org/api/v2

nuget Newtonsoft.Json
nuget Argu
nuget FSharp.Core

github fsharp/FAKE src/app/FakeLib/Globbing/Globbing.fs
github fsprojects/Chessie src/Chessie/ErrorHandling.fs

group Build

  source https://www.nuget.org/api/v2
  
  nuget FAKE
  nuget FSharp.Formatting
  nuget ILRepack

  github fsharp/FAKE modules/Octokit/Octokit.fsx
nuget FSharp.Compiler.Service 1.4.0.1

group Test

  source https://www.nuget.org/api/v2

  nuget NUnit.Runners.Net4
  nuget NUnit
  github forki/FsUnit FsUnit.fs"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add pinned package version to last group``() = 
    let config = """source https://www.nuget.org/api/v2

nuget Newtonsoft.Json
nuget Argu
nuget FSharp.Core

github fsharp/FAKE src/app/FakeLib/Globbing/Globbing.fs
github fsprojects/Chessie src/Chessie/ErrorHandling.fs

group Build

  source https://www.nuget.org/api/v2
  
  nuget FAKE
  nuget FSharp.Formatting
  nuget ILRepack

  github fsharp/FAKE modules/Octokit/Octokit.fsx

group Test

  source https://www.nuget.org/api/v2

  nuget NUnit.Runners.Net4
  nuget NUnit
  github forki/FsUnit FsUnit.fs"""

    let cfg = DependenciesFile.FromSource(config)
                .AddFixedPackage(
                    GroupName "Test",
                    PackageName "FSharp.Compiler.Service",
                    "= 1.4.0.1",
                    Paket.Requirements.InstallSettings.Default)
    
    let expected = """source https://www.nuget.org/api/v2

nuget Newtonsoft.Json
nuget Argu
nuget FSharp.Core

github fsharp/FAKE src/app/FakeLib/Globbing/Globbing.fs
github fsprojects/Chessie src/Chessie/ErrorHandling.fs

group Build

  source https://www.nuget.org/api/v2
  
  nuget FAKE
  nuget FSharp.Formatting
  nuget ILRepack

  github fsharp/FAKE modules/Octokit/Octokit.fsx

group Test

  source https://www.nuget.org/api/v2

  nuget NUnit.Runners.Net4
  nuget NUnit
  github forki/FsUnit FsUnit.fs
nuget FSharp.Compiler.Service 1.4.0.1"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add pinned package version to new group``() = 
    let config = """source https://www.nuget.org/api/v2

nuget Newtonsoft.Json
nuget Argu
nuget FSharp.Core

github fsharp/FAKE src/app/FakeLib/Globbing/Globbing.fs
github fsprojects/Chessie src/Chessie/ErrorHandling.fs

group Build

  source https://www.nuget.org/api/v2
  
  nuget FAKE
  nuget FSharp.Formatting
  nuget ILRepack

  github fsharp/FAKE modules/Octokit/Octokit.fsx"""

    let cfg = DependenciesFile.FromSource(config)
                .AddFixedPackage(
                    GroupName "Test",
                    PackageName "FSharp.Compiler.Service",
                    "= 1.4.0.1",
                    Paket.Requirements.InstallSettings.Default)
    
    let expected = """source https://www.nuget.org/api/v2

nuget Newtonsoft.Json
nuget Argu
nuget FSharp.Core

github fsharp/FAKE src/app/FakeLib/Globbing/Globbing.fs
github fsprojects/Chessie src/Chessie/ErrorHandling.fs

group Build

  source https://www.nuget.org/api/v2
  
  nuget FAKE
  nuget FSharp.Formatting
  nuget ILRepack

  github fsharp/FAKE modules/Octokit/Octokit.fsx

group Test
source https://www.nuget.org/api/v2

nuget FSharp.Compiler.Service 1.4.0.1"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add package to new group``() = 
    let config = """source https://www.nuget.org/api/v2

nuget Newtonsoft.Json
nuget Argu
nuget FSharp.Core

github fsharp/FAKE src/app/FakeLib/Globbing/Globbing.fs
github fsprojects/Chessie src/Chessie/ErrorHandling.fs

group Build

  source https://www.nuget.org/api/v2
  
  nuget FAKE
  nuget FSharp.Formatting
  nuget ILRepack

  github fsharp/FAKE modules/Octokit/Octokit.fsx"""

    let cfg = DependenciesFile.FromSource(config)
                .Add(GroupName "Test", PackageName "Microsoft.AspNet.WebApi","")
    
    let expected = """source https://www.nuget.org/api/v2

nuget Newtonsoft.Json
nuget Argu
nuget FSharp.Core

github fsharp/FAKE src/app/FakeLib/Globbing/Globbing.fs
github fsprojects/Chessie src/Chessie/ErrorHandling.fs

group Build

  source https://www.nuget.org/api/v2
  
  nuget FAKE
  nuget FSharp.Formatting
  nuget ILRepack

  github fsharp/FAKE modules/Octokit/Octokit.fsx

group Test
source https://www.nuget.org/api/v2

nuget Microsoft.AspNet.WebApi"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add package to existing group with only remote files``() = 
    let config = """source https://www.nuget.org/api/v2

nuget Newtonsoft.Json
nuget Argu
nuget FSharp.Core

github fsharp/FAKE src/app/FakeLib/Globbing/Globbing.fs
github fsprojects/Chessie src/Chessie/ErrorHandling.fs

group Build

  github fsharp/FAKE modules/Octokit/Octokit.fsx"""

    let cfg = DependenciesFile.FromSource(config)
                .Add(GroupName "Build", PackageName "Microsoft.AspNet.WebApi","")
    
    let expected = """source https://www.nuget.org/api/v2

nuget Newtonsoft.Json
nuget Argu
nuget FSharp.Core

github fsharp/FAKE src/app/FakeLib/Globbing/Globbing.fs
github fsprojects/Chessie src/Chessie/ErrorHandling.fs

group Build
source https://www.nuget.org/api/v2

nuget Microsoft.AspNet.WebApi


  github fsharp/FAKE modules/Octokit/Octokit.fsx"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add Microsoft.AspNet.WebApi package to very first group``() = 
    let config = ""

    let cfg = DependenciesFile.FromSource(config)
                .Add(GroupName "Build", PackageName "Microsoft.AspNet.WebApi","")
    
    let expected = """group Build
source https://www.nuget.org/api/v2

nuget Microsoft.AspNet.WebApi"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should pin in correct group``() = 
    let config = """source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.0
    nuget FAKE
    
    group Group
        source http://www.nuget.org/api/v2

        nuget Castle.Core-log4net
        nuget FAKE
        nuget log4net"""

    let cfg = DependenciesFile.FromSource(config)
                 .AddFixedPackage(
                        GroupName "main",
                        PackageName "Castle.Core",
                        "= 3.2.0",
                        InstallSettings.Default)

    
    let expected = """source http://www.nuget.org/api/v2

    nuget Castle.Core-log4net ~> 3.0
    nuget FAKE
nuget Castle.Core 3.2.0
    
    group Group
        source http://www.nuget.org/api/v2

        nuget Castle.Core-log4net
        nuget FAKE
        nuget log4net"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new packages with paket package resolution strategy``() = 
    let config = ""

    let cfg = DependenciesFile.FromSource(config).Add(Constants.MainDependencyGroup, PackageName "FAKE","@~> 1.2")
    
    let expected = """source https://www.nuget.org/api/v2

nuget FAKE @~> 1.2
"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should update packages with paket package resolution strategy``() = 
    let config = """source https://www.nuget.org/api/v2

nuget FAKE ~> 1.1
"""

    let cfg = DependenciesFile.FromSource(config).UpdatePackageVersion(Constants.MainDependencyGroup, PackageName "FAKE","@~> 1.2")
    
    let expected = """source https://www.nuget.org/api/v2

nuget FAKE @~> 1.2
"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)
