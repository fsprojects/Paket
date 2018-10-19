module Paket.InstallModel.Xml.RemoveOldNodesSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

[<Test>]
let ``should generate Xml for Fuchu 0.4``() = 
    ensureDir()
    let p = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyWithOldStuff.fsprojtest").Value
    let empty = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value
    p.RemovePaketNodes()
    p.Document.OuterXml |> shouldEqual empty.Document.OuterXml
