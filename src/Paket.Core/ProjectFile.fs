namespace Paket

open Paket.Domain
open Paket.Logging
open System
open System.IO
open System.Xml
open System.Collections.Generic
open Paket.Xml

/// File item inside of project files.
type FileItem = 
    { BuildAction : string
      Include : string 
      Link : string option }

/// Project references inside of project files.
type ProjectReference = 
    { Path : string
      Name : string
      GUID : Guid }

/// Project output type.
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

    member private this.FindNodes paketOnes name =
        [for node in this.Document |> getDescendants name do
            let isPaketNode = ref false
            for child in node.ChildNodes do
                if child.Name = "Paket" && child.InnerText.ToLower() = "true" then 
                    isPaketNode := true

            if !isPaketNode = paketOnes then yield node]

    member this.Name = FileInfo(this.FileName).Name

    member this.NameWithoutExtension = Path.GetFileNameWithoutExtension this.Name

    member this.GetCustomReferenceAndFrameworkNodes() = this.FindNodes false "Reference"

    /// Finds all project files
    static member FindAllProjects(folder) = 
        FindAllFiles(folder, "*.*proj")
        |> Array.filter (fun f -> f.Extension = ".csproj" || f.Extension = ".fsproj" || f.Extension = ".vbproj")
        |> Array.choose (fun fi -> ProjectFile.Load fi.FullName)

    static member FindCorrespondingFile (projectFile : FileInfo, correspondingFile:string) =
        let specificFile = FileInfo(Path.Combine(projectFile.Directory.FullName, projectFile.Name + "." + correspondingFile))
        if specificFile.Exists then Some specificFile.FullName
        else
            let rec findInDir (currentDir:DirectoryInfo) = 
                let generalFile = FileInfo(Path.Combine(currentDir.FullName, correspondingFile))
                if generalFile.Exists then Some generalFile.FullName
                elif (FileInfo(Path.Combine(currentDir.FullName, Constants.DependenciesFileName))).Exists then None
                elif currentDir.Parent = null then None
                else findInDir currentDir.Parent 
                    
            findInDir projectFile.Directory

    static member FindReferencesFile (projectFile : FileInfo) = ProjectFile.FindCorrespondingFile(projectFile, Constants.ReferencesFile)

    static member FindTemplatesFile (projectFile : FileInfo) = ProjectFile.FindCorrespondingFile(projectFile, Constants.TemplateFile)

    static member FindOrCreateReferencesFile (projectFile : FileInfo) =
        match ProjectFile.FindReferencesFile projectFile with
        | None ->
            let newFileName =
                let fi = FileInfo(Path.Combine(projectFile.Directory.FullName,Constants.ReferencesFile))
                if fi.Exists then
                    Path.Combine(projectFile.Directory.FullName,projectFile.Name + "." + Constants.ReferencesFile)
                else
                    fi.FullName

            ReferencesFile.New newFileName
        | Some fileName -> ReferencesFile.FromFile fileName

    member this.CreateNode(name) = 
        this.Document.CreateElement(name, Constants.ProjectDefaultNameSpace)

    member this.HasPackageInstalled(package:NormalizedPackageName) =        
        let proj = FileInfo(this.FileName)
        match ProjectFile.FindReferencesFile proj with
        | None -> false
        | Some fileName -> 
            let referencesFile = ReferencesFile.FromFile fileName
            referencesFile.NugetPackages |> Seq.exists (fun p -> NormalizedPackageName p.Name = package)

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

    member this.FindPaketNodes(name) = this.FindNodes true name

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
            let libReferenceNode = 
                this.CreateNode(fileItem.BuildAction)
                |> addAttribute "Include" fileItem.Include
                |> addChild (this.CreateNode("Paket","True"))
                |> (fun n -> match fileItem.Link with
                             | Some link -> addChild (this.CreateNode("Link" ,link.Replace("\\","/"))) n
                             | _ -> n)

            let fileItemsInSameDir =
                this.Document 
                |> getDescendants fileItem.BuildAction
                |> List.filter (fun node -> 
                    match node |> getAttribute "Include" with
                    | Some path when path.StartsWith(Path.GetDirectoryName(fileItem.Include)) -> true
                    | _ -> false)

            if Seq.isEmpty fileItemsInSameDir then 
                newItemGroups.[fileItem.BuildAction].PrependChild(libReferenceNode) |> ignore
            else
                let existingNode = 
                    fileItemsInSameDir 
                    |> Seq.tryFind (withAttributeValue "Include" fileItem.Include)

                match existingNode with
                | Some existingNode ->
                    if hard 
                    then 
                        if not <| (existingNode.ChildNodes |> Seq.cast<XmlNode> |> Seq.exists (fun n -> n.Name = "Paket"))
                        then existingNode :?> XmlElement |> addChild (this.CreateNode("Paket","True")) |> ignore
                    else verbosefn "  - custom nodes for %s in %s ==> skipping" fileItem.Include this.FileName
                | None  ->
                    let firstNode = fileItemsInSameDir |> Seq.head
                    firstNode.ParentNode.InsertBefore(libReferenceNode, firstNode) |> ignore
        
        this.DeleteIfEmpty("PropertyGroup")
        this.DeleteIfEmpty("ItemGroup")
        this.DeleteIfEmpty("Choose")

    member this.GetCustomModelNodes(model:InstallModel) =
        let libs =
            model.GetLibReferencesLazy.Force()
            |> Set.map (fun lib -> lib.ReferenceName)
       
        this.GetCustomReferenceAndFrameworkNodes()
        |> List.filter (fun node -> 
            let libName = node.Attributes.["Include"].InnerText.Split(',').[0]
            Set.contains libName libs)

    member this.DeleteCustomModelNodes(model:InstallModel) =
        let nodesToDelete = 
            this.GetCustomModelNodes(model)
            |> List.filter (fun node ->
                let isFrameworkNode = ref true
                for child in node.ChildNodes do
                    if child.Name = "HintPath" then isFrameworkNode := false
                    if child.Name = "Private" then isFrameworkNode := false

                not !isFrameworkNode)
        
        if nodesToDelete <> [] then
            let (PackageName name) = model.PackageName
            verbosefn "    - Deleting custom projects nodes for %s" name

        for node in nodesToDelete do            
            node.ParentNode.RemoveChild(node) |> ignore

    member this.GenerateXml(model:InstallModel,copyLocal:bool,importTargets:bool) =
        let references = 
            this.GetCustomReferenceAndFrameworkNodes()
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
                    |> addChild (this.CreateNode("Private",if copyLocal then "True" else "False"))
                    |> addChild (this.CreateNode("Paket","True"))
                    |> itemGroup.AppendChild
                    |> ignore
                | Reference.FrameworkAssemblyReference frameworkAssembly ->              
                    this.CreateNode("Reference")
                    |> addAttribute "Include" frameworkAssembly
                    |> addChild (this.CreateNode("Paket","True"))
                    |> itemGroup.AppendChild
                    |> ignore
                | Reference.TargetsFile _ -> ()
            itemGroup

        let createPropertyGroup references = 
            let propertyGroup = this.CreateNode("PropertyGroup")
                      
            let propertyNames =          
                references
                |> Seq.choose (fun lib ->
                    if not importTargets then None else
                    match lib with
                    | Reference.Library _ -> None
                    | Reference.FrameworkAssemblyReference _ -> None
                    | Reference.TargetsFile targetsFile ->                        
                        let fi = new FileInfo(normalizePath targetsFile)
                        let propertyName = "__paket__" + fi.Name.ToString().Replace(" ","_").Replace(".","_")
                        
                        let path = createRelativePath this.FileName (fi.FullName.Replace(fi.Extension,""))
                        let s = path.Substring(path.LastIndexOf("build\\") + 6)
                        let node = this.CreateNode propertyName
                        node.InnerText <- s
                        node
                        |> propertyGroup.AppendChild 
                        |> ignore
                        Some(propertyName,createRelativePath this.FileName fi.FullName,path.Substring(0,path.LastIndexOf("build\\") + 6)))
                |> Set.ofSeq
                    
            propertyNames,propertyGroup

        let conditions =
            model.ReferenceFileFolders
            |> List.map (fun lib -> PlatformMatching.getCondition lib.Targets,createItemGroup lib.Files.References)
            |> List.sortBy fst

        let targetsFileConditions =
            model.TargetsFileFolders
            |> List.map (fun lib -> PlatformMatching.getCondition lib.Targets,createPropertyGroup lib.Files.References)
            |> List.sortBy fst

        let chooseNode =
            match conditions with
            |  ["$(TargetFrameworkIdentifier) == 'true'",itemGroup] -> itemGroup
            |  _ ->
                let chooseNode = this.CreateNode("Choose")

                let containsReferences = ref false

                conditions
                |> List.map (fun (condition,itemGroup) ->
                    let whenNode = 
                        this.CreateNode("When")
                        |> addAttribute "Condition" condition 
               
                    if not itemGroup.IsEmpty then
                        whenNode.AppendChild(itemGroup) |> ignore
                        containsReferences := true
                    whenNode)
                |> List.iter(fun node -> chooseNode.AppendChild(node) |> ignore)              
                                
                if !containsReferences then chooseNode else this.CreateNode("Choose")

        let propertyNames,propertyChooseNode =
            match targetsFileConditions with
            |  ["$(TargetFrameworkIdentifier) == 'true'",(propertyNames,propertyGroup)] ->
                [propertyNames],this.CreateNode("Choose")
            |  _ ->
                let propertyChooseNode = this.CreateNode("Choose")

                let containsProperties = ref false
                targetsFileConditions
                |> List.map (fun (condition,(propertyNames,propertyGroup)) ->
                    let whenNode = 
                        this.CreateNode("When")
                        |> addAttribute "Condition" condition 
                    if not <| Set.isEmpty propertyNames then
                        whenNode.AppendChild(propertyGroup) |> ignore
                        containsProperties := true
                    whenNode)
                |> List.iter(fun node -> propertyChooseNode.AppendChild(node) |> ignore)                                                               
                
                (targetsFileConditions |> List.map (fun (_,(propertyNames,_)) -> propertyNames)),
                (if !containsProperties then propertyChooseNode else this.CreateNode("Choose"))
                

        let propertyNameNodes = 
            propertyNames
            |> Seq.concat
            |> Seq.distinctBy (fun (x,_,_) -> x)
            |> Seq.map (fun (propertyName,path,buildPath) -> 
                let fileName = 
                    match propertyName.ToLower() with
                    | _ when propertyChooseNode.ChildNodes.Count = 0 -> path
                    | name when name.EndsWith "props" ->
                        sprintf "%s$(%s).props" buildPath propertyName 
                    | name when name.EndsWith "targets" ->
                        sprintf "%s$(%s).targets" buildPath propertyName
                    | _ -> failwithf "Unknown .targets filename %s" propertyName

                this.CreateNode("Import")
                |> addAttribute "Project" fileName
                |> addAttribute "Condition" (sprintf "Exists('%s')" fileName)
                |> addAttribute "Label" "Paket")
            |> Seq.toList

        propertyNameNodes,chooseNode,propertyChooseNode
        
    member this.RemovePaketNodes() =
        this.DeletePaketNodes("Reference")

        let rec PaketNodes (node:XmlNode) =
            [for node in node.ChildNodes do                
                if node.Name.Contains("__paket__") || 
                    (node.Name = "Import" && match node |> getAttribute "Project" with Some v -> v.Contains("__paket__") | None -> false) ||
                    (node |> withAttributeValue "Label" "Paket")
                then
                    yield node
                yield! PaketNodes node]
        
        for node in PaketNodes this.Document do
            let parent = node.ParentNode
            try                
                node.ParentNode.RemoveChild(node) |> ignore
            with
            | _ -> ()

            try
                if parent.ChildNodes.Count = 0 then
                    parent.ParentNode.RemoveChild(parent) |> ignore
            with
            | _ -> ()

        ["ItemGroup";"When";"Otherwise";"Choose";"When";"Choose"]
        |> List.iter this.DeleteIfEmpty

    member this.UpdateReferences(completeModel: Map<NormalizedPackageName,InstallModel>, usedPackages : Map<NormalizedPackageName,PackageInstallSettings>, hard) =
        this.RemovePaketNodes() 
        
        completeModel
        |> Seq.filter (fun kv -> usedPackages.ContainsKey kv.Key)
        |> Seq.map (fun kv -> 
            if hard then
                this.DeleteCustomModelNodes(kv.Value)
            let installSettings = usedPackages.[kv.Key]
            let projectModel =
                kv.Value
                    .ApplyFrameworkRestrictions(installSettings.Settings.FrameworkRestrictions)
                    .RemoveIfCompletelyEmpty()

            this.GenerateXml(projectModel,installSettings.Settings.CopyLocal,installSettings.Settings.ImportTargets))
        |> Seq.iter (fun (propertyNameNodes,chooseNode,propertyChooseNode) -> 
            if chooseNode.ChildNodes.Count > 0 then
                this.ProjectNode.AppendChild chooseNode |> ignore

            if propertyChooseNode.ChildNodes.Count > 0 then
                this.ProjectNode.AppendChild propertyChooseNode |> ignore

            propertyNameNodes
            |> Seq.iter (this.ProjectNode.AppendChild >> ignore))
                
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

        [for node in this.Document |> getDescendants "ProjectReference" -> 
            { Path = node.Attributes.["Include"].Value
              Name = forceGetInnerText node "Name"
              GUID =  forceGetInnerText node "Project" |> Guid.Parse }]

    member this.ReplaceNuGetPackagesFile() =
        let noneAndContentNodes = 
            (this.Document |> getDescendants "None") @ 
            (this.Document |> getDescendants "Content")

        match noneAndContentNodes |> List.tryFind (withAttributeValue "Include" "packages.config") with
        | None -> ()
        | Some nugetNode ->
            match noneAndContentNodes |> List.filter (withAttributeValue "Include" Constants.ReferencesFile) with 
            | [_] -> nugetNode.ParentNode.RemoveChild(nugetNode) |> ignore
            | [] -> nugetNode.Attributes.["Include"].Value <- Constants.ReferencesFile
            | _::_ -> failwithf "multiple %s nodes in project file %s" Constants.ReferencesFile this.FileName

    member this.RemoveNuGetTargetsEntries() =
        let toDelete = 
            [ this.Document |> getDescendants "RestorePackages" |> Seq.firstOrDefault
              this.Document 
              |> getDescendants "Import" 
              |> List.tryFind (fun n -> 
                    match n |> getAttribute "Project" with
                    | Some p -> p.Equals("$(SolutionDir)\\.nuget\\nuget.targets", 
                                         StringComparison.InvariantCultureIgnoreCase)
                    | None -> false)
              this.Document
              |> getDescendants "Target"
              |> List.tryFind (withAttributeValue "Name" "EnsureNuGetPackageBuildImports") ]
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
                | "Exe"    -> ProjectOutputType.Exe
                | "WinExe" -> ProjectOutputType.Exe
                | _        -> ProjectOutputType.Library }
        |> Seq.head

    member this.GetTargetFramework() =
        seq {for outputType in this.Document |> getDescendants "TargetFrameworkVersion" ->
                outputType.InnerText  }
        |> Seq.map (fun s -> // TODO make this a separate function
                        s.Replace("v","net")
                        |> FrameworkDetection.Extract)                        
        |> Seq.map (fun o -> o.Value)
        |> Seq.head
    
    member this.AddImportForPaketTargets(relativeTargetsPath) =
        match this.Document 
              |> getDescendants "Import" 
              |> List.tryFind (withAttributeValue "Project" relativeTargetsPath) with
        | Some _ -> ()
        | None -> 
            let node = this.CreateNode("Import") |> addAttribute "Project" relativeTargetsPath
            this.ProjectNode.AppendChild(node) |> ignore

    member this.RemoveImportForPaketTargets(relativeTargetsPath) =
        this.Document
        |> getDescendants "Import"
        |> List.tryFind (withAttributeValue "Project" relativeTargetsPath)
        |> Option.iter (fun n -> n.ParentNode.RemoveChild(n) |> ignore)

    member this.DetermineBuildAction fileName =
        if Path.GetExtension(this.FileName) = Path.GetExtension(fileName) + "proj" 
        then "Compile"
        else "Content"

    member this.GetOutputDirectory buildConfiguration =
        this.Document
        |> getDescendants "PropertyGroup"
        |> List.filter (fun pg ->
            pg
            |> getAttribute "Condition"
            |> function
               | None -> false
               | Some s -> s.Contains "$(Configuration)" && s.Contains buildConfiguration)
        |> List.map (fun pg -> pg |> getNodes "OutputPath")
        |> List.concat
        |> fun outputPaths ->
               let clean (p : string) =
                   p.TrimEnd [|'\\'|] |> normalizePath
               match outputPaths with
               | [] -> failwith "Unable to find %s output path node in file %s" buildConfiguration this.FileName
               | [output] ->
                    clean output.InnerText
               | output::_ ->
                    traceWarnfn "Found multiple %s output path nodes in file %s, using first" buildConfiguration this.FileName
                    clean output.InnerText

    member this.GetAssemblyName () =
        let assemblyName =
            this.Document
            |> getDescendants "AssemblyName"
            |> function
               | [] -> failwith "Project %s has no AssemblyName set" this.FileName
               | [assemblyName] -> assemblyName.InnerText
               | assemblyName::_ ->
                    traceWarnfn "Found multiple AssemblyName nodes in file %s, using first" this.FileName
                    assemblyName.InnerText
        sprintf "%s.%s" assemblyName (this.OutputType |> function ProjectOutputType.Library -> "dll" | ProjectOutputType.Exe -> "exe")

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