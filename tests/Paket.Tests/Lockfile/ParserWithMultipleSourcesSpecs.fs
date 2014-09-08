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
let ``should parse lockfile``() = 
    let result = LockFile.Parse(toLines lockFile) |> Seq.toArray
    result.Length |> shouldEqual 6

    result.[0].Sources |> List.head |> shouldEqual (Nuget "http://nuget.org/api/v2")
    result.[0].Name |> shouldEqual "Castle.Windsor"
    result.[0].VersionRange |> shouldEqual (VersionRange.Exactly "2.1")

    result.[1].Sources |> List.head |> shouldEqual (Nuget "http://nuget.org/api/v2")
    result.[1].Name |> shouldEqual "Castle.Windsor-log4net"
    result.[1].VersionRange |> shouldEqual (VersionRange.Exactly "3.3")
    
    result.[4].Sources |> List.head |> shouldEqual (Nuget "http://nuget.org/api/v3")
    result.[4].Name |> shouldEqual "Rx-Core"
    result.[4].VersionRange |> shouldEqual (VersionRange.Exactly "2.1")

    result.[5].Sources |> List.head |> shouldEqual (Nuget "http://nuget.org/api/v3")
    result.[5].Name |> shouldEqual "Rx-Main"
    result.[5].VersionRange |> shouldEqual (VersionRange.Exactly "2.0")