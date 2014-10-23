namespace Paket

open System
open System.Xml
open System.IO

[<RequireQualifiedAccess>]
type NuspecReferences = 
    | All
    | Explicit of string list

type FrameworkAssemblyReference = {
    AssemblyName: string
    TargetFramework : FrameworkIdentifier }

module NugetVersionRangeParser =
    
    /// Parses NuGet version ranges.
    let parse (text:string) = 
        if  text = null || text = "" || text = "null" then VersionRequirement.AllReleases else

        let parseRange text = 
            let failParse() = failwithf "unable to parse %s" text

            let parseBound  = function
                | '[' | ']' -> VersionRangeBound.Including
                | '(' | ')' -> VersionRangeBound.Excluding
                | _         -> failParse()
        
            if not <| text.Contains "," then
                if text.StartsWith "[" then Specific(text.Trim([|'['; ']'|]) |> SemVer.Parse)
                else Minimum(SemVer.Parse text)
            else
                let fromB = parseBound text.[0]
                let toB   = parseBound (Seq.last text)
                let versions = text
                                .Trim([|'['; ']';'(';')'|])
                                .Split([|','|], StringSplitOptions.RemoveEmptyEntries)
                                |> Array.map SemVer.Parse
                match versions.Length with
                | 2 ->
                    Range(fromB, versions.[0], versions.[1], toB)
                | 1 ->
                    if text.[1] = ',' then
                        match fromB, toB with
                        | VersionRangeBound.Excluding, VersionRangeBound.Including -> Maximum(versions.[0])
                        | VersionRangeBound.Excluding, VersionRangeBound.Excluding -> LessThan(versions.[0])
                        | _ -> failParse()
                    else 
                        match fromB, toB with
                        | VersionRangeBound.Excluding, VersionRangeBound.Excluding -> GreaterThan(versions.[0])
                        | _ -> failParse()
                | _ -> failParse()
        VersionRequirement(parseRange text,PreReleaseStatus.No)


type Nuspec = 
    { References : NuspecReferences 
      Dependencies : (string * VersionRequirement) list
      OfficialName : string
      FrameworkAssemblyReferences : FrameworkAssemblyReference list }
    static member KnownNamespaces =
        ["ns1","http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"
         "ns2","http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"
         "ns3","http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"
         "ns4","http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"
         "ns5","http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd"]

    static member All = { References = NuspecReferences.All; Dependencies = []; FrameworkAssemblyReferences = []; OfficialName = "" }
    static member Explicit references = { References = NuspecReferences.Explicit references; Dependencies = []; FrameworkAssemblyReferences = []; OfficialName = "" }
    static member Load(fileName : string) = 
        let fi = FileInfo(fileName)
        if not fi.Exists then Nuspec.All
        else 
            let doc = new XmlDocument()
            doc.Load fi.FullName
            let manager = new XmlNamespaceManager(doc.NameTable)

            Nuspec.KnownNamespaces
            |> List.iter (fun (name,ns) -> manager.AddNamespace(name, ns))

            let nsUri = doc.LastChild.NamespaceURI
            let ns = manager.LookupPrefix(nsUri)       
            if ns = null then
                failwithf "unrecognized namespace of %s in %s" nsUri fileName

            let dependencies = 
                doc.SelectNodes(sprintf "/%s:package/%s:metadata/%s:dependencies/%s:dependency" ns ns ns ns, manager)
                |> Seq.cast<XmlNode>
                |> Seq.map (fun node -> 
                                let name = node.Attributes.["id"].Value                            
                                let version = 
                                    if node.Attributes.["version"] <> null then 
                                        NugetVersionRangeParser.parse node.Attributes.["version"].Value 
                                    else 
                                        NugetVersionRangeParser.parse "0"
                                name,version) 
                |> Seq.toList

            let officialName = 
                doc.SelectNodes(sprintf "/%s:package/%s:metadata/%s:id" ns ns ns, manager)
                |> Seq.cast<XmlNode>
                |> Seq.head
                |> fun node -> node.InnerText

            let references =
                if List.isEmpty [ for node in doc.SelectNodes(sprintf "//%s:references" ns, manager) -> node] then [] else
                    [ for node in doc.SelectNodes(sprintf "//%s:reference" ns, manager) -> 
                        node.Attributes.["file"].InnerText]
            
            let frameworkAssemblyReferences =
                if List.isEmpty [ for node in doc.SelectNodes(sprintf "//%s:frameworkAssemblies" ns, manager) -> node] then [] else
                    [ for node in doc.SelectNodes(sprintf "//%s:frameworkAssembly" ns, manager) do
                        let name =  node.Attributes.["assemblyName"].InnerText
                        for framework in node.Attributes.["targetFramework"].InnerText.Split([|','; ' '|],System.StringSplitOptions.RemoveEmptyEntries) do
                            match FrameworkIdentifier.Extract framework with
                            | Some fw -> yield { AssemblyName = name; TargetFramework = fw }                            
                            | None -> () ]

            { References = if references = [] then NuspecReferences.All else NuspecReferences.Explicit references
              Dependencies = dependencies
              OfficialName = officialName
              FrameworkAssemblyReferences = frameworkAssemblyReferences }