namespace Paket

open Paket.Logging
open Paket.PackageResolver
open System
open System.IO
open System.Xml
open System.Collections.Generic
open Paket.Xml
open Nuspec

type FileItem = 
    { BuildAction : string
      Include : string 
      Link : string option }

/// Contains methods to read and manipulate project files.
type ProjectFile = 
    { FileName: string
      OriginalText : string
      Document : XmlDocument
      ProjectNode : XmlNode
      Namespaces : XmlNamespaceManager }

    /// Finds all project files
    static member FindAllProjects(folder) = 
        ["*.csproj";"*.fsproj";"*.vbproj"]
        |> List.map (fun projectType -> FindAllFiles(folder, projectType) |> Seq.toList)
        |> List.concat
        |> List.choose (fun fi -> ProjectFile.Load fi.FullName)

    static member FindReferencesFile (projectFile : FileInfo) =
        let specificReferencesFile = FileInfo(Path.Combine(projectFile.Directory.FullName, projectFile.Name + "." + Constants.ReferencesFile))
        if specificReferencesFile.Exists then Some specificReferencesFile.FullName
        else 
            let generalReferencesFile = FileInfo(Path.Combine(projectFile.Directory.FullName, Constants.ReferencesFile))
            if generalReferencesFile.Exists then Some generalReferencesFile.FullName
            else None

    member this.DeleteIfEmpty xPath =
        let nodesToDelete = List<_>()
        for node in this.Document.SelectNodes(xPath, this.Namespaces) do
            if node.ChildNodes.Count = 0 then
                nodesToDelete.Add node

        for node in nodesToDelete do
            node.ParentNode.RemoveChild(node) |> ignore

    member this.FindPaketNodes(name) = 
        [
            for node in this.Document.SelectNodes(sprintf "//ns:%s" name, this.Namespaces) do
                let isPaketNode = ref false
                for child in node.ChildNodes do
                        if child.Name = "Paket" then isPaketNode := true
            
                if !isPaketNode then yield node
        ]

    member this.DeletePaketNodes(name) =    
        let nodesToDelete = this.FindPaketNodes(name) 
        if nodesToDelete |> Seq.isEmpty |> not then
            verbosefn "    - Deleting Paket %s nodes" name

        for node in nodesToDelete do
            node.ParentNode.RemoveChild(node) |> ignore

    member this.CreateNode(name) = this.Document.CreateElement(name, Constants.ProjectDefaultNameSpace)

    member this.CreateNode(name,text) = 
        let node = this.CreateNode(name)
        node.InnerText <- text
        node

    member this.DeleteEmptyReferences() = 
        this.DeleteIfEmpty("//ns:Project/ns:Choose/ns:When/ns:ItemGroup")
        this.DeleteIfEmpty("//ns:Project/ns:Choose/ns:When")
        this.DeleteIfEmpty("//ns:Project/ns:Choose")
        this.DeleteIfEmpty("//ns:ItemGroup")

    member this.createFileItemNode fileItem =
        this.CreateNode(fileItem.BuildAction)
        |> addAttribute "Include" fileItem.Include
        |> addChild (this.CreateNode("Paket","True"))
        |> (fun n -> match fileItem.Link with
                     | Some link -> addChild (this.CreateNode("Link",link.Replace("\\","/"))) n
                     | _ -> n)

    member this.UpdateFileItems(fileItems : list<FileItem>, hard) = 
        this.DeletePaketNodes("Compile")
        this.DeletePaketNodes("Content")

        let newItemGroups = ["Content", this.CreateNode("ItemGroup")
                             "Compile", this.CreateNode("ItemGroup") ] |> dict

        for fileItem in fileItems do
            let paketNode = this.createFileItemNode fileItem
            let xpath = sprintf "//ns:%s[starts-with(@Include, '%s')]" 
                                fileItem.BuildAction 
                                (Path.GetDirectoryName(fileItem.Include))
            let fileItemsInSameDir = this.Document.SelectNodes(xpath, this.Namespaces) |> Seq.cast<XmlNode>
            if fileItemsInSameDir |> Seq.isEmpty 
            then 
                newItemGroups.[fileItem.BuildAction].AppendChild(paketNode) |> ignore
            else
                let existingNode = fileItemsInSameDir 
                                   |> Seq.tryFind (fun n -> n.Attributes.["Include"].Value = fileItem.Include)
                match existingNode with
                | Some existingNode ->
                    if hard 
                    then 
                        if not <| (existingNode.ChildNodes |> Seq.cast<XmlNode> |> Seq.exists (fun n -> n.Name = "Paket"))
                        then existingNode :?> XmlElement |> addChild (this.CreateNode("Paket", "True")) |> ignore
                    else verbosefn "  - custom nodes for %s in %s ==> skipping" fileItem.Include this.FileName
                | None  ->
                    let firstNode = fileItemsInSameDir |> Seq.head
                    firstNode.ParentNode.InsertBefore(paketNode, firstNode) |> ignore
        
        let firstItemGroup = this.Document.SelectNodes("//ns:ItemGroup", this.Namespaces) |> Seq.cast<XmlNode> |> Seq.firstOrDefault
        for newItemGroup in newItemGroups.Values do
            if newItemGroup.HasChildNodes then 
                match firstItemGroup with
                | Some firstItemGroup -> firstItemGroup.ParentNode.InsertBefore(newItemGroup, firstItemGroup) |> ignore
                | None -> this.ProjectNode.AppendChild(newItemGroup) |> ignore

        this.DeleteIfEmpty("//ns:ItemGroup")


    member this.UpdateReferences(extracted: (ResolvedPackage * FileInfo[])[], usedPackages : Dictionary<string,bool>, hard) = 
        this.DeletePaketNodes("Reference")  
        for kv in usedPackages do
            let packageName = kv.Key
            let nuspec = FileInfo(sprintf "./packages/%s/%s.nuspec" packageName packageName)
            let references = Nuspec.GetReferences nuspec.FullName

            let files = 
                [ for (package:ResolvedPackage), libraries in extracted do              
                  if packageName.ToLower() = package.Name.ToLower() then
                    yield! libraries ]
                |> List.map (fun fi -> fi.FullName)

            let installModel = InstallModel.CreateFromLibs(packageName,SemVer.parse "0",files,references)
            if hard then
                installModel.DeleteCustomNodes(this.Document)

            if installModel.HasCustomNodes(this.Document) then verbosefn "  - custom nodes for %s ==> skipping" packageName else
            let chooseNode = installModel.GenerateXml(this.FileName, this.Document)
            this.ProjectNode.AppendChild(chooseNode) |> ignore

        this.DeleteEmptyReferences()

    member this.Save() =
        if Utils.normalizeXml this.Document <> this.OriginalText then 
            verbosefn "Project %s changed" this.FileName
            this.Document.Save(this.FileName)

    member this.GetPaketFileItems() =
        this.FindPaketNodes("Content")
        |> List.append <| this.FindPaketNodes("Compile")
        |> List.map (fun n ->  FileInfo(Path.Combine(Path.GetDirectoryName(this.FileName), n.Attributes.["Include"].Value)))

    member this.ReplaceNugetPackagesFile() =
        let nugetNode = this.Document.SelectSingleNode("//ns:*[@Include='packages.config']", this.Namespaces)
        if nugetNode = null then () else
        match [for node in this.Document.SelectNodes("//ns:*[@Include='" + Constants.ReferencesFile + "']", this.Namespaces) -> node] with 
        | [_] -> nugetNode.ParentNode.RemoveChild(nugetNode) |> ignore
        | [] -> nugetNode.Attributes.["Include"].Value <- Constants.ReferencesFile
        | _::_ -> failwithf "multiple %s nodes in project file %s" Constants.ReferencesFile this.FileName

    member this.RemoveNugetTargetsEntries() =
        let toDelete = 
            [ this.Document.SelectNodes("//ns:RestorePackages", this.Namespaces)
              this.Document.SelectNodes("//ns:Import[@Project='$(SolutionDir)\\.nuget\\nuget.targets']", this.Namespaces) 
              this.Document.SelectNodes("//ns:Target[@Name='EnsureNuGetPackageBuildImports']", this.Namespaces)]
            |> List.map (Seq.cast<XmlNode> >> Seq.firstOrDefault)
        toDelete
        |> List.iter 
            (Option.iter 
                (fun node -> 
                     let parent = node.ParentNode
                     node.ParentNode.RemoveChild(node) |> ignore
                     if not parent.HasChildNodes then parent.ParentNode.RemoveChild(parent) |> ignore))
    
    member this.AddImportForPaketTargets() =
        match this.Document.SelectNodes("//ns:Import[@Project='$(SolutionDir)\\.paket\\paket.targets']", this.Namespaces)
                            |> Seq.cast |> Seq.firstOrDefault with
        | Some _ -> ()
        | None -> 
            let node = this.CreateNode("Import") |> addAttribute "Project" "$(SolutionDir)\\.paket\\paket.targets"
            this.Document.SelectSingleNode("//ns:Project", this.Namespaces).AppendChild(node) |> ignore

    member this.DetermineBuildAction fileName =
        if Path.GetExtension(this.FileName) = Path.GetExtension(fileName) + "proj" 
        then "Compile"
        else "Content"

    static member Load(fileName:string) =
        try
            let fi = FileInfo(fileName)
            let doc = new XmlDocument()
            doc.Load fi.FullName

            let manager = new XmlNamespaceManager(doc.NameTable)
            manager.AddNamespace("ns", Constants.ProjectDefaultNameSpace)
            let projectNode = doc.SelectNodes("//ns:Project", manager).[0]
            Some { FileName = fi.FullName; Document = doc; ProjectNode = projectNode; Namespaces = manager; OriginalText = Utils.normalizeXml doc }
        with
        | exn -> 
            traceWarnfn "Unable to parse %s:%s      %s" fileName Environment.NewLine exn.Message
            None