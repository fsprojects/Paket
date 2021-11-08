[<AutoOpen>]
/// Contains methods for Network IO.
module Paket.NetUtils

open System
open System.IO
open System.Net
open System.Text
open Paket
open Paket.Logging
open Paket.Constants
open Chessie.ErrorHandling
open Paket.Domain
open Paket.Utils
open FSharp.Polyfill

open System.Net.Http
open System.Threading
open Microsoft.FSharp.Core.Printf
open System.Threading.Tasks

let private requestTimeoutInMs = 10 * 60 * 1000
let private uploadRequestTimeoutInMs = 20 * 60 * 1000

let internal isRequestEnvVarSet = Environment.GetEnvironmentVariable("PAKET_DEBUG_REQUESTS") = "true"

type AuthType = | Basic | NTLM
type UserPassword = { Username: string; Password: string; Type : AuthType }
type Auth =
    | Credentials of UserPass : UserPassword
    | Token of string

/// Credentials Provider, the paramter indicates whether we need to retry, because some previous request failed
type AuthProvider =
    abstract Retrieve : isRetry:bool -> Auth option
module AuthProvider =
    let ofFunction f =
        { new AuthProvider with
            member x.Retrieve isRetry = f isRetry }
    let ofUserPasswordFunction f =
        ofFunction (f >> Option.map Credentials)

    let combine providers =
        ofFunction (fun isRetry ->
            providers
            |> Seq.tryPick (fun (p:AuthProvider) -> p.Retrieve isRetry))
    let retrieve isRetry (auth:AuthProvider) =
        auth.Retrieve isRetry
    let empty = ofFunction(fun _ -> None)
    let ofUserPassword userPass = ofUserPasswordFunction (fun _ -> Some userPass)
    let ofAuth auth = ofFunction (fun _ -> Some auth)

let internal parseAuthTypeString (str:string) =
    match str.Trim().ToLowerInvariant() with
        | "ntlm" -> AuthType.NTLM
        | _ -> AuthType.Basic

let normalizeFeedUrl (source:string) =
    match source.TrimEnd([|'/'|]) with
    | "https://api.nuget.org/v3/index.json" -> Constants.DefaultNuGetV3Stream
    | "http://api.nuget.org/v3/index.json" -> Constants.DefaultNuGetV3Stream.Replace("https","http")
    | "https://nuget.org/api/v2" -> Constants.DefaultNuGetStream
    | "http://nuget.org/api/v2" -> Constants.DefaultNuGetStream.Replace("https","http")
    | "https://www.nuget.org/api/v2" -> Constants.DefaultNuGetStream
    | "http://www.nuget.org/api/v2" -> Constants.DefaultNuGetStream.Replace("https","http")
    | source -> source

#if CUSTOM_WEBPROXY
type WebProxy = IWebProxy
#endif

let envProxies () =
    let getEnvValue (name:string) =
        let v = Environment.GetEnvironmentVariable(name.ToUpperInvariant())
        // under mono, env vars are case sensitive
        if isNull v then Environment.GetEnvironmentVariable(name.ToLowerInvariant()) else v
    let bypassList =
        let noproxyString = getEnvValue "NO_PROXY"
        let noproxy = if not (String.IsNullOrEmpty noproxyString) then System.Text.RegularExpressions.Regex.Escape(noproxyString).Replace(@"*", ".*")  else noproxyString

        if String.IsNullOrEmpty noproxy then [||] else
        noproxy.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)
    let getCredentials (uri:Uri) =
        let userPass = uri.UserInfo.Split([| ':' |], 2)
        if userPass.Length <> 2 || userPass.[0].Length = 0 then None else
        let credentials = NetworkCredential(Uri.UnescapeDataString userPass.[0], Uri.UnescapeDataString userPass.[1])
        Some credentials

    let getProxy (scheme:string) =
        let envVarName = sprintf "%s_PROXY" (scheme.ToUpperInvariant())
        let envVarValue = getEnvValue envVarName
        if isNull envVarValue then None else
        match Uri.TryCreate(envVarValue, UriKind.Absolute) with
        | true, envUri ->
