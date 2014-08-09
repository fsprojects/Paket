module Paket.ConfigDSL

open System
open System.IO
open System.Collections.Generic
open Microsoft.FSharp.Compiler.Interactive.Shell

type Version = {
    Min : string
    Max : string }
    with 
        static member Parse(text:string) : Version = 
            if text.StartsWith "~> " then
                // TODO: Make this pretty
                let min = text.Replace("~> ","")
                let parts = min.Split('.')
                let major = Int32.Parse parts.[0]
                let newParts = (major+1).ToString() :: Seq.toList (parts |> Seq.skip 1 |> Seq.map (fun _ -> "0"))
                { Min = min; Max = String.Join(".",newParts) }
            else
                { Min = text; Max = "" }

type ConfigValue = {
    Source : string
    Version : Version }

type Config = Map<string,ConfigValue>

let initialCode = """
let config = new System.Collections.Generic.Dictionary<string,string>()
let source x = ()  // Todo

let nuget x y = config.Add(x,y)
"""

let private executeInScript source (executeInScript:FsiEvaluationSession -> unit) : Config =
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
                value.ReflectionValue :?> Dictionary<string,string>
                |> Seq.fold (fun m x -> Map.add x.Key { Source = source; Version = Version.Parse x.Value } m) Map.empty

            | _ -> failwithf "Error: %s" <| sbErr.ToString()
        with    
        | _ -> failwithf "Error: %s" <| sbErr.ToString()
            
    with    
    | exn ->        
        failwithf "FsiEvaluationSession could not be created. %s" <| sbErr.ToString() 

let FromCode source code : Config = executeInScript source (fun session -> session.EvalExpression code |> ignore)
let ReadFromFile fileName : Config = executeInScript fileName (fun session -> session.EvalScript fileName)



// TODO make this correct        
let merge (config1:Config) (config2:Config) =
    config2
    |> Seq.fold (fun m x -> 
        match Map.tryFind x.Key m with
        | Some v ->  if v.Version > x.Value.Version then m else Map.add x.Key x.Value m
        | None ->    Map.add x.Key x.Value m

       ) config1

let (==>) c1 c2 = merge c1 c2
