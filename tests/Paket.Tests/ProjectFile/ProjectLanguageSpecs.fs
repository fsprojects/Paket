module Paket.ProjectFile.ProjectLanguageSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.Xml
open System.Xml.Linq

[<Test>]
let ``Language is unknown when no information is provided``() = 
    LanguageEvaluation.getProjectLanguage (new XmlDocument()) "" |> shouldEqual ProjectLanguage.Unknown

[<Test>]
let ``Language is detected from filename``() = 
    let evaluate ext = LanguageEvaluation.getProjectLanguage (new XmlDocument()) (sprintf "foo/bar/baz%s" ext)
    evaluate ".csproj" |> shouldEqual ProjectLanguage.CSharp
    evaluate ".fsproj" |> shouldEqual ProjectLanguage.FSharp
    evaluate ".vcxproj" |> shouldEqual ProjectLanguage.CPP
    evaluate ".vbproj" |> shouldEqual ProjectLanguage.VisualBasic
    evaluate ".sfproj" |> shouldEqual ProjectLanguage.ServiceFabric
    evaluate ".nproj" |> shouldEqual ProjectLanguage.Nemerle
    evaluate ".sqlproj" |> shouldEqual ProjectLanguage.Sql

let createProjectXml (projectTypeGuids : string option) : XmlDocument =
    let ns = XNamespace.Get "http://schemas.microsoft.com/developer/msbuild/2003"
    let propertyGroup = XElement(ns + "PropertyGroup")

    match projectTypeGuids with
    | Some(guids) ->
        let guidNode = XElement(ns + "ProjectTypeGuids")
        guidNode.Value <- guids
        propertyGroup.Add(guidNode)
    | _ -> ()

    let project = XElement(ns + "Project")
    project.Add(propertyGroup)

    let xdoc = XDocument(project)

    use reader = xdoc.CreateReader()
    let doc = new XmlDocument()
    doc.Load(reader)
    doc

[<Test>]
let ``Language is Unknown if guid is empty``() = 
    LanguageEvaluation.getProjectLanguage (createProjectXml None) "" |> shouldEqual ProjectLanguage.Unknown

let pclGuid = "{786C830F-07A1-408B-BD7F-6EE04809D6DB}"
let cSharpGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"
let visualBasicGuid = "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}"
let fsharpGuid = "{F2A71F9B-5D33-465A-A702-920D77279786}"
let nemerleGuid = "{EDCC3B85-0BAD-11DB-BC1A-00112FDE8B61}"

[<Test>]
let ``Language is detected from ProjectTypeGuids``() = 
    let evaluate guids = LanguageEvaluation.getProjectLanguage (createProjectXml guids) ""
        
    Some(fsharpGuid) |> evaluate  |> shouldEqual ProjectLanguage.FSharp
    Some(sprintf "%s;%s" pclGuid fsharpGuid) |> evaluate  |> shouldEqual ProjectLanguage.FSharp

    Some(cSharpGuid) |> evaluate  |> shouldEqual ProjectLanguage.CSharp
    Some(sprintf "%s;%s" pclGuid cSharpGuid) |> evaluate  |> shouldEqual ProjectLanguage.CSharp

    Some(visualBasicGuid) |> evaluate  |> shouldEqual ProjectLanguage.VisualBasic
    Some(sprintf "%s;%s" pclGuid visualBasicGuid) |> evaluate  |> shouldEqual ProjectLanguage.VisualBasic

    Some(nemerleGuid) |> evaluate  |> shouldEqual ProjectLanguage.Nemerle
    Some(sprintf "%s;%s" pclGuid nemerleGuid) |> evaluate  |> shouldEqual ProjectLanguage.Nemerle

[<Test>]
let ``Confusion in ProjectTypeGuids is unknown``() = 
    let evaluate guids = LanguageEvaluation.getProjectLanguage (createProjectXml guids) ""
        
    Some(sprintf "%s;%s" cSharpGuid fsharpGuid) |> evaluate  |> shouldEqual ProjectLanguage.Unknown

[<Test>]
let ``Confusion between filename and ProjectTypeGuids is unknown``() = 
    let evaluate ext guids = LanguageEvaluation.getProjectLanguage (createProjectXml guids) (sprintf "foo/bar/baz%s" ext)
        
    Some(fsharpGuid) |> evaluate ".csproj" |> shouldEqual ProjectLanguage.Unknown