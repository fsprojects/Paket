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
        let path = path.Replace("\\", "/").ToLower()
        let fi = new FileInfo(path)
        if path.Contains "lib/1.0/" then { Framework = Framework "v1.0" }
        elif path.Contains "lib/1.1/" then { Framework = Framework "v1.1" }
        elif path.Contains "lib/2.0/" then { Framework = Framework "v2.0" }
        elif path.Contains "lib/net20/" then { Framework = Framework "v2.0" }
        elif path.Contains "lib/net35/" then { Framework = Framework "v3.5" }
        elif path.Contains "lib/net40/" then { Framework = Framework "v4.0" }
        elif path.Contains "lib/net40-full/" then { Framework = Framework "v4.0" }
        elif path.Contains "lib/net40-client/" then { Framework = Framework "v4.0" } // TODO: fix me
        elif path.Contains "lib/net45/" then { Framework = Framework "v4.5" }
        elif path.Contains("lib/" + fi.Name.ToLower()) then { Framework = All }
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
    static member DefaultNameSpace = "http://schemas.microsoft.com/developer/msbuild/2003"
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

    member this.DeleteOldReferences(name) =
        this.GetReferences()
        |> Seq.filter (fun node -> node.DLLName = name) 
        |> Seq.iter (fun targetNode -> 
            match targetNode.Node with
            | Some node -> node.ParentNode.RemoveChild(node) |> ignore
            | None -> ())

    member this.AddReference(referenceNode: ReferenceNode) =
        let firstNode =
            seq { for node in this.Document.SelectNodes("//ns:Project/ns:ItemGroup/ns:Reference", this.Namespaces) -> node }
            |> Seq.last

            
        let copy = this.Document.CreateElement("Reference", ProjectFile.DefaultNameSpace)
        copy.SetAttribute("Include", referenceNode.DLLName)
        match referenceNode.Condition with
        | Some c ->  copy.SetAttribute("Condition", c) |> ignore
        | None -> ()

        match referenceNode.HintPath with
        | Some hintPath ->
            let element = this.Document.CreateElement("HintPath",ProjectFile.DefaultNameSpace)
            element.InnerText <- hintPath
            
            copy.AppendChild(element) |> ignore
        | None -> ()
            
        match referenceNode.Private with
        | true ->
            let element = this.Document.CreateElement("Private",ProjectFile.DefaultNameSpace)
            element.InnerText <- "True"
            
            copy.AppendChild(element) |> ignore
        | _ -> ()

        firstNode.ParentNode.AppendChild(copy) |> ignore

    member this.UpdateReferences(extracted,usedPackages:System.Collections.Generic.HashSet<string>) =
        for _, libraries in extracted do            
            let libraries = libraries |> Seq.toArray
            for (lib:FileInfo) in libraries do                                       
                this.DeleteOldReferences (lib.Name.Replace(lib.Extension, ""))

        for package, libraries in extracted do
            if usedPackages.Contains package.Name then
                let libraries = libraries |> Seq.toArray
                for (lib:FileInfo) in libraries do
                    let relativePath = Uri(this.FileName).MakeRelativeUri(Uri(lib.FullName)).ToString()

                    let framworkCondition = FramworkCondition.DetectFromPath relativePath
                    let installIt,condition =
                        if libraries.Length = 1 then true,None else // we don't have any other chance
                        match framworkCondition.Framework with
                        | Unknown -> false,None 
                        | Framework fw -> true,Some(sprintf "$(TargetFrameworkVersion) == '%s'" fw)
                        | All -> true,None

                    
                    if installIt then                        
                        { DLLName = lib.Name.Replace(lib.Extension, "")
                          HintPath = Some(relativePath.Replace("/", "\\"))
                          Private = true
                          Condition = condition
                          Node = None }
                        |> this.AddReference

        if Utils.normalizeXml this.Document <> this.OriginalText then
            this.Document.Save(this.FileName)

    static member Load(fileName:string) =
        let fi = FileInfo(fileName)
        let doc = new XmlDocument()
        doc.Load fi.FullName

        let manager = new XmlNamespaceManager(doc.NameTable)
        manager.AddNamespace("ns", ProjectFile.DefaultNameSpace)
        { FileName = fi.FullName; Document = doc; Namespaces = manager; OriginalText = Utils.normalizeXml doc }