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
| Silverlight of string
    member x.GetGroup() =
        match x with
        | DotNetFramework(v,_) -> ".NET " + v.GetGroup()        
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
        elif path.Contains "lib/sl4/" then { Framework = Silverlight("v4.0"); CLRVersion = None; }
        elif path.Contains("lib/" + fi.Name.ToLower()) then { Framework = DotNetFramework(All,Full); CLRVersion = None; }
        else { Framework = DotNetFramework(Unknown,Full); CLRVersion = None }

/// Contains methods to read and manipulate project file ndoes.
type ReferenceNode = 
    { DLLName : string
      Condition : string option
      Private : bool
      HintPath : string option }

type InstallInfo = {
    DllName : string
    Path : string
    Condition : FramworkCondition
}

module DLLGrouping = 
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
        if frameworkVersion = "v4.5" && not <| hasFramworkExtensions libs then 
            let copy = libs |> Seq.head
            Seq.append 
                [ { copy with Condition = { copy.Condition with Framework = DotNetFramework(FrameworkExtension("v4.5", "v4.5.1"),Full) } } ] 
                libs
        else libs

    let handleClientFrameworks frameworkVersion libs = 
        if frameworkVersion = "v4.0" && hasClientProfile libs && not <| hasFullProfile libs then 
            let copy = libs |> Seq.head
            Seq.append 
                [ { copy with Condition = { copy.Condition with Framework = DotNetFramework(match copy.Condition.Framework with | DotNetFramework(v,_) -> v,Full) } } ] 
                libs
        else libs

    let handleCLRVersions (libs:InstallInfo seq) =
        let withoutCLR =
            libs 
            |> Seq.filter (fun l -> l.Condition.CLRVersion = None)
        
        let withCLR = 
            libs 
            |> Seq.filter (fun l -> l.Condition.CLRVersion <> None)
        
        if Seq.isEmpty withCLR then libs else
            [withCLR |> Seq.maxBy (fun l -> l.Condition.CLRVersion)]
            |> Seq.append withoutCLR

/// Contains methods to read and manipulate project files.
type ProjectFile = 
    { FileName: string
      OriginalText : string
      Document : XmlDocument
      Namespaces : XmlNamespaceManager }
    static member DefaultNameSpace = "http://schemas.microsoft.com/developer/msbuild/2003"

    member this.DeleteOldReferences(name) =
       for node in this.Document.SelectNodes("//ns:Project/ns:ItemGroup/ns:Reference", this.Namespaces) do
            if node.Attributes.["Include"].InnerText.Split(',').[0] = name then
                node.ParentNode.RemoveChild(node) |> ignore
       
    member this.AddReference(referenceNode: ReferenceNode) =
        let firstNode =
            seq { for node in this.Document.SelectNodes("//ns:Project/ns:ItemGroup/ns:Reference", this.Namespaces) -> node }
            |> Seq.last

            
        let copy = this.Document.CreateElement("Reference", ProjectFile.DefaultNameSpace)
        copy.SetAttribute("Include", referenceNode.DLLName)
        match referenceNode.Condition with
        | Some c ->  copy.SetAttribute("Condition", c) |> ignore
        | None -> ()

        match referenceNode.HintPath with
        | Some hintPath ->
            let element = this.Document.CreateElement("HintPath",ProjectFile.DefaultNameSpace)
            element.InnerText <- hintPath
            
            copy.AppendChild(element) |> ignore
        | None -> ()
            
        match referenceNode.Private with
        | true ->
            let element = this.Document.CreateElement("Private",ProjectFile.DefaultNameSpace)
            element.InnerText <- "True"
            
            copy.AppendChild(element) |> ignore
        | _ -> ()

        firstNode.ParentNode.AppendChild(copy) |> ignore

    member this.UpdateReferences(extracted,usedPackages:HashSet<string>) =
        for _, libraries in extracted do
            for (lib:FileInfo) in libraries do                                       
                this.DeleteOldReferences (lib.Name.Replace(lib.Extension, ""))

        let installInfos = DLLGrouping.groupDLLs usedPackages extracted
        for _,group1 in installInfos do
            let libsWithSameName = group1 |> Seq.toArray
            for (_,frameworkVersion),libs in libsWithSameName do
                let libsWithSameFrameworkVersion = 
                    libs 
                    |> Seq.cache
                    |> DLLGrouping.handleCLRVersions 
                    |> DLLGrouping.handleFrameworkExtensions frameworkVersion 
                    |> DLLGrouping.handleClientFrameworks frameworkVersion 
                    |> Seq.toArray              

                for lib in libsWithSameFrameworkVersion do
                    let installIt,condition =
                        if libsWithSameName.Length = 1 then true,None else
                        let profileTypeCondition =
                            if not <| DLLGrouping.hasClientProfile libsWithSameFrameworkVersion then "" else
                            sprintf " And $(TargetFrameworkProfile) == '%s'" (match lib.Condition.Framework with | DotNetFramework(_,Client) -> "Client" | _ -> "")

                        match lib.Condition.Framework with
                        | DotNetFramework(v,_) ->
                            match v with
                            | Unknown -> false,None 
                            | Framework fw -> true,Some(sprintf "$(TargetFrameworkVersion) == '%s'%s" fw profileTypeCondition)
                            | FrameworkExtension(_,fw) -> true,Some(sprintf "$(TargetFrameworkVersion) == '%s'%s" fw profileTypeCondition)
                            | All -> true,None
                    
                    if installIt then                        
                        { DLLName = lib.DllName
                          HintPath = Some(Uri(this.FileName).MakeRelativeUri(Uri(lib.Path)).ToString().Replace("/", "\\"))
                          Private = true
                          Condition = condition }
                        |> this.AddReference

        if Utils.normalizeXml this.Document <> this.OriginalText then
            this.Document.Save(this.FileName)

    static member Load(fileName:string) =
        let fi = FileInfo(fileName)
        let doc = new XmlDocument()
        doc.Load fi.FullName

        let manager = new XmlNamespaceManager(doc.NameTable)
        manager.AddNamespace("ns", ProjectFile.DefaultNameSpace)
        { FileName = fi.FullName; Document = doc; Namespaces = manager; OriginalText = Utils.normalizeXml doc }