namespace Paket

open System
open System.IO
open Paket

/// [omit]
module DependenciesFileParser = 

    let parseVersionRange (text : string) : VersionRange = 
        let splitVersion (text:string) =
            let tokens = ["~>";">=";"=" ]
            match tokens |> List.tryFind(text.StartsWith) with
            | Some token -> token, text.Replace(token + " ", "")
            | None -> "=", text

        try
            match splitVersion text with
            | ">=", version -> VersionRange.AtLeast(version)
            | "~>", minimum ->
                let maximum =                    
                    let promote index (values:string array) =
                        let parsed, number = Int32.TryParse values.[index]
                        if parsed then values.[index] <- (number + 1).ToString()
                        if values.Length > 1 then values.[values.Length - 1] <- "0"
                        values

                    let parts = minimum.Split '.'
                    let penultimateItem = Math.Max(parts.Length - 2, 0)
                    let promoted = parts |> promote penultimateItem
                    String.Join(".", promoted)
                VersionRange.Between(minimum, maximum)
            | _, version -> VersionRange.Exactly(version)
        with
        | _ -> failwithf "could not parse version range \"%s\"" text

    let private (|Remote|Package|Blank|ReferencesMode|) (line:string) =
        match line.Trim() with
        | _ when String.IsNullOrWhiteSpace line -> Blank
        | trimmed when trimmed.StartsWith "source" -> 
            let fst = trimmed.IndexOf("\"")
            let snd = trimmed.IndexOf("\"",fst+1)
            Remote (trimmed.Substring(fst,snd-fst).Replace("\"",""))                
        | trimmed when trimmed.StartsWith "nuget" -> Package(trimmed.Replace("nuget","").Trim())
        | trimmed when trimmed.StartsWith "references" -> ReferencesMode(trimmed.Replace("references","").Trim() = "strict")
        | _ -> Blank
    
    let parseDependenciesFile (lines:string seq) =
        ((0, false, [], []), lines)
        ||> Seq.fold(fun (lineNo, referencesMode, sources: PackageSource list, packages) line ->
            let lineNo = lineNo + 1
            try
                match line with
                | Remote newSource -> lineNo, referencesMode, (PackageSource.Parse(newSource.TrimEnd([|'/'|])) :: sources), packages
                | Blank -> lineNo, referencesMode, sources, packages
                | ReferencesMode mode -> lineNo, mode, sources, packages
                | Package details ->
                    let parts = details.Split('"')
                    if parts.Length < 4 || String.IsNullOrWhiteSpace parts.[1] || String.IsNullOrWhiteSpace parts.[3] then
                        failwith "missing \""
                    let version = parts.[3]
                    lineNo, referencesMode, sources, 
                        { Sources = sources
                          Name = parts.[1]
                          ResolverStrategy = if version.StartsWith "!" then ResolverStrategy.Min else ResolverStrategy.Max
                          VersionRange = parseVersionRange(version.Trim '!') } :: packages
            with
            | exn -> failwithf "Error in paket.dependencies line %d%s  %s" lineNo Environment.NewLine exn.Message)
        |> fun (_,mode,_,xs) -> mode, List.rev xs

/// Allows to parse and analyze Dependencies files.
type DependenciesFile(strictMode,packages : UnresolvedPackage seq) = 
    let packages = packages |> Seq.toList
    let dependencyMap = Map.ofSeq (packages |> Seq.map (fun p -> p.Name, p.VersionRange))
    member __.DirectDependencies = dependencyMap
    member __.Packages = packages
    member __.Strict = strictMode
    member __.Resolve(force, discovery : IDiscovery) = Resolver.Resolve(force, discovery, packages)
    static member FromCode(code:string) : DependenciesFile = 
        DependenciesFile(DependenciesFileParser.parseDependenciesFile <| code.Replace("\r\n","\n").Replace("\r","\n").Split('\n'))
    static member ReadFromFile fileName : DependenciesFile = 
        DependenciesFile(DependenciesFileParser.parseDependenciesFile <| File.ReadAllLines fileName)
