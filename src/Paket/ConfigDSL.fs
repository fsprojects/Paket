module Paket.ConfigDSL

open System
open System.IO
open System.Collections.Generic
open Microsoft.FSharp.Compiler.Interactive.Shell

type Version =
| MinVersion of string
| SpecificVersion of string
| VersionRange of string*string
| Conflict of Version
    with 
        static member Between(min,max) : Version = VersionRange(min,max)
        static member Exactly version : Version = SpecificVersion version
        static member AtLeast version : Version = MinVersion version
        static member Parse(text:string) : Version = 
            // TODO: Make this pretty
            if text.StartsWith "~> " then                
                let min = text.Replace("~> ","")
                let parts = min.Split('.')
                let major = Int32.Parse parts.[0]
                let newParts = (major+1).ToString() :: Seq.toList (parts |> Seq.skip 1 |> Seq.map (fun _ -> "0"))
                Version.Between(min,String.Join(".",newParts))
            else
                if text.StartsWith "= " then
                    Version.Exactly(text.Replace("= ",""))
                else
                    Version.AtLeast text

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

let Shrink(version1,version2) = 
    match version1,version2 with
    | MinVersion v1, MinVersion v2 -> Version.AtLeast(max v1 v2)
    | MinVersion v1, SpecificVersion v2 when v2 >= v1 -> Version.Exactly v2
    | SpecificVersion v1, MinVersion v2 when v1 >= v2 -> Version.Exactly v1    
    | VersionRange(min1,max1), SpecificVersion v2 when min1 <= v2 && max1 > v2 -> Version.Exactly v2
    | SpecificVersion v1, VersionRange(min2,max2) when min2 <= v1 && max2 > v1-> Version.Exactly v1

// TODO make this correct        
let merge (config1:Config) (config2:Config) =
    config2
    |> Seq.fold (fun m x -> 
        match Map.tryFind x.Key m with
        | Some v ->  if v.Version > x.Value.Version then m else Map.add x.Key x.Value m
        | None ->    Map.add x.Key x.Value m

       ) config1

let (==>) c1 c2 = merge c1 c2
