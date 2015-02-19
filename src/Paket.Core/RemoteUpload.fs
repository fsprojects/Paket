module Paket.RemoteUpload

open System
open System.IO
open Paket
open Paket.Logging

let Push maxTrials url apiKey packageFileName =
    let rec push trial =
        tracefn "Pushing package %s to %s - trial %d" packageFileName url trial
        try
            let client = Utils.createWebClient(url, None)
            client.Headers.Add("X-NuGet-ApiKey", apiKey)
            client.UploadFile(Uri(url + "/api/v2/package"), "PUT", packageFileName)
            |> ignore
            tracefn "Pushing %s complete." packageFileName
        with
        | exn when trial < maxTrials ->             
            traceWarnfn "Could not push %s: %s" packageFileName exn.Message            
            push (trial + 1)
        
    push 1    

let PushAll maxTrials root url apiKey =
    Directory.GetFiles(root, "*.nupkg", SearchOption.AllDirectories)
    |> Seq.iter (fun p -> Push maxTrials url apiKey <| Path.Combine(root, p))