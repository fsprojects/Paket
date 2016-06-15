namespace Paket

open Paket
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
    | Compile | Content | Reference
    | Resource | Page // These two are WPF only - https://msdn.microsoft.com/library/aa970494%28v=vs.100%29.aspx?f=255&MSPPError=-2147217396
    // There are some other build actions - http://stackoverflow.com/questions/145752/what-are-the-various-build-action-settings-in-visual-studio-project-properties/145769#145769

    override this.ToString() = 
        match this with
        | Compile -> "Compile"
        | Content -> "Content"
        | Reference -> "Reference"
        | Resource -> "Resource"
        | Page -> "Page"

    static member PaketFileNodeNames =
        [Compile; Content; Resource; Page] |> List.map (fun x->x.ToString())

/// File item inside of project files.
type FileItem = 
    { BuildAction : BuildAction
      Include : string
      WithPaketSubNode: bool
      CopyToOutputDirectory: CopyToOutputDirectorySettings option
      Link : string option }

/// Project references inside of project files.
type ProjectReference = 
    { Path : string
      RelativePath : string
      Name : string
      GUID : Guid }

/// Compile items inside of project files.
type CompileItem =
    { SourceFile : string
      DestinationPath : string
      BaseDir : string }

/// Project output type.
[<RequireQualifiedAccess>]
type ProjectOutputType =
| Exe 
| Library

type ProjectLanguage = Unknown | CSharp | FSharp | VisualBasic | WiX | Nemerle | CPP

