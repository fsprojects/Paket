/// Contains Nuspec support.
module Paket.Nuspec

open System.Xml
open System.IO

[<RequireQualifiedAccess>]
type References =
| All
| Explicit of string list

[<Literal>]
let PlaceHolder = "_._"

let GetReferences(fileName:string) =
    let fi = FileInfo(fileName)
    if not fi.Exists then References.All else
    let doc = new XmlDocument()
    doc.Load fi.FullName

    let manager = new XmlNamespaceManager(doc.NameTable)
    manager.AddNamespace("ns1", "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd")
    manager.AddNamespace("ns2", "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd")

    let referencesNodes =
        [for node in doc.SelectNodes("//ns1:references", manager) do yield node
         for node in doc.SelectNodes("//ns2:references", manager) do yield node]

    if List.isEmpty referencesNodes then  References.All  else    
        References.Explicit(
            [for node in doc.SelectNodes("//ns1:reference", manager) do 
                yield node.Attributes.["file"].InnerText 
             for node in doc.SelectNodes("//ns2:reference", manager) do 
                yield node.Attributes.["file"].InnerText])