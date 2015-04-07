module Paket.ProjectFile.TargetFrameworkSpecs

open Paket
open NUnit.Framework
open FsUnit

let element x = 
    match x with 
    | Some y -> y
    | None -> failwith "not found"

[<Test>]
let ``should detect TargetFramework in Project2 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project2.fsprojtest").Value.GetTargetFramework().ToString()
    |> shouldEqual "net40"

[<Test>]
let ``should detect Pnet40 in empty proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GetTargetFramework().ToString()
    |> shouldEqual "net40"