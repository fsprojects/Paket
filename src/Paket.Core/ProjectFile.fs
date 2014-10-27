namespace Paket

open Paket.Logging
open Paket.PackageResolver
open System
open System.IO
open System.Xml
open System.Collections.Generic
open Paket.Xml

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

    member this.Name = FileInfo(this.FileName).Name

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
        this.DeleteIfEmpty("//ns:ItemGroup")
        this.DeleteIfEmpty("//ns:When")
        this.DeleteIfEmpty("//ns:Otherwise")
        this.DeleteIfEmpty("//ns:Choose")
        this.DeleteIfEmpty("//ns:When")
        this.DeleteIfEmpty("//ns:Choose")

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

    member this.HasCustomNodes(model:InstallModel) =
        let libs = model.GetReferenceNames.Force()
        
        let hasCustom = ref false
        for node in this.Document.SelectNodes("//ns:Reference", this.Namespaces) do
            if Set.contains (node.Attributes.["Include"].InnerText.Split(',').[0]) libs then
                let isPaket = ref false
                for child in node.ChildNodes do
                    if child.Name = "Paket" then 
                        isPaket := true
                if not !isPaket then
                    hasCustom := true
            
        !hasCustom

    member this.HasFrameworkAssemblyNode(assemblyName) =        
        let found = ref false
        for node in this.Document.SelectNodes("//ns:Reference", this.Namespaces) do
            if node.Attributes.["Include"].InnerText.Split(',').[0] = assemblyName then
                found := true
            
        !found

    member this.DeleteCustomNodes(model:InstallModel) =
        let nodesToDelete = List<_>()
        
        let libs = model.GetReferenceNames.Force()
        for node in this.Document.SelectNodes("//ns:Reference", this.Namespaces) do
            if Set.contains (node.Attributes.["Include"].InnerText.Split(',').[0]) libs then          
                nodesToDelete.Add node

        if nodesToDelete |> Seq.isEmpty |> not then
            verbosefn "    - Deleting custom projects nodes for %s" model.PackageName

        for node in nodesToDelete do            
            node.ParentNode.RemoveChild(node) |> ignore

    member this.GenerateXml(model:InstallModel) =
        let createItemGroup references = 
            let itemGroup = createNode(this.Document,"ItemGroup")
                                
            for lib in references do
                match lib with
                | Reference.Library lib ->
                    let fi = new FileInfo(normalizePath lib)
                    
                    createNode(this.Document,"Reference")
                    |> addAttribute "Include" (fi.Name.Replace(fi.Extension,""))
                    |> addChild (createNodeWithText(this.Document,"HintPath",createRelativePath this.FileName fi.FullName))
                    |> addChild (createNodeWithText(this.Document,"Private","True"))
                    |> addChild (createNodeWithText(this.Document,"Paket","True"))
                    |> itemGroup.AppendChild
                    |> ignore
                | Reference.FrameworkAssemblyReference frameworkAssembly -> 
                    if not <| this.HasFrameworkAssemblyNode(frameworkAssembly) then
                        createNode(this.Document,"Reference")
                        |> addAttribute "Include" frameworkAssembly
                        |> addChild (createNodeWithText(this.Document,"Paket","True"))
                        |> itemGroup.AppendChild
                        |> ignore
                
            itemGroup

        let groupChooseNode = this.Document.CreateElement("Choose", Constants.ProjectDefaultNameSpace)
        let foundCase = ref false
        for group in model.Groups do
            let frameworks = group.Value.Frameworks
            let groupWhenNode = 
                createNode(this.Document,"When")
                |> addAttribute "Condition" group.Key


            let chooseNode = this.Document.CreateElement("Choose", Constants.ProjectDefaultNameSpace)

            let foundSpecialCase = ref false

            for kv in frameworks do
                let currentLibs = kv.Value.References
                let condition = kv.Key.GetFrameworkCondition()
                let whenNode = 
                    createNode(this.Document,"When")
                    |> addAttribute "Condition" condition                
               
                whenNode.AppendChild(createItemGroup currentLibs) |> ignore
                chooseNode.AppendChild(whenNode) |> ignore
                foundSpecialCase := true
                foundCase := true


            let fallbackLibs = group.Value.Fallbacks.References
            
            if !foundSpecialCase then
                let otherwiseNode = createNode(this.Document,"Otherwise")
                otherwiseNode.AppendChild(createItemGroup fallbackLibs) |> ignore
                chooseNode.AppendChild(otherwiseNode) |> ignore
                groupWhenNode.AppendChild(chooseNode) |> ignore
            else
                groupWhenNode.AppendChild(createItemGroup fallbackLibs) |> ignore
            
            groupChooseNode.AppendChild(groupWhenNode) |> ignore

        if !foundCase then
            let otherwiseNode = createNode(this.Document,"Otherwise")
            otherwiseNode.AppendChild(createItemGroup model.DefaultFallback.References) |> ignore
            groupChooseNode.AppendChild(otherwiseNode) |> ignore
            groupChooseNode
        else
            createItemGroup model.DefaultFallback.References
        

    member this.UpdateReferences(completeModel: Map<string,InstallModel>, usedPackages : Dictionary<string,bool>, hard, useTargets) = 
        this.DeletePaketNodes("Reference")  
        this.DeleteEmptyReferences()

        if hard then
            for kv in usedPackages do
                let installModel = completeModel.[kv.Key.ToLower()]
                this.DeleteCustomNodes(installModel)

        for kv in usedPackages do
            let packageName = kv.Key
            let installModel = completeModel.[packageName.ToLower()]

            if this.HasCustomNodes(installModel) then 
                verbosefn "  - custom nodes for %s ==> skipping" packageName 
            else
                this.ConfigureReference(installModel, useTargets)
              
    member private this.ConfigureReference(installModel : InstallModel, useTargets) =
        if not useTargets then
            let chooseNode = this.GenerateXml(installModel)
            this.ProjectNode.AppendChild(chooseNode) |> ignore
        else
            failwith "--usetargets not implemented"

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
    
    member this.AddImportForPaketTargets(relativeTargetsPath) =
        match this.Document.SelectNodes(sprintf "//ns:Import[@Project='%s']" relativeTargetsPath, this.Namespaces)
                            |> Seq.cast |> Seq.firstOrDefault with
        | Some _ -> ()
        | None -> 
            let node = this.CreateNode("Import") |> addAttribute "Project" relativeTargetsPath
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