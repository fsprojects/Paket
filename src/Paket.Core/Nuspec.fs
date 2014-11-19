namespace Paket

open System
open System.Xml
open System.IO
open Xml
open Paket.Requirements

[<RequireQualifiedAccess>]
type NuspecReferences = 
    | All
    | Explicit of string list

type FrameworkAssemblyReference = {
    AssemblyName: string
    TargetFramework : FrameworkRestriction }

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
      Dependencies : (string * VersionRequirement * (FrameworkIdentifier option)) list
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

            let officialName = 
                match doc |> getNode "package" |> optGetNode "metadata" |> optGetNode "id" with
                | Some node -> node.InnerText
                | None -> failwithf "unable to find package id in %s" fileName

            let dependency node = 
                let name = 
                    match node |> getAttribute "id" with
                    | Some name -> name
                    | None -> failwithf "unable to find dependency id in %s" fileName                            
                let version = 
                    match node |> getAttribute "version" with
                    | Some version -> NugetVersionRangeParser.parse version
                    | None ->         NugetVersionRangeParser.parse "0"
                let restriction =
                    let parent = node.ParentNode 
                    match parent.Name.ToLower(), parent |> getAttribute "targetFramework" with
                    | "group", Some framework -> FrameworkIdentifier.Extract framework
                    | _ -> None
                name,version,restriction

            let dependencies = 
                doc 
                |> getDescendants "dependency"
                |> List.map dependency
            
            let references = 
                doc
                |> getDescendants "reference"
                |> List.choose (getAttribute "file")

            let assemblyRefs node =
                let name = node |> getAttribute "assemblyName"
                let targetFrameworks = node |> getAttribute "targetFramework"
                match name,targetFrameworks with
                | Some name, Some targetFrameworks when targetFrameworks = "" ->
                    [{ AssemblyName = name; TargetFramework = None }]
                | Some name, None ->                     
                    [{ AssemblyName = name; TargetFramework = None }]
                | Some name, Some targetFrameworks ->                     
                    targetFrameworks.Split([|','; ' '|],System.StringSplitOptions.RemoveEmptyEntries)
                    |> Array.choose FrameworkIdentifier.Extract
                    |> Array.map (fun fw -> { AssemblyName = name; TargetFramework = Some fw })
                    |> Array.toList
                | _ -> []

            let frameworkAssemblyReferences =
                doc
                |> getDescendants "frameworkAssembly"
                |> List.collect assemblyRefs
           
            { References = if references = [] then NuspecReferences.All else NuspecReferences.Explicit references
              Dependencies = dependencies
              OfficialName = officialName
              FrameworkAssemblyReferences = frameworkAssemblyReferences }