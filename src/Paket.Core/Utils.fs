[<AutoOpen>]
/// Contains methods for IO.
module Paket.Utils

open System
open System.IO
open System.Net
open System.Xml
open System.Text
open Paket
open Paket.Logging
open Chessie.ErrorHandling
open Paket.Domain

let acceptXml = "application/atom+xml,application/xml"
let acceptJson = "application/atom+json,application/json"

let notNullOrEmpty = not << System.String.IsNullOrEmpty

type Auth = 
    | Credentials of Username : string * Password : string
    | Token of string

let TimeSpanToReadableString(span:TimeSpan) =
    let pluralize x = if x = 1 then String.Empty else "s"
    let notZero x y = if x > 0 then y else String.Empty
    let days = notZero (span.Duration().Days)  <| String.Format("{0:0} day{1}, ", span.Days, pluralize span.Days)
    let hours = notZero (span.Duration().Hours) <| String.Format("{0:0} hour{1}, ", span.Hours, pluralize span.Hours) 
    let minutes = notZero (span.Duration().Minutes) <| String.Format("{0:0} minute{1}, ", span.Minutes, pluralize span.Minutes)
    let seconds = notZero (span.Duration().Seconds) <| String.Format("{0:0} second{1}", span.Seconds, pluralize span.Seconds) 

    let formatted = String.Format("{0}{1}{2}{3}", days, hours, minutes, seconds)

    let formatted = if formatted.EndsWith(", ") then formatted.Substring(0, formatted.Length - 2) else formatted

    if String.IsNullOrEmpty(formatted) then "0 seconds" else formatted

let GetHomeDirectory() =
    if (Environment.OSVersion.Platform = PlatformID.Unix || Environment.OSVersion.Platform = PlatformID.MacOSX) then
        Environment.GetEnvironmentVariable("HOME")
    else
        Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%")

type PathReference =
    | AbsolutePath of string
    | RelativePath of string

let normalizeLocalPath(path:string) =
    if path.StartsWith("~/") then
        AbsolutePath (Path.Combine(GetHomeDirectory(), path.Substring(2)))
    else if Path.IsPathRooted(path) then
        AbsolutePath path
    else
        RelativePath path
        
let getDirectoryInfo pathInfo root =
    match pathInfo with
            | AbsolutePath s -> DirectoryInfo(s)
            | RelativePath s -> DirectoryInfo(Path.Combine(root, s))
        
/// Creates a directory if it does not exist.
let createDir path = 
    try
        let dir = DirectoryInfo path
        if not dir.Exists then dir.Create()
        ok ()
    with _ ->
        DirectoryCreateError path |> fail

/// Cleans a directory by deleting it and recreating it.
let CleanDir path = 
    let di = DirectoryInfo path
    if di.Exists then 
        try
            di.Delete(true)
        with
        | exn -> failwithf "Error during deletion of %s%s  - %s" di.FullName Environment.NewLine exn.Message 
    createDir path |> returnOrFail
    // set writeable
    File.SetAttributes(path, FileAttributes.Normal)

// http://stackoverflow.com/a/19283954/1397724
let getFileEncoding path =
    let bom = Array.zeroCreate 4
    use fs = new FileStream(path, FileMode.Open, FileAccess.Read)
    fs.Read(bom, 0, 4) |> ignore
    match bom with
    | [| 0x2buy ; 0x2fuy ; 0x76uy ; _      |] -> Encoding.UTF7
    | [| 0xefuy ; 0xbbuy ; 0xbfuy ; _      |] -> Encoding.UTF8
    | [| 0xffuy ; 0xfeuy ; _      ; _      |] -> Encoding.Unicode //UTF-16LE
    | [| 0xfeuy ; 0xffuy ; _      ; _      |] -> Encoding.BigEndianUnicode //UTF-16BE
    | [| 0uy    ; 0uy    ; 0xfeuy ; 0xffuy |] -> Encoding.UTF32
    | _ -> Encoding.ASCII

