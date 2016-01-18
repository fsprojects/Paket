module Paket.ProjectFile.GetCompileItemsSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.Xml
open System.Xml.Linq

[<Test>]
let ``Compile items with wildcards should return correct number of files``() = 
    let proj = ProjectFile.LoadFromFile("./ProjectFile/TestData/WithWildcardCompileItems.csprojtest")
    let items = ProjectFile.getCompileItems(proj, false)
    items |> Seq.length |> shouldEqual 13

[<Test>]
let ``Compile items with wildcards and links should return correct number of files``() = 
    let proj = ProjectFile.LoadFromFile("./ProjectFile/TestData/WithWildcardCompileItems.csprojtest")
    let items = ProjectFile.getCompileItems(proj, false)
    items |> Seq.length |> shouldEqual 13

