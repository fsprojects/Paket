namespace Paket

open Paket.Logging
open Paket.ModuleResolver
open Paket.PackageResolver
open System
open System.IO
open System.Xml
open System.Collections.Generic
open Paket.Xml
open Nuspec

/// Contains methods to read and manipulate project file nodes.
type private InstallInfo = {
    DllName : string
    Path : string
    Condition : FrameworkIdentifier
    Package : ResolvedPackage
}

module private InstallRules = 
    let groupDLLs (usedPackages : Dictionary<string,bool>) extracted projectPath = 
        let used = HashSet<_>()      
        for x in usedPackages do
            used.Add(x.Key.ToLower()) |> ignore

        [ for (package:ResolvedPackage), libraries in extracted do              
              if used.Contains(package.Name.ToLower()) |> not then verbosefn "    - %s is not used ==> ignored" package.Name else
                let libraries = Seq.toArray libraries
                for (lib : FileInfo) in libraries do
                    match FrameworkIdentifier.DetectFromPath lib.FullName with
                    | None -> verbosefn "    - could not understand %s ==> ignored" lib.FullName
                    | Some condition ->
                        let dllName = lib.Name.Replace(lib.Extension, "")
                        verbosefn "    - adding new condition %A fo %s" condition dllName
                        yield { DllName = dllName
                                Path = createRelativePath projectPath lib.FullName
                                Package = package
                                Condition = condition } ]
        |> Seq.groupBy (fun info -> info.Package.Name, info.DllName, info.Condition.GetFrameworkIdentifier())
        |> Seq.groupBy (fun ((packageName,name, _), _) -> packageName,name)
        |> Seq.groupBy (fun ((packageName,_),_) -> packageName)

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
    static member DefaultNameSpace = "http://schemas.microsoft.com/developer/msbuild/2003"

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

    member this.HasCustomNodes(dllName) =
        let hasCustom = ref false
        for node in this.Document.SelectNodes("//ns:Reference", this.Namespaces) do
            if node.Attributes.["Include"].InnerText.Split(',').[0] = dllName then
                let isPaket = ref false
                for child in node.ChildNodes do
                    if child.Name = "Paket" then 
                        isPaket := true
                if not !isPaket then
                    hasCustom := true
            
        !hasCustom

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

    member this.DeleteCustomNodes(dllName) =        
        let nodesToDelete = List<_>()
        for node in this.Document.SelectNodes("//ns:Reference", this.Namespaces) do
            if node.Attributes.["Include"].InnerText.Split(',').[0] = dllName then            
                nodesToDelete.Add node

        if nodesToDelete |> Seq.isEmpty |> not then
            verbosefn "    - Deleting custom projects nodes for %s" dllName

        for node in nodesToDelete do            
            node.ParentNode.RemoveChild(node) |> ignore

    member this.CreateNode(name) = this.Document.CreateElement(name, ProjectFile.DefaultNameSpace)

    member this.CreateNode(name,text) = 
        let node = this.CreateNode(name)
        node.InnerText <- text
        node

    member private this.CreateWhenNode(lib:InstallInfo,condition)=
        let whenNode = 
            this.CreateNode "When"
            |> addAttribute "Condition" condition
                        
        let reference = 
            this.CreateNode "Reference"
            |> addAttribute "Include" lib.DllName
            |> addChild (this.CreateNode("HintPath",lib.Path))
            |> addChild (this.CreateNode("Private","True"))
            |> addChild (this.CreateNode("Paket","True"))

        let itemGroup = this.CreateNode "ItemGroup"
        itemGroup.AppendChild(reference) |> ignore
        whenNode.AppendChild(itemGroup) |> ignore
        whenNode

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


    member this.UpdateReferences(extracted, usedPackages : Dictionary<string,bool>, hard) = 
        this.DeletePaketNodes("Reference")
        let installInfos = InstallRules.groupDLLs usedPackages extracted this.FileName        
        for packageName, installInfos in installInfos do
            let nuspec = FileInfo(sprintf "./packages/%s/%s.nuspec" packageName packageName)
            let references = Nuspec.GetReferences nuspec.FullName
            for (_,dllName), libsWithSameName in installInfos do
                if hard then
                    this.DeleteCustomNodes(dllName)
            
                if this.HasCustomNodes(dllName) then verbosefn "  - custom nodes for %s ==> skipping" dllName
                else
                    let install = 
                        match references with
                        | References.All -> true
                        | References.Explicit references -> references |> List.exists (fun x -> x = dllName + ".dll" || x = dllName + ".exe")

                    if not install then verbosefn "  - %s not listed in %s ==> excluded" dllName nuspec.Name else
                    verbosefn "  - installing %s" dllName
                    let lastLib = ref None
                    for (_), libs in libsWithSameName do                            
                        let chooseNode = this.Document.CreateElement("Choose", ProjectFile.DefaultNameSpace)
                    
                        let libsWithSameFrameworkVersion = 
                            libs
                            |> List.ofSeq
                            |> List.sortBy (fun lib -> lib.Path)
                        for lib in libsWithSameFrameworkVersion do
                            verbosefn "     - %A" lib.Condition
                            chooseNode.AppendChild(this.CreateWhenNode(lib, lib.Condition.GetCondition())) |> ignore
                            lastLib := Some lib
                        match !lastLib with
                        | None -> ()
                        | Some lib -> 
                            chooseNode.AppendChild(this.CreateWhenNode(lib, lib.Condition.GetFrameworkIdentifier())) 
                            |> ignore
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
            manager.AddNamespace("ns", ProjectFile.DefaultNameSpace)
            let projectNode = doc.SelectNodes("//ns:Project", manager).[0]
            Some { FileName = fi.FullName; Document = doc; ProjectNode = projectNode; Namespaces = manager; OriginalText = Utils.normalizeXml doc }
        with
        | exn -> 
            traceWarnfn "Unable to parse %s:%s      %s" fileName Environment.NewLine exn.Message
            None