#if CUSTOM_WEBPROXY
            Some
                { new IWebProxy with
                    member __.Credentials
                        with get () = (Option.toObj (getCredentials envUri)) :> ICredentials
                        and set value = ()
                    member __.GetProxy _ =
                        Uri (sprintf "http://%s:%d" envUri.Host envUri.Port)
                    member __.IsBypassed (host : Uri) =
                        Array.contains (string host) bypassList
                }
#else
            let proxy = WebProxy (Uri (sprintf "http://%s:%d" envUri.Host envUri.Port))
            proxy.Credentials <- Option.toObj (getCredentials envUri)
            proxy.BypassProxyOnLocal <- true
            proxy.BypassList <- bypassList
            Some proxy
#endif
        | _ -> None

    let addProxy (map:Map<string, WebProxy>) scheme =
        match getProxy scheme with
        | Some p -> Map.add scheme p map
        | _ -> map

    [ "http"; "https" ]
    |> List.fold addProxy Map.empty

let calcEnvProxies = lazy (envProxies())

let getDefaultProxyFor =
    memoize
      (fun (url:string) ->
            let uri = Uri url
            let getDefault () =
#if CUSTOM_WEBPROXY
                let result =
                    { new IWebProxy with
                        member __.Credentials
                            with get () = null
                            and set _value = ()
                        member __.GetProxy _ = null
                        member __.IsBypassed (_host : Uri) = true
                    }
#else
                let result = WebRequest.GetSystemWebProxy()
#endif
#if CUSTOM_WEBPROXY
                let proxy = result
#else
                let address = result.GetProxy uri
                if address = uri then null else
                let proxy = WebProxy address
                proxy.BypassProxyOnLocal <- true
#endif
                proxy.Credentials <- CredentialCache.DefaultCredentials
                proxy

            match calcEnvProxies.Force().TryFind uri.Scheme with
            | Some p -> if p.GetProxy uri <> uri then p else getDefault()
            | None -> getDefault())


type RequestFailedInfo =
    { StatusCode:HttpStatusCode
      Content:Stream
      MediaType:string option
      Url:string }
    static member ofResponse (resp:HttpResponseMessage) = async {
        let mem = new MemoryStream()
        if not (isNull resp.Content) then
            do! resp.Content.CopyToAsync(mem) |> Async.AwaitTaskWithoutAggregate
        mem.Position <- 0L

        let mediaType =
            resp.Content
            |> Option.ofObj
            |> Option.bind (fun c -> c.Headers |> Option.ofObj)
            |> Option.bind (fun h -> h.ContentType |> Option.ofObj)
            |> Option.bind (fun c -> c.MediaType |> Option.ofObj)
        return
            { StatusCode = resp.StatusCode
              Content = mem
              MediaType = mediaType
              Url = resp.RequestMessage.RequestUri.ToString() } }
    override x.ToString() =
        use ms = new MemoryStream() // .lol
        x.Content.CopyTo ms
        ms.Seek(0L, SeekOrigin.Begin) |> ignore
        let contents = Encoding.UTF8.GetString(ms.ToArray())
        sprintf "Request to '%s' failed with: %i %A — %s" x.Url (int x.StatusCode) x.StatusCode contents

/// Exception for request errors
#if !NETSTANDARD1_6
[<System.Serializable>]
#endif
type RequestFailedException =
    val private info : RequestFailedInfo option
    inherit Exception
    new (msg:string, inner:exn) = {
      inherit Exception(msg, inner)
      info = None }
    new (info:RequestFailedInfo, inner:exn) = {
      inherit Exception(info.ToString(), inner)
      info = Some info }
#if !NETSTANDARD1_5
    new (info:System.Runtime.Serialization.SerializationInfo, context:System.Runtime.Serialization.StreamingContext) = {
      inherit Exception(info, context)
      info = None
    }
#endif
    member x.Info with get () = x.info
    member x.Wrap() =
        match x.info with
        | Some info ->
            RequestFailedException(info, x:>exn)
        | None ->
            RequestFailedException(x.Message, x:>exn)

let failIfNoSuccess (resp:HttpResponseMessage) = async {
    if not resp.IsSuccessStatusCode then
        if verbose then
            tracefn "Request failed with '%d': '%s'" (int resp.StatusCode) (resp.RequestMessage.RequestUri.ToString())
        let! info = RequestFailedInfo.ofResponse resp
        raise (RequestFailedException(info, null))
    () }