/// [omit]
let inline createRelativePath root path = 
    let basePath = 
        if String.IsNullOrEmpty root then Environment.CurrentDirectory + Path.DirectorySeparatorChar.ToString()
        else root
    
    let uri = Uri(basePath)
    uri.MakeRelativeUri(Uri(path)).ToString().Replace("/", "\\").Replace("%20", " ")

let extractPath infix (fileName : string) : string option=
    let path = fileName.Replace("\\", "/").ToLower()
    let fi = new FileInfo(path)

    let startPos = path.LastIndexOf(sprintf "%s/" infix)
    let endPos = path.IndexOf('/', startPos + infix.Length + 1)
    if startPos < 0 then None 
    elif endPos < 0 then Some("")
    else 
        Some(path.Substring(startPos + infix.Length + 1, endPos - startPos - infix.Length - 1))

/// [omit]
let inline normalizeXml(doc:XmlDocument) =
    use stringWriter = new StringWriter()
    let settings = XmlWriterSettings()
    settings.Indent <- true
        
    use xmlTextWriter = XmlWriter.Create(stringWriter, settings)
    doc.WriteTo(xmlTextWriter)
    xmlTextWriter.Flush()
    stringWriter.GetStringBuilder().ToString()

let envProxies () =
    let getEnvValue (name:string) =
        let v = Environment.GetEnvironmentVariable(name.ToUpperInvariant())
        // under mono, env vars are case sensitive
        if v = null then Environment.GetEnvironmentVariable(name.ToLowerInvariant()) else v
    let bypassList =
        let noproxy = getEnvValue "NO_PROXY"
        if String.IsNullOrEmpty(noproxy) then [||] else
        noproxy.Split([| ',' |], StringSplitOptions.RemoveEmptyEntries)
    let getCredentials (uri:Uri) =
        let userPass = uri.UserInfo.Split([| ':' |], 2)
        if userPass.Length <> 2 || userPass.[0].Length = 0 then None else
        let credentials = new NetworkCredential(Uri.UnescapeDataString userPass.[0], Uri.UnescapeDataString userPass.[1])
        Some credentials

    let getProxy (scheme:string) =
        let envVarName = sprintf "%s_PROXY" (scheme.ToUpperInvariant())
        let envVarValue = getEnvValue envVarName
        if envVarValue = null then 
            None 
        else
            match Uri.TryCreate(envVarValue, UriKind.Absolute) with
            | true, envUri ->
                let proxy = new WebProxy(new Uri(sprintf "http://%s:%d" envUri.Host envUri.Port))
                proxy.Credentials <- Option.toObj <| getCredentials envUri
                proxy.BypassProxyOnLocal <- true
                proxy.BypassList <- bypassList
                Some proxy
            | _ -> None

    let addProxy (map:Map<string, WebProxy>) scheme =
        match getProxy scheme with
        | Some p -> Map.add scheme p map
        | _ -> map

    [ "http"; "https" ]
    |> List.fold addProxy Map.empty 

let getDefaultProxyFor url =
    let uri = new Uri(url)
    let getDefault () =
        let result = WebRequest.GetSystemWebProxy()
        let address = result.GetProxy(uri)

        if address = uri then null else
        let proxy = new WebProxy(address)
        proxy.Credentials <- CredentialCache.DefaultCredentials
        proxy.BypassProxyOnLocal <- true
        proxy

    match envProxies().TryFind uri.Scheme with
    | Some p -> if p.GetProxy(uri) <> uri then p else getDefault()
    | None -> getDefault()

