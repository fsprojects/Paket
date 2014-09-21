/// Contains Nuspec support.
module Paket.Nuspec

open System.Xml
open System.IO

[<RequireQualifiedAccess>]
type References =
| All
| Explicit of string list

let GetReferences(fileName:string) =
    let fi = FileInfo(fileName)
    if not fi.Exists then References.All else
    let doc = new XmlDocument()
    doc.Load fi.FullName

    let manager = new XmlNamespaceManager(doc.NameTable)
    manager.AddNamespace("ns", "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd")

    if [for node in doc.SelectNodes("//ns:references", manager) -> node] = [] then References.All else
    
    References.Explicit 
        [for node in doc.SelectNodes("//ns:reference", manager) do 
            yield node.Attributes.["file"].InnerText]