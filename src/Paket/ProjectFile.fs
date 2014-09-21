namespace Paket

open Paket.Logging
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
        [ for (package:ResolvedPackage), libraries in extracted do
              if usedPackages.ContainsKey(package.Name) then 
                  let libraries = libraries |> Seq.toArray
                  for (lib : FileInfo) in libraries do
                      match FrameworkIdentifier.DetectFromPath lib.FullName with
                      | None -> ()
                      | Some condition ->
                          yield { DllName = lib.Name.Replace(lib.Extension, "")
                                  Path = createRelativePath projectPath lib.FullName
                                  Package = package
                                  Condition = condition } ]
        |> Seq.groupBy (fun info -> info.Package.Name, info.DllName, info.Condition.GetFrameworkIdentifier())
        |> Seq.groupBy (fun ((packageName,name, _), _) -> packageName,name)
        |> Seq.groupBy (fun ((packageName,_),_) -> packageName)


/// Contains methods to read and manipulate project files.
type ProjectFile = 
    { FileName: string
      OriginalText : string
      Document : XmlDocument
      Namespaces : XmlNamespaceManager }
    static member DefaultNameSpace = "http://schemas.microsoft.com/developer/msbuild/2003"

    /// Finds all project files
    static member FindAllProjects(folder) = 
        ["*.csproj";"*.fsproj";"*.vbproj"]
        |> List.map (fun projectType -> FindAllFiles(folder, projectType) |> Seq.toList)
        |> List.concat

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
        for node in this.FindPaketNodes(name) do
            node.ParentNode.RemoveChild(node) |> ignore

    member this.DeletePaketCompileNodes() =    
        let nodesToDelete = List<_>()
        for node in this.Document.SelectNodes("//ns:Compile", this.Namespaces) do            
            let remove = ref false
            for child in node.ChildNodes do
                if child.Name = "Paket" then remove := true
            
            if !remove then
                nodesToDelete.Add node

        for node in nodesToDelete do
            node.ParentNode.RemoveChild(node) |> ignore

    member this.DeleteCustomNodes(dllName) =    
        let nodesToDelete = List<_>()
        for node in this.Document.SelectNodes("//ns:Reference", this.Namespaces) do
            if node.Attributes.["Include"].InnerText.Split(',').[0] = dllName then            
                nodesToDelete.Add node

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
        this.DeleteIfEmpty("//ns:ItemGroup")
        this.DeleteIfEmpty("//ns:Project/ns:Choose/ns:When")
        this.DeleteIfEmpty("//ns:Project/ns:Choose")

    member this.UpdateSourceFiles(sourceFiles:SourceFile list) =
        match [ for node in this.Document.SelectNodes("//ns:Project", this.Namespaces) -> node ] with
        | [] -> ()
        | _ -> 
            this.DeletePaketCompileNodes()
            // If there is no item group for compiled items, create one.
            let compileItemGroup =
                match this.Document.SelectNodes("//ns:Project/ns:ItemGroup/ns:Compile", this.Namespaces) with
                | items when items.Count = 0 ->
                    let itemGroup = this.CreateNode("ItemGroup")
                    let project = this.Document.SelectNodes("//ns:Project", this.Namespaces).[0]
                    project.AppendChild(itemGroup)
                | compileItems -> compileItems.[0].ParentNode            
        
            // Insert all source files to the top of the list, but keep alphabetical order
            for sourceFile in sourceFiles |> List.sortBy (fun x -> x.Name) |> List.rev do
                let path = Uri(this.FileName).MakeRelativeUri(Uri(sourceFile.FilePath)).ToString().Replace("/", "\\")
                let node =
                    let node = this.CreateNode("Compile")
                    node.SetAttribute("Include", path)
                    node
                    |> addChild (this.CreateNode("Paket","True"))
                    |> addChild (this.CreateNode("Link","paket-files/" + sourceFile.Name))

                compileItemGroup.PrependChild(node) |> ignore                

    member this.UpdateReferences(extracted, usedPackages : Dictionary<string,bool>, hard) = 
        match [ for node in this.Document.SelectNodes("//ns:Project", this.Namespaces) -> node ] with
        | [] -> verbosefn "%s is not a project file ==> skipping" this.FileName
        | projectNode :: _ -> 
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
                            | References.Explicit references -> references |> List.exists ((=) (dllName + ".dll"))

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
                                chooseNode.AppendChild(this.CreateWhenNode(lib, lib.Condition.GetCondition())) |> ignore
                                lastLib := Some lib
                            match !lastLib with
                            | None -> ()
                            | Some lib -> 
                                chooseNode.AppendChild(this.CreateWhenNode(lib, lib.Condition.GetFrameworkIdentifier())) 
                                |> ignore
                            projectNode.AppendChild(chooseNode) |> ignore
            this.DeleteEmptyReferences()

    member this.Save() =
            if Utils.normalizeXml this.Document <> this.OriginalText then this.Document.Save(this.FileName)

    member this.GetContentFiles() =
        this.FindPaketNodes("Content")
        |> List.map (fun n ->  FileInfo(Path.Combine(Path.GetDirectoryName(this.FileName), n.Attributes.["Include"].Value)))

    member this.UpdateContentFiles(contentFiles : list<FileInfo>) =
        let contentNode (fi: FileInfo) = 
            this.CreateNode "Content" 
            |> addAttribute "Include" (createRelativePath this.FileName fi.FullName)
            |> addChild (this.CreateNode("Paket","True"))
            :> XmlNode
        
        match [ for node in this.Document.SelectNodes("//ns:Project", this.Namespaces) -> node ] with
        | [] -> ()
        | projectNode :: _ -> 
            this.DeletePaketNodes("Content")
            let itemGroupNode = this.Document.CreateElement("ItemGroup", ProjectFile.DefaultNameSpace)

            let firstNodeForDirs =
                this.Document.SelectNodes("//ns:Content", this.Namespaces)
                |> Seq.cast<XmlNode>
                |> Seq.groupBy (fun node -> Path.GetDirectoryName(node.Attributes.["Include"].Value))
                |> Seq.map (fun (key, nodes) -> (key, nodes |> Seq.head))
                |> Map.ofSeq
            
            contentFiles
            |> List.map (fun file -> (createRelativePath this.FileName file.DirectoryName), contentNode file)
            |> List.iter (fun (dir, paketNode) ->
                    match Map.tryFind dir firstNodeForDirs with
                    | Some (firstNodeForDir) -> 
                        match (this.Document.SelectNodes(sprintf "//ns:*[@Include='%s']" paketNode.Attributes.["Include"].Value, this.Namespaces) 
                                                |> Seq.cast<XmlNode> |> Seq.firstOrDefault) with
                        | Some (existingNode) -> 
                            if not <| (existingNode.ChildNodes |> Seq.cast<XmlNode> |> Seq.exists (fun n -> n.Name = "Paket"))
                            then existingNode :?> XmlElement |> addChild (this.CreateNode("Paket", "True")) |> ignore
                        | None -> firstNodeForDir.ParentNode.InsertBefore(paketNode, firstNodeForDir) |> ignore

                    | None -> itemGroupNode.AppendChild(paketNode) |> ignore )
            
            projectNode.AppendChild(itemGroupNode) |> ignore
            this.DeleteIfEmpty("//ns:Project/ns:ItemGroup")

    member this.ReplaceNugetPackagesFile() =
        let nugetNode = this.Document.SelectSingleNode("//ns:*[@Include='packages.config']", this.Namespaces)
        if nugetNode = null then () else
        match [for node in this.Document.SelectNodes("//ns:*[@Include='paket.references']", this.Namespaces) -> node] with 
        | [_] -> nugetNode.ParentNode.RemoveChild(nugetNode) |> ignore
        | [] -> nugetNode.Attributes.["Include"].Value <- "paket.references"
        | _::_ -> failwithf "multiple paket.references nodes in project file %s" this.FileName

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

    static member Load(fileName:string) =
        try
            let fi = FileInfo(fileName)
            let doc = new XmlDocument()
            doc.Load fi.FullName

            let manager = new XmlNamespaceManager(doc.NameTable)
            manager.AddNamespace("ns", ProjectFile.DefaultNameSpace)
            { FileName = fi.FullName; Document = doc; Namespaces = manager; OriginalText = Utils.normalizeXml doc }
        with
        | exn -> failwithf "Error while parsing %s:%s      %s" fileName Environment.NewLine exn.Message
