module Paket.IntegrationTests.LocalOverrideSpecs

open System.IO
open System.Xml

open Paket
open Paket.Xml

open FsUnit

open NUnit.Framework

[<Test>]
let ``#1633 paket.local local source override``() =
    clearPackageAtVersion "NUnit" "2.6.3"

    use __1 = paketEx true "restore" "i001633-local-source-override" |> fst
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

    // The package should not be in cache
    isPackageCached "NUnit" "2.6.3"
    |> shouldEqual []

    // Issue #2690
    use __2 = paketEx true "restore" "i001633-local-source-override" |> fst

    // The package should not be in cache
    isPackageCached "NUnit" "2.6.3"
    |> shouldEqual []

let replaceInFile filePath (searchText: string) replaceText =
    File.ReadAllText filePath
    |> fun x -> x.Replace(searchText, replaceText)
    |> fun x -> File.WriteAllText (filePath, x)
