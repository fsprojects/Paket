module Paket.IntegrationTests.LocalOverrideSpecs

open System.IO
open System.Xml

open Paket.Xml

open FsUnit

open NUnit.Framework

[<Test>]
let ``#1633 paket.local local source override``() = 
    paket "restore" "i001633-local-source-override" |> ignore
    let doc = new XmlDocument()
    Path.Combine(
        scenarioTempPath "i001633-local-source-override",
        "packages",
        "NUnit",
        "NUnit.nuspec")
    |> doc.Load

    doc 
    |> getNode "package" 
    |> optGetNode "metadata" 
    |> optGetNode "devSourceOverride"
    |> Option.map (fun n -> n.InnerText)
    |> shouldEqual (Some "true")

[<Test>]
let ``#1633 paket.local local git override``() = 
    paket "restore" "i001633-local-git-override" |> ignore
    let doc = new XmlDocument()
    Path.Combine(
        scenarioTempPath "i001633-local-git-override",
        "packages",
        "Argu",
        "Argu.nuspec")
    |> doc.Load

    doc 
    |> getNode "package" 
    |> optGetNode "metadata" 
    |> optGetNode "summary"
    |> Option.map (fun n -> n.InnerText)
    |> shouldEqual (Some "Test paket source remote git override.")

[<Test>]
let ``#1633 paket.local local git override (git origin)``() = 
    paket "restore" "i001633-local-git-override-git_origin" |> ignore
    let doc = new XmlDocument()
    Path.Combine(
        scenarioTempPath "i001633-local-git-override-git_origin",
        "packages",
        "Argu",
        "Argu.nuspec")
    |> doc.Load

    doc 
    |> getNode "package" 
    |> optGetNode "metadata" 
    |> optGetNode "summary"
    |> Option.map (fun n -> n.InnerText)
    |> shouldEqual (Some "Test paket source remote git override.")