namespace Paket

open Paket
open Paket.Domain
open Paket.Logging
open Paket.InterprojectReferencesConstraint
open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.IO
open System.Text
open System.Text.RegularExpressions
open System.Xml
open Paket.Xml
open Paket.Requirements
open Paket.PackageSources
open System.Globalization

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
      Name : string option
      ReferenceOutputAssembly : bool
      GUID : Guid option }

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

[<RequireQualifiedAccess>]
type ProjectLanguage = Unknown | CSharp | FSharp | VisualBasic | WiX | Nemerle | CPP | IronPython | ServiceFabric | Sql

module LanguageEvaluation =
    let private extractProjectTypeGuids (projectDocument:XmlDocument) =
        projectDocument
        |> getDescendants "PropertyGroup"
        |> List.filter(fun g -> g.Attributes.Count = 0)
        |> List.collect(fun g -> g |> getDescendants "ProjectTypeGuids")
        |> List.filter(fun pt -> pt.Attributes.Count = 0)
        |> List.collect(fun pt -> pt.InnerText.Split ';' |> List.ofArray)
        |> List.distinct
        |> List.choose(fun guid -> match Guid.TryParse guid with | true, g -> Some g | _ -> None)

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

    let private ironPythonGuids =
        [
            "{D499C55F-46C0-4FE1-8D05-02605C1891EA}" // IronPython
        ] |> List.map Guid.Parse |> Set.ofList

    let private serviceFabric =
        [
            "{e1296024-58b6-4e25-843e-b9faa9b07be6}" // ServiceFabric
        ] |> List.map Guid.Parse |> Set.ofList

    let private getGuidLanguage (guid:Guid) =
        let isCsharp = csharpGuids.Contains(guid)
        let isVb = vbGuids.Contains(guid)
        let isFsharp = fsharpGuids.Contains(guid)
        let isNemerle = nemerleGuids.Contains(guid)
        let isIronPython = ironPythonGuids.Contains(guid)
        let isServiceFabric = serviceFabric.Contains(guid)

        match (isCsharp, isVb, isFsharp, isNemerle, isIronPython) with
        | true, false, false, false, false -> Some ProjectLanguage.CSharp
        | false, true, false, false, false -> Some ProjectLanguage.VisualBasic
        | false, false, true, false, false -> Some ProjectLanguage.FSharp
        | false, false, false, true, false -> Some ProjectLanguage.Nemerle
        | false, false, false, false, true -> Some ProjectLanguage.IronPython
        | _ -> None

    let private getLanguageFromExtension = function
        | ".csproj" -> Some ProjectLanguage.CSharp
        | ".vbproj" -> Some ProjectLanguage.VisualBasic
        | ".fsproj" -> Some ProjectLanguage.FSharp
        | ".vcxproj" -> Some ProjectLanguage.CPP
        | ".wixproj" -> Some ProjectLanguage.WiX
        | ".nproj"  -> Some ProjectLanguage.Nemerle
        | ".pyproj"  -> Some ProjectLanguage.IronPython
        | ".sfproj"  -> Some ProjectLanguage.ServiceFabric
        | ".sqlproj"  -> Some ProjectLanguage.Sql
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
        | _ -> ProjectLanguage.Unknown

