module Paket.ProjectFile.ProjectFileTypesSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.Xml

let createProject name = 
        { FileName = name 
          OriginalText = ""
          Document = XmlDocument()
          Namespaces = XmlNamespaceManager(null)}

[<Test>]
let ``should recognize compilable files``() =

    (createProject "A.csproj").DetermineBuildAction "Class.cs" |> shouldEqual "Compile"
    (createProject "B.fsproj").DetermineBuildAction "Module.fs" |> shouldEqual "Compile"
    (createProject "C.vbproj").DetermineBuildAction "Whatever.vb" |> shouldEqual "Compile"