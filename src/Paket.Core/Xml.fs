module Paket.Xml

open System.Xml

/// [omit]
let addAttribute name value (node:XmlElement) =
    node.SetAttribute(name, value) |> ignore
    node

/// [omit]
let addChild child (node:XmlElement) =
    node.AppendChild(child) |> ignore
    node

let createNode(doc:XmlDocument,name) = doc.CreateElement(name, Constants.ProjectDefaultNameSpace)

let createNodeWithText(doc,name,text) = 
    let node = createNode(doc,name)
    node.InnerText <- text
    node