let rec requestStatus (ex:Exception) =
    match ex with
    | null -> None
    | :? RequestFailedException as rfex ->
        match rfex.Info with
        | Some info -> Some info.StatusCode
        | _ -> None
    | :? WebException as wfex ->
        match wfex.Response with
        | :? HttpWebResponse as webresp ->
            Some webresp.StatusCode
        | _ -> None
    | ex -> requestStatus ex.InnerException

// active pattern for nested HttpStatusCode
let (|RequestStatus|_|) (ex:Object) =
    match ex with
    | :? Exception as except -> requestStatus except
    | :? HttpWebResponse as resp -> Some resp.StatusCode
    | :? RequestFailedInfo as info -> Some info.StatusCode
    | null -> None
    | _ -> None


#if USE_WEB_CLIENT_FOR_UPLOAD
type System.Net.WebClient with
    member x.UploadFileAsMultipart (url : Uri) filename =
        let fileTemplate =
            "--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\"\r\nContent-Type: {3}\r\n\r\n"
        let boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x", System.Globalization.CultureInfo.InvariantCulture)
        let fileInfo = (new FileInfo(Path.GetFullPath(filename)))
        let fileHeaderBytes =
            System.String.Format
                (System.Globalization.CultureInfo.InvariantCulture, fileTemplate, boundary, "package", "package", "application/octet-stream")
            |> Encoding.UTF8.GetBytes
        // we use a windows-style newline rather than Environment.NewLine for compatibility
        let newlineBytes = "\r\n" |> Encoding.UTF8.GetBytes
        let trailerbytes = String.Format(System.Globalization.CultureInfo.InvariantCulture, "--{0}--", boundary) |> Encoding.UTF8.GetBytes
        x.Headers.Add(HttpRequestHeader.ContentType, "multipart/form-data; boundary=" + boundary)
        use stream = x.OpenWrite(url, "PUT")
        stream.Write(fileHeaderBytes, 0, fileHeaderBytes.Length)
        use fileStream = File.OpenRead fileInfo.FullName
        fileStream.CopyTo(stream, (4 * 1024))
        stream.Write(newlineBytes, 0, newlineBytes.Length)
        stream.Write(trailerbytes, 0, trailerbytes.Length)
        stream.Write(newlineBytes, 0, newlineBytes.Length)
        ()
#endif