module LanguageEvaluation =
    let private extractProjectTypeGuids (projectDocument:XmlDocument) =
        projectDocument
        |> getDescendants "PropertyGroup"
        |> List.filter(fun g -> g.Attributes.Count = 0)
        |> List.collect(fun g -> g |> getDescendants "ProjectTypeGuids") 
        |> List.filter(fun pt -> pt.Attributes.Count = 0)
        |> List.collect(fun pt -> pt.InnerText.Split ';' |> List.ofArray)
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

    let private nemerleGuids =
        [
            "{EDCC3B85-0BAD-11DB-BC1A-00112FDE8B61}" // Nemerle
        ] |> List.map Guid.Parse |> Set.ofList

    let private getGuidLanguage (guid:Guid) = 
        let isCsharp = csharpGuids.Contains(guid)
        let isVb = vbGuids.Contains(guid)
        let isFsharp = fsharpGuids.Contains(guid)
        let isNemerle = nemerleGuids.Contains(guid)

        match (isCsharp, isVb, isFsharp, isNemerle) with
        | (true, false, false, false) -> Some CSharp
        | (false, true, false, false) -> Some VisualBasic
        | (false, false, true, false) -> Some FSharp
        | (false, false, false, true) -> Some Nemerle
        | _ -> None

    let private getLanguageFromExtension = function
        | ".csproj" -> Some CSharp
        | ".vbproj" -> Some VisualBasic
        | ".fsproj" -> Some FSharp
        | ".vcxproj" -> Some CPP
        | ".wixproj" -> Some WiX
        | ".nproj"  -> Some Nemerle
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


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ProjectFile =

    let name (projectFile:ProjectFile) = FileInfo(projectFile.FileName).Name

    let nameWithoutExtension (projectFile:ProjectFile) = Path.GetFileNameWithoutExtension (name projectFile)


    let loadFromStream (fullName:string) (stream:Stream) =
        let doc = XmlDocument()
        doc.Load stream

        let manager = XmlNamespaceManager doc.NameTable
        manager.AddNamespace ("ns", Constants.ProjectDefaultNameSpace)
        let projectNode = 
            match doc |> getNode "Project" with
            | Some node -> node
            | _ -> failwithf "unable to find Project node in file %s" fullName
        {   FileName = fullName
            Document = doc
            ProjectNode = projectNode
            OriginalText = Utils.normalizeXml doc
            Language = LanguageEvaluation.getProjectLanguage doc (Path.GetFileName fullName) 
        }

    let loadFromFile(fileName:string) =
        let fileInfo = FileInfo (normalizePath fileName)
        use stream = fileInfo.OpenRead()
        loadFromStream fileInfo.FullName stream

    let tryLoad(fileName:string) =
        try
            Some(loadFromFile fileName)
        with
        | exn -> 
            traceWarnfn "Unable to parse %s:%s      %s" fileName Environment.NewLine exn.Message
            None

    let createNode name (project:ProjectFile) = 
        project.Document.CreateElement (name, Constants.ProjectDefaultNameSpace)

    let createNodeSet name text (project:ProjectFile) = 
        let node = createNode name project
        node.InnerText <- text
        node

    open System.Text

    let getPropertyWithDefaults propertyName defaultProperties (projectFile:ProjectFile) =

        let processPlaceholders (data : Map<string, string>) text =
            
            let getPlaceholderValue (name:string) =
                // Change "$(Configuration)" to "Configuration",
                // then find in the data map
                let name = name.Substring(2, name.Length - 3)
                match data.TryFind(name) with
                | None -> ""
                | Some s -> s

            let replacePlaceholder (s:string) (m:RegularExpressions.Match) =
                let front = s.Substring(0, m.Index)
                let value = getPlaceholderValue m.Value
                let back = s.Substring(m.Index + m.Length)
                front + value + back

            // The placeholder name must be a valid XML node name,
            // else where would its value be defined?
            let regex = @"\$\([a-zA-Z_\-\:][a-zA-Z0-9_\.\-\:]*\)"

            RegularExpressions.Regex.Matches(text, regex)
            |> fun x -> Seq.cast<RegularExpressions.Match>(x)
            |> Seq.toArray
            |> Array.rev
            |> Array.fold replacePlaceholder text


        let rec parseWord (data:StringBuilder) (input:string) index inQuotes =
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
                |     _,  true, '\'' -> Some (string data, index + 1)
                |  true, false, ' '  -> Some (string data, index + 1)
                |     _,  true, c    -> parseWord (data.Append c) input (index + 1) true
                |     _, false, c    -> parseWord (data.Append c) input (index + 1) false


        let rec parseComparison (data:StringBuilder) (input:string) index =
            let isCompChar c = c = '<' || c = '>' || c = '!' || c = '='
                       
            if input.Length <= index then None else
            let c = input.[index]
            if data.Length = 0 && c = ' 'then 
                parseComparison data input (index + 1)
            elif data.Length = 2 && isCompChar c then 
                None
            elif isCompChar c then 
                parseComparison (data.Append c) input (index + 1)
            else
                let s = string data
                let valid = [ "=="; "!="; "<"; ">"; "<="; ">=" ]
                match (valid |> List.tryFind ((=) s)) with
                | None -> None
                | Some _ -> Some(s, index)


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
            if input.Length <= index then None else

            let c = input.[index]
            if   data.Length = 0 && c = ' '
            then parseAndOr data input (index + 1)
            elif c <> ' ' then parseAndOr (data.Append c) input (index + 1) else
            let  s = string data
            if   s.Equals ("and", StringComparison.OrdinalIgnoreCase) then Some("and", index)
            elif s.Equals ("or", StringComparison.OrdinalIgnoreCase)  then Some("or", index)
            else None

        let rec containsMoreText (input:string) index =
            if input.Length <= index then false else
            match input.[index] with
            | ' ' -> containsMoreText input (index + 1)
            | _ -> true


        let rec parseFullCondition data (sb:System.Text.StringBuilder) (input:string) index =
            if input.Length <= index
            then data
            else
                match data with
                | None -> None
                | Some data ->
                    sb.Clear() |> ignore
                    let andOr, index =
                        match data with
                        | [] -> None, index
                        | _ ->
                            let moreText = containsMoreText input index
                            match (parseAndOr sb input index), moreText with
                            | None, false -> None, index
                            | Some(andOr, index), _ -> Some andOr, index
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

        let rec handleConditions data xs lastCondition =
            match xs with
            | [] -> lastCondition
            | (cond, left, comp, right)::xs ->
                let left  = processPlaceholders data left
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
                        match System.Int64.TryParse left, System.Int64.TryParse right with
                        | (true, l), (true, r) -> doComp l r
                        | _ -> false

                match lastCondition, cond with
                |    _, None       -> handleConditions data xs result
                | true, Some "and" -> handleConditions data xs result
                |    _, Some "or"  -> handleConditions data xs (lastCondition || result)
                | _ -> false


        let conditionMatches data condition =
            let allConditions = 
                parseFullCondition (Some []) (StringBuilder()) condition 0
            match allConditions with
            | None -> false
            | Some conditions -> handleConditions data conditions true


        let addData data (node:XmlNode) =
            let text = processPlaceholders data node.InnerText
            // Note that using Map.add overrides the value assigned
            // to this key if it already exists in the map; so long
            // as we process nodes top-to-bottom, this matches the
            // behavior of MSBuild.
            Map.add node.Name text data


        let rec handleElement (data : Map<string, string>) (node : XmlNode) =

            let handleConditionalElement data node =
                match getAttribute "Condition" node with
                | None ->
                    node |> getChildNodes
                    |> Seq.fold handleElement data
                | Some s ->
                    if not (conditionMatches data s)
                    then data
                    elif node.ChildNodes.Count > 0 then
                        node |> getChildNodes
                        |> Seq.fold handleElement data
                    else data

            match node.Name with
            | "PropertyGroup" -> handleConditionalElement data node
            // Don't handle these yet
            | "Choose" | "Import" | "ItemGroup" | "ProjectExtensions" | "Target" | "UsingTask" -> data
            // Any other node types are intended to be values being defined
            | _ ->
                match getAttribute "Condition" node with
                | None -> addData data node
                | Some s ->
                    if not (conditionMatches data s)
                    then data
                    else addData data node

        let map =
            projectFile.Document
            |> getDescendants "PropertyGroup"
            |> Seq.fold handleElement defaultProperties
        
        Map.tryFind propertyName map

    let getProperty propertyName (projectFile:ProjectFile) =
        getPropertyWithDefaults propertyName Map.empty<string, string> projectFile



    let deleteIfEmpty name (project:ProjectFile) =
        let nodesToDelete = List<_>()
        for node in project.Document |> getDescendants name do
            if node.ChildNodes.Count = 0 then
                nodesToDelete.Add node

        for node in nodesToDelete do
            node.ParentNode.RemoveChild node |> ignore

        Seq.isEmpty nodesToDelete |> not

    let internal findNodes paketOnes name (project:ProjectFile) =
        [for node in project.Document |> getDescendants name do
            
            let isPaketNode = ref false

            for child in node.ChildNodes do
                if child.Name = "Paket" && String.equalsIgnoreCase child.InnerText "true" then 
                    isPaketNode := true

            if !isPaketNode = paketOnes then yield node]

    let getCustomReferenceAndFrameworkNodes project = (findNodes false "Reference" project) @ (findNodes false "NativeReference" project)

    let findPaketNodes name (project:ProjectFile) = findNodes true name project

    let getFrameworkAssemblies (project:ProjectFile) = 
        [for node in project.Document |> getDescendants "Reference" do
            let hasHintPath = ref false
            for child in node.ChildNodes do
                if child.Name = "HintPath" then 
                    hasHintPath := true
            if not !hasHintPath then
                yield node.Attributes.["Include"].InnerText.Split(',').[0] ]

    let deletePaketNodes name (project:ProjectFile) =
        let nodesToDelete = findPaketNodes name project
        if nodesToDelete |> Seq.isEmpty |> not then
            verbosefn "    - Deleting Paket %s nodes" name

        for node in nodesToDelete do
            node.ParentNode.RemoveChild node |> ignore

    let updateFileItems (fileItems:FileItem list) (project:ProjectFile) = 
        let newItemGroups = 
            let firstItemGroup = project.ProjectNode |> getNodes "ItemGroup" |> List.tryHead
            match firstItemGroup with
            | None ->
                [BuildAction.Content, createNode "ItemGroup" project
                 BuildAction.Compile, createNode "ItemGroup" project
                 BuildAction.Reference, createNode "ItemGroup" project
                 BuildAction.Resource, createNode "ItemGroup" project
                 BuildAction.Page, createNode "ItemGroup" project ]
            | Some node ->
                [BuildAction.Content, node :?> XmlElement
                 BuildAction.Compile, node :?> XmlElement
                 BuildAction.Reference, node :?> XmlElement
                 BuildAction.Resource, node :?> XmlElement
                 BuildAction.Page, node :?> XmlElement ]
            |> dict

        for fileItem in fileItems |> List.rev do
            let libReferenceNode = 
                let name = 
                    match fileItem.BuildAction with
                    | BuildAction.Reference ->
                        fileItem.Include |> normalizePath |> Path.GetFileNameWithoutExtension
                    | _ -> fileItem.Include

                createNode (string fileItem.BuildAction) project
                |> addAttribute "Include" name
                |> fun node -> 
                    match fileItem.BuildAction with
                    | BuildAction.Reference -> 
                        node
                        |> addChild (createNodeSet "HintPath" fileItem.Include project)
                        |> addChild (createNodeSet "Private" "True" project)
                    | BuildAction.Page ->
                        node
                        |> addChild (createNodeSet "Generator" "MSBuild:Compile" project)
                        |> addChild (createNodeSet "SubType" "Designer" project)
                    | _ -> node
                |> fun n -> if fileItem.WithPaketSubNode then addChild (createNodeSet "Paket" "True" project) n else n
                |> fun n -> 
                    if fileItem.BuildAction <> BuildAction.Content then n else
                    match fileItem.CopyToOutputDirectory with
                    | Some CopyToOutputDirectorySettings.Always -> addChild (createNodeSet "CopyToOutputDirectory" "Always" project) n
                    | Some CopyToOutputDirectorySettings.Never  -> addChild (createNodeSet "CopyToOutputDirectory" "Never" project) n
                    | Some CopyToOutputDirectorySettings.PreserveNewest  -> addChild (createNodeSet "CopyToOutputDirectory" "PreserveNewest" project) n
                    | None -> n
                |> fun n -> match fileItem.Link with
                            | Some link -> addChild (createNodeSet "Link" (link.Replace("\\","/")) project) n
                            | _ -> n

            let fileItemsInSameDir =
                project.Document 
                |> getDescendants (string fileItem.BuildAction)
                |> List.filter (fun node -> 
                    match node |> getAttribute "Include" with
                    | Some path when path.StartsWith (fileItem.Include |> normalizePath |> Path.GetDirectoryName |> windowsPath) -> true
                    | _ -> false)
            

            if Seq.isEmpty fileItemsInSameDir then 
                newItemGroups.[fileItem.BuildAction].PrependChild libReferenceNode |> ignore
            else
                let existingNode = 
                    fileItemsInSameDir 
                    |> Seq.tryFind (withAttributeValue "Include" fileItem.Include)

                match existingNode with
                | Some existingNode ->
                    let parent = existingNode.ParentNode
                    parent.InsertBefore(libReferenceNode, existingNode) |> ignore
                    parent.RemoveChild(existingNode) |> ignore
                | None  ->
                    let firstNode = fileItemsInSameDir |> Seq.head 
                    firstNode.ParentNode.InsertBefore(libReferenceNode, firstNode) |> ignore

        let paketNodes =
            BuildAction.PaketFileNodeNames
            |> List.map (fun name -> findPaketNodes name project)
            |> List.concat

        // remove unneeded files
        for paketNode in paketNodes do
            match getAttribute "Include" paketNode with
            | Some path ->
                if not (fileItems |> List.exists (fun fi -> fi.Include = path)) then 
                  paketNode.ParentNode.RemoveChild paketNode |> ignore
            | _ -> ()

        deleteIfEmpty "PropertyGroup" project |> ignore
        deleteIfEmpty "ItemGroup"     project |> ignore
        deleteIfEmpty "When"          project |> ignore
        deleteIfEmpty "Choose"        project |> ignore

    let getCustomModelNodes(model:InstallModel) (project:ProjectFile)  =
        let libs =
            model.GetLibReferencesLazy.Force()
            |> Set.map (fun lib -> lib.ReferenceName)
       
        getCustomReferenceAndFrameworkNodes project
        |> List.filter (fun node -> 
            let libName = node.Attributes.["Include"].InnerText.Split(',').[0]
            Set.contains libName libs)

    let deleteCustomModelNodes (model:InstallModel) (project:ProjectFile) =
        let nodesToDelete = 
            getCustomModelNodes model project
            |> List.filter (fun node ->
                let isFrameworkNode = ref true
                for child in node.ChildNodes do
                    if child.Name = "HintPath" then isFrameworkNode := false
                    if child.Name = "Private" then isFrameworkNode := false

                not !isFrameworkNode)
        
        if nodesToDelete <> [] then
            verbosefn "    - Deleting custom projects nodes for %O" model.PackageName

        for node in nodesToDelete do
            node.ParentNode.RemoveChild node |> ignore

    let generateAnalyzersXml (model:InstallModel) (project:ProjectFile)  =
        let createAnalyzersNode (analyzers: AnalyzerLib list) =
            let itemGroup = createNode "ItemGroup" project
                                
            for lib in analyzers do
                let fi = FileInfo (normalizePath lib.Path)
                createNode "Analyzer" project
                |> addAttribute "Include" (createRelativePath project.FileName fi.FullName)
                |> addChild (createNodeSet "Paket" "True" project)
                |> itemGroup.AppendChild
                |> ignore
            itemGroup

        let shouldBeInstalled (analyzer : AnalyzerLib) = 
            match analyzer.Language, project.Language with
            | AnalyzerLanguage.Any, projectLanguage -> projectLanguage <> ProjectLanguage.Unknown
            | AnalyzerLanguage.CSharp, ProjectLanguage.CSharp -> true
            | AnalyzerLanguage.VisualBasic, ProjectLanguage.VisualBasic -> true
            | AnalyzerLanguage.FSharp, ProjectLanguage.FSharp -> true
            | _ -> false

        model.Analyzers
        |> List.filter shouldBeInstalled
        |> List.sortBy(fun lib -> lib.Path)
        |> createAnalyzersNode

    let generateXml (model:InstallModel) (aliases:Map<string,string>) (copyLocal:bool) (importTargets:bool) (referenceCondition:string option) (project:ProjectFile) =
        let references = 
            getCustomReferenceAndFrameworkNodes project
            |> List.map (fun node -> node.Attributes.["Include"].InnerText.Split(',').[0])
            |> Set.ofList

        let model = model.FilterReferences references
        let createItemGroup references = 
            let itemGroup = createNode "ItemGroup" project
                                
            for lib in references do
                match lib with
                | Reference.Library lib ->
                    let fi = FileInfo (normalizePath lib)
                    let l = lib.ToLower()
                    let aliases =
                        aliases
                        |> Seq.tryFind (fun kv -> l.Contains(kv.Key))
                        |> Option.map (fun kv -> kv.Value)
                    
                    let relativePath = createRelativePath project.FileName fi.FullName
                    if relativePath.Contains @"\native\" then createNode "NativeReference" project else createNode "Reference" project
                    |> addAttribute "Include" (fi.Name.Replace(fi.Extension,""))
                    |> addChild (createNodeSet "HintPath" relativePath project)
                    |> addChild (createNodeSet "Private" (if copyLocal then "True" else "False") project)
                    |> addChild (createNodeSet "Paket" "True" project)
                    |> fun n ->
                        match aliases with
                        | None -> n
                        | Some alias -> addChild (createNodeSet "Aliases" alias project) n
                    |> itemGroup.AppendChild
                    |> ignore
                | Reference.FrameworkAssemblyReference frameworkAssembly ->
                    createNode "Reference" project
                    |> addAttribute "Include" frameworkAssembly
                    |> addChild (createNodeSet "Paket" "True" project)
                    |> itemGroup.AppendChild
                    |> ignore
                | Reference.TargetsFile _ -> ()
            itemGroup

        let createPropertyGroup references = 
            let propertyGroup = createNode "PropertyGroup" project
                      
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
                        
                        let path = createRelativePath project.FileName (fi.FullName.Replace(fi.Extension,""))
                        let s = path.Substring(path.LastIndexOf("build\\") + 6)
                        let node = createNode propertyName project
                        node.InnerText <- s
                        node
                        |> propertyGroup.AppendChild 
                        |> ignore
                        Some(propertyName,createRelativePath project.FileName fi.FullName,path.Substring(0,path.LastIndexOf("build\\") + 6)))
                |> Set.ofSeq
                    
            propertyNames,propertyGroup

        let conditions =
            model.ReferenceFileFolders
            |> List.choose (fun lib -> 
                match lib with
                | x when (match x.Targets with | [SinglePlatform(Runtimes(_))] -> true | _ -> false) -> None  // TODO: Add reference to custom task instead
                | _ -> 
                    match PlatformMatching.getCondition referenceCondition lib.Targets with
                    | "" -> None
                    | condition -> Some (condition,createItemGroup lib.Files.References))
            |> List.sortBy fst

        let targetsFileConditions =
            model.TargetsFileFolders
            |> List.map (fun lib -> PlatformMatching.getCondition referenceCondition lib.Targets,createPropertyGroup lib.Files.References)
            |> List.sortBy fst

        let chooseNode =
            match conditions with
            |  ["$(TargetFrameworkIdentifier) == 'true'",itemGroup] -> itemGroup
            |  ["true",itemGroup] -> itemGroup
            |  _ ->
                let chooseNode = createNode "Choose" project

                let containsReferences = ref false

                conditions
                |> List.map (fun (condition,itemGroup) ->
                    let whenNode = 
                        createNode "When" project
                        |> addAttribute "Condition" condition 
               
                    if not itemGroup.IsEmpty then
                        whenNode.AppendChild itemGroup |> ignore
                        containsReferences := true
                    whenNode)
                |> List.iter (fun node -> chooseNode.AppendChild node |> ignore)
                                
                if !containsReferences then chooseNode else createNode "Choose" project

        let propertyNames,propertyChooseNode =
            match targetsFileConditions with
            |  ["$(TargetFrameworkIdentifier) == 'true'",(propertyNames,propertyGroup)] ->
                [propertyNames], createNode "Choose" project
            |  ["true",(propertyNames,propertyGroup)] ->
                [propertyNames], createNode "Choose" project
            |  _ ->
                let propertyChooseNode = createNode "Choose" project

                let containsProperties = ref false
                targetsFileConditions
                |> List.map (fun (condition,(propertyNames,propertyGroup)) ->
                    let whenNode = 
                        createNode "When" project
                        |> addAttribute "Condition" condition 
                    if not <| Set.isEmpty propertyNames then
                        whenNode.AppendChild(propertyGroup) |> ignore
                        containsProperties := true
                    whenNode)
                |> List.iter(fun node -> propertyChooseNode.AppendChild node |> ignore)
                
                (targetsFileConditions |> List.map (fun (_,(propertyNames,_)) -> propertyNames)),
                (if !containsProperties then propertyChooseNode else createNode "Choose" project)
                

        let propsNodes = 
            propertyNames
            |> Seq.concat
            |> Seq.distinctBy (fun (x,_,_) -> x)
            |> Seq.filter (fun (propertyName,path,buildPath) -> String.endsWithIgnoreCase "props" propertyName)
            |> Seq.map (fun (propertyName,path,buildPath) -> 
                let fileName = 
                    match propertyName with
                    | _ when propertyChooseNode.ChildNodes.Count = 0 -> path
                    | name when String.endsWithIgnoreCase "props" name  -> sprintf "%s$(%s).props" buildPath propertyName 
                    | _ -> failwithf "Unknown .props filename %s" propertyName

                createNode "Import" project
                |> addAttribute "Project" fileName
                |> addAttribute "Condition" (sprintf "Exists('%s')" fileName)
                |> addAttribute "Label" "Paket")
            |> Seq.toList

        let targetsNodes = 
            propertyNames
            |> Seq.concat
            |> Seq.distinctBy (fun (x,_,_) -> x)
            |> Seq.filter (fun (propertyName,path,buildPath) -> String.endsWithIgnoreCase "props" propertyName  |> not)
            |> Seq.map (fun (propertyName,path,buildPath) -> 
                let fileName = 
                    match propertyName with
                    | _ when propertyChooseNode.ChildNodes.Count = 0 -> path
                    | name when String.endsWithIgnoreCase  "targets" name ->
                        sprintf "%s$(%s).targets" buildPath propertyName
                    | _ -> failwithf "Unknown .targets filename %s" propertyName

                createNode "Import" project
                |> addAttribute "Project" fileName
                |> addAttribute "Condition" (sprintf "Exists('%s')" fileName)
                |> addAttribute "Label" "Paket")
            |> Seq.toList
        
        let analyzersNode = generateAnalyzersXml model project

        propsNodes,targetsNodes,chooseNode,propertyChooseNode,analyzersNode

    let removePaketNodes (project:ProjectFile) = 
        deletePaketNodes "Analyzer" project
        deletePaketNodes "Reference" project
        deletePaketNodes "NativeReference" project

        let rec getPaketNodes (node:XmlNode) =
            [for node in node.ChildNodes do
                if node.Name.Contains "__paket__" || 
                    (node.Name = "Import" && match node |> getAttribute "Project" with Some v -> v.Contains "__paket__" | None -> false) ||
                    (node |> withAttributeValue "Label" "Paket")
                then
                    yield node
                elif node.Name = "UsingTask" && node.Attributes.["TaskName"] <> null && node.Attributes.["TaskName"].Value = "CopyRuntimeDependencies" then
                    yield node
                elif node.Name = "CopyRuntimeDependencies" then
                    yield node
                yield! getPaketNodes node]
        
        for node in getPaketNodes project.Document do
            let parent = node.ParentNode
            try
                node.ParentNode.RemoveChild node |> ignore
            with
            | _ -> ()

            try
                if parent.ChildNodes.Count = 0 then
                    parent.ParentNode.RemoveChild parent |> ignore
            with
            | _ -> ()

        while List.exists (fun x -> deleteIfEmpty x project) ["ItemGroup";"When";"Otherwise";"Choose"] do
            ()

    let getTargetFrameworkIdentifier (project:ProjectFile) = getProperty "TargetFrameworkIdentifier" project

    let getTargetFrameworkProfile (project:ProjectFile) = getProperty "TargetFrameworkProfile" project

    let getTargetProfile (project:ProjectFile) =
        match getTargetFrameworkProfile project with
        | Some profile when profile = "Client" ->
            SinglePlatform (DotNetFramework FrameworkVersion.V4_Client)
        | Some profile when String.IsNullOrWhiteSpace profile |> not ->
            KnownTargetProfiles.FindPortableProfile profile
        | _ ->
            let prefix =
                match getTargetFrameworkIdentifier project with
                | None -> "net"
                | Some x -> x
            let framework = getProperty "TargetFrameworkVersion" project
            let defaultResult = SinglePlatform (DotNetFramework FrameworkVersion.V4)
            match framework with
            | None -> defaultResult
            | Some s ->
                match FrameworkDetection.Extract(prefix + s.Replace("v","")) with
                | None -> defaultResult
                | Some x -> SinglePlatform x

    let getTargetFramework (project:ProjectFile) = 
        match getTargetProfile project with
        | SinglePlatform x -> Some x
        | PortableProfile (_, x::_) -> Some x
        | _ -> None

    let updateReferences
            rootPath
            (completeModel: Map<GroupName*PackageName,_*InstallModel>) 
            (usedPackages : Map<GroupName*PackageName,_*InstallSettings>) (project:ProjectFile) =
        removePaketNodes project


        let findInsertSpot() =
            let j = ref 0
            while !j < project.ProjectNode.ChildNodes.Count && String.startsWithIgnoreCase  "<import" (project.ProjectNode.ChildNodes.[!j].OuterXml.ToString()) do
                incr j

            let k = ref !j
            while !k < project.ProjectNode.ChildNodes.Count &&
                (String.startsWithIgnoreCase  "<PropertyGroup" (project.ProjectNode.ChildNodes.[!k].OuterXml.ToString()) ||
                 (String.startsWithIgnoreCase  "<import" (project.ProjectNode.ChildNodes.[!k].OuterXml.ToString()) &&
                  not (String.containsIgnoreCase "label" (project.ProjectNode.ChildNodes.[!k].OuterXml.ToString()) &&
                       String.containsIgnoreCase "paket" (project.ProjectNode.ChildNodes.[!k].OuterXml.ToString())))) do
                incr k

            let l = ref !k
            while !l < project.ProjectNode.ChildNodes.Count do
                let node = project.ProjectNode.ChildNodes.[!l].OuterXml.ToString()
                if String.startsWithIgnoreCase  "<import" node && 
                   (String.containsIgnoreCase "microsoft.csharp.targets" node || 
                     String.containsIgnoreCase "microsoft.fsharp.targets" node ||
                     String.containsIgnoreCase "fsharptargetspath" node)
                then
                    k := !l + 1
                incr l
            !j,!k

        let packagesWithRuntimeDependencies = System.Collections.Generic.HashSet<_>()
        completeModel
        |> Seq.filter (fun kv -> usedPackages.ContainsKey kv.Key)
        |> Seq.map (fun kv -> 
            deleteCustomModelNodes (snd kv.Value) project
            let installSettings = snd usedPackages.[kv.Key]
            let projectModel =
                (snd kv.Value)
                    .ApplyFrameworkRestrictions(installSettings.FrameworkRestrictions |> getRestrictionList)
                    .FilterExcludes(installSettings.Excludes)
                    .RemoveIfCompletelyEmpty()
            
            match getTargetFramework project with 
            | Some targetFramework ->
                if projectModel.GetLibReferences targetFramework |> Seq.isEmpty then
                    let libReferences = 
                        projectModel.GetLibReferencesLazy |> force
                        |> Seq.filter (fun l ->  match l with | Reference.Library _ -> true | _ -> false)

                    if not (Seq.isEmpty libReferences) then
                        traceWarnfn "Package %O contains libraries, but not for the selected TargetFramework %O in project %s."
                            (snd kv.Key) targetFramework project.FileName
            | None -> ()

            let copyLocal = defaultArg installSettings.CopyLocal true
            let importTargets = defaultArg installSettings.ImportTargets true

            let hasRunTimeStuff =
                projectModel.ReferenceFileFolders
                |> Seq.collect (fun lib -> 
                    match lib with
                    | x when (match x.Targets with | [SinglePlatform(Runtimes(_))] -> true | _ -> false) -> lib.Files.References
                    | _ -> Set.empty)
                |> Seq.isEmpty
                |> not

            if hasRunTimeStuff then
                packagesWithRuntimeDependencies.Add(kv.Key) |> ignore

            generateXml projectModel installSettings.Aliases copyLocal importTargets installSettings.ReferenceCondition project)
        |> Seq.iter (fun (propsNodes,targetsNodes,chooseNode,propertyChooseNode, analyzersNode) ->

            let i = ref (project.ProjectNode.ChildNodes.Count-1)
            while 
              !i >= 0 && 
                (String.startsWithIgnoreCase "<import" (project.ProjectNode.ChildNodes.[!i].OuterXml.ToString())  && 
                 String.containsIgnoreCase "label" (project.ProjectNode.ChildNodes.[!i].OuterXml.ToString())  &&
                 String.containsIgnoreCase "paket" (project.ProjectNode.ChildNodes.[!i].OuterXml.ToString()) )  do
                decr i
            
            if !i <= 0 then
                if chooseNode.ChildNodes.Count > 0 then
                    project.ProjectNode.AppendChild chooseNode |> ignore
            else
                let node = project.ProjectNode.ChildNodes.[!i]
                if chooseNode.ChildNodes.Count > 0 then
                    project.ProjectNode.InsertAfter(chooseNode,node) |> ignore

            let j,k = findInsertSpot()

            let addProps() =
                if j = 0 then
                    propsNodes
                    |> Seq.iter (project.ProjectNode.PrependChild >> ignore)
                else
                    propsNodes
                    |> Seq.iter (fun n -> project.ProjectNode.InsertAfter(n,project.ProjectNode.ChildNodes.[j-1]) |> ignore)
            
            if propertyChooseNode.ChildNodes.Count > 0 then
                if k = 0 then
                    project.ProjectNode.AppendChild propertyChooseNode |> ignore

                    propsNodes
                    |> Seq.iter (project.ProjectNode.AppendChild >> ignore)
                else
                    let node = project.ProjectNode.ChildNodes.[k-1]
                    
                    propsNodes
                    |> Seq.iter (fun n -> project.ProjectNode.InsertAfter(n,node) |> ignore)

                    project.ProjectNode.InsertAfter(propertyChooseNode,node) |> ignore
            else
               addProps()

            targetsNodes
            |> Seq.iter (project.ProjectNode.AppendChild >> ignore)

            if analyzersNode.ChildNodes.Count > 0 then
                project.ProjectNode.AppendChild analyzersNode |> ignore
            )

        if Seq.isEmpty packagesWithRuntimeDependencies then () else

        let j,k = findInsertSpot()
        let toolPath = createRelativePath project.FileName (Path.Combine(rootPath, Constants.PaketFolderName, Constants.PaketFileName))
        let usingTaskNode = 
            createNode "UsingTask" project
            |> addAttribute "TaskName" "CopyRuntimeDependencies"
            |> addAttribute "AssemblyFile" toolPath

        if k > 0 then
            let pos = project.ProjectNode.ChildNodes.[k-1]
            project.ProjectNode.InsertAfter(usingTaskNode,pos) |> ignore
        else
            project.ProjectNode.AppendChild(usingTaskNode) |> ignore

        let allPackages =
            packagesWithRuntimeDependencies 
            |> Seq.map (fun (group,packageName) ->
                if group = Constants.MainDependencyGroup then
                   packageName.ToString()
                else
                   sprintf "%O#%O" group packageName)
            |> Seq.sort
            |> fun xs -> String.Join(";",xs)

        let runtimeDependenciesNode = 
            createNode "CopyRuntimeDependencies" project
            |> addAttribute "OutputPath" "$(OutputPath)"
            |> addAttribute "ProjectsWithRuntimeLibs" allPackages

        let j = ref 0
        let pos = ref None
        while !j < project.ProjectNode.ChildNodes.Count && !pos = None do
            let node = project.ProjectNode.ChildNodes.[!j]
            let nodeText = node.OuterXml.ToString()
            if String.startsWithIgnoreCase  "<target" nodeText && String.containsIgnoreCase "\"afterbuild\"" nodeText then
                pos := Some node
            incr j

        match !pos with
        | Some node -> node.AppendChild(runtimeDependenciesNode) |> ignore
        | None ->
            let afterBuildNode = 
                createNode "Target" project
                |> addAttribute "Name" "AfterBuild"

            afterBuildNode.AppendChild(runtimeDependenciesNode) |> ignore
            
            project.ProjectNode.InsertAfter(afterBuildNode,usingTaskNode) |> ignore

    let save forceTouch project =
        if Utils.normalizeXml project.Document <> project.OriginalText || not (File.Exists(project.FileName)) then
            verbosefn "Project %s changed" project.FileName
            project.Document.Save(project.FileName)
        elif forceTouch then
            File.SetLastWriteTimeUtc(project.FileName, DateTime.UtcNow)

    let getPaketFileItems project =
        BuildAction.PaketFileNodeNames
        |> List.map (fun name -> findPaketNodes name project)
        |> List.concat
        |> List.map (fun n -> FileInfo(Path.Combine(Path.GetDirectoryName project.FileName, n.Attributes.["Include"].Value)))

    let getProjectGuid project = 
        try
            let forceGetInnerText node name =
                match node |> getNode name with 
                | Some n -> n.InnerText
                | None -> failwithf "unable to parse %s" node.Name

            let node = project.Document |> getDescendants "PropertyGroup" |> Seq.head
            forceGetInnerText node "ProjectGuid" |> Guid.Parse
        with
        | _ -> Guid.Empty

    let getInterProjectDependencies project =
        let forceGetInnerText node name =
            match node |> getNode name with 
            | Some n -> n.InnerText
            | None ->
                match node |> getAttribute "Include" with
                | Some fileName ->
                    let fi = FileInfo(normalizePath fileName)
                    fi.Name.Replace(fi.Extension,"")
                | None -> failwithf "unable to parse %O" node

        [for node in project.Document |> getDescendants "ProjectReference" -> 
            let path =
                let normalizedPath = node.Attributes.["Include"].Value |> normalizePath 
                if normalizedPath.Contains "$(SolutionDir)" then 
                    match getProperty "SolutionDir" project with
                    | Some slnDir -> normalizedPath.Replace("$(SolutionDir)",slnDir) 
                    | None -> normalizedPath.Replace("$(SolutionDir)", Environment.CurrentDirectory + Path.DirectorySeparatorChar.ToString())
                else normalizedPath

            { Path =
                if Path.IsPathRooted path then Path.GetFullPath path else 
                let di = FileInfo(normalizePath project.FileName).Directory
                Path.Combine(di.FullName,path) |> Path.GetFullPath

              RelativePath = path.Replace("/","\\")
              Name = forceGetInnerText node "Name"
              GUID = forceGetInnerText node "Project" |> Guid.Parse }]

    let replaceNuGetPackagesFile project =
        let noneAndContentNodes = 
            (project.Document |> getDescendants "None") @ 
            (project.Document |> getDescendants "Content")
        
        match noneAndContentNodes |> List.tryFind (withAttributeValue "Include" Constants.PackagesConfigFile) with
        | None -> ()
        | Some nugetNode ->
            match noneAndContentNodes |> List.filter (withAttributeValue "Include" Constants.ReferencesFile) with 
            | [_] -> nugetNode.ParentNode.RemoveChild nugetNode |> ignore
            | [] -> 
                for c in nugetNode.ChildNodes do
                    nugetNode.RemoveChild c |> ignore
                nugetNode.Attributes.["Include"].Value <- Constants.ReferencesFile
            | _::_ -> failwithf "multiple %s nodes in project file %s" Constants.ReferencesFile project.FileName

    let removeNuGetTargetsEntries project =
        let toDelete = 
            [ project.Document |> getDescendants "RestorePackages" |> List.tryHead
              project.Document 
              |> getDescendants "Import" 
              |> List.tryFind (fun n -> 
                    match n |> getAttribute "Project" with
                    | Some p -> p.Equals("$(SolutionDir)\\.nuget\\nuget.targets", 
                                         StringComparison.InvariantCultureIgnoreCase)
                    | None -> false)
              project.Document
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

    let removeImportAndTargetEntries (packages : list<string * SemVerInfo> ) (project:ProjectFile) =
        let toDelete = 
            project.Document 
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
                    project.FileName 
                    (node |> getAttribute "Project" |> Option.get)
                node.ParentNode.RemoveChild node |> ignore
                match sibling with
                | null -> ()
                | sibling when sibling.Name.Equals "Target" ->
                    let deleteTarget = 
                        Utils.askYesNo
                            (sprintf "Do you want to delete Target named '%s' from %s ?" 
                                (sibling |> getAttribute "Name" |> Option.get)
                                project.FileName)
                    if deleteTarget then
                        sibling.ParentNode.RemoveChild sibling |> ignore
                | _ -> ())

    let outputType (project:ProjectFile) =
        seq {for outputType in project.Document |> getDescendants "OutputType" ->
                match outputType.InnerText with
                | "Exe"    -> ProjectOutputType.Exe
                | "WinExe" -> ProjectOutputType.Exe
                | _        -> ProjectOutputType.Library }
        |> Seq.head
    
    let addImportForPaketTargets relativeTargetsPath (project:ProjectFile) =
        match project.Document 
              |> getDescendants "Import" 
              |> List.tryFind (withAttributeValue "Project" relativeTargetsPath) with
        | Some _ -> ()
        | None -> 
            let node = createNode "Import" project |> addAttribute "Project" relativeTargetsPath
            project.ProjectNode.AppendChild node |> ignore

    let removeImportForPaketTargets relativeTargetsPath (project:ProjectFile) =
        project.Document
        |> getDescendants "Import"
        |> List.tryFind (withAttributeValue "Project" relativeTargetsPath)
        |> Option.iter (fun n -> n.ParentNode.RemoveChild n |> ignore)

    let determineBuildAction fileName (project:ProjectFile) =
        match (Path.GetExtension fileName).ToLowerInvariant() with
        | ext when Path.GetExtension project.FileName = ext + "proj"
            -> BuildAction.Compile
        | ".fsi" -> BuildAction.Compile
        | ".xaml" -> BuildAction.Page
        | ".ttf" | ".png" | ".ico" | ".jpg" | ".jpeg"| ".bmp" | ".gif"
        | ".wav" | ".mid" | ".midi"| ".wma" | ".mp3" | ".ogg" | ".rma"
        | ".avi" | ".mp4" | ".divx"| ".wmv"  //TODO: and other media types
            -> BuildAction.Resource
        | _ -> BuildAction.Content

    let determineBuildActionForRemoteItems fileName (project:ProjectFile) =
        if Path.GetExtension fileName = ".dll"
        then BuildAction.Reference
        else determineBuildAction fileName project

    let getAssemblyName (project:ProjectFile) = 
        let assemblyName =
            project.Document
            |> getDescendants "AssemblyName"
            |> function
               | [] -> failwithf "Project %s has no AssemblyName set" project.FileName
               | [assemblyName] -> assemblyName.InnerText
               | assemblyName::_ ->
                    traceWarnfn "Found multiple AssemblyName nodes in file %s, using first" project.FileName
                    assemblyName.InnerText
            |> fun assemblyName ->
                if String.IsNullOrWhiteSpace assemblyName then 
                    let fi = FileInfo project.FileName
                    fi.Name.Replace(fi.Extension,"")
                else assemblyName

        sprintf "%s.%s" assemblyName (outputType project |> function ProjectOutputType.Library -> "dll" | ProjectOutputType.Exe -> "exe")

    let getOutputDirectory buildConfiguration buildPlatform (project:ProjectFile) =
        let platforms =
            if not <| String.IsNullOrWhiteSpace buildPlatform
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
                    failwithf "Unable to find %s output path node in file %s for any known platforms" buildConfiguration project.FileName
                else
                    failwithf "Unable to find %s output path node in file %s targeting the %s platform" buildConfiguration project.FileName buildPlatform
            | x::xs ->
                let startingData = Map.ofList [("Configuration", buildConfiguration); ("Platform", x)]
                getPropertyWithDefaults "OutputPath" startingData project
                |> function
                    | None -> tryNextPlat xs (x::attempted)
                    | Some s ->
                        if String.IsNullOrWhiteSpace(buildPlatform) then
                            let tested = String.Join(", ", Array.ofList attempted)
                            traceWarnfn "No platform specified; found output path node for the %s platform after failing to find one for the following: %s" x tested
                        s.TrimEnd [|'\\'|] |> normalizePath

        tryNextPlat platforms []

