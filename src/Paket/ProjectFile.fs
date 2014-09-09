namespace Paket

open System
open System.IO
open System.Xml
open System.Collections.Generic

/// The Framework version.
type FrameworkVersion =
| All
| Framework of string

/// The Framework profile.
type FrameworkProfile =
| Client
| Full

/// Framework Identifier type.
type FrameworkIdentifier =
| DotNetFramework of FrameworkVersion * FrameworkProfile
| WindowsPhoneApp of string
| Silverlight of string
    member x.GetGroup() =
        match x with
        | DotNetFramework _ -> ".NET"  
        | WindowsPhoneApp _ -> "WindowsPhoneApp"
        | Silverlight _ -> "Silverlight"


/// Contains methods to analyze .NET Framework Conditions.
type FramworkCondition = 
    { Framework : FrameworkIdentifier;
      CLRVersion : string option; }
    static member DetectFromPath(path : string) : FramworkCondition list = 

        let rec mapPath acc parts =
            match parts with
            | [] -> acc
            | path::rest ->
                match path with
                | "net" -> mapPath ({ Framework = DotNetFramework(All,Full); CLRVersion = None } :: acc) rest
                | "1.0" -> mapPath ({ Framework = DotNetFramework(All,Full); CLRVersion = Some "1.0" } :: acc) rest
                | "1.1" -> mapPath ({ Framework = DotNetFramework(All,Full); CLRVersion = Some "1.1" } :: acc) rest
                | "2.0" -> mapPath ({ Framework = DotNetFramework(All,Full); CLRVersion = Some "2.0" } :: acc) rest
                | "net20" -> mapPath ({ Framework = DotNetFramework(Framework "v2.0",Full); CLRVersion = None } :: acc) rest
                | "net35" -> mapPath ({ Framework = DotNetFramework(Framework "v3.5",Full); CLRVersion = None } :: acc) rest
                | "net4" -> mapPath ({ Framework = DotNetFramework(Framework "v4.0",Full); CLRVersion = None } :: acc) rest
                | "net40" -> mapPath ({ Framework = DotNetFramework(Framework "v4.0",Full); CLRVersion = None } :: acc) rest                
                | "net40-full" -> mapPath ({ Framework = DotNetFramework(Framework "v4.0",Full); CLRVersion = None } :: acc) rest
                | "net40-client" -> mapPath ({ Framework = DotNetFramework(Framework "v4.0",Client); CLRVersion = None } :: acc) rest
                | "portable-net4" -> mapPath ({ Framework = DotNetFramework(Framework "v4.0",Full); CLRVersion = None } :: acc) rest
                | "net45" -> mapPath ({ Framework = DotNetFramework(Framework "v4.5",Full); CLRVersion = None } :: acc) rest
                | "net45-full" -> mapPath ({ Framework = DotNetFramework(Framework "v4.5",Full); CLRVersion = None } :: acc) rest
                | "net451" -> mapPath ({ Framework = DotNetFramework(Framework "v4.5.1",Full); CLRVersion = None } :: acc) rest
                | "sl3" -> mapPath ({ Framework = Silverlight("v3.0"); CLRVersion = None; } :: acc) rest
                | "sl4" -> mapPath ({ Framework = Silverlight("v4.0"); CLRVersion = None; } :: acc) rest
                | "sl5" -> mapPath ({ Framework = Silverlight("v5.0"); CLRVersion = None; } :: acc) rest
                | "sl4-wp" -> mapPath ({ Framework = WindowsPhoneApp("7.1"); CLRVersion = None; } :: acc) rest
                | "sl4-wp71" -> mapPath ({ Framework = WindowsPhoneApp("7.1"); CLRVersion = None; } :: acc) rest
                | _ -> mapPath acc rest
               
        let path = path.Replace("\\", "/").ToLower()
        let fi = new FileInfo(path)

        if path.Contains("lib/" + fi.Name.ToLower()) then [{ Framework = DotNetFramework(All,Full); CLRVersion = None; }] else
        let startPos = path.IndexOf("lib/")
        let endPos = path.IndexOf(fi.Name.ToLower())
        if startPos < 0 || endPos < 0 then [] else
        path.Substring(startPos+4,endPos-startPos-5).Split('+')
        |> Seq.toList
        |> mapPath []
        |> List.rev
        

