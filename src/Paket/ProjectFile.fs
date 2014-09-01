module Paket.ProjectFile

open System.Xml

let getProject (text:string) =
    let xmlDocument = new XmlDocument()
    xmlDocument.LoadXml text
    xmlDocument