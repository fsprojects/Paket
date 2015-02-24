module Paket.RemoteUpload

open System
open System.Globalization
open System.IO
open System.Net
open System.Text
open Paket
open Paket.Logging

type System.Net.WebClient with
        member x.UploadFileAsMultipart (url:Uri) filename =
            let fileTemplate = "--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\"\r\nContent-Type: {3}\r\n\r\n"
            let boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x", CultureInfo.InvariantCulture)
            let fileInfo = (new FileInfo(Path.GetFullPath(filename)))
            let fileHeaderBytes = String.Format(CultureInfo.InvariantCulture, fileTemplate, boundary, "package", "package", "application/octet-stream")
                                  |> Encoding.UTF8.GetBytes
            let newlineBytes = Environment.NewLine |> Encoding.UTF8.GetBytes
            let trailerbytes = String.Format(CultureInfo.InvariantCulture, "--{0}--", boundary) |> Encoding.UTF8.GetBytes
            x.Headers.Add(HttpRequestHeader.ContentType, "multipart/form-data; boundary=" + boundary);
            use stream = x.OpenWrite(url, "PUT")
            stream.Write(fileHeaderBytes,0,fileHeaderBytes.Length)
            use fileStream = File.OpenRead fileInfo.FullName
            fileStream.CopyTo(stream, (4*1024))
            stream.Write(newlineBytes, 0, newlineBytes.Length)
            stream.Write(trailerbytes, 0, trailerbytes.Length)
            ()
            
let Push maxTrials url apiKey packageFileName =
    let rec push trial =
        tracefn "Pushing package %s to %s - trial %d" packageFileName url trial
        try
            let url = if url.Contains("nuget.org") then url + "/api/v2/package" else url
            let client = Utils.createWebClient(url, None)
            client.Headers.Add("X-NuGet-ApiKey", apiKey)
            client.UploadFileAsMultipart (new Uri(url)) packageFileName
            |> ignore
            tracefn "Pushing %s complete." packageFileName
        with
        | exn when trial < maxTrials ->             
            traceWarnfn "Could not push %s: %s" packageFileName exn.Message            
            push (trial + 1)

    push 1