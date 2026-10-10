module Paket.RemoteUpload

open System
open System.Globalization
open System.IO
open System.Net
open System.Text
open Paket
open Paket.Logging

let GetUrlWithEndpoint (url: string option) (endPoint: string option) =
    let (|UrlWithEndpoint|_|) url =
        match url with
        | Some url when not (String.IsNullOrEmpty(Uri(url).AbsolutePath.TrimStart('/'))) -> Some(Uri(url))
        | _                                                                              -> None

    let (|IsUrl|_|) (url: string option) =
        match url with
        | Some url -> Uri(url.TrimEnd('/') + "/") |> Some
        | _        -> None

    let defaultEndpoint = "/api/v2/package"
    let urlWithEndpoint =
        match (url, endPoint) with
        | None                   , _                   -> Uri(Uri("https://nuget.org"), defaultEndpoint)
        | IsUrl baseUrl          , Some customEndpoint -> Uri(baseUrl, customEndpoint.TrimStart('/'))
        | UrlWithEndpoint baseUrl, _                   -> baseUrl
        | IsUrl baseUrl          , None                -> Uri(baseUrl, defaultEndpoint)
        | Some whyIsThisNeeded   , _                   -> failwith "Url and endpoint combination not supported"
    urlWithEndpoint.ToString ()

let Push maxTrials url apiKey clientVersion packageFileName ignoreConflicts =
    let tracefnVerbose m = Printf.kprintf traceVerbose m
    let rec push trial =
        if not (File.Exists packageFileName) then
            failwithf "The package file %s does not exist." packageFileName
        tracefn "Pushing package %s to %s - trial %d" packageFileName url trial

        try
            let authOpt = AuthService.GetGlobalAuthenticationProvider(url).Retrieve (1 = trial)
            match authOpt with
            | Some (Auth.Credentials {Username = u}) ->
                tracefnVerbose "Authorizing using credentials for user %s" u
            | Some (Auth.Token _) ->
                tracefnVerbose "Authorizing using token"
            | None ->
                tracefnVerbose "No authorization found in config file."
            let uploadWithHttpClient () =
                let client = NetUtils.createHttpClient(url, authOpt)
                NetUtils.addHeader client "X-NuGet-ApiKey" apiKey
                NetUtils.addHeader client "X-NuGet-Client-Version" clientVersion // see https://github.com/NuGet/NuGetGallery/issues/4315

                client.UploadFileAsMultipart (new Uri(url)) packageFileName
                    |> ignore

            uploadWithHttpClient()
            tracefn "Pushing %s complete." packageFileName
        with
        | :? RequestFailedException as rfe when rfe.Info.IsSome && rfe.Info.Value.StatusCode = HttpStatusCode.Conflict && ignoreConflicts ->
            tracefn "Package %s already exists. - Conflict (409) status ignored!" packageFileName
        | exn when exn.Message.Contains("(409)") && ignoreConflicts ->
            tracefn "Package %s already exists. - Conflict (409) status ignored!" packageFileName
        | :? RequestFailedException as rfe when rfe.Info.IsSome && rfe.Info.Value.StatusCode = HttpStatusCode.Conflict ->
            rethrowf Exception rfe "Package %s already exists" packageFileName
        | exn when exn.Message.Contains("(409)") ->
            failwithf "Package %s already exists." packageFileName
        | exn when trial < maxTrials ->
            match exn with
            | :? RequestFailedException as rfe ->
                match rfe.Info with
                | Some info ->
                    use reader = new StreamReader(info.Content, Encoding.UTF8)
                    let text = reader.ReadToEnd()
                    tracefnVerbose "Response body was: %s" text
                | None -> ()
            | _ -> ()
            traceWarnfn "Could not push %s: %s" packageFileName exn.Message
            push (trial + 1)
    push 1
