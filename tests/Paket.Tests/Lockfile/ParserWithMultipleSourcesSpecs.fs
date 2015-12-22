module Paket.LockFile.ParserWithMultipleSourcesSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain

let lockFile = """NUGET
  remote: https://www.nuget.org/api/v2
  specs:
    Castle.Windsor (2.1)
    Castle.Windsor-log4net (3.3)
    log (1.2)
    log4net (1.1)
  remote: http://nuget.org/api/v3
  specs:
    Rx-Core (2.1)
    Rx-Main (2.0)"""

[<Test>]
let ``should parse lock file``() = 
    let lockFile = LockFileParser.Parse(toLines lockFile) |> List.head
    let packages = List.rev lockFile.Packages
    packages.Length |> shouldEqual 6
    lockFile.Options.Strict |> shouldEqual false

    packages.[0].Source |> shouldEqual PackageSources.DefaultNuGetSource
    packages.[0].Name |> shouldEqual (PackageName "Castle.Windsor")
    packages.[0].Version |> shouldEqual (SemVer.Parse "2.1")

    packages.[1].Source |> shouldEqual PackageSources.DefaultNuGetSource
    packages.[1].Name |> shouldEqual (PackageName "Castle.Windsor-log4net")
    packages.[1].Version |> shouldEqual (SemVer.Parse "3.3")
    
    packages.[4].Source |> shouldEqual (PackageSources.PackageSource.NuGetV2Source "http://nuget.org/api/v3")
    packages.[4].Name |> shouldEqual (PackageName "Rx-Core")
    packages.[4].Version |> shouldEqual (SemVer.Parse "2.1")

    packages.[5].Source |> shouldEqual (PackageSources.PackageSource.NuGetV2Source "http://nuget.org/api/v3")
    packages.[5].Name |> shouldEqual (PackageName "Rx-Main")
    packages.[5].Version |> shouldEqual (SemVer.Parse "2.0")
