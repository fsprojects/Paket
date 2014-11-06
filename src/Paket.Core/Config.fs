module Paket.Config

open System.IO
open System.Xml

let rootElement = "configuration"

let getConfigNode (nodeName : string) =
    let rootNode = 
        if File.Exists Constants.PaketConfigFile then 
            let doc = new XmlDocument()
            doc.Load Constants.PaketConfigFile
            doc.DocumentElement
        else 
            if not (Directory.Exists Constants.PaketConfigFile) then 
                Directory.CreateDirectory Constants.PaketConfigFolder |> ignore

            let doc = new XmlDocument()
            let el = doc.CreateElement(rootElement)
            doc.AppendChild(el) |> ignore
            doc.Save Constants.PaketConfigFile
            el

    let node = rootNode.SelectSingleNode(sprintf "//%s" nodeName)
    if node <> null then
        node
    else
        let node = rootNode.OwnerDocument.CreateElement(nodeName)
        rootNode.AppendChild (node) 


let saveConfigNode (node : XmlNode) =
    node.OwnerDocument.Save (Constants. PaketConfigFile)
