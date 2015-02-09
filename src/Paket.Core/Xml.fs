module Paket.Xml


open System.Xml.Linq
open System
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
let inline hasAttribute name (node:XmlNode) =
    if node = null || node.Attributes = null then false else
    node.Attributes 
    |> Seq.cast<XmlAttribute>
    |> Seq.exists (fun a -> a.Name = name)

/// [omit]
let inline getAttribute name (node:XmlNode) =
    if node = null || node.Attributes = null then None else
    node.Attributes 
    |> Seq.cast<XmlAttribute> 
    |> Seq.tryFind (fun a -> a.Name = name && a.Value <> null) 
    |> Option.map (fun a -> a.Value)

/// [omit]
let inline withAttributeValue attributeName valueText node =
    getAttribute attributeName node = Some valueText

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


module Linq =

    let asOption = function | null -> None | x -> Some x
    let private xname ns name = XName.Get(name, defaultArg ns "")
    let tryGetElement ns name (xe:XContainer) =
        let XName = xname ns name
        xe.Element XName |> asOption
    let getElements ns name (xe:XContainer) = xname ns name |> xe.Elements
    let tryGetAttribute name (xe:XElement) = xe.Attribute(xname None name) |> asOption
    let createElement ns name attributes = XElement(xname ns name, attributes |> Seq.map(fun (name,value) -> XAttribute(xname None name, value)))
    let ensurePathExists (xpath:string) (item:XContainer) =
        (item, xpath.Split([|'/'|], StringSplitOptions.RemoveEmptyEntries))
        ||> Seq.fold(fun parent node ->
            let node, ns =
                match node.Split '!' with
                | [| node; ns |] -> node, Some ns
                | _ -> node, None
            match parent |> tryGetElement ns node with
            | None ->
                let node = XElement(XName.Get(node, defaultArg ns ""))
                parent.Add node
                node :> XContainer
            | Some existingNode -> existingNode :> XContainer)
