namespace Paket

open System
open System.IO
open System.Xml
open System.Collections.Generic

/// The Framework version.
type FrameworkVersion =
| Unknown
| All
| Framework of string
| FrameworkExtension of string * string
    member x.GetGroup() =
        match x with
        | Unknown -> "Unknown"
        | All -> "All"
        | Framework v -> v
        | FrameworkExtension(v,_) -> v

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
        | DotNetFramework(v,_) -> ".NET " + v.GetGroup()        
        | WindowsPhoneApp(v) -> "WindowsPhoneApp " + v
        | Silverlight(v) -> "Silverlight " + v


/// Contains methods to analyze .NET Framework Conditions.
type FramworkCondition = 
    { Framework : FrameworkIdentifier;
      CLRVersion : string option; }
    static member DetectFromPath(path : string) = 
        let path = path.Replace("\\", "/").ToLower()
        let fi = new FileInfo(path)
        if path.Contains "lib/1.0/" then { Framework = DotNetFramework(All,Full); CLRVersion = Some "1.0" }
        elif path.Contains "lib/1.1/" then { Framework = DotNetFramework(All,Full); CLRVersion = Some "1.1" }
        elif path.Contains "lib/2.0/" then { Framework = DotNetFramework(All,Full); CLRVersion = Some "2.0" }
        elif path.Contains "lib/net20/" then { Framework = DotNetFramework(Framework "v2.0",Full); CLRVersion = None }
        elif path.Contains "lib/net35/" then { Framework = DotNetFramework(Framework "v3.5",Full); CLRVersion = None }
        elif path.Contains "lib/net40/" then { Framework = DotNetFramework(Framework "v4.0",Full); CLRVersion = None }
        elif path.Contains "lib/net40-full/" then { Framework = DotNetFramework(Framework "v4.0",Full); CLRVersion = None }
        elif path.Contains "lib/net40-client/" then { Framework = DotNetFramework(Framework "v4.0",Client); CLRVersion = None }
        elif path.Contains "lib/net45/" then { Framework = DotNetFramework(Framework "v4.5",Full); CLRVersion = None }
        elif path.Contains "lib/net451/" then { Framework = DotNetFramework(FrameworkExtension("v4.5","v4.5.1"),Full); CLRVersion = None }
        elif path.Contains "lib/sl3/" then { Framework = Silverlight("v3.0"); CLRVersion = None; }
        elif path.Contains "lib/sl4/" then { Framework = Silverlight("v4.0"); CLRVersion = None; }
        elif path.Contains "lib/sl4-wp/" then { Framework = WindowsPhoneApp("7.1"); CLRVersion = None; }
        elif path.Contains "lib/sl4-wp71/" then { Framework = WindowsPhoneApp("7.1"); CLRVersion = None; }
        elif path.Contains("lib/" + fi.Name.ToLower()) then { Framework = DotNetFramework(All,Full); CLRVersion = None; }
        else { Framework = DotNetFramework(Unknown,Full); CLRVersion = None }

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
                      yield { DllName = lib.Name.Replace(lib.Extension, "")
                              Path = lib.FullName
                              Condition = FramworkCondition.DetectFromPath lib.FullName } ]
        |> Seq.groupBy (fun info -> info.DllName, info.Condition.Framework.GetGroup())
        |> Seq.groupBy (fun ((name, _), _) -> name)
    
    let hasClientProfile libs = libs |> Seq.exists (fun x -> match x.Condition.Framework with | DotNetFramework (_,p) -> p = Client | _ -> false)
    let hasFullProfile libs = libs |> Seq.exists (fun x -> match x.Condition.Framework with | DotNetFramework (_,p) -> p = Full | _ -> false)
    
    let hasFramworkExtensions libs = 
        libs |> Seq.exists (fun x -> 
                    match x.Condition.Framework with
                    | DotNetFramework(v,_) ->
                        match v with
                        | FrameworkExtension _ -> true
                        | _ -> false
                    | _ -> false)

    let handleFrameworkExtensions frameworkVersion libs = 
        if frameworkVersion = ".NET v4.5" && not <| hasFramworkExtensions libs then 
            let copy = libs |> Seq.head
            Seq.append 
                [ { copy with Condition = { copy.Condition with Framework = DotNetFramework(FrameworkExtension("v4.5", "v4.5.1"),Full) } } ] 
                libs
        else libs

    let handleClientFrameworks frameworkVersion libs = 
        if frameworkVersion = ".NET v4.0" && hasClientProfile libs && not <| hasFullProfile libs then 
            let copy = libs |> Seq.head
            Seq.append 
                [ { copy with Condition = { copy.Condition with Framework = DotNetFramework(match copy.Condition.Framework with | DotNetFramework(v,_) -> v,Full) } } ] 
                libs
        else libs

    let handleCLRVersions (libs:InstallInfo list) =
        let withoutCLR,withCLR =
            libs 
            |> Seq.toList
            |> List.partition (fun l -> l.Condition.CLRVersion = None)

        if List.isEmpty withCLR then libs else
        (withCLR |> List.maxBy (fun l -> l.Condition.CLRVersion)) :: withoutCLR


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
        for node in this.Document.SelectNodes("//ns:Project/ns:Choose/ns:When/ns:ItemGroup/ns:Reference", this.Namespaces) do
            let remove = ref false
            for child in node.ChildNodes do
                if child.Name = "Paket" then remove := true
            
            if !remove then
                node.ParentNode.RemoveChild(node) |> ignore

    member this.DeleteOldReferences() =     
        this.DeletePaketNodes()

        this.DeleteIfEmpty("//ns:Project/ns:Choose/ns:When/ns:ItemGroup")
        this.DeleteIfEmpty("//ns:Project/ns:Choose/ns:When")
        this.DeleteIfEmpty("//ns:Project/ns:Choose")

    member this.UpdateReferences(extracted,usedPackages:HashSet<string>) =
        this.DeleteOldReferences()

        let projectNode =
            seq { for node in this.Document.SelectNodes("//ns:Project", this.Namespaces) -> node }
            |> Seq.head

        let installInfos = InstallRules.groupDLLs usedPackages extracted
        for dllName,group1 in installInfos do
            if this.HasCustomNodes(dllName) then () else
            let chooseNode = this.Document.CreateElement("Choose", ProjectFile.DefaultNameSpace)
            let libsWithSameName = Seq.toArray group1
            for (_,frameworkVersion),libs in libsWithSameName do
                let libsWithSameFrameworkVersion = 
                    libs 
                    |> List.ofSeq
                    |> InstallRules.handleCLRVersions 
                    |> InstallRules.handleFrameworkExtensions frameworkVersion 
                    |> InstallRules.handleClientFrameworks frameworkVersion 
                    |> Seq.toArray              

                for lib in libsWithSameFrameworkVersion do
                    let installIt,condition =                        
                        match lib.Condition.Framework with
                        | DotNetFramework(v,_) ->
                            if libsWithSameName.Length = 1 then true,"true" else
                            let profileTypeCondition =
                                if not <| InstallRules.hasClientProfile libsWithSameFrameworkVersion then "" else
                                sprintf " And $(TargetFrameworkProfile) == '%s'" (match lib.Condition.Framework with | DotNetFramework(_,Client) -> "Client" | _ -> "")
                            match v with
                            | Unknown -> false,"true" 
                            | Framework fw -> true,sprintf "$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == '%s'%s" fw profileTypeCondition
                            | FrameworkExtension(_,fw) -> true,sprintf "$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == '%s'%s" fw profileTypeCondition
                            | All -> true,"true"
                        | WindowsPhoneApp v -> true,sprintf "$(TargetFrameworkIdentifier) == 'WindowsPhoneApp' And $(TargetPlatformVersion) == '%s'" v
                        | Silverlight v -> true,sprintf "$(TargetFrameworkIdentifier) == 'Silverlight' And $(SilverlightVersion) == '%s'" v
                    
                    if installIt then                    
                        let whenNode = this.Document.CreateElement("When", ProjectFile.DefaultNameSpace)
                        whenNode.SetAttribute("Condition", condition) |> ignore
                        
                        let reference = this.Document.CreateElement("Reference", ProjectFile.DefaultNameSpace)
                        reference.SetAttribute("Include", lib.DllName)

                        let element = this.Document.CreateElement("HintPath",ProjectFile.DefaultNameSpace)
                        element.InnerText <- Uri(this.FileName).MakeRelativeUri(Uri(lib.Path)).ToString().Replace("/", "\\")
            
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

        if Utils.normalizeXml this.Document <> this.OriginalText then
            this.Document.Save(this.FileName)

    static member Load(fileName:string) =
        let fi = FileInfo(fileName)
        let doc = new XmlDocument()
        doc.Load fi.FullName

        let manager = new XmlNamespaceManager(doc.NameTable)
        manager.AddNamespace("ns", ProjectFile.DefaultNameSpace)
        { FileName = fi.FullName; Document = doc; Namespaces = manager; OriginalText = Utils.normalizeXml doc }