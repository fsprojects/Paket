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

    /// formats a VersionRange in NuGet syntax
    let format (v:VersionRange) =
        match v with
        | Minimum(version) -> 
            match version.ToString() with
            | "0" -> ""
            | x  -> x
        | GreaterThan(version) -> sprintf "(%s,)" (version.ToString())
        | Maximum(version) -> sprintf "(,%s]" (version.ToString())
        | LessThan(version) -> sprintf "(,%s)" (version.ToString())
        | Specific(version) -> sprintf "[%s]" (version.ToString())
        | OverrideAll(version) -> sprintf "[%s]" (version.ToString()) 
        | Range(fromB, from,_to,_toB) -> 
            let getMinDelimiter (v:VersionRangeBound) =
                match v with
                | VersionRangeBound.Including -> "["
                | VersionRangeBound.Excluding -> "("

            let getMaxDelimiter (v:VersionRangeBound) =
                match v with
                | VersionRangeBound.Including -> "]"
                | VersionRangeBound.Excluding -> ")"
        
            sprintf "%s%s,%s%s" (getMinDelimiter fromB) (from.ToString()) (_to.ToString()) (getMaxDelimiter _toB) 


type Nuspec = 
    { References : NuspecReferences 
      Dependencies : (string * VersionRequirement) list
      OfficialName : string
      FrameworkAssemblyReferences : FrameworkAssemblyReference list }

    static member All = { References = NuspecReferences.All; Dependencies = []; FrameworkAssemblyReferences = []; OfficialName = "" }
    static member Explicit references = { References = NuspecReferences.Explicit references; Dependencies = []; FrameworkAssemblyReferences = []; OfficialName = "" }
    static member Load(fileName : string) = 
        let fi = FileInfo(fileName)
        if not fi.Exists then Nuspec.All
        else 
            let doc = new XmlDocument()
            doc.Load fi.FullName

            let dependencies = 
                doc.SelectNodes "//*[local-name() = 'metadata']/*[local-name() = 'dependencies']/*[local-name() = 'dependency']"
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
                doc.SelectNodes "//*[local-name() = 'metadata']/*[local-name() = 'id']"
                |> Seq.cast<XmlNode>
                |> Seq.head
                |> fun node -> node.InnerText

            let references =
                if List.isEmpty [ for node in doc.SelectNodes "//*[local-name() = 'references']" -> node] then [] else
                    [ for node in doc.SelectNodes "//*[local-name() = 'reference']" -> 
                        node.Attributes.["file"].InnerText]
            
            let frameworkAssemblyReferences =
                if List.isEmpty [ for node in doc.SelectNodes "//*[local-name() = 'frameworkAssemblies']"  -> node] then [] else
                    [ for node in doc.SelectNodes "//*[local-name() = 'frameworkAssembly']" do
                        let name =  node.Attributes.["assemblyName"].InnerText
                        for framework in node.Attributes.["targetFramework"].InnerText.Split([|','; ' '|],System.StringSplitOptions.RemoveEmptyEntries) do
                            match FrameworkIdentifier.Extract framework with
                            | Some fw -> yield { AssemblyName = name; TargetFramework = fw }                            
                            | None -> () ]

            { References = if references = [] then NuspecReferences.All else NuspecReferences.Explicit references
              Dependencies = dependencies
              OfficialName = officialName
              FrameworkAssemblyReferences = frameworkAssemblyReferences }
