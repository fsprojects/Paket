module FSharp.ProjectTemplate.ConfigDSL

open System
open System.IO
open System.Collections.Generic
open Microsoft.FSharp.Compiler.Interactive.Shell

let initialCode = """
let config = new System.Collections.Generic.Dictionary<string,string>()
let source x = ()  // Todo

let nuget x y = config.Add(x,y)
"""

let runConfig fileName  =
    let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()

    let commonOptions = [| "fsi.exe"; "--noninteractive" |]

    let sbOut = new Text.StringBuilder()  
    let sbErr = new Text.StringBuilder()  // TODO: evtl. irgendwo ausgeben
    let outStream = new StringWriter(sbOut)
    let errStream = new StringWriter(sbErr)

    let stdin = new StreamReader(Stream.Null)   
    
    try
        let session = FsiEvaluationSession.Create(fsiConfig, commonOptions, stdin, outStream, errStream)
       
        try 
            
            session.EvalInteraction initialCode |> ignore                    
            session.EvalScript fileName
            match session.EvalExpression "config" with
            | Some x -> x.ReflectionValue :?> System.Collections.Generic.Dictionary<string,string>
            | _ -> failwithf "Error: %s" <| sbErr.ToString()
        with    
        | _ -> failwithf "Error: %s" <| sbErr.ToString()
            
    with    
    | exn ->
        printfn "FsiEvaluationSession could not be created."
        
        raise exn    

// TODO make this correct        
let merge (config1:Dictionary<string,string>) (config2:Dictionary<string,string>) =

    let config = Dictionary<string,string>()
    for x in config1 do
      config.Add(x.Key,x.Value)
    
    for x in config2 do
      config.[x.Key] <- x.Value

    config

let (==>) c1 c2 = merge c1 c2