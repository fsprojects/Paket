module Paket.IntegrationTests.GroupsSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics

[<Test>]
let ``#1018 should resolve Newtonsoft.Json from two groups``() = 
    update "i001018-legacy-groups" |> ignore
    let path = scenarioTempPath "i001018-legacy-groups"
   
    File.Exists(Path.Combine(path,"packages","Newtonsoft.Json","Newtonsoft.Json.7.0.1.nupkg"))
    |> shouldEqual true

    File.Exists(Path.Combine(path,"packages","legacy","Newtonsoft.Json","Newtonsoft.Json.6.0.8.nupkg"))
    |> shouldEqual true