type HttpClient with
    member x.DownloadFileTaskAsync (uri : Uri, tok : CancellationToken, filePath : string) =
      async {
        if uri.Scheme = "file" then
            File.Copy(uri.AbsolutePath, filePath, true)
        else
            let! response = x.GetAsync(uri, tok) |> Async.AwaitTaskWithoutAggregate
            do! failIfNoSuccess response
            use fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None)
            do! response.Content.CopyToAsync(fileStream) |> Async.AwaitTaskWithoutAggregate
            fileStream.Flush()
      } |> Async.StartAsTask
    member x.DownloadFileTaskAsync (uri : string, tok : CancellationToken, filePath : string) = x.DownloadFileTaskAsync(Uri uri, tok, filePath)
    member x.DownloadFile (uri : string, filePath : string) =
        x.DownloadFileTaskAsync(uri, CancellationToken.None, filePath).GetAwaiter().GetResult()
    member x.DownloadFile (uri : Uri, filePath : string) =
        x.DownloadFileTaskAsync(uri, CancellationToken.None, filePath).GetAwaiter().GetResult()
    member x.DownloadStringTaskAsync (uri : Uri, tok : CancellationToken) =
      async {
        let! response = x.GetAsync(uri, tok) |> Async.AwaitTaskWithoutAggregate
        do! failIfNoSuccess response
        let! result = response.Content.ReadAsStringAsync() |> Async.AwaitTaskWithoutAggregate
        return result
      } |> Async.StartAsTask
    member x.DownloadStringTaskAsync (uri : string, tok : CancellationToken) = x.DownloadStringTaskAsync(Uri uri, tok)
    member x.DownloadString (uri : string) =
        x.DownloadStringTaskAsync(uri, CancellationToken.None).GetAwaiter().GetResult()
    member x.DownloadString (uri : Uri) =
        x.DownloadStringTaskAsync(uri, CancellationToken.None).GetAwaiter().GetResult()

    member x.DownloadDataTaskAsync(uri : Uri, tok : CancellationToken) =
      async {
        let! response = x.GetAsync(uri, tok) |> Async.AwaitTaskWithoutAggregate
        do! failIfNoSuccess response
        let! result = response.Content.ReadAsByteArrayAsync() |> Async.AwaitTaskWithoutAggregate
        return result
      } |> Async.StartAsTask
    member x.DownloadDataTaskAsync (uri : string, tok : CancellationToken) = x.DownloadDataTaskAsync(Uri uri, tok)
    member x.DownloadData(uri : string) =
        x.DownloadDataTaskAsync(uri, CancellationToken.None).GetAwaiter().GetResult()
    member x.DownloadData(uri : Uri) =
        x.DownloadDataTaskAsync(uri, CancellationToken.None).GetAwaiter().GetResult()

    member x.UploadFileAsMultipart (url : Uri) filename =
        let fileTemplate =
            "--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\"\r\nContent-Type: {3}\r\n\r\n"
        let boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x", System.Globalization.CultureInfo.InvariantCulture)
        let fileInfo = (new FileInfo(Path.GetFullPath(filename)))
        let fileHeaderBytes =
            System.String.Format
                (System.Globalization.CultureInfo.InvariantCulture, fileTemplate, boundary, "package", "package", "application/octet-stream")
            |> Encoding.UTF8.GetBytes
        let newlineBytes = Environment.NewLine |> Encoding.UTF8.GetBytes
        let trailerbytes = String.Format(System.Globalization.CultureInfo.InvariantCulture, "--{0}--", boundary) |> Encoding.UTF8.GetBytes
        use stream = new MemoryStream() // x.OpenWrite(url, "PUT")
        stream.Write(fileHeaderBytes, 0, fileHeaderBytes.Length)
        use fileStream = File.OpenRead fileInfo.FullName
        fileStream.CopyTo(stream, (4 * 1024))
        stream.Write(newlineBytes, 0, newlineBytes.Length)
        stream.Write(trailerbytes, 0, trailerbytes.Length)
        stream.Write(newlineBytes, 0, newlineBytes.Length)
        stream.Position <- 0L
        use content = new StreamContent(stream)
        content.Headers.Add("Content-Type", "multipart/form-data; boundary=" + boundary)
        let result = x.PutAsync(url, content).GetAwaiter().GetResult()
        failIfNoSuccess result |> Async.RunSynchronously
        result

let internal addAcceptHeader (client:HttpClient) (contentType:string) =
    for headerVal in contentType.Split([|','|], System.StringSplitOptions.RemoveEmptyEntries) do
        client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(headerVal))
let internal addHeader (client:HttpClient) (headerKey:string) (headerVal:string) =
    client.DefaultRequestHeaders.Add(headerKey, headerVal)

let useDefaultHandler =
    match Environment.GetEnvironmentVariable("PAKET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER") with
    | null -> false
    | env ->
        let env = env.ToLowerInvariant()
        env = "true" || env = "yes" || env = "y"


let createHttpHandlerRaw(url, auth: Auth option) : HttpMessageHandler =
    let proxy = getDefaultProxyFor url

    if isWindows && not useDefaultHandler then
        // See https://github.com/dotnet/corefx/issues/31098
        let handler = new WinHttpHandler(Proxy = proxy)
        handler.AutomaticDecompression <- DecompressionMethods.GZip ||| DecompressionMethods.Deflate
        handler.MaxConnectionsPerServer <- 4

        match auth with
        | None -> handler.ServerCredentials <- CredentialCache.DefaultCredentials
        | Some(Credentials({Username = username; Password = password; Type = AuthType.Basic})) ->
            // handled via defaultrequestheaders
            ()
        | Some(Credentials({Username = username; Password = password; Type = AuthType.NTLM})) ->
            let cred = System.Net.NetworkCredential(username,password)
            handler.ServerCredentials <- cred.GetCredential(new Uri(url), "NTLM")
        | Some(Token token) ->
            // handled via defaultrequestheaders
            ()
        // from https://github.com/dotnet/corefx/blob/b6b9a1ad24339266a27fef826233dbbe192cf254/src/System.Net.Http/src/System/Net/Http/HttpClientHandler.Windows.cs#L454-L477
        if isNull handler.Proxy then
            handler.WindowsProxyUsePolicy <- WindowsProxyUsePolicy.UseWinInetProxy
        else
            handler.WindowsProxyUsePolicy <- WindowsProxyUsePolicy.UseCustomProxy
        handler :> _
    else
    let handler =
        new HttpClientHandler(
            UseProxy = true,
            Proxy = getDefaultProxyFor url)
    handler.AutomaticDecompression <- DecompressionMethods.GZip ||| DecompressionMethods.Deflate
