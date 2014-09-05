namespace Paket

open System
open System.IO
open Paket

/// [omit]
module ConfigHelpers = 

    let parseVersionRange (text : string) : VersionRange = 
        // TODO: Make this pretty
        if text.StartsWith "~> " then 
            let min = text.Replace("~> ", "")
            let parts = min.Split('.')            
            if parts.Length > 1 then
                let idx = parts.Length-2
                parts.[idx] <-
                    match Int32.TryParse parts.[idx] with
                    | true, number -> (number+1).ToString()
                    | _ ->  parts.[idx]
                parts.[parts.Length-1] <- "0"
            else
                parts.[0] <-
                    match Int32.TryParse parts.[0] with
                    | true, number -> (number+1).ToString()
                    | _ ->  parts.[0]

            VersionRange.Between(min, String.Join(".", parts))
        else if text.StartsWith ">= " then VersionRange.AtLeast(text.Replace(">= ", ""))
        else if text.StartsWith "= " then VersionRange.Exactly(text.Replace("= ", ""))
        else VersionRange.Exactly(text)

    let private (|Remote|Package|Blank|) (line:string) =
        match line.Trim() with
        | _ when String.IsNullOrWhiteSpace line -> Blank
        | trimmed when trimmed.StartsWith "source" -> 
            let fst = trimmed.IndexOf("\"")
            let snd = trimmed.IndexOf("\"",fst+1)
            Remote (trimmed.Substring(fst,snd-fst).Replace("\"",""))                
        | trimmed when trimmed.StartsWith "nuget" -> Package(trimmed.Replace("nuget","").Trim())
        | _ -> Blank
    
    let parseDependenciesFile (lines:string seq) = 
        (("http://nuget.org/api/v2", []), lines)
        ||> Seq.fold(fun (currentSource, packages) line ->
            match line with
            | Remote newSource -> newSource, packages
            | Blank -> (currentSource, packages)
            | Package details ->
                let parts = details.Split('"')
                let version = parts.[3]
                currentSource, { Source = Nuget currentSource 
                                 Name = parts.[1]
                                 DirectDependencies = []
                                 ResolverStrategy = if version.StartsWith "!" then ResolverStrategy.Min else ResolverStrategy.Max
                                 VersionRange = parseVersionRange(version.Trim '!') } :: packages)
        |> snd
        |> List.rev

/// Allows to parse and analyze packages.fsx files.
type Config(packages : Package seq) = 
    let packages = packages |> Seq.toList
    let dependencyMap = Map.ofSeq (packages |> Seq.map (fun p -> p.Name, p.VersionRange))
    member __.DirectDependencies = dependencyMap
    member __.Packages = packages
    member __.Resolve(force, discovery : IDiscovery) = Resolver.Resolve(force, discovery, packages)
    static member FromCode(code:string) : Config = 
        Config(ConfigHelpers.parseDependenciesFile <| code.Replace("\r\n","\n").Replace("\r","\n").Split('\n'))
    static member ReadFromFile fileName : Config = 
        Config(ConfigHelpers.parseDependenciesFile <| File.ReadAllLines fileName)