let inline createWebClient(url,auth:Auth option) =
    let client = new WebClient()
    match auth with
    | None -> client.UseDefaultCredentials <- true
    | Some(Credentials(username, password)) -> 
        // htttp://stackoverflow.com/questions/16044313/webclient-httpwebrequest-with-basic-authentication-returns-404-not-found-for-v/26016919#26016919
        //this works ONLY if the server returns 401 first
        //client DOES NOT send credentials on first request
        //ONLY after a 401
        //client.Credentials <- new NetworkCredential(auth.Username,auth.Password)

        //so use THIS instead to send credenatials RIGHT AWAY
        let credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(username + ":" + password))
        client.Headers.[HttpRequestHeader.Authorization] <- sprintf "Basic %s" credentials
    | Some(Token token) -> client.Headers.[HttpRequestHeader.Authorization] <- sprintf "token %s" token
    client.Headers.Add("user-agent", "Paket")
    client.Proxy <- getDefaultProxyFor url
    client


#nowarn "40"

open System.Diagnostics
open System.Threading

/// [omit]
let downloadFromUrl (auth:Auth option, url : string) (filePath: string) =
    async {
        try
            use client = createWebClient(url,auth)
            
            let task = client.DownloadFileTaskAsync(Uri(url), filePath) |> Async.AwaitTask
            do! task
        with
        | exn ->
            failwithf "Could not download from %s%s Message: %s" url Environment.NewLine exn.Message
    }

/// [omit]
let getFromUrl (auth:Auth option, url : string, contentType : string) =
    async { 
        try
            use client = createWebClient(url,auth)
            if notNullOrEmpty contentType then
                client.Headers.Add(HttpRequestHeader.Accept, contentType)
            let s = client.DownloadStringTaskAsync(Uri(url)) |> Async.AwaitTask
            return! s
        with
        | exn -> 
            failwithf "Could not retrieve data from %s%s Message: %s" url Environment.NewLine exn.Message
            return ""
    }

let getXmlFromUrl (auth:Auth option, url : string) =
    async { 
        try
            use client = createWebClient(url,auth)

            // mimic the headers sent from nuget client to odata/ endpoints
            client.Headers.Add(HttpRequestHeader.Accept, "application/atom+xml, application/xml")
            client.Headers.Add(HttpRequestHeader.AcceptCharset, "UTF-8")
            client.Headers.Add("DataServiceVersion", "1.0;NetFx")
            client.Headers.Add("MaxDataServiceVersion", "2.0;NetFx")
            
            let s = client.DownloadStringTaskAsync(Uri(url)) |> Async.AwaitTask
            return! s
        with
        | exn -> 
            failwithf "Could not retrieve data from %s%s Message: %s" url Environment.NewLine exn.Message
            return ""
    }
    
/// [omit]
let safeGetFromUrl (auth:Auth option, url : string, contentType : string) =
    async { 
        try 
            use client = createWebClient(url,auth)
            
            if notNullOrEmpty contentType then
                client.Headers.Add(HttpRequestHeader.Accept, contentType)

            let s = client.DownloadStringTaskAsync(Uri(url)) |> Async.AwaitTask
            let! raw = s
            return Some raw
        with _ -> return None
    }

let readKey() = System.Console.ReadKey().KeyChar.ToString()

/// If the guard is true then a [Y]es / [N]o question will be ask.
/// Until the user pressed y or n.
let askYesNo question =
    let rec getAnswer() = 
        Logging.tracefn "%s" question
        Logging.tracef "    [Y]es/[N]o => "
        let answer = readKey()
        Logging.tracefn ""
        match answer.ToLower() with
        | "y" -> true
        | "n" -> false
        | _ -> getAnswer()

    getAnswer()

let inline normalizePath(path:string) = path.Replace("\\",Path.DirectorySeparatorChar.ToString()).Replace("/",Path.DirectorySeparatorChar.ToString()).TrimEnd(Path.DirectorySeparatorChar)

/// Gets all files with the given pattern
let inline FindAllFiles(folder, pattern) = DirectoryInfo(folder).GetFiles(pattern, SearchOption.AllDirectories)

let getTargetFolder root groupName packageName (version:SemVerInfo) includeVersionInPath = 
    let packageFolder = packageName.ToString() + if includeVersionInPath then "." + version.ToString() else ""
    if groupName = Constants.MainDependencyGroup then
        Path.Combine(root, Constants.PackagesFolderName, packageFolder)
    else
        Path.Combine(root, Constants.PackagesFolderName, groupName.GetCompareString(), packageFolder)

