namespace Paket

open Paket.Domain
open Paket.Logging
open System
open System.IO
open System.Xml
open System.Collections.Generic
open Paket.Xml

type FileItem = 
    { BuildAction : string
      Include : string 
      Link : string option }

type ProjectReference = 
    { Path : string
      Name : string
      GUID : Guid
      Private : bool }

[<RequireQualifiedAccess>]
type ProjectOutputType =
| Exe 
| Library

/// Contains methods to read and manipulate project files.
type ProjectFile = 
    { FileName: string
      OriginalText : string
      Document : XmlDocument
      ProjectNode : XmlNode }

    member this.Name = FileInfo(this.FileName).Name

    member this.GetCustomReferenceNodes() =
        [for node in this.Document |> getDescendants "Reference" do
            let isPaket = ref false
            for child in node.ChildNodes do
                if child.Name = "Paket" then 
                    isPaket := true
            if not !isPaket then
                yield node]

    /// Finds all project files
    static member FindAllProjects(folder) = 
        FindAllFiles(folder, "*.*proj")
        |> Array.filter (fun f -> f.Extension = ".csproj" || f.Extension = ".fsproj" || f.Extension = ".vbproj")
        |> Array.choose (fun fi -> ProjectFile.Load fi.FullName)

    static member FindReferencesFile (projectFile : FileInfo) =
        let specificReferencesFile = FileInfo(Path.Combine(projectFile.Directory.FullName, projectFile.Name + "." + Constants.ReferencesFile))
        if specificReferencesFile.Exists then Some specificReferencesFile.FullName
        else
            let rec findInDir (currentDir:DirectoryInfo) = 
                let generalReferencesFile = FileInfo(Path.Combine(currentDir.FullName, Constants.ReferencesFile))
                if generalReferencesFile.Exists then Some generalReferencesFile.FullName
                elif currentDir.Parent = null then None
                else findInDir currentDir.Parent 
                    
            findInDir projectFile.Directory

    member this.CreateNode(name) = 
        this.Document.CreateElement(name, Constants.ProjectDefaultNameSpace)

    member this.CreateNode(name, text) = 
        let node = this.CreateNode(name)
        node.InnerText <- text
        node

    member this.DeleteIfEmpty name =
        let nodesToDelete = List<_>()
        for node in this.Document |> getDescendants name do
            if node.ChildNodes.Count = 0 then
                nodesToDelete.Add node

        for node in nodesToDelete do
            node.ParentNode.RemoveChild(node) |> ignore

    member this.FindPaketNodes(name) = 
        [
            for node in this.Document |> getDescendants name do
                let isPaketNode = ref false
                for child in node.ChildNodes do
                    if child.Name = "Paket" then isPaketNode := true
            
                if !isPaketNode then yield node
        ]

    member this.GetFrameworkAssemblies() = 
        [for node in this.Document |> getDescendants "Reference" do
            let hasHintPath = ref false
            for child in node.ChildNodes do
                if child.Name = "HintPath" then 
                    hasHintPath := true
            if not !hasHintPath then
                yield node.Attributes.["Include"].InnerText.Split(',').[0] ]

    member this.DeletePaketNodes(name) =    
        let nodesToDelete = this.FindPaketNodes(name) 
        if nodesToDelete |> Seq.isEmpty |> not then
            verbosefn "    - Deleting Paket %s nodes" name

        for node in nodesToDelete do
            node.ParentNode.RemoveChild(node) |> ignore

    member this.createFileItemNode fileItem =
        this.CreateNode(fileItem.BuildAction)
        |> addAttribute "Include" fileItem.Include
        |> addChild (this.CreateNode("Paket","True"))
        |> (fun n -> match fileItem.Link with
                     | Some link -> addChild (this.CreateNode("Link" ,link.Replace("\\","/"))) n
                     | _ -> n)

    member this.UpdateFileItems(fileItems : FileItem list, hard) = 
        this.DeletePaketNodes("Compile")
        this.DeletePaketNodes("Content")

        let firstItemGroup = this.ProjectNode |> getNodes "ItemGroup" |> Seq.firstOrDefault

        let newItemGroups = 
            match firstItemGroup with
            | None ->
                ["Content", this.CreateNode("ItemGroup")
                 "Compile", this.CreateNode("ItemGroup") ] 
            | Some node ->
                ["Content", node :?> XmlElement
                 "Compile", node :?> XmlElement ] 
            |> dict

        for fileItem in fileItems |> List.rev do
            let paketNode = this.createFileItemNode fileItem

            let fileItemsInSameDir =
                this.Document 
                |> getDescendants fileItem.BuildAction
                |> List.filter (fun node -> 
                    match node |> getAttribute "Include" with
                    | Some path when path.StartsWith(Path.GetDirectoryName(fileItem.Include)) ->
                        true
                    | _ -> false)

            if fileItemsInSameDir |> Seq.isEmpty 
            then 
                newItemGroups.[fileItem.BuildAction].PrependChild(paketNode) |> ignore
            else
                let existingNode = fileItemsInSameDir 
                                   |> Seq.tryFind (fun n -> n.Attributes.["Include"].Value = fileItem.Include)
                match existingNode with
                | Some existingNode ->
                    if hard 
                    then 
                        if not <| (existingNode.ChildNodes |> Seq.cast<XmlNode> |> Seq.exists (fun n -> n.Name = "Paket"))
                        then existingNode :?> XmlElement |> addChild (this.CreateNode("Paket","True")) |> ignore
                    else verbosefn "  - custom nodes for %s in %s ==> skipping" fileItem.Include this.FileName
                | None  ->
                    let firstNode = fileItemsInSameDir |> Seq.head
                    firstNode.ParentNode.InsertBefore(paketNode, firstNode) |> ignore
        
        this.DeleteIfEmpty("ItemGroup")

    member this.GetCustomModelNodes(model:InstallModel) =
        let libs = model.GetReferenceNames()
        
        this.GetCustomReferenceNodes()
        |> List.filter (fun node -> Set.contains (node.Attributes.["Include"].InnerText.Split(',').[0]) libs)
    
    member this.DeleteCustomModelNodes(model:InstallModel) =
        let nodesToDelete = this.GetCustomModelNodes(model)
        
        if nodesToDelete <> [] then
            let (PackageName name) = model.PackageName
            verbosefn "    - Deleting custom projects nodes for %s" name

        for node in nodesToDelete do            
            node.ParentNode.RemoveChild(node) |> ignore

    member this.GenerateXml(model:InstallModel) =
        let references = 
            this.GetCustomReferenceNodes()
            |> List.map (fun node -> node.Attributes.["Include"].InnerText.Split(',').[0])
            |> Set.ofList

        let model = model.FilterReferences(references)
        let createItemGroup references = 
            let itemGroup = this.CreateNode("ItemGroup")
                                
            for lib in references do
                match lib with
                | Reference.Library lib ->
                    let fi = new FileInfo(normalizePath lib)
                    
                    this.CreateNode("Reference")
                    |> addAttribute "Include" (fi.Name.Replace(fi.Extension,""))
                    |> addChild (this.CreateNode("HintPath", createRelativePath this.FileName fi.FullName))
                    |> addChild (this.CreateNode("Private","True"))
                    |> addChild (this.CreateNode("Paket","True"))
                    |> itemGroup.AppendChild
                    |> ignore
                | Reference.FrameworkAssemblyReference frameworkAssembly ->              
                    this.CreateNode("Reference")
                    |> addAttribute "Include" frameworkAssembly
                    |> addChild (this.CreateNode("Paket","True"))
                    |> itemGroup.AppendChild
                    |> ignore
            itemGroup

        let conditions =
            model.LibFolders
            |> List.map (fun lib -> PlatformMatching.getCondition lib.Targets,createItemGroup lib.Files.References)
            |> List.sortBy fst

        match conditions with
        |  ["$(TargetFrameworkIdentifier) == 'true'",itemGroup] -> itemGroup
        |  _ ->
            let chooseNode = this.CreateNode("Choose")

            conditions
            |> List.map (fun (condition,itemGroup) ->
                let whenNode = 
                    this.CreateNode("When")
                    |> addAttribute "Condition" condition                
               
                whenNode.AppendChild(itemGroup) |> ignore
                whenNode)
            |> List.iter(fun node -> chooseNode.AppendChild(node) |> ignore)

            chooseNode
        

    member this.UpdateReferences(completeModel: Map<NormalizedPackageName,InstallModel>, usedPackages : Set<NormalizedPackageName>, hard) = 
        this.DeletePaketNodes("Reference")  
        
        ["ItemGroup";"When";"Otherwise";"Choose";"When";"Choose"]
        |> List.iter this.DeleteIfEmpty

        
        completeModel
        |> Seq.filter (fun kv -> usedPackages.Contains kv.Key)
        |> Seq.map (fun kv -> 
            if hard then
                this.DeleteCustomModelNodes(kv.Value)

            this.GenerateXml kv.Value)
        |> Seq.filter (fun node -> node.ChildNodes.Count > 0)
        |> Seq.iter (this.ProjectNode.AppendChild >> ignore)
                
    member this.Save() =
        if Utils.normalizeXml this.Document <> this.OriginalText then 
            verbosefn "Project %s changed" this.FileName
            this.Document.Save(this.FileName)

    member this.GetPaketFileItems() =
        this.FindPaketNodes("Content")
        |> List.append <| this.FindPaketNodes("Compile")
        |> List.map (fun n -> FileInfo(Path.Combine(Path.GetDirectoryName(this.FileName), n.Attributes.["Include"].Value)))

    member this.GetInterProjectDependencies() =  
        let forceGetInnerText node name =
            match node |> getNode name with 
            | Some n -> n.InnerText
            | None -> failwithf "unable to parse %s" node.Name

        [for n in this.Document |> getDescendants "ProjectReference" -> 
            { Path = n.Attributes.["Include"].Value
              Name = forceGetInnerText n "Name"
              GUID =  forceGetInnerText n "Project" |> Guid.Parse
              Private =  forceGetInnerText n "Private" |> bool.Parse }]

    member this.ReplaceNuGetPackagesFile() =
        let noneNodes = this.Document |> getDescendants "None"
        match noneNodes |> List.tryFind (fun n -> n |> getAttribute "Include" = Some "packages.config") with
        | None -> ()
        | Some nugetNode ->
            match noneNodes |> List.filter (fun n -> n |> getAttribute "Include" = Some Constants.ReferencesFile) with 
            | [_] -> nugetNode.ParentNode.RemoveChild(nugetNode) |> ignore
            | [] -> nugetNode.Attributes.["Include"].Value <- Constants.ReferencesFile
            | _::_ -> failwithf "multiple %s nodes in project file %s" Constants.ReferencesFile this.FileName

    member this.RemoveNuGetTargetsEntries() =
        let toDelete = 
            [ this.Document |> getDescendants "RestorePackages" |> Seq.firstOrDefault
              this.Document 
              |> getDescendants "Import" 
              |> List.tryFind (fun n -> n |> getAttribute "Project" = Some "$(SolutionDir)\\.nuget\\nuget.targets")
              this.Document
              |> getDescendants "Target"
              |> List.tryFind (fun n -> n |> getAttribute "Name" = Some "EnsureNuGetPackageBuildImports") ]
            |> List.choose id
        
        toDelete
        |> List.iter 
            (fun node -> 
                let parent = node.ParentNode
                node.ParentNode.RemoveChild node |> ignore
                if not parent.HasChildNodes then 
                    parent.ParentNode.RemoveChild parent |> ignore)

    member this.OutputType =
        seq {for outputType in this.Document |> getDescendants "OutputType" ->
                match outputType.InnerText with
                | "Exe" -> ProjectOutputType.Exe
                | _     -> ProjectOutputType.Library }
        |> Seq.head

    member this.GetTargetFramework() =
        seq {for outputType in this.Document |> getDescendants "TargetFrameworkVersion" ->
                outputType.InnerText  }
        |> Seq.map (fun s -> // TODO make this a separate function
                        s.Replace("v","net")
                        |> FrameworkIdentifier.Extract)                        
        |> Seq.map (fun o -> o.Value)
        |> Seq.head
    
    member this.AddImportForPaketTargets(relativeTargetsPath) =
        match this.Document 
              |> getDescendants "Import" 
              |> List.tryFind (fun n -> n |> getAttribute "Project" = Some relativeTargetsPath) with
        | Some _ -> ()
        | None -> 
            let node = this.CreateNode("Import") |> addAttribute "Project" relativeTargetsPath
            this.ProjectNode.AppendChild(node) |> ignore

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
            let projectNode = 
                match doc |> getNode "Project" with
                | Some node -> node
                | _ -> failwith "unable to find Project node in file %s" fileName
            Some { FileName = fi.FullName; Document = doc; ProjectNode = projectNode; OriginalText = Utils.normalizeXml doc }
        with
        | exn -> 
            traceWarnfn "Unable to parse %s:%s      %s" fileName Environment.NewLine exn.Message
            None