/// Contains methods to read and manipulate project file ndoes.
type InstallInfo = {
    DllName : string
    Path : string
    Condition : FramworkCondition
}

module InstallRules = 
    let groupDLLs (usedPackages : HashSet<string>) extracted = 
        [ for package, libraries in extracted do
              if usedPackages.Contains package.Name then 
                  let libraries = libraries |> Seq.toArray
                  for (lib : FileInfo) in libraries do
                      let conditions = FramworkCondition.DetectFromPath lib.FullName
                      for condition in conditions do
                          yield { DllName = lib.Name.Replace(lib.Extension, "")
                                  Path = lib.FullName
                                  Condition = condition } ]
        |> Seq.groupBy (fun info -> info.DllName, info.Condition.Framework.GetGroup())
        |> Seq.groupBy (fun ((name, _), _) -> name)
    
    let hasClientProfile libs = libs |> Seq.exists (fun x -> match x.Condition.Framework with | DotNetFramework (_,p) -> p = Client | _ -> false)
    let hasFullProfile libs = libs |> Seq.exists (fun x -> match x.Condition.Framework with | DotNetFramework (_,p) -> p = Full | _ -> false)

    let handleClientFrameworks frameworkVersion libs = 
        if frameworkVersion = ".NET v4.0" && hasClientProfile libs && not <| hasFullProfile libs then 
            let copy = libs |> List.head
            List.append 
                [ { copy with Condition = { copy.Condition with Framework = DotNetFramework(match copy.Condition.Framework with | DotNetFramework(v,_) -> v,Full) } } ] 
                libs
        else libs

    let handleCLRVersions (libs:InstallInfo list) =
        let withoutCLR,withCLR =
            libs
            |> List.partition (fun l -> l.Condition.CLRVersion = None)

        if List.isEmpty withCLR then libs else
        (withCLR |> List.maxBy (fun l -> l.Condition.CLRVersion)) :: withoutCLR

    let handlePath root (libs:InstallInfo list) =
        libs 
        |> List.map (fun lib -> { lib with Path = Uri(root).MakeRelativeUri(Uri(lib.Path)).ToString().Replace("/", "\\")} )


