module Paket.IntegrationTests.DevSourceOverrideSpecs

open System.IO
open System.Xml.Linq
open System.Xml.XPath

open FsUnit

open NUnit.Framework

[<Test>]
let ``#1633 should favor overriden source from paket.local``() = 
    paket "restore" "i001633-dev-source-override" |> ignore
    let nunitVersion =
        Path.Combine(
            scenarioTempPath "i001633-dev-source-override",
            "packages",
            "NUnit",
            "NUnit.nuspec")
        |> XDocument.Load
        |> fun doc -> doc.XPathSelectElement("/package/metadata/version")
        |> fun elem -> elem.Value
    
    nunitVersion |> shouldEqual "2.6.4"