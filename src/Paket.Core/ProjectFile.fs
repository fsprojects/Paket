namespace Paket

open Paket.Domain
open Paket.Logging
open System
open System.IO
open System.Xml
open System.Collections.Generic
open Paket.Xml
open Paket.Requirements

[<RequireQualifiedAccess>]
type BuildAction =
| Compile
| Content
| Reference

    override this.ToString() = 
        match this with
        | Compile -> "Compile"
        | Content -> "Content"
        | Reference -> "Reference"

/// File item inside of project files.
type FileItem = 
    { BuildAction : BuildAction
      Include : string
      Link : string option }

/// Project references inside of project files.
type ProjectReference = 
    { Path : string
      RelativePath : string
      Name : string
      GUID : Guid }

/// Compile items inside of project files.
type CompileItem = 
    { Include : string
      Link : string option }

/// Project output type.
[<RequireQualifiedAccess>]
type ProjectOutputType =
| Exe 
| Library

type ProjectLanguage = Unknown | CSharp | FSharp | VisualBasic | WiX

module LanguageEvaluation =
    let private extractProjectTypeGuids (projectDocument:XmlDocument) =
        projectDocument
        |> getDescendants "PropertyGroup"
        |> List.filter(fun g -> g.Attributes.Count = 0)
        |> List.collect(fun g -> g |> getDescendants "ProjectTypeGuids") 
        |> List.filter(fun pt -> pt.Attributes.Count = 0)
        |> List.collect(fun pt -> pt.InnerText.Split(';') |> List.ofArray)
        |> List.distinct
        |> List.choose(fun guid -> match Guid.TryParse guid with | (true, g) -> Some g | _ -> None)

    let private csharpGuids =
        [
            "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}" // C#
            "{BF6F8E12-879D-49E7-ADF0-5503146B24B8}" // Dynamics 2012 AX C# in AOT
            "{20D4826A-C6FA-45DB-90F4-C717570B9F32}" // Legacy (2003) Smart Device (C#)
            "{593B0543-81F6-4436-BA1E-4747859CAAE2}" // SharePoint (C#)
            "{4D628B5B-2FBC-4AA6-8C16-197242AEB884}" // Smart Device (C#)
            "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}" // Windows (C#)
            "{C089C8C0-30E0-4E22-80C0-CE093F111A43}" // Windows Phone 8/8.1 App (C#)
            "{14822709-B5A1-4724-98CA-57A101D1B079}" // Workflow (C#)
        ] |> List.map Guid.Parse |> Set.ofList

    let private vbGuids =
        [
            "{CB4CE8C6-1BDB-4DC7-A4D3-65A1999772F8}" // Legacy (2003) Smart Device (VB.NET)
            "{EC05E597-79D4-47f3-ADA0-324C4F7C7484}" // SharePoint (VB.NET)
            "{68B1623D-7FB9-47D8-8664-7ECEA3297D4F}" // Smart Device (VB.NET)
            "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}" // VB.NET
            "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}" // Windows (VB.NET)
            "{DB03555F-0C8B-43BE-9FF9-57896B3C5E56}" // Windows Phone 8/8.1 App (VB.NET)
            "{D59BE175-2ED0-4C54-BE3D-CDAA9F3214C8}" // Workflow (VB.NET)
        ] |> List.map Guid.Parse |> Set.ofList

    let private fsharpGuids =
        [
            "{F2A71F9B-5D33-465A-A702-920D77279786}" // F#
        ] |> List.map Guid.Parse |> Set.ofList

    let private getGuidLanguage (guid:Guid) = 
        let isCsharp = csharpGuids.Contains(guid)
        let isVb = vbGuids.Contains(guid)
        let isFsharp = fsharpGuids.Contains(guid)

        match (isCsharp, isVb, isFsharp) with
        | (true, false, false) -> Some CSharp
        | (false, true, false) -> Some VisualBasic
        | (false, false, true) -> Some FSharp
        | _ -> None

    let private getLanguageFromExtension = function
        | ".csproj" -> Some CSharp
        | ".vbproj" -> Some VisualBasic
        | ".fsproj" -> Some FSharp
        | ".wixproj" -> Some WiX
        | _ -> None

    let private getLanguageFromFileName (fileName : string) =
        let ext = fileName |> Path.GetExtension
        getLanguageFromExtension (ext.ToLowerInvariant())

    /// Get the programming language for a project file using the "ProjectTypeGuids"
    let getProjectLanguage (projectDocument:XmlDocument) (fileName: string) = 
        let cons x y = x :: y

        let languageGroups =
            projectDocument
            |> extractProjectTypeGuids
            |> List.map getGuidLanguage
            |> cons (getLanguageFromFileName fileName)
            |> List.choose id
            |> List.groupBy id
            |> List.map fst

        match languageGroups with
        | [language] -> language
        | _ -> Unknown