/// Contains methods to read and manipulate project files.
type ProjectFile = 
    { FileName: string
      OriginalText : string
      Document : XmlDocument
      Namespaces : XmlNamespaceManager }
    static member DefaultNameSpace = "http://schemas.microsoft.com/developer/msbuild/2003"

    member this.DeleteIfEmpty xPath =
        for node in this.Document.SelectNodes(xPath, this.Namespaces) do
            if node.ChildNodes.Count = 0 then
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

    member this.DeletePaketNodes() =
        for node in this.Document.SelectNodes("//ns:Reference", this.Namespaces) do
            let remove = ref false
            for child in node.ChildNodes do
                if child.Name = "Paket" then remove := true
            
            if !remove then
                node.ParentNode.RemoveChild(node) |> ignore

    member this.DeleteEmptyReferences() =
        this.DeleteIfEmpty("//ns:Project/ns:Choose/ns:Otherwise/ns:ItemGroup")        
        this.DeleteIfEmpty("//ns:Project/ns:Choose/ns:When/ns:ItemGroup")
        this.DeleteIfEmpty("//ns:Project/ns:Choose/ns:Otherwise")
        this.DeleteIfEmpty("//ns:Project/ns:Choose/ns:When")
        this.DeleteIfEmpty("//ns:Project/ns:Choose")

    member this.UpdateReferences(extracted,usedPackages:HashSet<string>) =
        this.DeletePaketNodes()

        let projectNode =
            seq { for node in this.Document.SelectNodes("//ns:Project", this.Namespaces) -> node }
            |> Seq.head

        let installInfos = InstallRules.groupDLLs usedPackages extracted
        for dllName,libsWithSameName in installInfos do
            if this.HasCustomNodes(dllName) then () else            
            let lastLib = ref None
            for (_,frameworkVersion),libs in libsWithSameName do
                let chooseNode = this.Document.CreateElement("Choose", ProjectFile.DefaultNameSpace)
                let libsWithSameFrameworkVersion = 
                    libs 
                    |> List.ofSeq                    
                    |> InstallRules.handlePath this.FileName
                    |> InstallRules.handleCLRVersions 
                    |> InstallRules.handleClientFrameworks frameworkVersion
                    |> List.sortBy (fun lib -> lib.Path)

                for lib in libsWithSameFrameworkVersion do
                    let condition =
                        match lib.Condition.Framework with
                        | DotNetFramework(v,_) ->
                            let profileTypeCondition =
                                if not <| InstallRules.hasClientProfile libsWithSameFrameworkVersion then "" else
                                sprintf " And $(TargetFrameworkProfile) == '%s'" (match lib.Condition.Framework with | DotNetFramework(_,Client) -> "Client" | _ -> "")
                            match v with
                            | Framework fw -> sprintf "$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == '%s'%s" fw profileTypeCondition
                            | All -> "true"
                        | WindowsPhoneApp v -> sprintf "$(TargetFrameworkIdentifier) == 'WindowsPhoneApp' And $(TargetPlatformVersion) == '%s'" v
                        | Silverlight v -> sprintf "$(TargetFrameworkIdentifier) == 'Silverlight' And $(SilverlightVersion) == '%s'" v
                
                    let whenNode = this.Document.CreateElement("When", ProjectFile.DefaultNameSpace)
                    whenNode.SetAttribute("Condition", condition) |> ignore
                        
                    let reference = this.Document.CreateElement("Reference", ProjectFile.DefaultNameSpace)
                    reference.SetAttribute("Include", lib.DllName)

                    let element = this.Document.CreateElement("HintPath",ProjectFile.DefaultNameSpace)
                    element.InnerText <- lib.Path
            
                    reference.AppendChild(element) |> ignore
 
                    let element = this.Document.CreateElement("Private",ProjectFile.DefaultNameSpace)
                    element.InnerText <- "True"
                    reference.AppendChild(element) |> ignore

                    let element = this.Document.CreateElement("Paket",ProjectFile.DefaultNameSpace)
                    element.InnerText <- "True"            
                    reference.AppendChild(element) |> ignore

                    let itemGroup = this.Document.CreateElement("ItemGroup", ProjectFile.DefaultNameSpace)
                    itemGroup.AppendChild(reference) |> ignore
                    whenNode.AppendChild(itemGroup) |> ignore
                    chooseNode.AppendChild(whenNode) |> ignore

                    lastLib := Some lib

                match !lastLib with
                | None -> ()
                | Some lib ->
                    let condition =
                        match lib.Condition.Framework with
                        | DotNetFramework _ -> "$(TargetFrameworkIdentifier) == '.NETFramework'"
                        | WindowsPhoneApp _ -> "$(TargetFrameworkIdentifier) == 'WindowsPhoneApp'"
                        | Silverlight _ -> "$(TargetFrameworkIdentifier) == 'Silverlight'"

                    let whenNode = this.Document.CreateElement("When", ProjectFile.DefaultNameSpace)
                    whenNode.SetAttribute("Condition", condition) |> ignore

                    let reference = this.Document.CreateElement("Reference", ProjectFile.DefaultNameSpace)
                    reference.SetAttribute("Include", lib.DllName)

                    let element = this.Document.CreateElement("HintPath",ProjectFile.DefaultNameSpace)
                    element.InnerText <- lib.Path
            
                    reference.AppendChild(element) |> ignore
 
                    let element = this.Document.CreateElement("Private",ProjectFile.DefaultNameSpace)
                    element.InnerText <- "True"
                    reference.AppendChild(element) |> ignore

                    let element = this.Document.CreateElement("Paket",ProjectFile.DefaultNameSpace)
                    element.InnerText <- "True"            
                    reference.AppendChild(element) |> ignore

                    let itemGroup = this.Document.CreateElement("ItemGroup", ProjectFile.DefaultNameSpace)
                    itemGroup.AppendChild(reference) |> ignore
                    whenNode.AppendChild(itemGroup) |> ignore
                    chooseNode.AppendChild(whenNode) |> ignore

                projectNode.AppendChild(chooseNode) |> ignore

        this.DeleteEmptyReferences()

        if Utils.normalizeXml this.Document <> this.OriginalText then
            this.Document.Save(this.FileName)

    static member Load(fileName:string) =
        let fi = FileInfo(fileName)
        let doc = new XmlDocument()
        doc.Load fi.FullName

        let manager = new XmlNamespaceManager(doc.NameTable)
        manager.AddNamespace("ns", ProjectFile.DefaultNameSpace)
        { FileName = fi.FullName; Document = doc; Namespaces = manager; OriginalText = Utils.normalizeXml doc }