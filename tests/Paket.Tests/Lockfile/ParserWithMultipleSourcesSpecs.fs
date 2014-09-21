module Paket.LockFile.ParserWithMultipleSourcesSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

let lockFile = """NUGET
  remote: http://nuget.org/api/v2
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
    let lockFile = LockFile.Parse("",toLines lockFile)
    lockFile.ResolvedPackages.Length |> shouldEqual 6
    lockFile.Strict |> shouldEqual false

    lockFile.ResolvedPackages.[0].Source |> shouldEqual (Nuget Constants.DefaultNugetStream)
    lockFile.ResolvedPackages.[0].Name |> shouldEqual "Castle.Windsor"
    lockFile.ResolvedPackages.[0].Version |> shouldEqual (SemVer.parse "2.1")

    lockFile.ResolvedPackages.[1].Source |> shouldEqual (Nuget Constants.DefaultNugetStream)
    lockFile.ResolvedPackages.[1].Name |> shouldEqual "Castle.Windsor-log4net"
    lockFile.ResolvedPackages.[1].Version |> shouldEqual (SemVer.parse "3.3")
    
    lockFile.ResolvedPackages.[4].Source |> shouldEqual (Nuget "http://nuget.org/api/v3")
    lockFile.ResolvedPackages.[4].Name |> shouldEqual "Rx-Core"
    lockFile.ResolvedPackages.[4].Version |> shouldEqual (SemVer.parse "2.1")

    lockFile.ResolvedPackages.[5].Source |> shouldEqual (Nuget "http://nuget.org/api/v3")
    lockFile.ResolvedPackages.[5].Name |> shouldEqual "Rx-Main"
    lockFile.ResolvedPackages.[5].Version |> shouldEqual (SemVer.parse "2.0")
