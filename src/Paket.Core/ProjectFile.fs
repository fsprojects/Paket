namespace Paket

open Paket.Domain
open Paket.Logging
open System
open System.IO
open System.Xml
open System.Collections.Generic
open Paket.Xml
open Paket.Requirements

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
        let FindAllFiles(folder, pattern) = 
            let rec search (di:DirectoryInfo) = 
                try
                    let files = di.GetFiles(pattern, SearchOption.TopDirectoryOnly)
                    di.GetDirectories()
                    |> Array.filter (fun di -> try Path.Combine(di.FullName, Constants.DependenciesFileName) |> File.Exists |> not with | _ -> false)
                    |> Array.collect search
                    |> Array.append files
                with
                | _ -> Array.empty

            search <| DirectoryInfo folder

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

        Seq.isEmpty nodesToDelete |> not

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
        let newItemGroups = 
            let firstItemGroup = this.ProjectNode |> getNodes "ItemGroup" |> List.tryHead
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
                |> fun n -> match fileItem.Link with
                            | Some link -> addChild (this.CreateNode("Link" ,link.Replace("\\","/"))) n
                            | _ -> n

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
        
        let paketNodes = 
            this.FindPaketNodes("Compile")
            @ this.FindPaketNodes("Content")
           
        // remove unneeded files
        for paketNode in paketNodes do
            match getAttribute "Include" paketNode with
            | Some path ->
                if not (fileItems |> List.exists (fun fi -> fi.Include = path)) then 
                  paketNode.ParentNode.RemoveChild(paketNode) |> ignore
            | _ -> ()

        this.DeleteIfEmpty("PropertyGroup") |> ignore
        this.DeleteIfEmpty("ItemGroup") |> ignore
        this.DeleteIfEmpty("When") |> ignore
        this.DeleteIfEmpty("Choose") |> ignore

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

        while List.exists this.DeleteIfEmpty ["ItemGroup";"When";"Otherwise";"Choose"] do
            ()
        

    member this.UpdateReferences(completeModel: Map<NormalizedPackageName,InstallModel>, usedPackages : Map<NormalizedPackageName,InstallSettings>, hard) =
        this.RemovePaketNodes() 
        
        completeModel
        |> Seq.filter (fun kv -> usedPackages.ContainsKey kv.Key)
        |> Seq.map (fun kv -> 
            if hard then
                this.DeleteCustomModelNodes(kv.Value)
            let installSettings = usedPackages.[kv.Key]
            let projectModel =
                kv.Value
                    .ApplyFrameworkRestrictions(installSettings.FrameworkRestrictions)
                    .RemoveIfCompletelyEmpty()

            let copyLocal = defaultArg installSettings.CopyLocal true
            let importTargets = defaultArg installSettings.ImportTargets true

            this.GenerateXml(projectModel,copyLocal,importTargets))
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

    member this.GetProjectGuid() = 
        try
            let forceGetInnerText node name =
                match node |> getNode name with 
                | Some n -> n.InnerText
                | None -> failwithf "unable to parse %s" node.Name

            let node = this.Document |> getDescendants "PropertyGroup" |> Seq.head
            forceGetInnerText node "ProjectGuid" |> Guid.Parse
        with
        | _ -> Guid.Empty

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

        match noneAndContentNodes |> List.tryFind (withAttributeValue "Include" Constants.PackagesConfigFile) with
        | None -> ()
        | Some nugetNode ->
            match noneAndContentNodes |> List.filter (withAttributeValue "Include" Constants.ReferencesFile) with 
            | [_] -> nugetNode.ParentNode.RemoveChild(nugetNode) |> ignore
            | [] -> nugetNode.Attributes.["Include"].Value <- Constants.ReferencesFile
            | _::_ -> failwithf "multiple %s nodes in project file %s" Constants.ReferencesFile this.FileName

    member this.RemoveNuGetTargetsEntries() =
        let toDelete = 
            [ this.Document |> getDescendants "RestorePackages" |> List.tryHead
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

    member this.RemoveImportAndTargetEntries(packages : list<string * SemVerInfo> ) =
        let toDelete = 
            this.Document 
            |> getDescendants "Import"
            |> List.filter (fun node -> 
                match node |> getAttribute "Project" with
                | Some p -> packages |> List.exists (fun (id, version) ->
                    p.IndexOf(sprintf "%s.%O" id version, StringComparison.OrdinalIgnoreCase) >= 0)
                | None -> false)
        
        toDelete
        |> List.iter
            (fun node -> 
                let sibling = node.NextSibling
                tracefn "Removing 'Import' entry from %s for project %s" 
                    this.FileName 
                    (node |> getAttribute "Project" |> Option.get)
                node.ParentNode.RemoveChild node |> ignore
                match sibling with
                | null -> ()
                | sibling when sibling.Name.Equals("Target") ->
                    let deleteTarget = 
                        Utils.askYesNo
                            (sprintf "Do you want to delete Target named '%s' from %s ?" 
                                (sibling |> getAttribute "Name" |> Option.get)
                                this.FileName)
                    if deleteTarget then
                        sibling.ParentNode.RemoveChild sibling |> ignore
                | _ -> ())

    member this.OutputType =
        seq {for outputType in this.Document |> getDescendants "OutputType" ->
                match outputType.InnerText with
                | "Exe"    -> ProjectOutputType.Exe
                | "WinExe" -> ProjectOutputType.Exe
                | _        -> ProjectOutputType.Library }
        |> Seq.head

    member this.GetTargetFrameworkIdentifier() =
        seq { for outputType in this.Document |> getDescendants "TargetFrameworkIdentifier" ->
                outputType.InnerText }
        |> Seq.tryHead

    member this.GetTargetFrameworkProfile() =
        seq {for outputType in this.Document |> getDescendants "TargetFrameworkProfile" ->
                outputType.InnerText }
        |> Seq.tryHead

    member this.GetTargetProfile() =
        match this.GetTargetFrameworkProfile() with
        | Some profile when String.IsNullOrWhiteSpace profile |> not ->
            KnownTargetProfiles.FindPortableProfile profile
        | _ ->
            let framework =
                seq {for outputType in this.Document |> getDescendants "TargetFrameworkVersion" ->
                        outputType.InnerText }
                |> Seq.map (fun s -> 
                                // TODO make this a separate function
                                let prefix = 
                                    match this.GetTargetFrameworkIdentifier() with
                                    | None -> "net"
                                    | Some x -> x

                                prefix + s.Replace("v","")
                                |> FrameworkDetection.Extract)
                |> Seq.map (fun o -> o.Value)
                |> Seq.tryHead

            SinglePlatform(defaultArg framework (DotNetFramework(FrameworkVersion.V4)))

    
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
        let rec handleElement (data : Map<string, string>) (node : XmlNode) =
            let processPlaceholders (data : Map<string, string>) text =
                let getPlaceholderValue (name:string) =
                    // Change "$(Configuration)" to "Configuration",
                    // then find in the data map
                    let name = name.Substring(2, name.Length - 3)
                    match data.TryFind(name) with
                    | None -> ""
                    | Some s -> s

                let replacePlaceholder (s:string) (m:System.Text.RegularExpressions.Match) =
                    let front = s.Substring(0, m.Index)
                    let value = getPlaceholderValue m.Value
                    let back = s.Substring(m.Index + m.Length)
                    front + value + back

                // The placeholder name must be a valid XML node name,
                // else where would its value be defined?
                let regex = @"\$\([a-zA-Z_\-\:][a-zA-Z0-9_\.\-\:]*\)"

                System.Text.RegularExpressions.Regex.Matches(text, regex)
                |> fun x -> System.Linq.Enumerable.Cast<System.Text.RegularExpressions.Match>(x)
                |> Seq.toArray
                |> Array.rev
                |> Array.fold replacePlaceholder text

            let conditionMatches data condition =
                let rec parseWord (data:System.Text.StringBuilder) (input:string) index inQuotes =
                    if input.Length <= index
                    then
                        if data.Length > 0 && not inQuotes then Some(data.ToString(), index)
                        else None
                    else
                        let c = input.[index]
                        let gtz = data.Length > 0
                        match gtz, inQuotes, c with
                        | false, false, ' '  -> parseWord data input (index + 1) false
                        | false, false, '\'' -> parseWord data input (index + 1) true
                        |     _,  true, '\'' -> Some(data.ToString(), index + 1)
                        |  true, false, ' '  -> Some(data.ToString(), index + 1)
                        |     _,  true, c    -> parseWord (data.Append(c)) input (index + 1) true
                        |     _, false, c    -> parseWord (data.Append(c)) input (index + 1) false

                let rec parseComparison (data:System.Text.StringBuilder) (input:string) index =
                    let isCompChar c = c = '<' || c = '>' || c = '!' || c = '='
                    if input.Length <= index
                    then None
                    else
                        let c = input.[index]
                        if data.Length = 0 && c = ' '
                        then parseComparison data input (index + 1)
                        elif data.Length = 2 && isCompChar c
                        then None
                        elif isCompChar c
                        then parseComparison (data.Append(c)) input (index + 1)
                        else
                            let s = data.ToString()
                            let valid = [ "=="; "!="; "<"; ">"; "<="; ">=" ]
                            match (valid |> List.tryFind ((=) s)) with
                            | None -> None
                            | Some(_) -> Some(s, index)

                let parseCondition (data:System.Text.StringBuilder) (input:string) index =
                    if input.Length <= index
                    then None
                    else
                        data.Clear() |> ignore
                        match parseWord data input index false with
                        | None -> None
                        | Some(left, index) ->
                            data.Clear() |> ignore
                            let comp = parseComparison data input index
                            match comp with
                            | None -> None
                            | Some(comp, index) ->
                                data.Clear() |> ignore
                                match parseWord data input index false with
                                | None -> None
                                | Some(right, index) ->
                                    Some(left, comp, right, index)

                let rec parseAndOr (data:System.Text.StringBuilder) (input:string) index =
                    if input.Length <= index
                    then None
                    else
                        let c = input.[index]
                        if data.Length = 0 && c = ' '
                        then parseAndOr data input (index + 1)
                        elif c = ' ' then
                            let s = data.ToString()
                            if s.Equals("and", StringComparison.OrdinalIgnoreCase) then Some("and", index)
                            elif s.Equals("or", StringComparison.OrdinalIgnoreCase) then Some("or", index)
                            else None
                        else parseAndOr (data.Append(c)) input (index + 1)

                let rec containsMoreText (input:string) index =
                    if input.Length <= index then false
                    else
                        match input.[index] with
                        | ' ' -> containsMoreText input (index + 1)
                        | _ -> true

                let rec parseFullCondition data (sb:System.Text.StringBuilder) (input:string) index =
                    if input.Length <= index
                    then data
                    else
                        match data with
                        | None -> None
                        | Some(data) ->
                            sb.Clear() |> ignore
                            let andOr, index =
                                match data with
                                | [] -> None, index
                                | _ ->
                                    let moreText = containsMoreText input index
                                    match (parseAndOr sb input index), moreText with
                                    | None, false -> None, index
                                    | Some(andOr, index), _ -> Some(andOr), index
                                    | None, true -> failwith "Could not parse condition; multiple conditions found with no \"AND\" or \"OR\" between them."
                            sb.Clear() |> ignore
                            let nextCondition = parseCondition sb input index
                            let moreText = containsMoreText input index
                            match nextCondition, moreText with
                            | None, true -> None
                            | None, false -> Some(data)
                            | Some(left, comp, right, index), _ ->
                                let data = Some <| data @[(andOr, left, comp, right)]
                                parseFullCondition data sb input index

                let allConditions = parseFullCondition (Some([])) (System.Text.StringBuilder()) condition 0

                let rec handleConditions xs lastCondition =
                    match xs with
                    | [] -> lastCondition
                    | (cond, left, comp, right)::xs ->
                        let left = processPlaceholders data left
                        let right = processPlaceholders data right
                        let inline doComp l r =
                            match comp with
                            | "==" -> l =  r
                            | "!=" -> l <> r
                            | ">"  -> l >  r
                            | "<"  -> l <  r
                            | "<=" -> l <= r
                            | ">=" -> l >= r
                        let result =
                            match comp with
                            | "==" | "!=" -> doComp left right
                            | _ ->
                                match System.Int64.TryParse(left), System.Int64.TryParse(right) with
                                | (true, l), (true, r) -> doComp l r
                                | _ -> false

                        match lastCondition, cond with
                        |    _, None        -> handleConditions xs result
                        | true, Some("and") -> handleConditions xs result
                        |    _, Some("or")  -> handleConditions xs (lastCondition || result)
                        | _ -> false

                match allConditions with
                | None -> false
                | Some(conditions) -> handleConditions conditions true


            let addData data (node:XmlNode) =
                let text = processPlaceholders data node.InnerText
                // Note that using Map.add overrides the value assigned
                // to this key if it already exists in the map; so long
                // as we process nodes top-to-bottom, this matches the
                // behavior of MSBuild.
                Map.add node.Name text data

            let handleConditionalElement data node =
                node
                |> getAttribute "Condition"
                |> function
                    | None ->
                        node
                        |> getChildNodes
                        |> Seq.fold handleElement data
                    | Some s ->
                        if not (conditionMatches data s)
                        then data
                        else
                            if node.ChildNodes.Count > 0 then
                                node
                                |> getChildNodes
                                |> Seq.fold handleElement data
                            else
                                data

            match node.Name with
            | "PropertyGroup" -> handleConditionalElement data node
            // Don't handle these yet
            | "Choose" | "Import" | "ItemGroup" | "ProjectExtensions" | "Target" | "UsingTask" -> data
            // Any other node types are intended to be values being defined
            | _ ->
                node
                |> getAttribute "Condition"
                |> function
                    | None -> addData data node
                    | Some s ->
                        if not (conditionMatches data s)
                        then data
                        else addData data node

        let startingData = Map.empty<string,string>.Add("Configuration", buildConfiguration)

        this.Document
        |> getDescendants "PropertyGroup"
        |> Seq.fold handleElement startingData
        |> Map.tryFind "OutputPath"
        |> function
            | None -> failwithf "Unable to find %s output path node in file %s" buildConfiguration this.FileName
            | Some s -> s.TrimEnd [|'\\'|] |> normalizePath

    member this.GetAssemblyName () =
        let assemblyName =
            this.Document
            |> getDescendants "AssemblyName"
            |> function
               | [] -> failwithf "Project %s has no AssemblyName set" this.FileName
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
                | _ -> failwithf "unable to find Project node in file %s" fileName
            Some { 
                FileName = fi.FullName
                Document = doc
                ProjectNode = projectNode
                OriginalText = Utils.normalizeXml doc }
        with
        | exn -> 
            traceWarnfn "Unable to parse %s:%s      %s" fileName Environment.NewLine exn.Message
            None

    static member TryFindProject(projects: ProjectFile seq,projectName) =
        match projects |> Seq.tryFind (fun p -> p.NameWithoutExtension = projectName || p.Name = projectName) with
        | Some p -> Some p
        | None ->
            try
                let fi = FileInfo (normalizePath (projectName.Trim().Trim([|'\"'|]))) // check if we can detect the path
                let rec checkDir (dir:DirectoryInfo) = 
                    match projects |> Seq.tryFind (fun p -> (FileInfo p.FileName).Directory.ToString().ToLower() = dir.ToString().ToLower()) with
                    | Some p -> Some p
                    | None ->
                        if dir.Parent = null then None else
                        checkDir dir.Parent

                checkDir fi.Directory
            with
            | _ -> None