#if !NO_MAXCONNECTIONPERSERVER
    handler.MaxConnectionsPerServer <- 4
#endif
    match auth with
    | None -> handler.UseDefaultCredentials <- true
    | Some(Credentials({Username = username; Password = password; Type = AuthType.Basic})) ->
        // handled via defaultrequestheaders
        ()
    | Some(Credentials({Username = username; Password = password; Type = AuthType.NTLM})) ->
        let cred = System.Net.NetworkCredential(username,password)
        handler.Credentials <- cred.GetCredential(new Uri(url), "NTLM")
    | Some(Token token) ->
        // handled via defaultrequestheaders
        ()
    handler.UseProxy <- true
    handler :> _

let createHttpHandler = 
    memoizeBy 
        // Truncates the url to only to host part, so there is only one handler per source/host.
        // 8 chars as startindex is chosen because `https://` is 8 chars long and we need the host delimiting `/`.
        // For instance `https://api.nuget.org/v3/index.json` and `https://api.nuget.org/v3-flatcontainer/fsharp.core/index.json?semVerLevel=2.0.0` both get truncated to `https://api.nuget.org/` and share one handler (and one connection pool).
        (fun (url : string, auth) -> url.Substring(0, url.IndexOf('/', 8) + 1), auth) 
        createHttpHandlerRaw 

let private paketVersion = 
    let attrs = System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(false)

    attrs
    |> Seq.pick (fun a -> match a with | :? System.Reflection.AssemblyInformationalVersionAttribute as i -> Some i.InformationalVersion | _ -> None)

let createHttpClient (url, auth:Auth option) : HttpClient =
    let handler = createHttpHandler (url, auth)
    let client = new HttpClient(handler)
    match auth with
    | None ->
        // handled in handler
        ()
    | Some(Credentials({Username = username; Password = password; Type = AuthType.Basic})) ->
        // http://stackoverflow.com/questions/16044313/webclient-httpwebrequest-with-basic-authentication-returns-404-not-found-for-v/26016919#26016919
        //this works ONLY if the server returns 401 first
        //client DOES NOT send credentials on first request
        //ONLY after a 401
        //client.Credentials <- new NetworkCredential(auth.Username,auth.Password)

        //so use THIS instead to send credentials RIGHT AWAY
        let credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(username + ":" + password))
        client.DefaultRequestHeaders.Authorization <-
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials)
    | Some(Credentials({Username = username; Password = password; Type = AuthType.NTLM})) ->
        // handled in handler
        ()
    | Some(Token token) ->
        client.DefaultRequestHeaders.Authorization <-
            new System.Net.Http.Headers.AuthenticationHeaderValue("token", token)
    client.DefaultRequestHeaders.Add("user-agent", sprintf "Paket (%s)" paketVersion)
    client

#if USE_WEB_CLIENT_FOR_UPLOAD
type CustomTimeoutWebClient(timeout) =
    inherit WebClient()
    override x.GetWebRequest (uri:Uri) =
        let w = base.GetWebRequest(uri)
        w.Timeout <- timeout
        w

