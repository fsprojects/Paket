module Paket.BindingRedirects

open System
open System.Xml.Linq
open System.IO

[<AutoOpen>]
module private Helpers =
    let asOption = function | null -> None | x -> Some x
    let tryGetElement name (xe:XContainer) = xe.Element(XName.Get name) |> asOption
    let getElements name (xe:XContainer) = xe.Elements(XName.Get name)
    let tryGetAttribute name (xe:XElement) = xe.Attribute(XName.Get name) |> asOption
    let createElement name attributes = XElement(XName.Get name, attributes |> Seq.map(fun (name,value) -> XAttribute(XName.Get name, value)))    
    let ensurePathExists (xpath:string) (item:XContainer) =
        (item, xpath.Split([|'/'|], StringSplitOptions.RemoveEmptyEntries))
        ||> Seq.fold(fun parent node ->
            match parent |> tryGetElement node with
            | None ->
                let node = XElement(XName.Get node)
                parent.Add node
                node :> XContainer
            | Some existingNode -> existingNode :> XContainer)

/// Represents a binding redirection
type BindingRedirect =
    {   AssemblyName : string
        Version : string
        PublicKeyToken : string option
        Culture : string option }

/// Updates the supplied MSBuild document with the supplied binding redirect.
let setRedirect (doc:XDocument) bindingRedirect =
    let assemblyBinding = doc |> ensurePathExists "/configuration/runtime/assemblyBinding"
    let dependentAssembly =
        assemblyBinding |> getElements "dependentAssembly"
        |> Seq.tryFind(fun dependentAssembly ->
            defaultArg
                (dependentAssembly
                 |> tryGetElement "assemblyIdentity"
                 |> Option.bind(tryGetAttribute "name")
                 |> Option.map(fun attribute -> attribute.Value = bindingRedirect.AssemblyName))
                false)
        |> function
           | Some dependentAssembly -> dependentAssembly
           | None ->
                let dependentAssembly = createElement "dependentAssembly" []
                dependentAssembly.Add(createElement "assemblyIdentity" ([ "name", Some bindingRedirect.AssemblyName
                                                                          "publicKeyToken", bindingRedirect.PublicKeyToken
                                                                          "culture", bindingRedirect.Culture ]
                                                                        |> Seq.choose(fun (key,value) -> match value with Some v -> Some(key, v) | None -> None)))
                assemblyBinding.Add(dependentAssembly)
                dependentAssembly
                
    let newRedirect = createElement "bindingRedirect" [ "oldVersion", sprintf "0.0.0.0-%s" bindingRedirect.Version
                                                        "newVersion", bindingRedirect.Version ]
    match dependentAssembly |> tryGetElement "bindingRedirect" with
    | Some redirect -> redirect.ReplaceWith(newRedirect)
    | None -> dependentAssembly.Add(newRedirect)
    doc

/// Applies a set of binding redirects to a single configuration file.
let applyBindingRedirects bindingRedirects (configFilePath:string) =
    let config = XDocument.Load configFilePath
    let config = Seq.fold setRedirect config bindingRedirects
    config.Save configFilePath

/// Applies a set of binding redirects to all .config files in a specific folder.
let applyBindingRedirectsToFolder bindingRedirects rootPath =
    Directory.GetFiles(rootPath, "*.config", SearchOption.AllDirectories)
    |> Seq.iter (applyBindingRedirects bindingRedirects)