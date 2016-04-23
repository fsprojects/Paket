module Paket.IntegrationTests.DevSourceOverrideSpecs

open System.IO
open System.Xml

open Paket.Xml

open FsUnit

open NUnit.Framework

[<Test>]
let ``#1633 should favor overriden source from paket.local``() = 
    paket "restore" "i001633-dev-source-override" |> ignore
    let doc = new XmlDocument()
    Path.Combine(
        scenarioTempPath "i001633-dev-source-override",
        "packages",
        "NUnit",
        "NUnit.nuspec")
    |> doc.Load

    let nunitVersion =
        doc 
        |> getNode "package" 
        |> optGetNode "metadata" 
        |> optGetNode "version"
        |> Option.map (fun n -> n.InnerText)
    
    nunitVersion |> shouldEqual (Some "2.6.4")