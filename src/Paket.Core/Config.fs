namespace Paket

open System
open System.IO
open System.Collections.Generic
open Microsoft.FSharp.Compiler.Interactive.Shell
open Paket

// [omit]
module ConfigHelpers = 
    let initialCode = """
open System.Collections.Generic
let __nuget = new Dictionary<string,string*string*string>()
let mutable __nugetSource = ""

let source url = __nugetSource <- url

let nuget package version = __nuget.Add(package,("nuget",__nugetSource,version))
"""
    
    let parseVersionRange (text : string) : VersionRange = 
        // TODO: Make this pretty
        if text.StartsWith "~> " then 
            let min = text.Replace("~> ", "")
            let parts = min.Split('.')
            let major = Int32.Parse parts.[0]
            
            let newParts = 
                (major + 1).ToString() :: Seq.toList (parts
                                                      |> Seq.skip 1
                                                      |> Seq.map (fun _ -> "0"))
            VersionRange.Between(min, String.Join(".", newParts))
        else if text.StartsWith "= " then VersionRange.Exactly(text.Replace("= ", ""))
        else VersionRange.AtLeast(text)
    
    let formatVersionRange (version : VersionRange) = 
        match version with
        | Minimum v -> ">= " + v.ToString()
        | Specific v -> v.ToString()
        | Range(v1, v2) -> ">= " + v1.ToString() + ", < " + v2.ToString()
    
    let executeInScript (executeInScript : FsiEvaluationSession -> unit) = 
        let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
        let commonOptions = [| "fsi.exe"; "--noninteractive" |]
        let sbOut = new Text.StringBuilder()
        let sbErr = new Text.StringBuilder()
        let outStream = new StringWriter(sbOut)
        let errStream = new StringWriter(sbErr)
        let stdin = new StreamReader(Stream.Null)
        try 
            let session = FsiEvaluationSession.Create(fsiConfig, commonOptions, stdin, outStream, errStream)
            try 
                session.EvalInteraction initialCode |> ignore
                executeInScript session
                match session.EvalExpression "__nuget" with
                | Some value -> 
                    let dependencies = 
                        value.ReflectionValue :?> Dictionary<string, string * string * string> 
                        |> Seq.map (fun x -> 
                               let sourceType, source, version = x.Value
                               { Name = x.Key
                                 VersionRange = parseVersionRange version
                                 SourceType = sourceType
                                 Source = source })
                    dependencies
                | _ -> failwithf "Error: %s" <| sbErr.ToString()
            with _ -> failwithf "Error: %s" <| sbErr.ToString()
        with exn -> failwithf "FsiEvaluationSession could not be created. %s" <| sbErr.ToString()

/// Allows to parse and analyze packages.fsx files.
type Config(packages : Package seq) = 
    let packages = packages |> Seq.toList
    let dependencyMap = Map.ofSeq (packages |> Seq.map (fun p -> p.Name, p.VersionRange))
    member __.DirectDependencies = dependencyMap
    member __.Packages = packages
    member __.Resolve(discovery : IDiscovery) = Resolver.Resolve(discovery, packages)
    static member FromCode code : Config = 
        Config(ConfigHelpers.executeInScript (fun session -> session.EvalExpression code |> ignore))
    static member ReadFromFile fileName : Config = 
        Config(ConfigHelpers.executeInScript (fun session -> session.EvalScript fileName))
