module Paket.IntegrationTests.LocalOverrideSpecs

open System.IO
open System.Xml

open Paket
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

let replaceInFile filePath (searchText: string) replaceText =
    File.ReadAllText filePath
    |> fun x -> x.Replace(searchText, replaceText)
    |> fun x -> File.WriteAllText (filePath, x)

[<Test>]
let ``#1633 paket.local local git override``() = 
    let scenario = "i001633-local-git-override"
    replaceInFile 
        (Path.Combine (originalScenarioPath scenario, "paket.local"))
        "[build-command]" 
        (if isUnix then "build.sh NuGet" else "build.cmd NuGet") 
    paket "restore" scenario |> ignore
    let doc = new XmlDocument()
    Path.Combine(
        scenarioTempPath scenario,
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
    let scenario = "i001633-local-git-override-git_origin"
    replaceInFile 
        (Path.Combine (originalScenarioPath scenario, "paket.local"))
        "[build-command]" 
        (if isUnix then "build.sh NuGet" else "build.cmd NuGet") 
    paket "restore" scenario |> ignore
    let doc = new XmlDocument()
    Path.Combine(
        scenarioTempPath scenario,
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