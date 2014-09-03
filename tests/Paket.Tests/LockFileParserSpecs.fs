module Paket.LockFileParserSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

let lockFile = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Windsor (2.1)
    Castle.Windsor-log4net (3.3)
    Rx-Core (2.1)
    Rx-Main (2.0)
    log (1.2)
    log4net (1.1)
"""   

[<Test>]
let ``should parse lockfile``() = 
    let result = LockFile.Parse(toLines lockFile) |> Seq.toArray
    result.Length |> shouldEqual 6

    result.[0].SourceType |> shouldEqual "nuget"
    result.[0].Source |> shouldEqual "http://nuget.org/api/v2"
    result.[0].Name |> shouldEqual "Castle.Windsor"
    result.[0].VersionRange |> shouldEqual (VersionRange.Exactly "2.1")
    result.[0].Hash |> shouldEqual None

    result.[1].SourceType |> shouldEqual "nuget"
    result.[1].Source |> shouldEqual "http://nuget.org/api/v2"
    result.[1].Name |> shouldEqual "Castle.Windsor-log4net"
    result.[1].VersionRange |> shouldEqual (VersionRange.Exactly "3.3")
    result.[1].Hash |> shouldEqual None
    
    result.[5].SourceType |> shouldEqual "nuget"
    result.[5].Source |> shouldEqual "http://nuget.org/api/v2"
    result.[5].Name |> shouldEqual "log4net"
    result.[5].VersionRange |> shouldEqual (VersionRange.Exactly "1.1")
    result.[5].Hash |> shouldEqual None


let lockFileWithHashes = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    Castle.Windsor (2.1)
    Castle.Windsor-log4net (3.3)
    Rx-Core (2.1) SHA512 4uPnsxJYz8mU4X/LG2WKKz9tf7LLMImhIxYig5h3S6o0X8iRFYxsyzzBoJfLK76SuarqFBVjlwhkA7v8fBoHXg==
    Rx-Main (2.0)
    log (1.2)
    log4net (1.1)
"""   

[<Test>]
let ``should parse lockfile whith hashes``() = 
    let result = LockFile.Parse(toLines lockFileWithHashes) |> Seq.toArray
    result.Length |> shouldEqual 6

    result.[2].SourceType |> shouldEqual "nuget"
    result.[2].Source |> shouldEqual "http://nuget.org/api/v2"
    result.[2].Name |> shouldEqual "Rx-Core"
    result.[2].VersionRange |> shouldEqual (VersionRange.Exactly "2.1")
    result.[2].Hash |> shouldEqual (Some {Algorithm = "SHA512"; Hash = "4uPnsxJYz8mU4X/LG2WKKz9tf7LLMImhIxYig5h3S6o0X8iRFYxsyzzBoJfLK76SuarqFBVjlwhkA7v8fBoHXg==" })