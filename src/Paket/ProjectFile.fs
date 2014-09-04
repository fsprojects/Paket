namespace Paket

open System
open System.IO
open System.Xml


/// Contains methods to analyze .NET Framework Conditions.
type FrameworkConditionType =
| Unknown
| All
| Framework of string

/// Contains methods to analyze .NET Framework Conditions.
type FramworkCondition = 
    { Framework : FrameworkConditionType }
    static member DetectFromPath(path : string) = 
        let fi = new FileInfo(path)
        let path = path.Replace("\\","/").ToLower()
        if path.Contains "lib/net20/" then { Framework = Framework  "v2.0" }
        else if path.Contains "lib/net35/" then { Framework = Framework "v3.5" }
        else if path.Contains "lib/net40/" then { Framework = Framework "v4.0" }
        else if path.Contains "lib/net45/" then { Framework = Framework "v4.5" }
        else if path.Contains("lib/" + fi.Name.ToLower())  then { Framework = All }
        else { Framework = Unknown }

/// Contains methods to read and manipulate project file ndoes.
type ReferenceNode = 
    { DLLName : string
      Node : XmlNode option
      Condition : string option
      Private : bool
      HintPath : string option }
    member x.Inner() = 
        String.Join(Environment.NewLine, 
                    [ match x.HintPath with
                      | Some path -> yield sprintf "      <HintPath>%s</HintPath>" path
                      | _ -> ()
                      if x.Private then yield "      <Private>True</Private>"])
    override x.ToString() = 
        let condition =
            match x.Condition with
            | Some c -> sprintf " Condition=\"%s\"" c
            | _ -> ""
        String.Join(Environment.NewLine, 
                    [ yield sprintf "    <Reference Include=\"%s\"%s>" x.DLLName condition
                      yield x.Inner()
                      yield "    </Reference>" ])

/// Contains methods to read and manipulate project files.
type ProjectFile = 
    { FileName: string
      OriginalText : string
      Document : XmlDocument
      Namespaces : XmlNamespaceManager }
    member this.GetReferences() =
        [ for node in this.Document.SelectNodes("//ns:Project/ns:ItemGroup/ns:Reference", this.Namespaces) do
              let hintPath = ref None
              let privateDll = ref false
              for c in node.ChildNodes do
                  if c.Name.ToLower() = "hintpath" then hintPath := Some c.InnerText
                  if c.Name.ToLower() = "private" then privateDll := true
              yield { DLLName = node.Attributes.["Include"].InnerText.Split(',').[0]
                      Private = !privateDll
                      HintPath = !hintPath
                      Condition = 
                        match [for attr in node.Attributes -> attr.Name, attr.Value] |> List.tryFind (fun (name,v) -> name.ToLower() = "condition") with
                        | Some(n,v) -> Some v
                        | None -> None
                      Node = Some node } ]

    member this.UpdateReference(referenceNode: ReferenceNode) =
        let nodes = this.GetReferences()
        match nodes |> Seq.tryFind (fun node -> node.DLLName = referenceNode.DLLName && node.Condition = referenceNode.Condition) with
        | Some targetNode ->
            match targetNode.Node with
            | Some node -> 
                node.Attributes.["Include"].Value <- referenceNode.DLLName
                let newText = Environment.NewLine + referenceNode.Inner() + Environment.NewLine + "    "
                node.InnerXml <- newText
            | _ -> failwith "Unexpected error"
        | None ->
            let firstNode =
                seq { for node in this.Document.SelectNodes("//ns:Project/ns:ItemGroup/ns:Reference", this.Namespaces) -> node }
                |> Seq.last

            let copy = firstNode.Clone()
            copy.Attributes.["Include"].Value <- referenceNode.DLLName
            match referenceNode.Condition with
            | Some c ->
                if [for attr in copy.Attributes -> attr.Name.ToLower() ] |> List.exists ((=) "condition") then
                    copy.Attributes.["Condition"].Value <- c
                else
                    (copy :?> XmlElement).SetAttribute("Condition", c) |> ignore
            | None -> ()

            copy.InnerXml <- Environment.NewLine + referenceNode.Inner() + Environment.NewLine + "    "
            firstNode.ParentNode.AppendChild(copy) |> ignore

    member this.UpdateReferences(extracted,usedPackages:System.Collections.Generic.HashSet<string>) =
        for package, libraries in extracted do
            if usedPackages.Contains package.Name then
                for (lib:FileInfo) in libraries do
                    let relativePath = Uri(this.FileName).MakeRelativeUri(Uri(lib.FullName)).ToString()

                    let framworkCondition = FramworkCondition.DetectFromPath relativePath
                    let installIt,condition = 
                        match framworkCondition.Framework with
                        | Unknown -> false,None 
                        | Framework fw -> true,Some(sprintf "$(TargetFrameworkVersion) == '%s'" fw)
                        | All -> true,None

                    
                    if installIt then
                        this.UpdateReference ({ DLLName = lib.Name.Replace(lib.Extension, "")
                                                HintPath = Some(relativePath.Replace("/", "\\"))
                                                Private = true
                                                Condition = condition
                                                Node = None })

        if Utils.normalizeXml this.Document <> this.OriginalText then
            this.Document.Save(this.FileName)

    static member Load(fileName:string) =
        let fi = FileInfo(fileName)
        let doc = new XmlDocument()
        doc.Load fi.FullName

        let manager = new XmlNamespaceManager(doc.NameTable)
        manager.AddNamespace("ns", "http://schemas.microsoft.com/developer/msbuild/2003")    
        { FileName = fi.FullName; Document = doc; Namespaces = manager; OriginalText = Utils.normalizeXml doc }