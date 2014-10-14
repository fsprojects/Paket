[<AutoOpen>]
/// Contains methods for IO.
module Paket.Utils

open System
open System.IO
open System.Net
open System.Xml
open System.Text

type Auth = 
    { Username : string
      Password : string }

/// Creates a directory if it does not exist.
let CreateDir path = 
    let dir = DirectoryInfo path
    if not dir.Exists then dir.Create()

/// Cleans a directory by deleting it and recreating it.
let CleanDir path = 
    let di = DirectoryInfo path
    if di.Exists then 
        di.Delete(true)
    CreateDir path
    // set writeable
    File.SetAttributes(path, FileAttributes.Normal)

/// [omit]
let createRelativePath root path =
    Uri(if String.IsNullOrEmpty root then System.Environment.CurrentDirectory + Path.DirectorySeparatorChar.ToString() else root).MakeRelativeUri(Uri(path)).ToString().Replace("/", "\\")

/// [omit]
let normalizeXml(doc:XmlDocument) =
    use stringWriter = new StringWriter()
    let settings = XmlWriterSettings()
    settings.Indent <- true
        
    use xmlTextWriter = XmlWriter.Create(stringWriter, settings)
    doc.WriteTo(xmlTextWriter)
    xmlTextWriter.Flush()
    stringWriter.GetStringBuilder().ToString()

let defaultProxy =
    let result = WebRequest.GetSystemWebProxy()
    let irrelevantDestination = new Uri(@"http://google.com")
    let address = result.GetProxy(irrelevantDestination)

    if address = irrelevantDestination then null else
    let proxy = new WebProxy(address)
    proxy.Credentials <- CredentialCache.DefaultCredentials
    proxy

let createWebClient(auth:Auth option) =
    let client = new WebClient()
    match auth with
    | None -> client.UseDefaultCredentials <- true
    | Some auth -> 
        // htttp://stackoverflow.com/questions/16044313/webclient-httpwebrequest-with-basic-authentication-returns-404-not-found-for-v/26016919#26016919
        //this works ONLY if the server returns 401 first
        //client DOES NOT send credentials on first request
        //ONLY after a 401
        //client.Credentials <- new NetworkCredential(auth.Username,auth.Password)

        //so use THIS instead to send credenatials RIGHT AWAY
        let credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(auth.Username + ":" + auth.Password))
        client.Headers.[HttpRequestHeader.Authorization] <- String.Format("Basic {0}", credentials)

    client.Headers.Add("user-agent", "Paket")
    client.Proxy <- defaultProxy
    client

type System.Net.WebClient with
    member this.AsyncDownloadFile (address: Uri, filePath: string) : Async<unit> =
        let downloadAsync =
            Async.FromContinuations (fun (cont, econt, ccont) ->
                        let userToken = new obj()
                        let rec handler = 
                                System.ComponentModel.AsyncCompletedEventHandler (fun _ args ->
                                    if userToken = args.UserState then
                                        this.DownloadFileCompleted.RemoveHandler(handler)
                                        if args.Cancelled then
                                            ccont (new OperationCanceledException())
                                        elif args.Error <> null then
                                            econt args.Error
                                        else
                                            cont ())
                        this.DownloadFileCompleted.AddHandler(handler)
                        this.DownloadFileAsync(address, filePath, userToken)
                    )

        async {
            use! _holder = Async.OnCancel(fun _ -> this.CancelAsync())
            return! downloadAsync
        }

/// [omit]
let downloadFromUrl (auth:Auth option, url : string) (filePath: string) =
    async {
        try
            use client = createWebClient auth
            do! client.AsyncDownloadFile(Uri(url), filePath)
        with
        | exn ->
            failwithf "Could not retrieve data from %s%s Message: %s" url Environment.NewLine exn.Message
    }

/// [omit]
let getFromUrl (auth:Auth option, url : string) = 
    async { 
        try
            use client = createWebClient auth
            return! client.AsyncDownloadString(Uri(url))
        with
        | exn -> 
            failwithf "Could not retrieve data from %s%s Message: %s" url Environment.NewLine exn.Message
            return ""
    }

/// [omit]
let safeGetFromUrl (auth:Auth option, url : string) = 
    async { 
        try 
            use client = createWebClient auth
            let! raw = client.AsyncDownloadString(Uri(url))
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


/// If the guard is true then a [0] / .. / [n] question will be ask.
/// Until the user pressed a valid number.
let askNumberedQuestion question options =
    let rec getAnswer() = 
        Logging.tracef "%s\r\n  => " question
        let answer = readKey()
        Logging.tracefn ""
        match System.Int32.TryParse answer with
        | true, x when x >= 0 && x < options -> x
        | _ -> getAnswer()

    getAnswer()


let normalizePath(path:string) = path.Replace("\\","/")

/// Enumerates all files with the given pattern
let FindAllFiles(folder, pattern) = DirectoryInfo(folder).EnumerateFiles(pattern, SearchOption.AllDirectories)

/// [omit]
module Seq = 
    let firstOrDefault seq = Seq.tryFind (fun _ -> true) seq
