[<AutoOpen>]
/// Contains methods for IO.
module Paket.Utils

open System
open System.IO
open System.Net
open System.Xml
open System.Text
open Paket.Logging

type Auth = 
    { Username : string
      Password : string }

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

/// Creates a directory if it does not exist.
let CreateDir path = 
    let dir = DirectoryInfo path
    if not dir.Exists then dir.Create()

/// Cleans a directory by deleting it and recreating it.
let CleanDir path = 
    let di = DirectoryInfo path
    if di.Exists then 
        try
            di.Delete(true)
        with
        | exn -> failwithf "Error during deletion of %s%s  - %s" di.FullName Environment.NewLine exn.Message 
    CreateDir path
    // set writeable
    File.SetAttributes(path, FileAttributes.Normal)

/// [omit]
let inline createRelativePath root path = 
    let basePath = 
        if String.IsNullOrEmpty root then Environment.CurrentDirectory + Path.DirectorySeparatorChar.ToString()
        else root
    
    let uri = Uri(basePath)
    uri.MakeRelativeUri(Uri(path)).ToString().Replace("/", "\\").Replace("%20", " ")    

/// [omit]
let inline normalizeXml(doc:XmlDocument) =
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
    proxy.BypassProxyOnLocal <- true
    proxy

let inline createWebClient(auth:Auth option) =
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


#nowarn "40"

open System.Diagnostics
open System.Threading

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

let inline normalizePath(path:string) = path.Replace("\\",Path.DirectorySeparatorChar.ToString()).Replace("/",Path.DirectorySeparatorChar.ToString())

/// Gets all files with the given pattern
let inline FindAllFiles(folder, pattern) = DirectoryInfo(folder).GetFiles(pattern, SearchOption.AllDirectories)


let RunInLockedAccessMode(rootFolder,action) =
    let packagesFolder = Path.Combine(rootFolder,Constants.PackagesFolderName)
    if Directory.Exists packagesFolder |> not then
        Directory.CreateDirectory packagesFolder |> ignore

    let p = Process.GetCurrentProcess()
    let fileName = Path.Combine(packagesFolder,Constants.AccessLockFileName)

    // Checks the packagesFolder for a paket.locked file or waits until it get access to it.
    let rec acquireLock (startTime:DateTime) (timeOut:TimeSpan) =
        try
            let rec waitForUnlocked counter =
                if File.Exists fileName then
                    let content = File.ReadAllText fileName
                    if content <> p.Id.ToString() then
                        let processes = Process.GetProcessesByName(p.ProcessName)
                        if processes |> Array.exists (fun p -> p.HasExited = false && content = p.Id.ToString()) then
                            if startTime + timeOut <= DateTime.Now then
                                failwith "timeout"
                            if counter % 10 = 0 then
                                traceWarnfn "packages folder is locked by paket.exe (PID = %s). Waiting..." content
                            Thread.Sleep(100)
                            waitForUnlocked(counter + 1)

            waitForUnlocked 0
            File.WriteAllText(fileName,p.Id.ToString())
        with
        | _ -> failwithf "Could not acquire %s file in %s." Constants.AccessLockFileName packagesFolder
    
    let releaseLock() =
         if File.Exists fileName then
            let content = File.ReadAllText fileName
            if content = p.Id.ToString() then
               File.Delete fileName

    try
        acquireLock DateTime.Now (TimeSpan.FromMinutes 2.)

        let result = action()
        
        releaseLock()
        result
    with
    | exn ->
            releaseLock()
            raise exn

/// [omit]
module Seq = 
    let inline firstOrDefault seq = Seq.tryFind (fun _ -> true) seq

module String =
    let (|StartsWith|_|) prefix (input: string) =
        if input.StartsWith prefix then
            Some (input.Substring(prefix.Length))
        else None

let inline orElse v =
    function
    | Some x -> Some x
    | None -> v