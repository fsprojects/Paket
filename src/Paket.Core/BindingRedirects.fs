module Paket.BindingRedirects

open System
open System.Xml.Linq
open System.IO
open System.Reflection

[<AutoOpen>]
module private XmlLinq =
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

/// Represents a binding redirection
type BindingRedirect =
    {   AssemblyName : string
        Version : string
        PublicKeyToken : string
        Culture : string option }

/// Updates the supplied MSBuild document with the supplied binding redirect.
let internal setRedirect (doc:XDocument) bindingRedirect =
    let bindingNs = "urn:schemas-microsoft-com:asm.v1"
    let createElementWithNs = createElement (Some bindingNs)
    let tryGetElementWithNs = tryGetElement (Some bindingNs)
    let getElementsWithNs = getElements (Some bindingNs)

    let assemblyBinding = doc |> ensurePathExists ("/configuration/runtime/assemblyBinding!" + bindingNs)
    let dependentAssembly =
        assemblyBinding
        |> getElementsWithNs "dependentAssembly"
        |> Seq.tryFind(fun dependentAssembly ->
            defaultArg
                (dependentAssembly
                 |> tryGetElementWithNs "assemblyIdentity"
                 |> Option.bind(tryGetAttribute "name")
                 |> Option.map(fun attribute -> attribute.Value = bindingRedirect.AssemblyName))
                false)
        |> function
           | Some dependentAssembly -> dependentAssembly
           | None ->
                let dependentAssembly = createElementWithNs "dependentAssembly" []
                dependentAssembly.Add(createElementWithNs "assemblyIdentity" ([ "name", bindingRedirect.AssemblyName
                                                                                "publicKeyToken", bindingRedirect.PublicKeyToken
                                                                                "culture", defaultArg bindingRedirect.Culture "neutral" ]))
                assemblyBinding.Add(dependentAssembly)
                dependentAssembly
                
    let newRedirect = createElementWithNs "bindingRedirect" [ "oldVersion", sprintf "0.0.0.0-%s" bindingRedirect.Version
                                                              "newVersion", bindingRedirect.Version ]
    match dependentAssembly |> tryGetElementWithNs "bindingRedirect" with
    | Some redirect -> redirect.ReplaceWith(newRedirect)
    | None -> dependentAssembly.Add(newRedirect)
    doc

/// Applies a set of binding redirects to a single configuration file.
let private applyBindingRedirects bindingRedirects (configFilePath:string) =
    let config = XDocument.Load configFilePath
    let config = Seq.fold setRedirect config bindingRedirects
    config.Save configFilePath

/// Applies a set of binding redirects to all .config files in a specific folder.
let applyBindingRedirectsToFolder rootPath bindingRedirects =
    Directory.GetFiles(rootPath, "*.config", SearchOption.AllDirectories) 
    |> Seq.filter (fun x -> x.EndsWith "web.config" || x.EndsWith "app.config")
    |> Seq.iter (applyBindingRedirects bindingRedirects)

/// Calculates the short form of the public key token for use with binding redirects, if it exists.
let getPublicKeyToken (assembly:Assembly) =
    ("", assembly.GetName().GetPublicKeyToken())
    ||> Array.fold(fun state b -> state + b.ToString("X2"))
    |> function
    | "" -> None
    | token -> Some <| token.ToLower()
