module Paket.RemoteUpload

open System
open System.IO
open Paket
open Paket.Logging

let Push url apiKey package =
    verbosefn "Pushing package %s to %s" package url
    let client = Utils.createWebClient(url, None)
    client.Headers.Add("X-NuGet-ApiKey", apiKey)
    client.UploadFile(Uri(url + "/api/v2/package"), "PUT", package)
    |> ignore
    verbosefn "Pushing %s complete." package

let PushAll root url apiKey =
    Directory.GetFiles(root, "*.nupkg", SearchOption.AllDirectories)
    |> Seq.iter (fun p -> Push url apiKey <| Path.Combine(root, p))