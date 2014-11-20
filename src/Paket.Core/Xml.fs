module Paket.Xml

open System.Xml

/// [omit]
let inline addAttribute name value (node:XmlElement) =
    node.SetAttribute(name, value) |> ignore
    node

/// [omit]
let inline addChild child (node:XmlElement) =
    node.AppendChild(child) |> ignore
    node

/// [omit]
let inline getAttribute name (node:XmlNode) =
    node.Attributes 
    |> Seq.cast<XmlAttribute> 
    |> Seq.tryFind (fun a -> a.Name = name) 
    |> Option.map (fun a -> a.Value)

/// [omit]
let inline optGetAttribute name node = node |> Option.bind (getAttribute name)

/// [omit]
let inline getNode name (node:XmlNode) =
    let xpath = sprintf "*[local-name() = '%s']" name
    match node.SelectSingleNode(xpath) with
    | null -> None
    | n -> Some(n)

/// [omit]
let inline optGetNode name node = node |> Option.bind (getNode name)

/// [omit]
let inline getNodes name (node:XmlNode) =
    let xpath = sprintf "*[local-name() = '%s']" name
    match node.SelectNodes(xpath) with
    | null -> []
    | nodeList -> 
        nodeList
        |> Seq.cast<XmlNode>
        |> Seq.toList

/// [omit]
let inline getDescendants name (node:XmlNode) = 
    let xpath = sprintf "//*[local-name() = '%s']" name
    match node.SelectNodes(xpath) with
    | null -> []
    | nodeList -> 
        nodeList
        |> Seq.cast<XmlNode>
        |> Seq.toList