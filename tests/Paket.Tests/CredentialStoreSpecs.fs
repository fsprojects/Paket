module Paket.CredentialStoreSpecs

open Paket
open Paket.ConfigFile
open NUnit.Framework
open System.Xml
open FsUnit


let sampleDoc() =
    let doc = XmlDocument()
    doc.LoadXml """<?xml version="1.0" encoding="utf-8"?>
<credentials>
</credentials>""" 
    doc

[<Test>]
let ``get username and password from node``() = 
    let doc = sampleDoc()
    let node = doc.CreateElement("credential")
    node.SetAttribute("username", "demo-user")
    let salt, password = encrypt "demopassword"
    node.SetAttribute("password", password)
    node.SetAttribute("salt", salt)
    // Act
    let auth = getAuthFromNode node

    // Assert
    auth.Value.Username.Expanded |> shouldEqual  "demo-user"
    auth.Value.Password.Expanded |> shouldEqual  "demopassword"

    
[<Test>]
let ``get source nodes``() = 
    let doc = sampleDoc()
    let node = doc.CreateElement("credential")
    node.SetAttribute("source", "wrongnode")
    doc.DocumentElement.AppendChild(node) |> ignore
    let node = doc.CreateElement("credential")
    node.SetAttribute("source", "goodnode")
    doc.DocumentElement.AppendChild(node) |> ignore
    // Act
    let nodes = getSourceNodes doc "goodnode"

    // Assert
    nodes.Length |> shouldEqual 1
    nodes.Head.Attributes.["source"].Value |> shouldEqual  "goodnode"

    