let RunInLockedAccessMode(rootFolder,action) =
    let packagesFolder = Path.Combine(rootFolder,Constants.PackagesFolderName)
    if Directory.Exists packagesFolder |> not then
        Directory.CreateDirectory packagesFolder |> ignore

    let p = Process.GetCurrentProcess()
    let fileName = Path.Combine(packagesFolder,Constants.AccessLockFileName)

    // Checks the packagesFolder for a paket.locked file or waits until it get access to it.
    let rec acquireLock (startTime:DateTime) (timeOut:TimeSpan) trials =
        try
            let rec waitForUnlocked counter =
                if File.Exists fileName then
                    let content = File.ReadAllText fileName
                    if content <> p.Id.ToString() then
                        let currentProcess = Process.GetCurrentProcess()
                        let hasRunningPaketProcess = 
                            Process.GetProcessesByName(p.ProcessName) 
                            |> Array.filter (fun p -> p.Id <> currentProcess.Id)
                            |> Array.exists (fun p -> content = p.Id.ToString() && (not p.HasExited))

                        if hasRunningPaketProcess then
                            if startTime + timeOut <= DateTime.Now then
                                failwith "timeout"
                            if counter % 10 = 0 then
                                traceWarnfn "packages folder is locked by paket.exe (PID = %s). Waiting..." content
                            Thread.Sleep(100)
                            waitForUnlocked(counter + 1)

            waitForUnlocked 0
            File.WriteAllText(fileName,p.Id.ToString())
        with
        | exn ->
            if trials > 0 && (startTime + timeOut) > DateTime.Now then 
                acquireLock startTime timeOut (trials - 1)
            else
                failwithf "Could not acquire %s file in %s.%s%s" 
                    Constants.AccessLockFileName packagesFolder Environment.NewLine exn.Message
    
    let releaseLock() =
         if File.Exists fileName then
            let content = File.ReadAllText fileName
            if content = p.Id.ToString() then
               File.Delete fileName

    try
        acquireLock DateTime.Now (TimeSpan.FromMinutes 5.) 5

        let result = action()
        
        releaseLock()
        result
    with
    | exn ->
        releaseLock()
        reraise()

module String =
    let (|StartsWith|_|) prefix (input: string) =
        if input.StartsWith prefix then
            Some (input.Substring(prefix.Length))
        else None

    let quoted(text:string) = (if text.Contains(" ") then "\"" + text + "\"" else text) 

// MonadPlus - "or else"
let inline (++) x y =
    match x with
    | None -> y
    | _ -> x

let parseKeyValuePairs(s:string) =
    let s = s.Trim().ToLower()
    let parts = s.Split([|','|], StringSplitOptions.RemoveEmptyEntries)
    let dict = new System.Collections.Generic.Dictionary<_,_>()

    let lastKey = ref ""

    for p in parts do
        if p.Contains ":" then
            let innerParts = p.Split(':') |> Array.map (fun x -> x.Trim())
            if innerParts.Length % 2 <> 0 then
                failwithf "\"%s\" can not be parsed as key/value pairs." p
            dict.Add(innerParts.[0],innerParts.[1])
            lastKey := innerParts.[0]
        else
            dict.[!lastKey] <- dict.[!lastKey] + ", " + p
    dict

let downloadStringSync (url : string) (client : System.Net.WebClient) = 
    try 
        client.DownloadString url |> ok
    with _ ->
        DownloadError url |> fail 

let downloadFileSync (url : string) (fileName : string) (client : System.Net.WebClient) = 
    tracefn "Downloading file from %s to %s" url fileName
    try 
        client.DownloadFile(url, fileName) |> ok
    with _ ->
        DownloadError url |> fail 