/// Contains methods to read and manipulate project files.
type ProjectFile =
    { FileName: string
      OriginalText : string
      Document : XmlDocument
      ProjectNode : XmlNode
      Language : ProjectLanguage
      // Caches for optimizations
      mutable DefaultProperties : Map<string,string> option
      // CalculatedMaps for optimizations
      mutable CalculatedProperties : System.Collections.Concurrent.ConcurrentDictionary<Map<string,string>, Map<string,string>> }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ProjectFile =
    let supportedEndings = [ ".csproj"; ".fsproj"; ".vbproj"; ".wixproj"; ".nproj"; ".vcxproj"; ".pyproj"; ".sfproj"; ".proj"; ".sqlproj" ]

    let isSupportedFile (fi:FileInfo) =
        supportedEndings
        |> List.exists fi.Extension.Contains

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
            DefaultProperties = None
            CalculatedProperties = new System.Collections.Concurrent.ConcurrentDictionary<Map<string,string>, Map<string,string>>()
        }

    let loadFromFile(fileName:string) =
        let fileInfo = FileInfo (normalizePath fileName)
        use stream = fileInfo.OpenRead()
        loadFromStream fileInfo.FullName stream

    let loadFromString (fullName:string) (text:string) =
        use stream =
            let bytes = text |> Encoding.UTF8.GetBytes
            new MemoryStream (bytes)
        loadFromStream fullName stream

    let tryLoad(fileName:string) =
        try
            if String.IsNullOrWhiteSpace fileName then None else
            let fi = FileInfo fileName
            if isSupportedFile fi then
                if verbose then
                    verbosefn "Loading %s" fileName
                Some(loadFromFile fileName)
            else
                None
        with
        | exn ->
            traceWarnfn "Unable to parse project %s:%s      %s" fileName Environment.NewLine exn.Message
            None

    let createNode name (project:ProjectFile) =
        try
            project.Document.CreateElement (name, project.Document.DocumentElement.NamespaceURI)
        with
        | _ ->
            project.Document.CreateElement (name, Constants.ProjectDefaultNameSpace)

    let createNodeSet name text (project:ProjectFile) =
        let node = createNode name project
        node.InnerText <- text
        node

    let private getReservedProperties (projectFile:ProjectFile) =
        let calculateNew () =
            let projectFileInfo = FileInfo projectFile.FileName
            let directoryNoRoot = Regex.Replace(projectFileInfo.FullName, "^.:\\\\?", "")
            [
                // Project file properties
                "MSBuildProjectDirectory", projectFileInfo.DirectoryName
                "MSBuildProjectDirectoryNoRoot", directoryNoRoot
                "MSBuildProjectExtension", projectFileInfo.Extension
                "MSBuildProjectFile", projectFileInfo.Name
                "MSBuildProjectFullPath", projectFileInfo.FullName
                "MSBuildProjectName", Path.GetFileNameWithoutExtension(projectFileInfo.FullName)

                // This file properties (Potentially an Imported file)
                "MSBuildThisFileDirectory", projectFileInfo.DirectoryName + (string Path.DirectorySeparatorChar)
                "MSBuildThisFileDirectoryNoRoot", directoryNoRoot + (string Path.DirectorySeparatorChar)
                "MSBuildThisFileExtension", projectFileInfo.Extension
                "MSBuildThisFile", projectFileInfo.Name
                "MSBuildThisFileFullPath", projectFileInfo.FullName
                "MSBuildThisFileName", Path.GetFileNameWithoutExtension(projectFileInfo.FullName)
            ] |> Map.ofList

        match projectFile.DefaultProperties with
        | Some p -> p
        | None ->
            let res = calculateNew()
            projectFile.DefaultProperties <- Some res
            res

    /// Append two maps with the properties of the second replacing properties of the first
    let private appendMap first second =
        Map.fold (fun state key value -> Map.add key value state) first second

    let inline getPackageIdAttribute (pf:ProjectFile) (node:XmlNode) =
        let maybePackageId =
            node
            |> getAttribute "Include"
            |> Option.orElseWith (fun _ -> node |> getAttribute "Update")
        match maybePackageId with
        | Some id -> id
        | None -> failwithf "project file '%s' contains a reference without 'Include' or 'Update' attribute" pf.FileName

    let private calculatePropertyMap (projectFile:ProjectFile) defaultProperties =
        let defaultProperties = appendMap defaultProperties (getReservedProperties projectFile)

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
            if input.Length <= index then
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


        let parseCondition (data:StringBuilder) (input:string) index =
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

        let rec parseAndOr (data:StringBuilder) (input:string) index =
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


        let rec parseFullCondition data (sb:StringBuilder) (input:string) index =
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
                        let data = Some (data @[(andOr, left, comp, right)])
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
                    | "==" | "!=" ->
                        let eq = left.Equals(right, StringComparison.OrdinalIgnoreCase)
                        if comp = "=="
                        then eq
                        else not eq
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
            let allConditions = parseFullCondition (Some []) (StringBuilder()) condition 0

            match allConditions with
            | None -> false
            | Some conditions -> handleConditions data conditions true


        let addData data (node:XmlNode) =
            let text = processPlaceholders data node.InnerText
            let unescapedString = Uri.UnescapeDataString text
            // Note that using Map.add overrides the value assigned
            // to this key if it already exists in the map; so long
            // as we process nodes top-to-bottom, this matches the
            // behavior of MSBuild.
            Map.add node.Name unescapedString data


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
        map

    let getPropertyWithDefaults propertyName defaultProperties (projectFile:ProjectFile) =
        let map = projectFile.CalculatedProperties.GetOrAdd(defaultProperties, calculatePropertyMap projectFile)
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

    let internal findNodes paketOnes name (project:ProjectFile) = [
        for node in project.Document |> getDescendants name do
            let mutable isPaketNode = false
            for child in node.ChildNodes do
                if child.Name = "Paket" && String.equalsIgnoreCase child.InnerText "true" then
                    isPaketNode <- true
            if isPaketNode = paketOnes then yield node
    ]

    let getCustomReferenceAndFrameworkNodes project = (findNodes false "Reference" project) @ (findNodes false "NativeReference" project)

    let findPaketNodes name (project:ProjectFile) = findNodes true name project

    let getFrameworkAssemblies (project:ProjectFile) = [
        for node in project.Document |> getDescendants "Reference" do
            let hasHintPath = ref false
            for child in node.ChildNodes do
                if child.Name = "HintPath" then
                    hasHintPath := true
            if not !hasHintPath then
                yield node.Attributes.["Include"].InnerText.Split(',').[0]
    ]

    let deletePaketNodes name (project:ProjectFile) =
        let nodesToDelete = findPaketNodes name project
        if nodesToDelete |> Seq.isEmpty |> not then
            if verbose then
                verbosefn "    - Deleting Paket %s nodes" name

        for node in nodesToDelete do
            node.ParentNode.RemoveChild node |> ignore

    let updateFileItems (fileItems:FileItem list) (project:ProjectFile) =
        let newItemGroups =
            let firstItemGroup =
                project.ProjectNode
                |> getNodes "ItemGroup"
                |> List.filter (fun n -> List.isEmpty (getNodes "Reference" n))
                |> List.tryHead

            match firstItemGroup with
            | None ->
                let node = createNode "ItemGroup" project
                if List.isEmpty fileItems |> not then
                    project.ProjectNode.AppendChild node |> ignore
                [   BuildAction.Content, node
                    BuildAction.Compile, node
                    BuildAction.Reference, node
                    BuildAction.Resource, node
                    BuildAction.Page, node
                ]
            | Some node ->
                [   BuildAction.Content, node :?> XmlElement
                    BuildAction.Compile, node :?> XmlElement
                    BuildAction.Reference, node :?> XmlElement
                    BuildAction.Resource, node :?> XmlElement
                    BuildAction.Page, node :?> XmlElement
                ]
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
                |> fun n ->
                    match fileItem.Link with
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
            |> List.collect (fun name -> findPaketNodes name project)

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
        let libs : string Set =
            (model.GetAllLegacyReferenceAndFrameworkReferenceNames())

        getCustomReferenceAndFrameworkNodes project
        |> List.filter (fun node ->
            let libName = node.Attributes.["Include"].InnerText.Split(',').[0]
            Set.contains libName libs)

    let deleteCustomModelNodes (model:InstallModel) (project:ProjectFile) =
        let nodesToDelete =
            getCustomModelNodes model project
            |> List.filter (fun node ->
                let mutable isFrameworkNode = true
                let mutable isManualNode = false
                for child in node.ChildNodes do
                    if child.Name = "HintPath" then isFrameworkNode <- false
                    if child.Name = "Private" then isFrameworkNode  <- false
                    if child.Name = "Paket" && String.equalsIgnoreCase child.InnerText "false" then
                        isManualNode <- true

                not isFrameworkNode && not isManualNode)

        if nodesToDelete <> [] then
            if verbose then
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
        |> List.sortBy (fun lib -> lib.Path)
        |> createAnalyzersNode

    type XmlContext = {
        GlobalPropsNodes : XmlElement list
        GlobalTargetsNodes : XmlElement list
        FrameworkSpecificPropsNodes : XmlElement list
        FrameworkSpecificTargetsNodes : XmlElement list
        ChooseNodes : XmlElement list
        FrameworkSpecificPropertyChooseNode : XmlElement
        AnalyzersNode : XmlElement
    }

    let generateXml (model:InstallModel) (usedFrameworkLibs:HashSet<TargetProfile*string>) (aliases:Map<string,string>) (embedInteropTypes:bool option) (copyLocal:bool option) (specificVersion:bool option) (importTargets:bool) (referenceCondition:string option) (allTargetProfiles:Set<TargetProfile>) (project:ProjectFile) : XmlContext =
        let references =
            getCustomReferenceAndFrameworkNodes project
            |> List.map (fun node -> node.Attributes.["Include"].InnerText.Split(',').[0])
            |> Set.ofList

        let model = model.FilterReferences references
        let createItemGroup (targets:TargetProfile Set) (frameworkReferences:FrameworkReference list) (libraries:Library list) =
            let itemGroup = createNode "ItemGroup" project

            for ref in frameworkReferences |> List.sortBy (fun f -> f.Name) do
                createNode "Reference" project
                |> addAttribute "Include" ref.Name
                |> addChild (createNodeSet "Paket" "True" project)
                |> itemGroup.AppendChild
                |> ignore
            for lib in libraries |> List.sortBy (fun f -> f.Name) do
                let fi = FileInfo (normalizePath lib.Path)
                let aliases =
                    aliases
                    |> Map.tryPick (fun dll alias -> if fi.Name.Equals(dll, StringComparison.OrdinalIgnoreCase) then Some(alias) else None)

                let relativePath = createRelativePath project.FileName fi.FullName
                let privateSettings =
                    match copyLocal with
                    | Some true -> "True"
                    | Some false -> "False"
                    | None -> if relativePath.Contains @"\ref\" then "False" else "True"

                let specificVersionSettings =
                    match specificVersion with
                    | Some true -> "True"
                    | Some false -> "False"
                    | None -> "True"

                if relativePath.Contains @"\native\" then createNode "NativeReference" project else createNode "Reference" project
                |> addAttribute "Include" (fi.Name.Replace(fi.Extension,""))
                |> addChild (createNodeSet "HintPath" relativePath project)
                |> addChild (createNodeSet "Private" privateSettings project)
                |> fun n ->
                    match specificVersion with
                    | None -> n
                    | Some(bool) -> addChild (createNodeSet "SpecificVersion" specificVersionSettings project) n
                |> addChild (createNodeSet "Paket" "True" project)
                |> fun n ->
                    match embedInteropTypes with
                    | None -> n
                    | _ -> addChild (createNodeSet "EmbedInteropTypes" "True" project) n
                |> fun n ->
                    match aliases with
                    | None -> n
                    | Some alias -> addChild (createNodeSet "Aliases" alias project) n
                |> itemGroup.AppendChild
                |> ignore

            itemGroup

        let createPropertyGroup (references:MsBuildFile list) =
            let propertyGroup = createNode "PropertyGroup" project

            let propertyNames =
                references
                |> Seq.map (fun lib ->
                    let targetsFile = lib.Path
                    let fi = FileInfo (normalizePath targetsFile)
                    let propertyName = "__paket__" + fi.Name.ToString().Replace(" ","_").Replace(".","_")

                    let path = createRelativePath project.FileName (fi.FullName.Replace (fi.Extension,""))
                    let s = path.Substring(path.LastIndexOf("build\\") + 6)
                    let node = createNode propertyName project
                    node.InnerText <- s
                    node
                    |> propertyGroup.AppendChild
                    |> ignore
                    propertyName,createRelativePath project.FileName fi.FullName,path.Substring(0,path.LastIndexOf("build\\") + 6))
                |> Set.ofSeq

            propertyNames,propertyGroup

        let allTargets =
            model.CompileLibFolders
            |> List.map (fun lib -> lib.Targets)

        // Just in case anyone wants to compile FOR netcore in the old format...
        // I don't think there is anyone actually using this part, but it's there for backwards compat.
        let netCoreRestricted =
            model.ApplyFrameworkRestrictions
                ((List.map DotNetCoreApp KnownTargetProfiles.DotNetCoreAppVersions @ List.map DotNetStandard KnownTargetProfiles.DotNetStandardVersions)
                 |> List.map FrameworkRestriction.Exactly
                 |> List.fold FrameworkRestriction.combineRestrictionsWithOr FrameworkRestriction.EmptySet)

        // handle legacy conditions
        let conditions =
            (model.CompileLibFolders @ (List.map (FrameworkFolder.map (fun refs -> { ReferenceOrLibraryFolder.empty with Libraries = refs })) netCoreRestricted.CompileRefFolders))
            |> List.sortBy (fun libFolder -> libFolder.Path)
            |> List.collect (fun libFolder ->
                match libFolder with
                | _ ->
                    match PlatformMatching.getCondition referenceCondition libFolder.Targets with
                    | "" -> []
                    | condition ->
                        let condition =
                            match condition with
                            | "$(TargetFrameworkIdentifier) == 'true'" -> "true"
                            | _ -> condition

                        let frameworkReferences =
                            libFolder.FolderContents.FrameworkReferences
                            |> Seq.sortBy (fun r -> r.Name)
                            |> Seq.toList

                        let libraries =
                            libFolder.FolderContents.Libraries
                            |> Seq.sortBy (fun r -> r.Path)
                            |> Seq.toList

                        let assemblyTargets = ref libFolder.Targets
                        let duplicates = HashSet<_>()
                        for frameworkAssembly in frameworkReferences do
                            for t in libFolder.Targets do
                                if not (usedFrameworkLibs.Add(t,frameworkAssembly.Name)) then
                                    assemblyTargets := Set.remove t !assemblyTargets // List.filter ((<>) t) !assemblyTargets
                                    duplicates.Add frameworkAssembly.Name |> ignore

                        if !assemblyTargets = libFolder.Targets then
                            [condition,createItemGroup libFolder.Targets frameworkReferences libraries,false]
                        else
                            let specialFrameworkAssemblies, rest =
                                frameworkReferences
                                |> List.partition (fun fr -> duplicates.Contains fr.Name)

                            match PlatformMatching.getCondition referenceCondition !assemblyTargets with
                            | "" -> [condition,createItemGroup libFolder.Targets rest libraries,false]
                            | lowerCondition ->
                                [lowerCondition,createItemGroup !assemblyTargets specialFrameworkAssemblies [],true
                                 condition,createItemGroup libFolder.Targets rest libraries,false]
                        )

        // global targets are targets, that are either directly in the /build folder.
        // (ref https://docs.microsoft.com/en-us/nuget/create-packages/creating-a-package#including-msbuild-props-and-targets-in-a-package).
        let globalTargets, frameworkSpecificTargets =
            if not importTargets then Seq.empty, List.empty else
            let globalTargets = model.TargetsFileFolders |> Seq.collect (fun f -> f.RootContents)
            let frameworkSpecificTargets = model.TargetsFileFolders |> List.collect (fun f -> f.FrameworkFolders)
            globalTargets,frameworkSpecificTargets

        let frameworkSpecificTargetsFileConditions =
            frameworkSpecificTargets
            |> List.map (fun lib -> PlatformMatching.getCondition referenceCondition lib.Targets,createPropertyGroup (lib.FolderContents |> List.ofSeq))

        let chooseNodes =
            match conditions with
            |  ["$(TargetFrameworkIdentifier) == 'true'",itemGroup,_] -> [itemGroup]
            |  ["true",itemGroup,_] -> [itemGroup]
            |  _ ->
                let chooseNode = createNode "Choose" project
                let chooseNode2 = createNode "Choose" project

                let containsReferences = ref false
                let containsReferences2 = ref false

                let nodes =
                    conditions
                    |> List.map (fun (condition,itemGroup,ownNode) ->
                        let condition =
                            match condition with
                            | "$(TargetFrameworkIdentifier) == 'true'" -> "true"
                            | _ -> condition

                        let whenNode =
                            createNode "When" project
                            |> addAttribute "Condition" condition

                        if not itemGroup.IsEmpty then
                            whenNode.AppendChild itemGroup |> ignore
                            if ownNode then
                                containsReferences2 := true
                            else
                                containsReferences := true
                        whenNode,ownNode)

                for node,ownNode in nodes do
                    if ownNode then
                        chooseNode2.AppendChild node |> ignore
                    else
                        chooseNode.AppendChild node |> ignore

                match !containsReferences,!containsReferences2 with
                | true,true -> [chooseNode2; chooseNode]
                | true,false -> [chooseNode]
                | false,true -> [chooseNode2]
                | false,false -> [createNode "Choose" project]

        let frameworkSpecificPropertyNames,frameworkSpecificPropertyChooseNode =
            match frameworkSpecificTargetsFileConditions with
            |  ["$(TargetFrameworkIdentifier) == 'true'",(propertyNames,propertyGroup)] ->
                [propertyNames], createNode "Choose" project
            |  ["true",(propertyNames,propertyGroup)] ->
                [propertyNames], createNode "Choose" project
            |  _ ->
                let propertyChooseNode = createNode "Choose" project

                let containsProperties = ref false
                let nodes =
                    frameworkSpecificTargetsFileConditions
                    |> List.map (fun (condition,(propertyNames,propertyGroup)) ->
                        let finalCondition = if condition = "" || condition.Length > 3000 || condition = "$(TargetFrameworkIdentifier) == 'true'" then "1 == 1" else condition
                        let whenNode =
                            createNode "When" project
                            |> addAttribute "Condition" finalCondition
                        if not (Set.isEmpty propertyNames) then
                            whenNode.AppendChild(propertyGroup) |> ignore
                            containsProperties := true
                        whenNode)
                for node in nodes do
                    propertyChooseNode.AppendChild node |> ignore

                (frameworkSpecificTargetsFileConditions |> List.map (fun (_,(propertyNames,_)) -> propertyNames)),
                (if !containsProperties then propertyChooseNode else createNode "Choose" project)


        let frameworkSpecificPropsNodes =
            frameworkSpecificPropertyNames
            |> Seq.concat
            |> Seq.distinctBy (fun (x,_,_) -> x)
            |> Seq.filter (fun (propertyName,path,buildPath) -> String.endsWithIgnoreCase "props" propertyName)
            |> Seq.map (fun (propertyName,path,buildPath) ->
                let fileName =
                    match propertyName with
                    | _ when frameworkSpecificPropertyChooseNode.ChildNodes.Count = 0 -> path
                    | name when String.endsWithIgnoreCase "props" name  -> sprintf "%s$(%s).props" buildPath propertyName
                    | _ -> failwithf "Unknown .props filename %s" propertyName

                createNode "Import" project
                |> addAttribute "Project" fileName
                |> addAttribute "Condition" (sprintf "Exists('%s')" fileName)
                |> addAttribute "Label" "Paket")
            |> Seq.toList

        let frameworkSpecificTargetsNodes =
            frameworkSpecificPropertyNames
            |> Seq.concat
            |> Seq.distinctBy (fun (x,_,_) -> x)
            |> Seq.filter (fun (propertyName,path,buildPath) -> String.endsWithIgnoreCase "props" propertyName  |> not)
            |> Seq.map (fun (propertyName,path,buildPath) ->
                let fileName =
                    match propertyName with
                    | _ when frameworkSpecificPropertyChooseNode.ChildNodes.Count = 0 -> path
                    | name when String.endsWithIgnoreCase  "targets" name ->
                        sprintf "%s$(%s).targets" buildPath propertyName
                    | _ -> failwithf "Unknown .targets filename %s" propertyName

                createNode "Import" project
                |> addAttribute "Project" fileName
                |> addAttribute "Condition" (sprintf "Exists('%s')" fileName)
                |> addAttribute "Label" "Paket")
            |> Seq.toList

        let globalPropsNodes =
            globalTargets
            |> Seq.map (fun t -> t.Path)
            |> Seq.distinct
            |> Seq.filter (fun t -> String.endsWithIgnoreCase ".props" t)
            |> Seq.map (fun t ->
                let fileName = createRelativePath project.FileName t
                createNode "Import" project
                |> addAttribute "Project" fileName
                |> addAttribute "Condition" (sprintf "Exists('%s')" fileName)
                |> addAttribute "Label" "Paket")
            |> Seq.toList

        let globalTargetsNodes =
            globalTargets
            |> Seq.map (fun t -> t.Path)
            |> Seq.distinct
            |> Seq.filter (fun t -> String.endsWithIgnoreCase ".targets" t)
            |> Seq.map (fun t ->
                let fileName = createRelativePath project.FileName t
                createNode "Import" project
                |> addAttribute "Project" fileName
                |> addAttribute "Condition" (sprintf "Exists('%s')" fileName)
                |> addAttribute "Label" "Paket")
            |> Seq.toList

        let analyzersNode = generateAnalyzersXml model project

        {   GlobalTargetsNodes = globalTargetsNodes
            GlobalPropsNodes = globalPropsNodes
            FrameworkSpecificPropsNodes = frameworkSpecificPropsNodes
            FrameworkSpecificTargetsNodes = frameworkSpecificTargetsNodes
            ChooseNodes = chooseNodes
            FrameworkSpecificPropertyChooseNode = frameworkSpecificPropertyChooseNode
            AnalyzersNode = analyzersNode
        } : XmlContext

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
                elif node.Name = "UsingTask" && (not (isNull node.Attributes.["TaskName"])) && node.Attributes.["TaskName"].Value = "CopyRuntimeDependencies" then
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
    let getTargetFrameworkVersion (project:ProjectFile) = getProperty "TargetFrameworkVersion" project
    let getTargetFramework (project:ProjectFile) = getProperty "TargetFramework" project
    let getTargetFrameworks (project:ProjectFile) = getProperty "TargetFrameworks" project
    let getTargetFrameworksParsed (project:ProjectFile) =
        getTargetFrameworks project
        |> Option.map (fun x -> x.Split([|';'|],StringSplitOptions.RemoveEmptyEntries))
        |> Option.toArray
        |> Array.concat
        |> Array.map (fun x -> x.Trim())
        |> Array.toList

    let getToolsVersion (project:ProjectFile) =
        let adjustIfWeHaveSDK v =
            try
                let sdkAttr = project.ProjectNode.Attributes.["Sdk"]
                if isNull sdkAttr || String.IsNullOrWhiteSpace sdkAttr.Value
                then
                    // search for Sdk.props / Sdk.targets
                    let hasSdkImports =
                        project.ProjectNode.ChildNodes
                            |> Seq.cast<XmlNode>
                            |> Seq.tryFind (fun node -> node.LocalName = "Import" && let attr = node.Attributes.["Sdk"] in not (isNull attr) && attr.Value = "Microsoft.NET.Sdk")
                            |> Option.isSome
                    if hasSdkImports then 15.0
                    else v   // adjustment so paket still installs to old style msbuild projects that are using MSBuild15 but not the new format
                else 15.0
            with
            | _ -> v

        match project.ProjectNode.Attributes.["ToolsVersion"] with
        | null -> adjustIfWeHaveSDK 4.0
        | v ->
            match Double.TryParse(v.Value, NumberStyles.Any, CultureInfo.InvariantCulture) with
            | true , v when v >= 15.0 ->
                    let sdkAttr = project.ProjectNode.Attributes.["Sdk"]
                    if  isNull sdkAttr || String.IsNullOrWhiteSpace sdkAttr.Value
                    then 14.0   // adjustment so paket still installs to old style msbuild projects that are using MSBuild15 but not the new format
                    else v
            | true,  version -> adjustIfWeHaveSDK version
            | _         -> adjustIfWeHaveSDK 4.0

    let getTargetProfiles (project:ProjectFile) =
        let fallback () =
            let prefix() =
                match getTargetFrameworkIdentifier project with
                | None -> "net"
                | Some x -> x

            let frameworks =
                match getTargetFrameworkVersion project with
                | None ->
                    let xs = getTargetFrameworksParsed project
                    (getTargetFramework project |> Option.toList) @ xs

                | Some x -> [prefix() + (x.Replace("v",""))]

            match frameworks |> List.choose (fun s -> FrameworkDetection.internalExtract s |> Option.map TargetProfile.SinglePlatform) |> List.filter (fun x -> match x with TargetProfile.SinglePlatform(Unsupported _) -> false | _ -> true)  with
            | [] -> [TargetProfile.SinglePlatform (DotNetFramework FrameworkVersion.V4)]
            | xs -> xs


        match getTargetFrameworkProfile project with
        | Some profile when profile = "Unity Web v3.5" ->
            [TargetProfile.SinglePlatform (DotNetUnity DotNetUnityVersion.V3_5_Web)]
        | Some profile when profile = "Unity Micro v3.5" ->
            [TargetProfile.SinglePlatform (DotNetUnity DotNetUnityVersion.V3_5_Micro)]
        | Some profile when profile = "Unity Subset v3.5" ->
            [TargetProfile.SinglePlatform (DotNetUnity DotNetUnityVersion.V3_5_Subset)]
        | Some profile when profile = "Unity Full v3.5" ->
            [TargetProfile.SinglePlatform (DotNetUnity DotNetUnityVersion.V3_5_Full)]
        | Some profile when String.IsNullOrWhiteSpace profile |> not ->
            try
                [KnownTargetProfiles.FindPortableProfile profile]
            with e ->
                traceWarnfn "Could not detect TargetFrameworkProfile '%s' in '%s' via 'FindPortableProfile', using fallback" profile project.FileName
                fallback()
        | _ -> fallback()

    let updateReferences
            (completeModel: Map<GroupName*PackageName,_*InstallModel>)
            (directPackages : Map<GroupName*PackageName,_*InstallSettings>)
            (usedPackages : Map<GroupName*PackageName,_*InstallSettings>)
            (project:ProjectFile) =
        removePaketNodes project

        let findInsertSpot() =
            // nuget inserts properties directly at the top, and targets directly at the end.
            // our inserts depend on $(TargetFrameworkVersion), which may be set either from another import, or directly in the project file.
            let mutable iProp = 0
            while iProp < project.ProjectNode.ChildNodes.Count && String.startsWithIgnoreCase  "<import" (project.ProjectNode.ChildNodes.[iProp].OuterXml.ToString()) do
                iProp <- iProp + 1

            let mutable iTarget = iProp
            while iTarget < project.ProjectNode.ChildNodes.Count &&
                (String.startsWithIgnoreCase  "<PropertyGroup" (project.ProjectNode.ChildNodes.[iTarget].OuterXml.ToString()) ||
                 (String.startsWithIgnoreCase  "<import" (project.ProjectNode.ChildNodes.[iTarget].OuterXml.ToString()) &&
                  not (String.containsIgnoreCase "label" (project.ProjectNode.ChildNodes.[iTarget].OuterXml.ToString()) &&
                       String.containsIgnoreCase "paket" (project.ProjectNode.ChildNodes.[iTarget].OuterXml.ToString())))) do
                iTarget <- iTarget + 1

            let mutable l = iTarget
            while l < project.ProjectNode.ChildNodes.Count do
                let node = project.ProjectNode.ChildNodes.[l].OuterXml.ToString()
                if String.startsWithIgnoreCase  "<import" node &&
                   (String.containsIgnoreCase "microsoft.csharp.targets" node ||
                     String.containsIgnoreCase "microsoft.fsharp.targets" node ||
                     //List of xamarin csharp targets generated with following bash command inside $(MSBuildExtensionsPath)\Xamarin
                     //find . -name "*.CSharp.targets" | sed 's#.*/##'   | tr '[:upper:]' '[:lower:]' | xargs printf '                     String.containsIgnoreCase "%s" node ||\n'
                     String.containsIgnoreCase "xamarin.android.csharp.targets" node ||
                     String.containsIgnoreCase "xamarin.android.csharp.targets" node ||
                     String.containsIgnoreCase "xamarin.ios.appextension.csharp.targets" node ||
                     String.containsIgnoreCase "xamarin.ios.csharp.targets" node ||
                     String.containsIgnoreCase "xamarin.ios.objcbinding.csharp.targets" node ||
                     String.containsIgnoreCase "xamarin.ios.watchapp.csharp.targets" node ||
                     String.containsIgnoreCase "xamarin.monotouch.csharp.targets" node ||
                     String.containsIgnoreCase "xamarin.mac.appextension.csharp.targets" node ||
                     String.containsIgnoreCase "xamarin.mac.csharp.targets" node ||
                     String.containsIgnoreCase "xamarin.mac.objcbinding.csharp.targets" node ||
                     String.containsIgnoreCase "xamarin.tvos.appextension.csharp.targets" node ||
                     String.containsIgnoreCase "xamarin.tvos.csharp.targets" node ||
                     String.containsIgnoreCase "xamarin.tvos.objcbinding.csharp.targets" node ||
                     String.containsIgnoreCase "xamarin.watchos.app.csharp.targets" node ||
                     String.containsIgnoreCase "xamarin.watchos.appextension.csharp.targets" node ||
                     String.containsIgnoreCase "xamarin.watchos.csharp.targets" node ||
                     String.containsIgnoreCase "fsharptargetspath" node)
                then
                    iTarget <- l + 1
                l <- l + 1
            iProp,iTarget

        let usedFrameworkLibs = HashSet<TargetProfile*string>()

        let specialPackagesWithFrameworkConflictLibs =
            [PackageName "System.Net.Http"] // see https://github.com/fsprojects/Paket/issues/2352
            |> Set.ofList

        let filteredModel =
            completeModel
            |> Map.filter (fun kv _ -> usedPackages.ContainsKey kv)

        let contexts =
            filteredModel
            |> Seq.map (fun kv ->
                    deleteCustomModelNodes (snd kv.Value) project
                    let installSettings = snd usedPackages.[kv.Key]
                    let restrictionList =
                        installSettings.FrameworkRestrictions
                        |> getExplicitRestriction

                    let projectModel =
                        (snd kv.Value)
                            .ApplyFrameworkRestrictions(restrictionList)
                            .FilterExcludes(installSettings.Excludes)
                            .RemoveIfCompletelyEmpty()

                    let _, packageName = kv.Key
                    if specialPackagesWithFrameworkConflictLibs.Contains packageName then
                        for t in KnownTargetProfiles.AllProfiles do
                            if (projectModel.GetLibReferenceFiles t)
                               |> Seq.exists (fun t -> t.Name = packageName.ToString() + ".dll")
                            then
                                usedFrameworkLibs.Add(t,packageName.ToString()) |> ignore

                    kv,installSettings,restrictionList,projectModel)
            |> Seq.sortBy (fun (kv,_,_,_) ->
                    let group, packName = kv.Key
                    group.CompareString, packName.CompareString)
            |> Seq.map (fun (kv,installSettings,restrictionList,projectModel) ->
                if directPackages.ContainsKey kv.Key then
                    let targetProfiles = getTargetProfiles project
                    targetProfiles
                    |> Seq.filter (fun targetProfile -> isTargetMatchingRestrictions(restrictionList,targetProfile))
                    |> Seq.filter (projectModel.GetLibReferenceFiles >> Seq.isEmpty)
                    |> Seq.iter (fun targetProfile ->
                            let libReferences =
                                projectModel.GetAllLegacyReferences()

                            if not (Seq.isEmpty libReferences) then
                                traceWarnfn "Package %O contains libraries, but not for the selected TargetFramework %O in project %s."
                                    (snd kv.Key) targetProfile project.FileName)

                let importTargets = defaultArg installSettings.ImportTargets true

                let allFrameworks = applyRestrictionsToTargets restrictionList KnownTargetProfiles.AllProfiles
                generateXml projectModel usedFrameworkLibs installSettings.Aliases installSettings.EmbedInteropTypes installSettings.CopyLocal installSettings.SpecificVersion importTargets installSettings.ReferenceCondition (set allFrameworks) project)

        for ctx in contexts do
            for chooseNode in ctx.ChooseNodes do
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

                // global props are inserted at the top of the file
                for node in ctx.GlobalPropsNodes do
                    project.ProjectNode.PrependChild node |> ignore

                // global targets are just inserted at the end of the file
                for node in ctx.GlobalTargetsNodes do
                    project.ProjectNode.AppendChild node |> ignore

                // framework specific props/targets reference specific msbuild properties, so they need to be inserted later
                let iProp,iTarget = findInsertSpot()

                let addProps() =
                    if iProp = 0 then
                        for node in ctx.FrameworkSpecificPropsNodes do
                            project.ProjectNode.PrependChild node |> ignore
                    else
                        for node in ctx.FrameworkSpecificPropsNodes do
                            project.ProjectNode.InsertAfter(node,project.ProjectNode.ChildNodes.[iProp-1]) |> ignore

                if ctx.FrameworkSpecificPropertyChooseNode.ChildNodes.Count > 0 then
                    if iTarget = 0 then
                        project.ProjectNode.AppendChild ctx.FrameworkSpecificPropertyChooseNode |> ignore

                        for node in ctx.FrameworkSpecificPropsNodes do
                            project.ProjectNode.AppendChild node |> ignore
                    else
                        let node = project.ProjectNode.ChildNodes.[iTarget-1]

                        for n in ctx.FrameworkSpecificPropsNodes do
                            project.ProjectNode.InsertAfter(n,node) |> ignore

                        project.ProjectNode.InsertAfter(ctx.FrameworkSpecificPropertyChooseNode,node) |> ignore
                else
                   addProps()

                for node in ctx.FrameworkSpecificTargetsNodes do
                    project.ProjectNode.AppendChild node |> ignore

                if ctx.AnalyzersNode.ChildNodes.Count > 0 then
                    project.ProjectNode.AppendChild ctx.AnalyzersNode |> ignore

    let save forceTouch project =
        let determineEncoding fileName =
            if File.Exists fileName then
                let utf8WithBom = UTF8Encoding true
                let bom = utf8WithBom.GetPreamble()
                let bomBuffer = Array.zeroCreate bom.Length

                use fs = new FileStream(fileName, FileMode.Open, FileAccess.Read)
                fs.Read(bomBuffer, 0, bom.Length) |> ignore
                if bomBuffer = bom then utf8WithBom else UTF8Encoding false
            else UTF8Encoding false

        if Utils.normalizeXml project.Document <> project.OriginalText || not (File.Exists(project.FileName)) then
            if verbose then
                verbosefn "Project %s changed" project.FileName
            let encoding = determineEncoding project.FileName
            use f = File.Open(project.FileName, FileMode.Create)
            use sw = new StreamWriter(f, encoding)
            project.Document.Save(sw)
        elif forceTouch then
            File.SetLastWriteTimeUtc(project.FileName, DateTime.UtcNow)

    let getPaketFileItems project =
        BuildAction.PaketFileNodeNames
        |> List.collect (fun name -> findPaketNodes name project)
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
        let forceGetName node name =
            match node |> getNode name with
            | Some n -> Some n.InnerText
            | None ->
                match node |> getAttribute "Include" with
                | Some fileName ->
                    let fi = FileInfo(normalizePath fileName)
                    Some (fi.Name.Replace(fi.Extension,""))
                | None -> None

        let forceGetInnerText node name =
            node |> getNode name |> Option.map (fun n -> n.InnerText)

        [for node in project.Document |> getDescendants "ProjectReference" do
            let getNormalizedPath incPath =
                let normalizedPath = incPath |> normalizePath
                if normalizedPath.Contains "$(SolutionDir)" then
                    match getProperty "SolutionDir" project with
                    | Some slnDir -> normalizedPath.Replace("$(SolutionDir)",slnDir)
                    | None -> normalizedPath.Replace("$(SolutionDir)", Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar.ToString())
                else normalizedPath

            let optPath =
                let incPath = getAttribute "Include" node
                Option.map getNormalizedPath incPath

            let referenceOutputAssembly =
                match node |> getNode "ReferenceOutputAssembly" with
                | Some n -> n.InnerText.ToLower() = "true"
                | None -> true

            let makePathNode (path: string) =
                { Path =
                    if Path.IsPathRooted path then Path.GetFullPath path else
                    let di = FileInfo(normalizePath project.FileName).Directory
                    Path.Combine(di.FullName,path) |> Path.GetFullPath

                  RelativePath = path.Replace("/","\\")
                  Name = forceGetName node "Name"
                  ReferenceOutputAssembly = referenceOutputAssembly
                  GUID =
                    match forceGetInnerText node "Project" with
                    | Some projectGuid when String.IsNullOrWhiteSpace projectGuid |> not -> Some (Guid.Parse projectGuid)
                    | _ -> None }

            match optPath with
            | Some path -> yield makePathNode path
            | None -> () ]

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
                                         StringComparison.OrdinalIgnoreCase)
                    | None -> false)
              project.Document
              |> getDescendants "Target"
              |> List.tryFind (withAttributeValue "Name" "EnsureNuGetPackageBuildImports") ]
            |> List.choose id

        for node in toDelete do
            let parent = node.ParentNode
            node.ParentNode.RemoveChild node |> ignore
            if not parent.HasChildNodes then
                parent.ParentNode.RemoveChild parent |> ignore

    let removeNuGetPackageImportStamp project =
        let toDelete =
            project.Document
            |> getDescendants "PropertyGroup"
            |> List.collect (getDescendants "NuGetPackageImportStamp")

        for node in toDelete do
            let parent = node.ParentNode
            node.ParentNode.RemoveChild node |> ignore
            if not parent.HasChildNodes then
                parent.ParentNode.RemoveChild parent |> ignore

    let removeImportAndTargetEntries (packages : list<string * SemVerInfo> ) (project:ProjectFile) =
        let toDelete =
            project.Document
            |> getDescendants "Import"
            |> List.filter (fun node ->
                match node |> getAttribute "Project" with
                | Some p -> packages |> List.exists (fun (id, version) ->
                    p.IndexOf(sprintf "%s.%O" id version, StringComparison.OrdinalIgnoreCase) >= 0)
                | None -> false)

        for node in toDelete do
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
            | _ -> ()

    let packageReferencesNoPrivateAssets project =
        project.ProjectNode
        |> getDescendants "PackageReference"
        |> List.filter (getDescendants "PrivateAssets" >>
                        List.exists (fun x -> x.InnerText = "All") >>
                        not)


    let cliToolsNoPrivateAssets project =
        project.ProjectNode
        |> getDescendants "DotNetCliToolReference"
        |> List.filter (getDescendants "PrivateAssets" >>
                        List.exists (fun x -> x.InnerText = "All") >>
                        not)

    let getPackageReferences project =
        packageReferencesNoPrivateAssets project
        |> List.map (getPackageIdAttribute project)

    let getCliReferences project =
        cliToolsNoPrivateAssets project
        |> List.map (getAttribute "Include" >> Option.get)

    let removePackageReferenceEntries project =
        let toDelete = packageReferencesNoPrivateAssets project

        for node in toDelete do
            node.ParentNode.RemoveChild node |> ignore

        deleteIfEmpty "ItemGroup" project |> ignore

    let removeCliToolReferenceEntries project =
        let toDelete = cliToolsNoPrivateAssets project

        for node in toDelete do
            node.ParentNode.RemoveChild node |> ignore

        deleteIfEmpty "ItemGroup" project |> ignore

    let removeNugetAnalysers (packages : list<string*SemVerInfo>) (project : ProjectFile) : unit =
        let packageIds = packages |> List.map (fun (id,version) -> sprintf "%s.%O" id version)
        let pathContainsString (searchString :string) = packageIds |> List.exists (fun id -> searchString.Contains(id))

        let isAnalyserFromNuget (node : XmlNode) =
                defaultArg
                    (node |> getAttribute "Include" |> Option.map pathContainsString)
                    false

        let toDelete =
                project.Document
                |> getDescendants "Analyzer"
                |>  List.filter isAnalyserFromNuget

        for node in toDelete do
            tracefn "Removing 'Analyzer' entry from %s for project %s"
                (node |> getAttribute "Include" |> Option.get)
                project.FileName

            node.ParentNode.RemoveChild node |> ignore

    let outputType (project:ProjectFile) =
        seq {for outputType in project.Document |> getDescendants "OutputType" ->
                match outputType.InnerText with
                | "Exe"    -> ProjectOutputType.Exe
                | "WinExe" -> ProjectOutputType.Exe
                | _        -> ProjectOutputType.Library }
        |> Seq.tryHead
        |> function None -> ProjectOutputType.Library | Some x -> x

    let buildOutputTargetFolder (project:ProjectFile) =
        project.Document
        |> getDescendants "BuildOutputTargetFolder"
        |> Seq.tryHead
        |> Option.map (fun e -> e.InnerText)

    let appendTargetFrameworkToOutputPath (project:ProjectFile) =
        match project.Document |> getDescendants "AppendTargetFrameworkToOutputPath" |> Seq.tryHead with
        | Some e -> not (String.Equals(e.InnerText, "false", StringComparison.OrdinalIgnoreCase))
        | None -> true

    let addImportForPaketTargets relativeTargetsPath (project:ProjectFile) =
        match project.Document
              |> getDescendants "Import"
              |> List.tryFind (withAttributeValue "Project" relativeTargetsPath) with
        | Some _ -> ()
        | None ->
            let node = createNode "Import" project |> addAttribute "Project" relativeTargetsPath
            project.ProjectNode.AppendChild node |> ignore

    let removeImportForPaketTargets (project:ProjectFile) =
        project.Document
        |> getDescendants "Import"
        |> List.tryFind (fun n -> withAttributeValueEndsWith "Project" Constants.TargetsFileName n || withAttributeValueEndsWith "Project" "Paket.Restore.targets" n)
        |> Option.iter (fun n -> n.ParentNode.RemoveChild n |> ignore)

    let determineBuildAction (fileName : string) (project:ProjectFile) =
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

    let determineBuildActionForRemoteItems (fileName : string) (project:ProjectFile) =
        if Path.GetExtension fileName = ".dll"
        then BuildAction.Reference
        else determineBuildAction fileName project

    let getAssemblyName (project:ProjectFile) =
        let assemblyName =
            project.Document
            |> getDescendants "AssemblyName"
            |> function
               | [] ->
                  match getToolsVersion project with
                  | 15.0 -> ""
                  | _ -> failwithf "Project %s has no AssemblyName set" project.FileName
               | [assemblyName] -> assemblyName.InnerText
               | assemblyName::_ ->
                    traceWarnfn "Found multiple AssemblyName nodes in file %s, using first" project.FileName
                    assemblyName.InnerText
            |> fun assemblyName ->
                if String.IsNullOrWhiteSpace assemblyName then
                    let fi = FileInfo project.FileName
                    fi.Name.Replace(fi.Extension,"")
                else assemblyName

        let ending = outputType project |> function ProjectOutputType.Library -> "dll" | ProjectOutputType.Exe -> "exe"
        sprintf "%s.%s" assemblyName ending

    let getOutputDirectory buildConfiguration buildPlatform (targetProfile : TargetProfile option) (project:ProjectFile) =
        let targetFramework =
            match targetProfile with
            | Some targetProfile ->
                let targetProfile = targetProfile.ToString()
                match getTargetFramework project with
                | Some x -> if x = targetProfile then x else ""
                | None ->
                    let parsedTargetFrameworks = getTargetFrameworksParsed project
                    match List.tryFind ((=) targetProfile) parsedTargetFrameworks with
                    | Some x -> x
                    | None -> ""
            | None ->
                match getTargetFramework project with
                | Some x -> x
                | None ->
                    match getTargetFrameworksParsed project with
                    | fwk :: _ -> fwk
                    | [] -> ""

        let platforms =
            if not (String.IsNullOrWhiteSpace buildPlatform) then
                [buildPlatform]
            else
                [ "AnyCPU"
                  "AnyCPU32BitPreferred"
                  "x86"
                  "x64"
                  "Win32"
                  "ARM"
                  "Itanium" ]

        let rec tryNextPlat platforms attempted =
            match platforms with
            | [] ->
                if not (appendTargetFrameworkToOutputPath project) then
                    Path.Combine("bin", buildConfiguration)
                elif not (String.IsNullOrEmpty targetFramework) then
                    Path.Combine("bin", buildConfiguration, targetFramework)
                elif String.IsNullOrWhiteSpace buildPlatform then
                    failwithf "Unable to find %s output path node in file %s for any known platforms" buildConfiguration project.FileName
                else
                    failwithf "Unable to find %s output path node in file %s targeting the %s platform" buildConfiguration project.FileName buildPlatform
            | x::xs ->
                let startingData = Map.ofList [("Configuration", buildConfiguration); ("TargetFramework", targetFramework);("Platform", x)]
                [getPropertyWithDefaults "OutputPath" startingData project; getPropertyWithDefaults "OutDir" startingData project]
                |> List.choose id
                |> function
                    | [] -> tryNextPlat xs (x::attempted)
                    | s :: _ ->
                        if String.IsNullOrWhiteSpace buildPlatform && attempted <> [] then
                            let tested = String.Join(", ", attempted)
                            traceWarnfn "No platform specified; found output path node for the %s platform after failing to find one for the following: %s" x tested
                        if (appendTargetFrameworkToOutputPath project) then
                            Path.Combine(s.TrimEnd [|'\\'|] , targetFramework) |> normalizePath
                        else
                            s.TrimEnd [|'\\'|] |> normalizePath

        tryNextPlat platforms []

    let versionRequirement node =
        let v =
            match node |> getAttribute "Version" with
            | Some version -> version
            | None ->
                match node |> getNode "Version" with
                | Some n -> n.InnerText
                | None -> "*"

        if v.Contains "[" || v.Contains "(" then
            VersionRequirement.Parse v
        else
            let prerelease = if v.Contains "-" then PreReleaseStatus.All else PreReleaseStatus.No
            if v.Contains "*" then
                VersionRequirement.VersionRequirement(VersionRange.AtLeast (v.Replace("*","0")),prerelease)
            else
                VersionRequirement.VersionRequirement(VersionRange.Exactly v,prerelease)

    let dotNetCorePackages (projectFile: ProjectFile) =
        packageReferencesNoPrivateAssets projectFile
        |> List.map (fun node ->
            { NugetPackage.Id = getPackageIdAttribute projectFile node
              VersionRequirement = versionRequirement node
              Kind = NugetPackageKind.Package
              TargetFramework = None })

    let cliTools (projectFile: ProjectFile) =
        cliToolsNoPrivateAssets projectFile
        |> List.map (fun node ->
            { NugetPackage.Id = node |> getAttribute "Include" |> Option.get
              VersionRequirement = versionRequirement node
              Kind = NugetPackageKind.DotnetCliTool
              TargetFramework = None })

    let setOrCreateAutoGenerateBindingRedirects (project:ProjectFile) =
        let nodes = project.Document |> getDescendants "PropertyGroup"
        let mutable found = false
        for node in nodes do
            for autoNode in node |> getDescendants "AutoGenerateBindingRedirects" do
                found <- true
                autoNode.InnerText <- "true"

        if not found then
            match nodes with
            | x :: _ -> 
                x.AppendChild (createNodeSet "AutoGenerateBindingRedirects" "true" project) |> ignore
            | _ -> ()

        save false project

