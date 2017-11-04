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
      Language = ProjectLanguage.Unknown
      DefaultProperties = None
      CalculatedProperties = new System.Collections.Concurrent.ConcurrentDictionary<_,_>() }

[<Test>]
let ``should recognize compilable files``() =
    (createProject "A.csproj").DetermineBuildAction "Class.cs" |> shouldEqual BuildAction.Compile
    (createProject "B.fsproj").DetermineBuildAction "Module.fs" |> shouldEqual BuildAction.Compile
    (createProject "B.fsproj").DetermineBuildAction "Module.fsi" |> shouldEqual BuildAction.Compile
    (createProject "C.vbproj").DetermineBuildAction "Whatever.vb" |> shouldEqual BuildAction.Compile
    (createProject "D.nproj").DetermineBuildAction "Main.n" |> shouldEqual BuildAction.Compile
    (createProject "E.pyproj").DetermineBuildAction "Class.py" |> shouldEqual BuildAction.Compile

[<Test>]
let ``should recognize content files``() =
    (createProject "A.csproj").DetermineBuildAction "Something.js" |> shouldEqual BuildAction.Content
    (createProject "B.fsproj").DetermineBuildAction "config.yml" |> shouldEqual BuildAction.Content
    (createProject "C.vbproj").DetermineBuildAction "noext" |> shouldEqual BuildAction.Content
    (createProject "D.nproj").DetermineBuildAction "App.config" |> shouldEqual BuildAction.Content
    (createProject "E.pyproj").DetermineBuildAction "Something.xml" |> shouldEqual BuildAction.Content

[<Test>]
let ``should recognize page files``() =
    (createProject "A.csproj").DetermineBuildAction "Form1.xaml" |> shouldEqual BuildAction.Page
    (createProject "B.fsproj").DetermineBuildAction "Form1.Xaml" |> shouldEqual BuildAction.Page
    (createProject "C.vbproj").DetermineBuildAction "Form1.XAML" |> shouldEqual BuildAction.Page
    (createProject "D.nproj" ).DetermineBuildAction "Form1.XaML" |> shouldEqual BuildAction.Page
    (createProject "E.pyproj" ).DetermineBuildAction "Form1.xaml" |> shouldEqual BuildAction.Page

[<Test>]
let ``should recognize resource files``() =
    (createProject "A.csproj").DetermineBuildAction "Form1.ttf" |> shouldEqual BuildAction.Resource
    (createProject "B.fsproj").DetermineBuildAction "Form1.ico" |> shouldEqual BuildAction.Resource
    (createProject "C.vbproj").DetermineBuildAction "Form1.png" |> shouldEqual BuildAction.Resource
    (createProject "D.nproj" ).DetermineBuildAction "Form1.jpg" |> shouldEqual BuildAction.Resource
    (createProject "E.pyproj" ).DetermineBuildAction "Form1.jpg" |> shouldEqual BuildAction.Resource
