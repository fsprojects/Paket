namespace Paket

open System
open System.IO
open Paket

/// [omit]
module DependenciesFileParser = 

    let parseVersionRange (text : string) : VersionRange = 
        try
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
        with
        | _ -> failwithf "could not parse version range \"%s\"" text

    let private (|Remote|Package|Blank|SourceFile|) (line:string) =
        match line.Trim() with
        | _ when String.IsNullOrWhiteSpace line -> Blank
        | trimmed when trimmed.StartsWith "source" -> 
            let fst = trimmed.IndexOf("\"")
            let snd = trimmed.IndexOf("\"",fst+1)
            Remote (trimmed.Substring(fst,snd-fst).Replace("\"",""))                
        | trimmed when trimmed.StartsWith "nuget" -> Package(trimmed.Replace("nuget","").Trim())
        | trimmed when trimmed.StartsWith "github" ->
            let parts = trimmed.Replace("\"", "").Split ' '
            let getParts (projectSpec:string) =
                match projectSpec.Split ':' with
                | [| owner; project |] -> owner, project, None
                | [| owner; project; commit |] -> owner, project, Some commit
                | _ -> failwith "invalid github specification"
            match parts with
            | [| _; projectSpec; fileSpec |] -> SourceFile(getParts projectSpec, fileSpec)
            | _ -> failwith "invalid github specification"
        | _ -> Blank
    
    let parseDependenciesFile (lines:string seq) = 
        ((0,[], [], []), lines)
        ||> Seq.fold(fun (lineNo, sources: PackageSource list, packages, sourceFiles) line ->
            let lineNo = lineNo + 1
            try
                match line with
                | Remote newSource -> lineNo, (PackageSource.Parse(newSource.TrimEnd([|'/'|])) :: sources), packages, sourceFiles
                | Blank -> lineNo, sources, packages, sourceFiles
                | Package details ->
                    let parts = details.Split('"')
                    if parts.Length < 4 || String.IsNullOrWhiteSpace parts.[1] || String.IsNullOrWhiteSpace parts.[3] then
                        failwith "missing \""
                    let version = parts.[3]
                    lineNo, sources, { Sources = sources
                                       Name = parts.[1]
                                       DirectDependencies = []
                                       ResolverStrategy = if version.StartsWith "!" then ResolverStrategy.Min else ResolverStrategy.Max
                                       VersionRange = parseVersionRange(version.Trim '!') } :: packages, sourceFiles
                | SourceFile((owner,project, commit), path) ->
                    let newSourceFile = { Owner = owner
                                          Project = project
                                          Commit = commit
                                          Path = path }
                    tracefn "  %O" newSourceFile
                    lineNo, sources, packages, newSourceFile :: sourceFiles
                    
            with
            | exn -> failwithf "Error in paket.dependencies line %d%s  %s" lineNo Environment.NewLine exn.Message)
        |> fun (_,_,packages,remoteFiles) ->
            packages |> List.rev,
            remoteFiles |> List.rev

/// Allows to parse and analyze Dependencies files.
type DependenciesFile(packages : Package list, remoteFiles : SourceFile list) = 
    let dependencyMap = Map.ofSeq (packages |> Seq.map (fun p -> p.Name, p.VersionRange))
    member __.DirectDependencies = dependencyMap
    member __.Packages = packages
    member __.RemoteFiles = remoteFiles
    member __.Resolve(force, discovery : IDiscovery) = PackageResolver.Resolve(force, discovery, packages)
    static member FromCode(code:string) : DependenciesFile = 
        DependenciesFile(DependenciesFileParser.parseDependenciesFile <| code.Replace("\r\n","\n").Replace("\r","\n").Split('\n'))
    static member ReadFromFile fileName : DependenciesFile = 
        DependenciesFile(DependenciesFileParser.parseDependenciesFile <| File.ReadAllLines fileName)