type PackProcessCache =
    { ReferencesByProjectFilePath : ConcurrentDictionary<string, ProjectFile list>
      ProjectFileByPath : ConcurrentDictionary<string, ProjectFile option> }

module PackProcessCache =
    let empty = { ReferencesByProjectFilePath = ConcurrentDictionary<string, ProjectFile list>()
                  ProjectFileByPath = ConcurrentDictionary<string, ProjectFile option>() }

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

    member this.RemoveNugetAnalysers(packages : list<string*SemVerInfo>) = ProjectFile.removeNugetAnalysers packages this

    member this.GetCustomModelNodes(model:InstallModel) = ProjectFile.getCustomModelNodes model this

    member this.DeleteCustomModelNodes(model:InstallModel) = ProjectFile.deleteCustomModelNodes model this

    member this.GenerateXml(model, usedFrameworkLibs:HashSet<TargetProfile*string>, aliases, embedInteropTypes, copyLocal, specificVersion, importTargets, allTargetProfiles:#seq<TargetProfile>, referenceCondition) = ProjectFile.generateXml model usedFrameworkLibs aliases embedInteropTypes copyLocal specificVersion importTargets referenceCondition (set allTargetProfiles) this

    member this.RemovePaketNodes () = ProjectFile.removePaketNodes this

    member this.UpdateReferences (completeModel, directDependencies, usedPackages) = ProjectFile.updateReferences completeModel directDependencies usedPackages this

    member this.Save(forceTouch) = ProjectFile.save forceTouch this

    member this.GetPaketFileItems () = ProjectFile.getPaketFileItems this

    member this.GetProjectGuid () = ProjectFile.getProjectGuid this

    member this.GetInterProjectDependencies () =  ProjectFile.getInterProjectDependencies this

    member this.ReplaceNuGetPackagesFile () = ProjectFile.replaceNuGetPackagesFile this

    member this.RemoveNuGetTargetsEntries () =  ProjectFile.removeNuGetTargetsEntries this

    member this.RemoveNuGetPackageImportStamp () =  ProjectFile.removeNuGetPackageImportStamp this

    member this.RemoveImportAndTargetEntries (packages : list<string * SemVerInfo> ) =  ProjectFile.removeImportAndTargetEntries packages this

    member this.GetPackageReferences () = ProjectFile.getPackageReferences this

    member this.GetCliToolReferences () = ProjectFile.getCliReferences this

    member this.RemovePackageReferenceEntries () = ProjectFile.removePackageReferenceEntries this

    member this.RemoveCliToolReferenceEntries () = ProjectFile.removeCliToolReferenceEntries this

    member this.OutputType =  ProjectFile.outputType this

    member this.BuildOutputTargetFolder =  ProjectFile.buildOutputTargetFolder this

    member this.AppendTargetFrameworkToOutputPath =  ProjectFile.appendTargetFrameworkToOutputPath this

    member this.GetTargetFrameworkIdentifier () =  ProjectFile.getTargetFrameworkIdentifier this

    member this.GetTargetFrameworkProfile () = ProjectFile.getTargetFrameworkProfile this

    member this.GetTargetProfiles() =  ProjectFile.getTargetProfiles this

    member this.AddImportForPaketTargets relativeTargetsPath = ProjectFile.addImportForPaketTargets relativeTargetsPath this

    member this.RemoveImportForPaketTargets() =  ProjectFile.removeImportForPaketTargets this

    member this.DetermineBuildAction fileName = ProjectFile.determineBuildAction fileName this

    member this.DetermineBuildActionForRemoteItems fileName = ProjectFile.determineBuildActionForRemoteItems fileName this

    member this.GetOutputDirectory buildConfiguration buildPlatform targetFramework =  ProjectFile.getOutputDirectory buildConfiguration buildPlatform targetFramework this

    member this.GetAssemblyName () = ProjectFile.getAssemblyName this

    member this.SetOrCreateAutoGenerateBindingRedirects() = ProjectFile.setOrCreateAutoGenerateBindingRedirects this

    static member LoadFromStream(fullName:string, stream:Stream) = ProjectFile.loadFromStream fullName stream

    static member LoadFromFile(fileName:string) =  ProjectFile.loadFromFile fileName

    static member LoadFromString(fullName:string, text:string) = ProjectFile.loadFromString fullName text

    static member TryLoad(fileName:string) = ProjectFile.tryLoad fileName

    static member FindCorrespondingFile (projectFile:FileInfo,correspondingFile:string) =
        let specificFile = FileInfo (Path.Combine(projectFile.Directory.FullName, projectFile.Name + "." + correspondingFile))
        if specificFile.Exists then Some specificFile.FullName else

        let rec findInDir (currentDir:DirectoryInfo) =
            let generalFile = FileInfo(Path.Combine(currentDir.FullName, correspondingFile))
            if generalFile.Exists then Some generalFile.FullName
            elif (FileInfo (Path.Combine(currentDir.FullName, Constants.DependenciesFileName))).Exists then None
            elif isNull currentDir.Parent then None
            else findInDir currentDir.Parent

        findInDir projectFile.Directory

    member this.FindCorrespondingFile (correspondingFile:string) = ProjectFile.FindCorrespondingFile(FileInfo this.FileName,correspondingFile)

    member this.FindReferencesFile() = this.FindCorrespondingFile Constants.ReferencesFile

    member this.FindLocalizedLanguageNames() =
        let tryGetAttributeValue name node =
            if hasAttribute name node then
                Some node.Attributes.[name].Value
            else
                None

        let tryGetLanguage value =
            let pattern = @"\.(?<language>\w+(-\w+)?)\.resx$"
            let m = Regex.Match(value, pattern, RegexOptions.ExplicitCapture)
            if m.Success then
                let value = m.Groups.["language"].Value
                if Cultures.isLanguageName value then
                    Some value
                else
                    None
            else
                None

        this.ProjectNode
        |> getDescendants "EmbeddedResource"
        |> List.choose (tryGetAttributeValue "Include")
        |> List.choose tryGetLanguage
        |> List.distinct
        |> List.sort

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

    member this.GetToolsVersion () = ProjectFile.getToolsVersion this

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

    // searches directory and all subdirectories for the search pattern, skipping known problematic directories
    static member SearchAllProjectRelatedFiles searchPattern folder : FileInfo [] =
        let paketPath = Path.Combine(folder,Constants.PaketFilesFolderName) |> normalizePath

        let findAllFiles folder =
            let rec search topLevel (di:DirectoryInfo) =
                try
                    if verbose then
                        verbosefn "Searching %s in %s" searchPattern di.FullName

                    let files = di.GetFiles(searchPattern, SearchOption.TopDirectoryOnly)
                    di.GetDirectories()
                    |> Array.filter (fun di ->
                        try
                            let path = normalizePath di.FullName
                            if di.Name = Constants.DefaultPackagesFolderName then false else
                            if di.Name = ".git" then false else
                            if di.Name = ".fable" then false else
                            if di.Name = "node_modules" then false else
                            if topLevel && isLinux && di.FullName = "/sys" then false else
                            if topLevel && isLinux && di.FullName = "/usr" then false else
                            if topLevel && isLinux && di.FullName = "/proc" then false else
                            if topLevel && isLinux && di.FullName = "/lib" then false else
                            if topLevel && isLinux && di.FullName = "/tmp" then false else
                            if topLevel && isLinux && di.FullName = "/home" then false else
                            if topLevel && isLinux && di.FullName = "/media" then false else
                            if topLevel && isLinux && di.FullName = "/root" then false else
                            if topLevel && isLinux && di.FullName = "/var" then false else
                            if topLevel && isLinux && di.FullName = "/sbin" then false else
                            if topLevel && isLinux && di.FullName = "/etc" then false else
                            if path = paketPath then false else
                            Path.Combine(path, Constants.DependenciesFileName)
                            |> File.Exists
                            |> not
                        with
                        | _ -> false)
                    |> Array.collect (search false)
                    |> Array.append files
                with
                | _ -> Array.empty


            search true (DirectoryInfo folder)

        findAllFiles folder

    /// Finds all project files
    static member FindAllProjectFiles folder : FileInfo [] =
        ProjectFile.SearchAllProjectRelatedFiles "*proj*" folder
        |> Array.filter ProjectFile.isSupportedFile

    /// Finds all project files
    static member FindAllProjects folder : ProjectFile [] =
        ProjectFile.FindAllProjectFiles folder
        |> Array.choose (fun f -> ProjectFile.tryLoad f.FullName)

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

    member this.GetAllInterProjectDependenciesWithoutProjectTemplates cache =
        this.ProjectsWithoutTemplates(this.GetAllReferencedProjects(false, cache))

    member this.GetAllInterProjectDependenciesWithProjectTemplates cache =
        this.ProjectsWithTemplates(this.GetAllReferencedProjects(false, cache))

    member this.HasTemplateFile =
        let templateFilename = this.FindTemplatesFile()
        match templateFilename with
        | Some tfn -> TemplateFile.IsProjectType tfn
        | None -> false

    member this.ProjectsWithoutTemplates projects =
        projects
        |> List.filter (fun proj -> proj = this || not proj.HasTemplateFile)

    member this.ProjectsWithTemplates projects =
        projects
        |> List.filter (fun proj -> proj = this || proj.HasTemplateFile)

    member this.GetAllReferencedProjects (onlyWithOutput, cache) =

        let getDirectlyReferencedProjectFiles (project:ProjectFile) =
            let referencedProjectFiles = lazy (
                project.GetInterProjectDependencies()
                |> List.filter (fun dep -> dep.ReferenceOutputAssembly || (not onlyWithOutput))
                |> List.choose (fun dep -> cache.ProjectFileByPath.GetOrAdd(dep.Path, ProjectFile.tryLoad)))
            cache.ReferencesByProjectFilePath.GetOrAdd (project.FileName, (fun _ -> referencedProjectFiles.Force()))

        let seenProjects = new HashSet<string>()
        let rec getSelfAndAllReferencedProjects project = [
            if seenProjects.Add project.FileName then
                yield project
                if project = this || not project.HasTemplateFile then
                    let referencedProjects = getDirectlyReferencedProjectFiles project
                    yield! List.collect getSelfAndAllReferencedProjects referencedProjects
        ]

        getSelfAndAllReferencedProjects this

    member this.GetProjects includeReferencedProjects cache =
        if includeReferencedProjects then
            this.GetAllReferencedProjects(true, cache)
        else
            [this]

    member this.GetCompileItems (includeReferencedProjects : bool) cache =
        let getCompileRefs projectFile =
            projectFile.Document
            |> getDescendants "Compile"
            |> Seq.map (fun compileNode -> projectFile, compileNode)

        let getCompileItem (projectFile, compileNode) =
            let projectFolder = projectFile.FileName |> Path.GetFullPath |> Path.GetDirectoryName
            let sourceFile =
                let file =
                    match compileNode |> getAttribute "Include" with
                    | Some file -> file
                    | None ->
                        match compileNode |> getAttribute "Update" with
                        | Some file -> file
                        | None -> failwithf "The Compile entry is in unknown format and doesn't contain a Update or Include attribute."

                file
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

        this.GetProjects includeReferencedProjects cache
        |> this.ProjectsWithoutTemplates
        |> Seq.collect getCompileRefs
        |> Seq.map getCompileItem
        |> Seq.collect getRealItems


    member self.GetTemplateMetadata () =
        let prop name = self.GetProperty name

        let propOr name value =
            defaultArg (self.GetProperty name) value

        let propMap name value fn =
            defaultArg (self.GetProperty name|>Option.map fn) value

        let tryBool (s: string) = Boolean.TryParse s |> function true, value -> value | _ -> false

        let splitString = String.split[|';'|] >> Array.map (fun x -> x.Trim()) >> List.ofArray

        let coreInfo : ProjectCoreInfo = {
            Id = prop "id"
            Version = propMap "version" (Some(SemVer.Parse "0.0.1")) (SemVer.Parse>>Some)
            Authors = propMap "Authors" None (splitString >> Some)
            Description = prop "Description"
            Symbols = propMap "Symbols" false tryBool
        }
        let optionalInfo =  {
            Title = prop "Title"
            Owners = propMap "Owners" [] splitString
            ReleaseNotes = prop "ReleaseNotes"
            Summary = prop "Summary"
            Readme = prop "Readme"
            Language = prop "Langauge"
            ProjectUrl = prop "ProjectUrl"
            IconUrl = prop "IconUrl"
            LicenseExpression = prop "LicenseExpression"
            LicenseUrl = prop "LicenseUrl"
            Copyright = prop "Copyright"
            RepositoryType = prop "RepositoryType"
            RepositoryUrl = prop "RepositoryUrl"
            RepositoryBranch = prop "RepositoryBranch"
            RepositoryCommit = prop "RepositoryCommit"
            RequireLicenseAcceptance = propMap "RequireLicenseAcceptance" false tryBool
            Tags = propMap "Tags" [] splitString
            DevelopmentDependency = propMap "DevelopmentDependency" false tryBool
            DependencyGroups = []
            ExcludedDependencies = Set.empty //propOr "ExcludedDependencies"
            ExcludedGroups = Set.empty // propOr "ExcludedGroups" Set.empty
            References = [] //propOr "References" []
            FrameworkAssemblyReferences = [] //propOr "FrameworkAssemblyReferences" []
            Files = [] //propMap "Files" [] splitString
            FilesExcluded = [] //propMap  "FilesExcluded" [] splitString
            PackageTypes = []
            IncludePdbs = propMap "IncludePdbs" true tryBool
            IncludeReferencedProjects = propMap "IncludeReferencedProjects" true tryBool
            InterprojectReferencesConstraint = propMap "InterprojectReferencesConstraint" None InterprojectReferencesConstraint.Parse
        }

        (coreInfo, optionalInfo)

    member self.TryFindConfigFile () =
        let isAppOrWebConfig (fileInfo:FileInfo) =
            let baseName = fileInfo.Name.ToLowerInvariant()
            baseName = "app.config" || baseName = "web.config"

        ProjectFile.SearchAllProjectRelatedFiles "*.config" (FileInfo self.FileName).DirectoryName
        |> Array.tryFind isAppOrWebConfig
