module Paket.ConfigDSL

open System
open System.IO
open System.Collections.Generic
open Microsoft.FSharp.Compiler.Interactive.Shell
open Paket.DependencyGraph

type Config(dependencies : Dependency seq) =
    let dependencyMap = Map.ofSeq dependencies
    member __.DirectDependencies = dependencyMap
    member __.Resolve(discovery : IDiscovery) = Resolve(discovery, dependencies)

let initialCode = """
let config = new System.Collections.Generic.Dictionary<string,string>()
let source x = ()  // Todo

let nuget x y = config.Add(x,y)
"""

let private executeInScript (executeInScript : FsiEvaluationSession -> unit) : Config = 
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
            match session.EvalExpression "config" with
            | Some value -> 
                let dependencies =
                    value.ReflectionValue :?> Dictionary<string, string>
                    |> Seq.map (fun x -> x.Key,VersionRange.Parse x.Value)
                Config(dependencies)
            | _ -> failwithf "Error: %s" <| sbErr.ToString()
        with _ -> failwithf "Error: %s" <| sbErr.ToString()
    with exn -> failwithf "FsiEvaluationSession could not be created. %s" <| sbErr.ToString()

let FromCode code : Config = executeInScript (fun session -> session.EvalExpression code |> ignore)
let ReadFromFile fileName : Config = executeInScript (fun session -> session.EvalScript fileName)