let saveFile (fileName : string) (contents : string) =
    tracefn "Saving file %s" fileName
    try 
        File.WriteAllText(fileName, contents) |> ok
    with _ ->
        FileSaveError fileName |> fail

let removeFile (fileName : string) =
    if File.Exists fileName then
        tracefn "Removing file %s" fileName
        try
            File.Delete(fileName) |> ok
        with _ ->
            FileDeleteError fileName |> fail
    else ok ()

let normalizeLineEndings (text : string) = 
    text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine)

let isMonoRuntime =
    not (Object.ReferenceEquals(Type.GetType("Mono.Runtime"), null))

// adapted from MiniRx
// http://minirx.codeplex.com/
[<AutoOpen>]
module ObservableExtensions =

    let private synchronize f = 
        let ctx = System.Threading.SynchronizationContext.Current 
        f (fun g arg ->
            let nctx = System.Threading.SynchronizationContext.Current 
            if ctx <> null && ctx <> nctx then 
                ctx.Post((fun _ -> g(arg)), null)
            else 
                g(arg))

    type Microsoft.FSharp.Control.Async with 
      static member AwaitObservable(ev1:IObservable<'a>) =
        synchronize (fun f ->
          Async.FromContinuations((fun (cont,econt,ccont) -> 
            let rec callback = (fun value ->
              remover.Dispose()
              f cont value )
            and remover : IDisposable  = ev1.Subscribe(callback) 
            () )))

    [<RequireQualifiedAccess>]
    module Observable =
        open System.Collections.Generic

        /// Creates an observable that calls the specified function after someone
        /// subscribes to it (useful for waiting using 'let!' when we need to start
        /// operation after 'let!' attaches handler)
        let guard f (e:IObservable<'Args>) =
          { new IObservable<'Args> with
              member x.Subscribe(observer) =
                let rm = e.Subscribe(observer) in f(); rm } 

        let sample milliseconds source =
            let relay (observer:IObserver<'T>) =
                let rec loop () = async {
                    let! value = Async.AwaitObservable source
                    observer.OnNext value
                    do! Async.Sleep milliseconds
                    return! loop() 
                }
                loop ()

            { new IObservable<'T> with
                member this.Subscribe(observer:IObserver<'T>) =
                    let cts = new System.Threading.CancellationTokenSource()
                    Async.Start(relay observer, cts.Token)
                    { new IDisposable with 
                        member this.Dispose() = cts.Cancel() 
                    }
            }

        let ofSeq s = 
            let evt = new Event<_>()
            evt.Publish |> guard (fun o ->
                for n in s do evt.Trigger(n))

        let private oneAndDone (obs : IObserver<_>) value =
            obs.OnNext value
            obs.OnCompleted() 

        let ofAsync a : IObservable<'a> = 
            { new IObservable<'a> with
                  member __.Subscribe obs = 
                      let oneAndDone' = oneAndDone obs
                      let token = new CancellationTokenSource()
                      Async.StartWithContinuations(a,oneAndDone',obs.OnError,obs.OnError,token.Token)
                      { new IDisposable with
                            member __.Dispose() = 
                                token.Cancel |> ignore
                                token.Dispose() } }
        
        let ofAsyncWithToken (token : CancellationToken) a : IObservable<'a> = 
            { new IObservable<'a> with
                  member __.Subscribe obs = 
                      let oneAndDone' = oneAndDone obs
                      Async.StartWithContinuations(a,oneAndDone',obs.OnError,obs.OnError,token)
                      { new IDisposable with
                            member __.Dispose() = () } }

        let flatten (a: IObservable<#seq<'a>>): IObservable<'a> =
            { new IObservable<'a> with
                member __.Subscribe obs =
                    let sub = a |> Observable.subscribe (Seq.iter obs.OnNext)
                    { new IDisposable with member __.Dispose() = sub.Dispose() }}

        let distinct (a: IObservable<'a>): IObservable<'a> =
            let seen = HashSet()
            Observable.filter seen.Add a
 