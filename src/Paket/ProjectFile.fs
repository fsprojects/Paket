/// Contains methods to read and manipulate project files.
module Paket.ProjectFile

open System
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
    doc

type ReferenceNode = 
    { DLLName : string
      Node : XmlNode option
      Private : bool
      HintPath : string option }
    member x.Inner() = 
        String.Join(Environment.NewLine, 
                    [ match x.HintPath with
                      | Some path -> yield sprintf "      <HintPath>%s</HintPath>" path
                      | _ -> ()
                      if x.Private then yield "      <Private>True</Private>"])
    override x.ToString() = 
        String.Join(Environment.NewLine, 
                    [ yield sprintf "    <Reference Include=\"%s\">" x.DLLName
                      yield x.Inner()
                      yield "    </Reference>" ])

let getReferences (doc : XmlDocument) = 
    let manager = new XmlNamespaceManager(doc.NameTable)
    manager.AddNamespace("ns", "http://schemas.microsoft.com/developer/msbuild/2003")

    [ for node in doc.SelectNodes("//ns:Project/ns:ItemGroup/ns:Reference", manager) do
          let hintPath = ref None
          let privateDll = ref false
          for c in node.ChildNodes do
              if c.Name.ToLower() = "hintpath" then hintPath := Some c.InnerText
              if c.Name.ToLower() = "private" then privateDll := true

          yield { DLLName = node.Attributes.["Include"].InnerText.Split(',').[0]
                  Private = !privateDll
                  HintPath = !hintPath
                  Node = Some node } ]

let updateReference(doc : XmlDocument, referenceNode: ReferenceNode) =
    let nodes = getReferences (doc : XmlDocument)
    match nodes |> Seq.tryFind (fun node -> node.DLLName = referenceNode.DLLName) with
    | Some targetNode ->
        match targetNode.Node with
        | Some node -> 
            node.Attributes.["Include"].Value <- referenceNode.DLLName
            node.InnerXml <- Environment.NewLine +  referenceNode.Inner() + Environment.NewLine + "    "
        | _ -> failwith "Unexpected error"
    | None ->
        let manager = new XmlNamespaceManager(doc.NameTable)
        manager.AddNamespace("ns", "http://schemas.microsoft.com/developer/msbuild/2003")
        let firstNode =
            seq { for node in doc.SelectNodes("//ns:Project/ns:ItemGroup/ns:Reference", manager) -> node }
            |> Seq.head

        firstNode.ParentNode.InnerXml <- firstNode.ParentNode.InnerXml + Environment.NewLine + referenceNode.ToString() + Environment.NewLine

/// Finds all libraries in a nuget packge.
let FindAllProjects(folder) = DirectoryInfo(folder).EnumerateFiles("*.*proj", SearchOption.AllDirectories)