type ProjectFile with

    member this.GetPropertyWithDefaults propertyName defaultProperties = ProjectFile.getPropertyWithDefaults propertyName defaultProperties this

    member this.GetProperty propertyName =  ProjectFile.getProperty propertyName this

    member this.Name = ProjectFile.name this

    member this.NameWithoutExtension = ProjectFile.nameWithoutExtension this

    member this.GetCustomReferenceAndFrameworkNodes() = ProjectFile.getCustomReferenceAndFrameworkNodes this

    member this.CreateNode name =   ProjectFile.createNode name this

    member this.CreateNode(name, text) = ProjectFile.createNodeSet name text this

    member this.DeleteIfEmpty name = ProjectFile.deleteIfEmpty name this

    member this.FindPaketNodes name = ProjectFile.findPaketNodes name this

    member this.GetFrameworkAssemblies() =  ProjectFile.getFrameworkAssemblies this

    member this.DeletePaketNodes name = ProjectFile.deletePaketNodes name this
    
    member this.UpdateFileItems(fileItems : FileItem list) = ProjectFile.updateFileItems fileItems this

    member this.GetCustomModelNodes(model:InstallModel) = ProjectFile.getCustomModelNodes model this

    member this.DeleteCustomModelNodes(model:InstallModel) = ProjectFile.deleteCustomModelNodes model this

    member this.GenerateXml(model, aliases, copyLocal, importTargets, referenceCondition) = ProjectFile.generateXml model aliases copyLocal importTargets referenceCondition this

    member this.RemovePaketNodes () = ProjectFile.removePaketNodes this 

    member this.UpdateReferences (root, completeModel, usedPackages) = ProjectFile.updateReferences root completeModel usedPackages this

    member this.Save(forceTouch) = ProjectFile.save forceTouch this

    member this.GetPaketFileItems () = ProjectFile.getPaketFileItems this

    member this.GetProjectGuid () = ProjectFile.getProjectGuid this

    member this.GetInterProjectDependencies () =  ProjectFile.getInterProjectDependencies this

    member this.ReplaceNuGetPackagesFile () = ProjectFile.replaceNuGetPackagesFile this

    member this.RemoveNuGetTargetsEntries () =  ProjectFile.removeNuGetTargetsEntries this

    member this.RemoveImportAndTargetEntries (packages : list<string * SemVerInfo> ) =  ProjectFile.removeImportAndTargetEntries packages this

    member this.OutputType =  ProjectFile.outputType this

    member this.GetTargetFrameworkIdentifier () =  ProjectFile.getTargetFrameworkIdentifier this

    member this.GetTargetFramework () =  ProjectFile.getTargetFramework this

    member this.GetTargetFrameworkProfile () = ProjectFile.getTargetFrameworkProfile this

    member this.GetTargetProfile () =  ProjectFile.getTargetProfile this
    
    member this.AddImportForPaketTargets relativeTargetsPath = ProjectFile.addImportForPaketTargets relativeTargetsPath this

    member this.RemoveImportForPaketTargets relativeTargetsPath =  ProjectFile.removeImportForPaketTargets relativeTargetsPath this

    member this.DetermineBuildAction fileName = ProjectFile.determineBuildAction fileName this

    member this.DetermineBuildActionForRemoteItems fileName = ProjectFile.determineBuildActionForRemoteItems fileName this

    member this.GetOutputDirectory buildConfiguration buildPlatform =  ProjectFile.getOutputDirectory buildConfiguration buildPlatform this

    member this.GetAssemblyName () = ProjectFile.getAssemblyName this

    static member LoadFromStream(fullName:string, stream:Stream) = ProjectFile.loadFromStream fullName stream 

    static member LoadFromFile(fileName:string) =  ProjectFile.loadFromFile fileName

    static member TryLoad(fileName:string) = ProjectFile.tryLoad fileName

    static member FindCorrespondingFile (projectFile:FileInfo,correspondingFile:string) =
        let specificFile = FileInfo (Path.Combine(projectFile.Directory.FullName, projectFile.Name + "." + correspondingFile))
        if specificFile.Exists then Some specificFile.FullName else
        
        let rec findInDir (currentDir:DirectoryInfo) = 
            let generalFile = FileInfo(Path.Combine(currentDir.FullName, correspondingFile))
            if generalFile.Exists then Some generalFile.FullName
            elif (FileInfo (Path.Combine(currentDir.FullName, Constants.DependenciesFileName))).Exists then None
            elif currentDir.Parent = null then None
            else findInDir currentDir.Parent
                
        findInDir projectFile.Directory

    member this.FindCorrespondingFile (correspondingFile:string) = ProjectFile.FindCorrespondingFile(FileInfo this.FileName,correspondingFile)

    member this.FindReferencesFile() = this.FindCorrespondingFile Constants.ReferencesFile

    member this.HasPackageInstalled(groupName,package) =
        match this.FindReferencesFile() with
        | None -> false
        | Some fileName -> 
            let referencesFile = ReferencesFile.FromFile fileName
            match referencesFile.Groups |> Map.tryFind groupName with
            | None -> false
            | Some group ->
                group.NugetPackages 
                |> Seq.exists (fun p -> p.Name = package)

    static member FindReferencesFile(projectFile) = ProjectFile.FindCorrespondingFile(projectFile, Constants.ReferencesFile)

    member this.FindTemplatesFile() = this.FindCorrespondingFile Constants.TemplateFile

    static member FindOrCreateReferencesFile projectFile =
        match ProjectFile.FindReferencesFile(projectFile) with
        | None ->
            let newFileName =
                let fi = FileInfo(Path.Combine(projectFile.Directory.FullName,Constants.ReferencesFile))
                if fi.Exists then
                    Path.Combine(projectFile.Directory.FullName,projectFile.Name + "." + Constants.ReferencesFile)
                else
                    fi.FullName
            ReferencesFile.New newFileName
        | Some fileName -> ReferencesFile.FromFile fileName


    member this.FindOrCreateReferencesFile() = ProjectFile.FindOrCreateReferencesFile (FileInfo this.FileName)

    /// Finds all project files
    static member FindAllProjects folder : ProjectFile [] =
        let packagesPath = Path.Combine(folder,Constants.PackagesFolderName) |> normalizePath
        let paketPath = Path.Combine(folder,Constants.PaketFilesFolderName) |> normalizePath

        let findAllFiles (folder, pattern) = 
            let rec search (di:DirectoryInfo) = 
                try
                    let files = di.GetFiles(pattern, SearchOption.TopDirectoryOnly)
                    di.GetDirectories()
                    |> Array.filter (fun di ->
                        try 
                            let path = di.FullName |> normalizePath
                            if path = packagesPath then false else
                            if path = paketPath then false else
                            Path.Combine(path, Constants.DependenciesFileName) 
                            |> File.Exists 
                            |> not 
                        with 
                        | _ -> false)
                    |> Array.collect search
                    |> Array.append files
                with
                | _ -> Array.empty

            search <| DirectoryInfo folder

        findAllFiles(folder, "*proj*")
        |> Array.choose (fun f -> 
            if f.Extension = ".csproj" || f.Extension = ".fsproj" || f.Extension = ".vbproj" || f.Extension = ".wixproj" || f.Extension = ".nproj" || f.Extension = ".vcxproj" then
                ProjectFile.tryLoad f.FullName
            else None)

    static member TryFindProject(projects,projectName) =
        let isMatching (p:ProjectFile) = p.NameWithoutExtension = projectName || p.Name = projectName

        match projects |> Seq.tryFind isMatching with
        | Some p -> Some p
        | None ->
            try
                let fi = FileInfo (normalizePath (projectName.Trim().Trim([|'\"'|]))) // check if we can detect the path
                let rec checkDir (dir:DirectoryInfo) = 
                    match projects |> Seq.tryFind (fun p -> 
                        String.equalsIgnoreCase ((FileInfo p.FileName).Directory.ToString()) (dir.ToString())) with
                    | Some p -> Some p
                    | None ->
                        if isNull dir.Parent then None else
                        checkDir dir.Parent
                checkDir fi.Directory
            with
            | _ -> None

    member this.GetAllInterProjectDependenciesWithoutProjectTemplates() = this.ProjectsWithoutTemplates(this.GetAllReferencedProjects())

    member this.GetAllInterProjectDependenciesWithProjectTemplates() = this.ProjectsWithTemplates(this.GetAllReferencedProjects())

    member this.ProjectsWithoutTemplates projects =
        projects
        |> Seq.filter(fun proj ->
            if proj = this then true
            else
                let templateFilename = proj.FindTemplatesFile()
                match templateFilename with
                | Some tfn -> TemplateFile.IsProjectType tfn |> not
                | None -> true
        )

    member this.ProjectsWithTemplates projects =
        projects
        |> Seq.filter(fun proj ->
            if proj = this then true
            else
                let templateFilename = proj.FindTemplatesFile()
                match templateFilename with
                | Some tfn -> TemplateFile.IsProjectType tfn
                | None -> false
        )

    member this.GetAllReferencedProjects() =
        let rec getProjects (project:ProjectFile) = 
            seq {
                let projects = 
                    project.GetInterProjectDependencies() 
                    |> Seq.map (fun proj -> ProjectFile.tryLoad(proj.Path).Value)

                yield! projects
                for proj in projects do
                    yield! (getProjects proj)
            }
        seq { 
            yield this
            yield! getProjects this
        }

    member this.GetProjects includeReferencedProjects =
        seq {
            if includeReferencedProjects then
                yield! this.GetAllReferencedProjects()
            else
                yield this
        }

    member this.GetCompileItems (includeReferencedProjects : bool) = 
        let getCompileRefs projectFile =
            projectFile.Document
            |> getDescendants "Compile"
            |> Seq.map (fun compileNode -> projectFile, compileNode)

        let getCompileItem (projectFile, compileNode) =
            let projectFolder = projectFile.FileName |> Path.GetFullPath |> Path.GetDirectoryName
            let sourceFile =
                compileNode
                |> getAttribute "Include"
                |> fun attr -> attr.Value
                |> normalizePath
                |> fun relPath -> Path.Combine(projectFolder, relPath)

            let destPath =
                compileNode
                |> getDescendants "Link"
                |> function
                    | [] -> createRelativePath (projectFolder + string Path.DirectorySeparatorChar) sourceFile
                    | linkNode :: _ -> linkNode.InnerText
                |> normalizePath
                |> Path.GetDirectoryName
            {
                SourceFile = sourceFile
                DestinationPath = destPath
                BaseDir = projectFolder
            }

        let getRealItems compileItem =
            let sourceFolder = Path.GetDirectoryName(compileItem.SourceFile)
            let filespec = Path.GetFileName(compileItem.SourceFile)
            Directory.GetFiles(sourceFolder, filespec)
            |> Seq.map (fun realFile ->
            {
                SourceFile = realFile
                DestinationPath = compileItem.DestinationPath.Replace("%(FileName)", Path.GetFileName(realFile))
                BaseDir = compileItem.BaseDir
            })

        this.GetProjects includeReferencedProjects
        |> this.ProjectsWithoutTemplates
        |> Seq.collect getCompileRefs
        |> Seq.map getCompileItem
        |> Seq.collect getRealItems
