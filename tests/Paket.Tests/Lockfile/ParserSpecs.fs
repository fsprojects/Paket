module paket.lockFile.ParserSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

let lockFile = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Windsor (2.1)
    Castle.Windsor-log4net (3.3)
      Castle.Windsor (>= 2.0)
      log4net (>= 1.0)
    Rx-Core (2.1)
    Rx-Main (2.0)
      Rx-Core (>= 2.1)
    log (1.2)
    log4net (1.1)
      log (>= 1.0)
"""   

[<Test>]
let ``should parse lock file``() = 
    let strict,result = LockFile.Parse(toLines lockFile) 
    let result = result |> Seq.toArray
    result.Length |> shouldEqual 6
    strict |> shouldEqual false

    result.[0].Source |> shouldEqual (Nuget "http://nuget.org/api/v2")
    result.[0].Name |> shouldEqual "Castle.Windsor"
    result.[0].Version |> shouldEqual (SemVer.parse "2.1")
    result.[0].DirectDependencies |> shouldEqual []

    result.[1].Source |> shouldEqual (Nuget "http://nuget.org/api/v2")
    result.[1].Name |> shouldEqual "Castle.Windsor-log4net"
    result.[1].Version |> shouldEqual (SemVer.parse "3.3")
    result.[1].DirectDependencies |> shouldEqual ["Castle.Windsor", Latest; "log4net", Latest]
    
    result.[5].Source |> shouldEqual (Nuget "http://nuget.org/api/v2")
    result.[5].Name |> shouldEqual "log4net"
    result.[5].Version |> shouldEqual (SemVer.parse "1.1")
    result.[5].DirectDependencies |> shouldEqual ["log", Latest]

let strictLockFile = """REFERENCES: STRICT
NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Windsor (2.1)
    Castle.Windsor-log4net (3.3)
      Castle.Windsor (>= 2.0)
      log4net (>= 1.0)
    Rx-Core (2.1)
    Rx-Main (2.0)
      Rx-Core (>= 2.1)
    log (1.2)
    log4net (1.1)
      log (>= 1.0)
"""   

[<Test>]
let ``should parse strict lock file``() = 
    let strict,result = LockFile.Parse(toLines strictLockFile) 
    let result = result |> Seq.toArray
    result.Length |> shouldEqual 6
    strict |> shouldEqual true

    result.[5].Source |> shouldEqual (Nuget "http://nuget.org/api/v2")
    result.[5].Name |> shouldEqual "log4net"
    result.[5].Version |> shouldEqual (SemVer.parse "1.1")
    result.[5].DirectDependencies |> shouldEqual ["log", Latest]