let createWebClient (url,auth:Auth option) =
    let client = new CustomTimeoutWebClient(uploadRequestTimeoutInMs)
    client.Headers.Add("User-Agent", "Paket")
    client.Proxy <- getDefaultProxyFor url

    match auth with
    | Some (Credentials({Username = username; Password = password; Type = AuthType.Basic})) ->
        // htttp://stackoverflow.com/questions/16044313/webclient-httpwebrequest-with-basic-authentication-returns-404-not-found-for-v/26016919#26016919
        //this works ONLY if the server returns 401 first
        //client DOES NOT send credentials on first request
        //ONLY after a 401
        //client.Credentials <- new NetworkCredential(auth.Username,auth.Password)

        //so use THIS instead to send credentials RIGHT AWAY
        let credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password))
        client.Headers.[HttpRequestHeader.Authorization] <- sprintf "Basic %s" credentials
        client.Credentials <- new NetworkCredential(username,password)
    | Some (Credentials{Username = username; Password = password; Type = AuthType.NTLM}) ->
        let cred = NetworkCredential(username,password)
        client.Credentials <- cred.GetCredential(new Uri(url), "NTLM")
    | Some (Token token) ->
        client.Headers.[HttpRequestHeader.Authorization] <- sprintf "token %s" token
    | None ->
        client.UseDefaultCredentials <- true
    client
#endif

#nowarn "40"

open System.Diagnostics
open System.Collections.Generic
open System.Runtime.ExceptionServices


let private resolveAuth (auth: AuthProvider) doRequest =
    async {
        try return! doRequest (auth.Retrieve false)
        with
        | :? RequestFailedException as w ->
            match w.Info with
            | Some { StatusCode = HttpStatusCode.Unauthorized } ->
                return! doRequest (auth.Retrieve false)
            | _ ->
                return raise <| w.Wrap()
    }

/// [omit]
let downloadFromUrlWithTimeout (auth:AuthProvider, url : string) (timeout:TimeSpan option) (filePath: string) =
    let doRequest auth = async {
        let client = createHttpClient (url,auth)
        if timeout.IsSome then
            client.Timeout <- timeout.Value
        let! tok = Async.CancellationToken
        if verbose then
            verbosefn "Starting download from '%O'" url
        use _ = Profile.startCategory Profile.Category.NuGetDownload
        let task = client.DownloadFileTaskAsync (Uri url, tok, filePath) |> Async.AwaitTaskWithoutAggregate
        do! task
    }
    async {
        try return! resolveAuth auth doRequest
        with
        | exn ->
            raise (Exception(sprintf "Could not download from '%s'" url, exn))
    }

/// [omit]
let downloadFromUrl (auth:AuthProvider, url : string) (filePath: string) =
    downloadFromUrlWithTimeout (auth, url) None filePath


/// [omit]
let getFromUrl (auth:AuthProvider, url : string, contentType : string) =
    let uri = Uri url
    let doRequest auth = async {
        let client = createHttpClient(url,auth)
        let! tok = Async.CancellationToken
        if notNullOrEmpty contentType then
            addAcceptHeader client contentType

        if verbose then
            verbosefn "Starting request to '%O'" url
        use _ = Profile.startCategory Profile.Category.NuGetRequest

        return! client.DownloadStringTaskAsync (uri, tok) |> Async.AwaitTaskWithoutAggregate
    }

    async {
        try return! resolveAuth auth doRequest
        with exn ->
            return raise (Exception(sprintf "Could not retrieve data from '%s'" url, exn))

    }

let getXmlFromUrl (auth:AuthProvider, url : string) =
    let doRequest auth = async {
        let client = createHttpClient (url,auth)
        let! tok = Async.CancellationToken
        // mimic the headers sent from nuget client to odata/ endpoints
        addAcceptHeader client "application/atom+xml, application/xml"
        addHeader client "AcceptCharset" "UTF-8"
        addHeader client "DataServiceVersion" "1.0;NetFx"
        addHeader client "MaxDataServiceVersion" "2.0;NetFx"
        if verbose then
            verbosefn "Starting request to '%O'" url
        use _ = Profile.startCategory Profile.Category.NuGetRequest
        return! client.DownloadStringTaskAsync (Uri url, tok) |> Async.AwaitTaskWithoutAggregate
    }
    async {
        try return! resolveAuth auth doRequest
        with
        | exn ->
            return raise (Exception(sprintf "Could not retrieve data from '%s'" url, exn))
    }

type SafeWebResult<'a> =
    | NotFound
    | Unauthorized
    | SuccessResponse of 'a
    | UnknownError of ExceptionDispatchInfo
