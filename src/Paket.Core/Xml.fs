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

/// [omit]
let getAttribute name (node:XmlNode) =
    node.Attributes 
    |> Seq.cast<XmlAttribute> 
    |> Seq.tryFind (fun a -> a.Name = name) 
    |> Option.map (fun a -> a.Value)

/// [omit]
let getNode name (node:XmlNode) =
    let xpath = sprintf "*[local-name() = '%s']" name
    match node.SelectSingleNode(xpath) with
    | null -> None
    | n -> Some(n)

/// [omit]
let getNodes name (node:XmlNode) =
    let xpath = sprintf "*[local-name() = '%s']" name
    match node.SelectNodes(xpath) with
    | null -> []
    | nodeList -> 
        nodeList
        |> Seq.cast<XmlNode>
        |> Seq.toList

let createNode(doc:XmlDocument,name) = doc.CreateElement(name, Constants.ProjectDefaultNameSpace)

let createNodeWithText(doc,name,text) = 
    let node = createNode(doc,name)
    node.InnerText <- text
    node