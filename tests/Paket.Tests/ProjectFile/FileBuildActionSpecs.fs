module Paket.ProjectFile.FileBuildActionSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.Xml

let createProject name = 
    { FileName = name
      OriginalText = ""
      Document = XmlDocument()
      ProjectNode = null }

[<Test>]
let ``should recognize compilable files``() =

    (createProject "A.csproj").DetermineBuildAction "Class.cs" |> shouldEqual "Compile"
    (createProject "B.fsproj").DetermineBuildAction "Module.fs" |> shouldEqual "Compile"
    (createProject "C.vbproj").DetermineBuildAction "Whatever.vb" |> shouldEqual "Compile"

[<Test>]
let ``should recognize content files``() =

    (createProject "A.csproj").DetermineBuildAction "Something.js" |> shouldEqual "Content"
    (createProject "B.fsproj").DetermineBuildAction "config.yml" |> shouldEqual "Content"
    (createProject "C.vbproj").DetermineBuildAction "noext" |> shouldEqual "Content"