module SafeWebResult =
    let map f s =
        match s with
        | SuccessResponse r -> SuccessResponse (f r)
        | UnknownError err -> UnknownError err
        | NotFound -> NotFound
        | Unauthorized -> Unauthorized
    let asResult s =
        match s with
        | NotFound ->
            let notFound = Exception("Request returned 404")
            ExceptionDispatchInfo.Capture notFound
            |> FSharp.Core.Result.Error
        | Unauthorized ->
            let unauthorized = Exception("Request returned 401")
            ExceptionDispatchInfo.Capture unauthorized
            |> FSharp.Core.Result.Error
        | UnknownError err -> FSharp.Core.Result.Error err
        | SuccessResponse s -> FSharp.Core.Result.Ok s

let rec private _safeGetFromUrl (auth:Auth option, url : string, contentType : string, iTry, nTries) =
    let canRetry = iTry < nTries

    async {
        let uri = Uri url
        let! tok = Async.CancellationToken
        let tokSource = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(tok)
        try
            let client = createHttpClient (url,auth)
            tokSource.CancelAfter(requestTimeoutInMs)

            if notNullOrEmpty contentType then
                addAcceptHeader client contentType

            if verbose then
                verbosefn "Starting request to '%O'" uri
            use _ = Profile.startCategory Profile.Category.NuGetRequest
            let! raw = client.DownloadStringTaskAsync(uri, tokSource.Token) |> Async.AwaitTaskWithoutAggregate
            return SuccessResponse raw
        with
        | :? RequestFailedException as w ->
            match w.Info with
            | Some { StatusCode = HttpStatusCode.NotFound } -> return NotFound
            | Some { StatusCode = HttpStatusCode.Unauthorized } -> return Unauthorized
            | _ ->
                if verbose || isRequestEnvVarSet then
                    Logging.verbosefn "Error while retrieving '%s': %O" url w
                return UnknownError (ExceptionDispatchInfo.Capture w)
        | :? HttpRequestException as inner when not (isNull inner.InnerException) && inner.InnerException.Message.Contains("12002") ->
            if canRetry then
                // Timeout reached
                if verbose || isRequestEnvVarSet then
                    Logging.traceWarnfn "Request failed with strange HttpRequestException. Trying again, this was try %i/%i. Error was %O" iTry nTries inner
                else
                    Logging.traceWarnfn "Request failed with strange HttpRequestException. Trying again, this was try %i/%i." iTry nTries
                return! _safeGetFromUrl(auth, url, contentType, iTry + 1, nTries)
            else
                return UnknownError (ExceptionDispatchInfo.Capture (new exn(sprintf "Request to '%O' failed with strange HttpRequestException" uri, inner)))
        | inner when tokSource.IsCancellationRequested && not tok.IsCancellationRequested ->
            if canRetry then
                // Timeout reached
                if verbose || isRequestEnvVarSet then
                    Logging.traceWarnfn "Request failed due to timeout (%d ms). Trying again, this was try %i/%i. Error was %O" requestTimeoutInMs iTry nTries inner
                else
                    Logging.traceWarnfn "Request failed due to timeout (%d ms). Trying again, this was try %i/%i." requestTimeoutInMs iTry nTries
                return! _safeGetFromUrl(auth, url, contentType, iTry + 1, nTries)
            else
                return UnknownError (ExceptionDispatchInfo.Capture (new TimeoutException(sprintf "Request to '%O' timed out" uri, inner)))
        | inner ->
            if verbose || isRequestEnvVarSet then
                Logging.traceWarnfn "Request to '%O' failed with unknown error: %O" uri inner
            return UnknownError (ExceptionDispatchInfo.Capture (exn(sprintf "Request to '%O' failed with unknown error (_safeGetFromUrl)" uri, inner)))
    }

/// [omit]
let safeGetFromUrl (auth:AuthProvider, url : string, contentType : string) = async {
    let! result = _safeGetFromUrl(auth.Retrieve false, url, contentType, 1, 10)
    match result with
    | Unauthorized ->
        return! _safeGetFromUrl(auth.Retrieve true, url, contentType, 1, 10)
    | _ ->
        return result }

let downloadStringSync (url : string) (client : HttpClient) =
    try
        client.DownloadString url |> ok
    with _ ->
        DownloadError url |> fail

let downloadFileSync (url : string) (fileName : string) (client : HttpClient) =
    tracefn "Downloading file from %s to %s" url fileName
    try
        client.DownloadFile(url, fileName) |> ok
    with _ ->
        DownloadError url |> fail
