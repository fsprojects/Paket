/// Contains methods to read and manipulate project files.
module Paket.ProjectFile

open System.Xml
open System.IO

/// Reads the packages file which sits next to the projectFile.
let LoadReferencedPackages (projectFileName:string) = 
    let fi = FileInfo(projectFileName)
    let packagesDef = FileInfo(fi.Directory.FullName + Path.DirectorySeparatorChar.ToString() + "packages")
    if packagesDef.Exists then File.ReadAllLines(packagesDef.FullName) else [||]

let getProject (fileName:string) =
    let doc = new XmlDocument()
    doc.Load fileName
    let manager = new XmlNamespaceManager(doc.NameTable)
    manager.AddNamespace("ns", "http://schemas.microsoft.com/developer/msbuild/2003")
    doc,manager

type ReferenceNode = {
    DLLName : string
    Node: XmlNode
    Private : bool
    HintPath : string option
    }

let getReferences (doc : XmlDocument, manager) = 
    [ for node in doc.SelectNodes("//ns:Project/ns:ItemGroup/ns:Reference", manager) do
          let hintPath = ref None
          let privateDll = ref false
          for c in node.ChildNodes do
              if c.Name.ToLower() = "hintpath" then hintPath := Some c.InnerText
              if c.Name.ToLower() = "private" then privateDll := true

          yield { DLLName = node.Attributes.["Include"].InnerText.Split(',').[0]
                  Private = !privateDll
                  HintPath = !hintPath
                  Node = node } ]