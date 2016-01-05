module Paket.ProjectFile.FileBuildActionSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.Xml

let createProject name = 
    { FileName = name
      OriginalText = ""
      Document = XmlDocument()
      ProjectNode = null
      Language = ProjectLanguage.Unknown }

[<Test>]
let ``should recognize compilable files``() =
    (createProject "A.csproj").DetermineBuildAction "Class.cs" |> shouldEqual BuildAction.Compile
    (createProject "B.fsproj").DetermineBuildAction "Module.fs" |> shouldEqual BuildAction.Compile
    (createProject "C.vbproj").DetermineBuildAction "Whatever.vb" |> shouldEqual BuildAction.Compile

[<Test>]
let ``should recognize content files``() =
    (createProject "A.csproj").DetermineBuildAction "Something.js" |> shouldEqual BuildAction.Content
    (createProject "B.fsproj").DetermineBuildAction "config.yml" |> shouldEqual BuildAction.Content
    (createProject "C.vbproj").DetermineBuildAction "noext" |> shouldEqual BuildAction.Content