/// Contains methods to read and manipulate project files.
type ProjectFile = 
    { FileName: string
      OriginalText : string
      Document : XmlDocument
      ProjectNode : XmlNode
      Language : ProjectLanguage }

    member private this.FindNodes paketOnes name =
        [for node in this.Document |> getDescendants name do
            let isPaketNode = ref false
            for child in node.ChildNodes do
                if child.Name = "Paket" && child.InnerText.ToLower() = "true" then 
                    isPaketNode := true

            if !isPaketNode = paketOnes then yield node]

    member this.GetPropertyWithDefaults propertyName defaultProperties =
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
                            | _ -> failwithf "%s is not a valid comparison operator" comp

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

        this.Document
        |> getDescendants "PropertyGroup"
        |> Seq.fold handleElement defaultProperties
        |> Map.tryFind propertyName

    member this.GetProperty propertyName =
        this.GetPropertyWithDefaults propertyName Map.empty<string, string>

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
        |> Array.filter (fun f -> f.Extension = ".csproj" || f.Extension = ".fsproj" || f.Extension = ".vbproj" || f.Extension = ".wixproj")
        |> Array.choose (fun fi -> ProjectFile.TryLoad fi.FullName)

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

    member this.HasPackageInstalled(groupName,package:PackageName) = 
        let proj = FileInfo(this.FileName)
        match ProjectFile.FindReferencesFile proj with
        | None -> false
        | Some fileName -> 
            let referencesFile = ReferencesFile.FromFile fileName
            referencesFile.Groups.[groupName].NugetPackages 
            |> Seq.exists (fun p -> p.Name = package)

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
                [BuildAction.Content, this.CreateNode("ItemGroup")
                 BuildAction.Compile, this.CreateNode("ItemGroup") 
                 BuildAction.Reference, this.CreateNode("ItemGroup") ] 
            | Some node ->
                [BuildAction.Content, node :?> XmlElement
                 BuildAction.Compile, node :?> XmlElement 
                 BuildAction.Reference, node :?> XmlElement ]
            |> dict

        for fileItem in fileItems |> List.rev do
            let libReferenceNode = 
                let name = 
                    match fileItem.BuildAction with
                    | BuildAction.Reference -> 
                        let n = FileInfo(fileItem.Include).Name.TrimEnd('\\').Split('\\') |> Array.last
                        n.Replace(Path.GetExtension(n),"")
                    | _ -> fileItem.Include

                this.CreateNode(fileItem.BuildAction.ToString())
                |> addAttribute "Include" name
                |> fun node -> 
                    match fileItem.BuildAction with
                    | BuildAction.Reference -> 
                        node
                        |> addChild (this.CreateNode("HintPath",fileItem.Include)) 
                        |> addChild (this.CreateNode("Private","True"))
                    | _ -> node
                |> addChild (this.CreateNode("Paket","True"))
                |> fun n -> match fileItem.Link with
                            | Some link -> addChild (this.CreateNode("Link" ,link.Replace("\\","/"))) n
                            | _ -> n

            let fileItemsInSameDir =
                this.Document 
                |> getDescendants (fileItem.BuildAction.ToString())
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
            verbosefn "    - Deleting custom projects nodes for %O" model.PackageName

        for node in nodesToDelete do
            node.ParentNode.RemoveChild(node) |> ignore

    member private this.GenerateAnalyzersXml(model:InstallModel) =
        let createAnalyzersNode (analyzers: AnalyzerLib list) =
            let itemGroup = this.CreateNode("ItemGroup")
                                
            for lib in analyzers do
                let fi = new FileInfo(normalizePath lib.Path)

                this.CreateNode("Analyzer")
                |> addAttribute "Include" (createRelativePath this.FileName fi.FullName)
                |> addChild (this.CreateNode("Paket","True"))
                |> itemGroup.AppendChild
                |> ignore

            itemGroup

        let shouldBeInstalled (analyzer : AnalyzerLib) = 
            match analyzer.Language, this.Language with
            | AnalyzerLanguage.Any, projectLanguage -> projectLanguage <> ProjectLanguage.Unknown
            | AnalyzerLanguage.CSharp, ProjectLanguage.CSharp -> true
            | AnalyzerLanguage.VisualBasic, ProjectLanguage.VisualBasic -> true
            | AnalyzerLanguage.FSharp, ProjectLanguage.FSharp -> true
            | _ -> false

        model.Analyzers
            |> List.filter shouldBeInstalled
            |> List.sortBy(fun lib -> lib.Path)
            |> createAnalyzersNode

    member this.GenerateXml(model:InstallModel,copyLocal:bool,importTargets:bool,referenceCondition:string option) =
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
            |> List.map (fun lib -> PlatformMatching.getCondition referenceCondition lib.Targets,createItemGroup lib.Files.References)
            |> List.sortBy fst

        let targetsFileConditions =
            model.TargetsFileFolders
            |> List.map (fun lib -> PlatformMatching.getCondition referenceCondition lib.Targets,createPropertyGroup lib.Files.References)
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
                

        let propsNodes = 
            propertyNames
            |> Seq.concat
            |> Seq.distinctBy (fun (x,_,_) -> x)
            |> Seq.filter (fun (propertyName,path,buildPath) -> propertyName.ToLower().EndsWith "props")
            |> Seq.map (fun (propertyName,path,buildPath) -> 
                let fileName = 
                    match propertyName.ToLower() with
                    | _ when propertyChooseNode.ChildNodes.Count = 0 -> path
                    | name when name.EndsWith "props" -> sprintf "%s$(%s).props" buildPath propertyName 
                    | _ -> failwithf "Unknown .props filename %s" propertyName

                this.CreateNode("Import")
                |> addAttribute "Project" fileName
                |> addAttribute "Condition" (sprintf "Exists('%s')" fileName)
                |> addAttribute "Label" "Paket")
            |> Seq.toList

        let targetsNodes = 
            propertyNames
            |> Seq.concat
            |> Seq.distinctBy (fun (x,_,_) -> x)
            |> Seq.filter (fun (propertyName,path,buildPath) -> propertyName.ToLower().EndsWith "props" |> not)
            |> Seq.map (fun (propertyName,path,buildPath) -> 
                let fileName = 
                    match propertyName.ToLower() with
                    | _ when propertyChooseNode.ChildNodes.Count = 0 -> path
                    | name when name.EndsWith "targets" ->
                        sprintf "%s$(%s).targets" buildPath propertyName
                    | _ -> failwithf "Unknown .targets filename %s" propertyName

                this.CreateNode("Import")
                |> addAttribute "Project" fileName
                |> addAttribute "Condition" (sprintf "Exists('%s')" fileName)
                |> addAttribute "Label" "Paket")
            |> Seq.toList
        
        let analyzersNode = this.GenerateAnalyzersXml model

        propsNodes,targetsNodes,chooseNode,propertyChooseNode,analyzersNode
        
    member this.RemovePaketNodes() =
        this.DeletePaketNodes("Analyzer")
        this.DeletePaketNodes("Reference")

        let rec getPaketNodes (node:XmlNode) =
            [for node in node.ChildNodes do
                if node.Name.Contains("__paket__") || 
                    (node.Name = "Import" && match node |> getAttribute "Project" with Some v -> v.Contains("__paket__") | None -> false) ||
                    (node |> withAttributeValue "Label" "Paket")
                then
                    yield node
                yield! getPaketNodes node]
        
        for node in getPaketNodes this.Document do
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
        

    member this.UpdateReferences(completeModel: Map<GroupName*PackageName,_*InstallModel>, usedPackages : Map<GroupName*PackageName,_*InstallSettings>, hard) =
        this.RemovePaketNodes() 
        
        completeModel
        |> Seq.filter (fun kv -> usedPackages.ContainsKey kv.Key)
        |> Seq.map (fun kv -> 
            if hard then
                this.DeleteCustomModelNodes(snd kv.Value)
            let installSettings = snd usedPackages.[kv.Key]
            let projectModel =
                (snd kv.Value)
                    .ApplyFrameworkRestrictions(installSettings.FrameworkRestrictions)
                    .RemoveIfCompletelyEmpty()

            let copyLocal = defaultArg installSettings.CopyLocal true
            let importTargets = defaultArg installSettings.ImportTargets true

            this.GenerateXml(projectModel,copyLocal,importTargets,installSettings.ReferenceCondition))
        |> Seq.iter (fun (propsNodes,targetsNodes,chooseNode,propertyChooseNode, analyzersNode) ->

            let i = ref (this.ProjectNode.ChildNodes.Count-1)
            while 
              !i >= 0 && 
                (this.ProjectNode.ChildNodes.[!i].OuterXml.ToString().ToLower().StartsWith("<import") && 
                 this.ProjectNode.ChildNodes.[!i].OuterXml.ToString().ToLower().Contains("label") &&
                 this.ProjectNode.ChildNodes.[!i].OuterXml.ToString().ToLower().Contains("paket"))  do
                decr i
            
            if !i <= 0 then
                if chooseNode.ChildNodes.Count > 0 then
                    this.ProjectNode.AppendChild chooseNode |> ignore
            else
                let node = this.ProjectNode.ChildNodes.[!i]
                if chooseNode.ChildNodes.Count > 0 then
                    this.ProjectNode.InsertAfter(chooseNode,node) |> ignore

            let j = ref 0
            while !j < this.ProjectNode.ChildNodes.Count && this.ProjectNode.ChildNodes.[!j].OuterXml.ToString().ToLower().StartsWith("<import") do
                incr j
            
            if propertyChooseNode.ChildNodes.Count > 0 then
                if !i <= 0 then
                    if propertyChooseNode.ChildNodes.Count > 0 then
                        this.ProjectNode.AppendChild propertyChooseNode |> ignore

                    propsNodes
                    |> Seq.iter (this.ProjectNode.AppendChild >> ignore)
                else
                    let node = this.ProjectNode.ChildNodes.[!i]

                    propsNodes
                    |> Seq.iter (fun n -> this.ProjectNode.InsertAfter(n,node) |> ignore)

                    if propertyChooseNode.ChildNodes.Count > 0 then
                        this.ProjectNode.InsertAfter(propertyChooseNode,node) |> ignore
            else
                if !j = 0 then
                    propsNodes
                    |> Seq.iter (this.ProjectNode.PrependChild >> ignore)
                else
                    propsNodes
                    |> Seq.iter (fun n -> this.ProjectNode.InsertAfter(n,this.ProjectNode.ChildNodes.[!j-1]) |> ignore)

            targetsNodes
            |> Seq.iter (this.ProjectNode.AppendChild >> ignore)


            if analyzersNode.ChildNodes.Count > 0 then
                this.ProjectNode.AppendChild analyzersNode |> ignore
            )
                
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
            { Path = 
                let p = node.Attributes.["Include"].Value |> normalizePath
                if Path.IsPathRooted p then Path.GetFullPath p else 
                let di = FileInfo(normalizePath this.FileName).Directory
                Path.Combine(di.FullName,p) |> Path.GetFullPath
              RelativePath = node.Attributes.["Include"].Value
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
        this.GetProperty "TargetFrameworkIdentifier"

    member this.GetTargetFrameworkProfile() =
        this.GetProperty "TargetFrameworkProfile"

    member this.GetTargetProfile() =
        match this.GetTargetFrameworkProfile() with
        | Some profile when profile = "Client" ->
            SinglePlatform(DotNetFramework(FrameworkVersion.V4_Client))
        | Some profile when String.IsNullOrWhiteSpace profile |> not ->
            KnownTargetProfiles.FindPortableProfile profile
        | _ ->
            let prefix =
                match this.GetTargetFrameworkIdentifier() with
                | None -> "net"
                | Some x -> x
            let framework = this.GetProperty "TargetFrameworkVersion"
            let defaultResult = SinglePlatform(DotNetFramework(FrameworkVersion.V4))
            match framework with
            | None -> defaultResult
            | Some s ->
                let detectedFramework =
                    prefix + s.Replace("v","")
                    |> FrameworkDetection.Extract
                match detectedFramework with
                | None -> defaultResult
                | Some x -> SinglePlatform(x)
    
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
        then BuildAction.Compile
        else BuildAction.Content

    member this.DetermineBuildActionForRemoteItems fileName =
        if Path.GetExtension(fileName) = ".dll"
        then BuildAction.Reference
        else this.DetermineBuildAction fileName 

    member this.GetOutputDirectory buildConfiguration buildPlatform =
        let platforms =
            if not <| String.IsNullOrWhiteSpace(buildPlatform)
            then [buildPlatform]
            else
                [
                    "AnyCPU";
                    "AnyCPU32BitPreferred";
                    "x86";
                    "x64";
                    "ARM";
                    "Itanium";
                ]

        let rec tryNextPlat platforms attempted =
            match platforms with
            | [] ->
                if String.IsNullOrWhiteSpace(buildPlatform)
                then
                    failwithf "Unable to find %s output path node in file %s for any known platforms" buildConfiguration this.FileName
                else
                    failwithf "Unable to find %s output path node in file %s targeting the %s platform" buildConfiguration this.FileName buildPlatform
            | x::xs ->
                let startingData = Map.ofList [("Configuration", buildConfiguration); ("Platform", x)]
                this.GetPropertyWithDefaults "OutputPath" startingData
                |> function
                    | None -> tryNextPlat xs (x::attempted)
                    | Some s ->
                        if String.IsNullOrWhiteSpace(buildPlatform) then
                            let tested = String.Join(", ", Array.ofList attempted)
                            traceWarnfn "No platform specified; found output path node for the %s platform after failing to find one for the following: %s" x tested
                        s.TrimEnd [|'\\'|] |> normalizePath

        tryNextPlat platforms []

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
            |> fun assemblyName ->
                if String.IsNullOrWhiteSpace assemblyName then 
                    let fi = FileInfo this.FileName
                    fi.Name.Replace(fi.Extension,"")
                else assemblyName

        sprintf "%s.%s" assemblyName (this.OutputType |> function ProjectOutputType.Library -> "dll" | ProjectOutputType.Exe -> "exe")

    member this.GetCompileItems () =
        let getCompileItem (compileNode : XmlNode) =
            let includePath = compileNode |> getAttribute "Include" |> fun a -> a.Value
            compileNode
            |> getDescendants "Link"
            |> function
               | [] -> { Include = includePath; Link = None }
               | [link] | link::_ -> { Include = includePath; Link = Some link.InnerText }

        this.Document
        |> getDescendants "Compile"
        |> Seq.map getCompileItem

    static member LoadFromStream(fullName:string, stream:Stream) =
        let doc = new XmlDocument()
        doc.Load stream

        let manager = new XmlNamespaceManager(doc.NameTable)
        manager.AddNamespace("ns", Constants.ProjectDefaultNameSpace)
        let projectNode = 
            match doc |> getNode "Project" with
            | Some node -> node
            | _ -> failwithf "unable to find Project node in file %s" fullName
        { 
            FileName = fullName
            Document = doc
            ProjectNode = projectNode
            OriginalText = Utils.normalizeXml doc
            Language = LanguageEvaluation.getProjectLanguage doc (Path.GetFileName(fullName)) }

    static member LoadFromFile(fileName:string) =
        let fileInfo = FileInfo (normalizePath fileName)
        use stream = fileInfo.OpenRead()
        ProjectFile.LoadFromStream(fileInfo.FullName, stream)

    static member TryLoad(fileName:string) =
        try
            Some(ProjectFile.LoadFromFile(fileName))
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
                        if isNull dir.Parent then None else
                        checkDir dir.Parent

                checkDir fi.Directory
            with
            | _ -> None
