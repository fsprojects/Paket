module Paket.BindingRedirects

open System
open System.Xml.Linq

[<AutoOpen>]
module private Helpers =
    let asOption = function | null -> None | x -> Some x
    let tryGetElement name (xe:XContainer) = xe.Element(XName.Get name) |> asOption
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
let setRedirect bindingRedirect (doc:XDocument) =
    let assemblyBinding = doc |> ensurePathExists "/configuration/runtime/assemblyBinding"
    let dependentAssembly =
        assemblyBinding.Elements <| XName.Get "dependentAssembly"
        |> Seq.tryFind(fun dependentAssembly ->
            defaultArg
                (dependentAssembly
                 |> tryGetElement "assemblyIdentity"
                 |> Option.bind (tryGetAttribute "name")
                 |> Option.map(fun attribute -> attribute.Value = bindingRedirect.AssemblyName))
                false)
        |> function
           | Some dependentAssembly -> dependentAssembly
           | None ->
                let dependentAssembly = createElement "dependentAssembly" []
                let assemblyIdentity =
                    createElement "assemblyIdentity" ([ "name", Some bindingRedirect.AssemblyName
                                                        "publicKeyToken", bindingRedirect.PublicKeyToken
                                                        "culture", bindingRedirect.Culture ]
                                                      |> Seq.choose(fun (key,value) -> match value with Some v -> Some(key, v) | None -> None))

                dependentAssembly.Add assemblyIdentity
                assemblyBinding.Add dependentAssembly
                dependentAssembly
                
    let newRedirect = createElement "bindingRedirect" [ "oldVersion", sprintf "0.0.0.0-%s" bindingRedirect.Version; "newVersion", bindingRedirect.Version ]
    match dependentAssembly |> tryGetElement "bindingRedirect" with
    | Some redirect -> redirect.ReplaceWith(newRedirect)
    | None -> dependentAssembly.Add(newRedirect)
    doc