[<AutoOpen>]
/// Contains methods for IO.
module Paket.Utils

open System
open System.IO
open System.IO.Compression
open System.Xml
open System.Text
open Paket
open Paket.Logging
open Paket.Constants
open Chessie.ErrorHandling
open Paket.Domain

open System.Threading
open Microsoft.FSharp.Core.Printf
open System.Threading.Tasks
open System.Collections.Concurrent
open System.Diagnostics
open System.Collections.Generic

let sndOf3 (_,v,_) = v
let thirdOf3 (_,_,v) = v

let rethrowf f inner fmt =
    ksprintf (fun msg -> raise (f(msg,inner))) fmt

/// Adds quotes around the string
/// [omit]
let quote (str:string) = "\"" + str.Replace("\"","\\\"") + "\""


let inline isNotNull x = not (isNull x)

let acceptXml = "application/atom+xml,application/xml"
let acceptJson = "application/atom+json,application/json"

let notNullOrEmpty = not << System.String.IsNullOrEmpty

let inline force (lz: 'a Lazy)  = lz.Force()
let inline endsWith text x = (^a:(member EndsWith:string->bool)x, text)
let inline toLower str = (^a:(member ToLower:unit->string)str)

let internal removeInvalidChars (str : string) = RegularExpressions.Regex.Replace(str, "[:@\,]", "_")


let inline tryGet (key:^k) this =
    let mutable v = Unchecked.defaultof<'v>
    let scc = ( ^a : (member TryGetValue : 'k * 'v byref -> bool) this, key, &v)
    if scc then Some v else None

let inline internal memoizeByExt (getKey : 'a -> 'key) (f: 'a -> 'b) : ('a -> 'b) * ('key * 'b -> unit) =
    let cache = System.Collections.Concurrent.ConcurrentDictionary<'key, 'b>()
    (fun (x: 'a) ->
        cache.GetOrAdd(getKey x, fun _ -> f x)),
    (fun (key, c) ->
        cache.TryAdd(key, c) |> ignore)

let inline internal memoizeBy (getKey : 'a -> 'key) (f: 'a -> 'b) : ('a -> 'b) =
    memoizeByExt getKey f |> fst

let inline internal memoize (f: 'a -> 'b) : 'a -> 'b = memoizeBy id f

type MemoizeAsyncExResult<'TResult, 'TCached> =
    | FirstCall of ( 'TCached * 'TResult ) Task
    | SubsequentCall of 'TCached Task

let internal memoizeAsyncEx (f: 'iext -> 'i -> Async<'o * 'oext>) =
    let cache = ConcurrentDictionary<'i, Task<'o>>()
    let handle (ex:'iext) (x:'i) : MemoizeAsyncExResult<'oext, 'o> =
        let mutable tcs_result = null
        let task_cached = cache.GetOrAdd(x, fun x ->
            tcs_result <- TaskCompletionSource()
            let tcs = TaskCompletionSource()

            Async.Start (async {
                try
                    let! o, oext = f ex x
                    tcs.SetResult o
                    tcs_result.SetResult (o, oext)
                with
                  exn ->
                    tcs.SetException exn
                    tcs_result.SetException exn
            })

            tcs.Task)
        // if this was the first call for the key, then tcs_result was set inside 'cache.GetOrAdd'
        if tcs_result <> null then
            FirstCall tcs_result.Task
        else
            SubsequentCall task_cached
    handle


let internal memoizeAsync f =
    let cache = System.Collections.Concurrent.ConcurrentDictionary<'a, System.Threading.Tasks.Task<'b>>()
    fun (x: 'a) -> // task.Result serialization to sync after done.
        cache.GetOrAdd(x, fun x -> f(x) |> Async.StartAsTask) |> Async.AwaitTask

let TimeSpanToReadableString(span:TimeSpan) =
    let pluralize x = if x = 1 then String.Empty else "s"
    let notZero x y = if x > 0 then y else String.Empty
    let days = notZero (span.Duration().Days) (String.Format("{0:0} day{1}, ", span.Days, pluralize span.Days))
    let hours = notZero (span.Duration().Hours) (String.Format("{0:0} hour{1}, ", span.Hours, pluralize span.Hours))
    let minutes = notZero (span.Duration().Minutes) (String.Format("{0:0} minute{1}, ", span.Minutes, pluralize span.Minutes))
    let seconds = notZero (span.Duration().Seconds) (String.Format("{0:0} second{1}", span.Seconds, pluralize span.Seconds))
    let milliseconds = notZero (span.Duration().Milliseconds) (String.Format("{0:0} millisecond{1}", span.Milliseconds, pluralize span.Milliseconds) )

    let formatted = String.Format("{0}{1}{2}{3}", days, hours, minutes, seconds)

    let formatted = if formatted.EndsWith ", " then formatted.Substring(0, formatted.Length - 2) else formatted

    if not (String.IsNullOrEmpty formatted) then formatted
    elif not (String.IsNullOrEmpty milliseconds) then milliseconds
    else "0 milliseconds"

let GetHomeDirectory() =
#if DOTNETCORE
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
#else
    if  Environment.OSVersion.Platform = PlatformID.Unix || Environment.OSVersion.Platform = PlatformID.MacOSX then
        Environment.GetEnvironmentVariable "HOME"
    else
        Environment.ExpandEnvironmentVariables "%HOMEDRIVE%%HOMEPATH%"
#endif

type PathReference =
    | AbsolutePath of string
    | RelativePath of string

let normalizeLocalPath (path:string) =
    if path.StartsWith "~/" then
        AbsolutePath (Path.Combine(GetHomeDirectory(), path.Substring 2))
    elif Path.IsPathRooted path then
        AbsolutePath path
    else
        RelativePath path

let getDirectoryInfoForLocalNuGetFeed pathInfo alternativeProjectRoot root =
    match pathInfo with
    | AbsolutePath s -> DirectoryInfo s
    | RelativePath s ->
        match alternativeProjectRoot with
        | Some root -> DirectoryInfo(Path.Combine(root, s))
        | None -> DirectoryInfo(Path.Combine(root, s))


// show the path that was too long
let FileInfo str =
    try
        FileInfo(str)
    with
      :? PathTooLongException as exn -> raise (PathTooLongException("Path too long: " + str, exn))


// show the path that was too long
let DirectoryInfo str =
    try
        DirectoryInfo(str)
    with
      :? PathTooLongException as exn -> raise (PathTooLongException("Path too long: " + str, exn))

/// Creates a directory if it does not exist.
let createDir path =
    try
        let dir = DirectoryInfo path
        if not dir.Exists then dir.Create()
        ok ()
    with _ ->
        DirectoryCreateError path |> fail

let rec emptyDir (dirInfo:DirectoryInfo) =
    if dirInfo.Exists then
        for fileInfo in dirInfo.GetFiles() do
            fileInfo.Attributes <- FileAttributes.Normal
            fileInfo.Delete()

        for childInfo in dirInfo.GetDirectories() do
            try
                Directory.Delete(childInfo.FullName,true)
            with
            | _ -> deleteDir childInfo

        dirInfo.Attributes <- FileAttributes.Normal

and deleteDir (dirInfo:DirectoryInfo) =
    if dirInfo.Exists then
        emptyDir dirInfo

        dirInfo.Delete()

/// Cleans a directory by deleting it and recreating it.
let CleanDir path =
    let di = DirectoryInfo path
    if di.Exists then
        try
            emptyDir di
        with
        | e -> raise <| exn(sprintf "Error during cleaning of %s%s  - %s" di.FullName Environment.NewLine e.Message, e)
    else
        Directory.CreateDirectory path |> ignore
    // set writeable
    try
        File.SetAttributes (path, FileAttributes.Normal)
    with
    | _ -> ()

// http://stackoverflow.com/a/19283954/1397724
let getFileEncoding path =
    let bom = Array.zeroCreate 4
    use fs = new FileStream (path, FileMode.Open, FileAccess.Read)
    fs.Read (bom, 0, 4) |> ignore
    match bom with
    | [| 0x2buy ; 0x2fuy ; 0x76uy ; _      |] -> Encoding.UTF7
    | [| 0xefuy ; 0xbbuy ; 0xbfuy ; _      |] -> Encoding.UTF8
    | [| 0xffuy ; 0xfeuy ; _      ; _      |] -> Encoding.Unicode //UTF-16LE
    | [| 0xfeuy ; 0xffuy ; _      ; _      |] -> Encoding.BigEndianUnicode //UTF-16BE
    | [| 0uy    ; 0uy    ; 0xfeuy ; 0xffuy |] -> Encoding.UTF32
    | _ -> Encoding.ASCII

/// [omit]
let createRelativePath root (path:string) =
    let path = Path.GetFullPath path
    let basePath =
        if String.IsNullOrEmpty root then Directory.GetCurrentDirectory() + string Path.DirectorySeparatorChar
        else Path.GetFullPath root

    let uri = Uri basePath
    let relative = uri.MakeRelativeUri(Uri path).ToString().Replace("/", "\\").Replace("%20", " ")
    relative

/// The path of the "Program Files" folder - might be x64 on x64 machine
let ProgramFiles = Environment.GetFolderPath Environment.SpecialFolder.ProgramFiles

/// The path of Program Files (x86)
/// It seems this covers all cases where PROCESSOR\_ARCHITECTURE may misreport and the case where the other variable
/// PROCESSOR\_ARCHITEW6432 can be null
let ProgramFilesX86 =
    let wow64 = Environment.GetEnvironmentVariable "PROCESSOR_ARCHITEW6432"
    let globalArch = Environment.GetEnvironmentVariable "PROCESSOR_ARCHITECTURE"
    match wow64, globalArch with
    | "AMD64", "AMD64"
    | null, "AMD64"
    | "x86", "AMD64" -> Environment.GetEnvironmentVariable "ProgramFiles(x86)"
    | _ -> Environment.GetEnvironmentVariable "ProgramFiles"
    |> fun detected -> if detected = null then @"C:\Program Files (x86)\" else detected

/// The system root environment variable. Typically "C:\Windows"
let SystemRoot = Environment.GetEnvironmentVariable "SystemRoot"

let isMonoRuntime =
    not (Object.ReferenceEquals(Type.GetType "Mono.Runtime", null))

/// Determines if the current system is an Unix system
let isUnix =
#if NETSTANDARD1_6 || NETSTANDARD2_0
    System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
        System.Runtime.InteropServices.OSPlatform.Linux) ||
    System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
        System.Runtime.InteropServices.OSPlatform.OSX)
#else
    int Environment.OSVersion.Platform |> fun p -> (p = 4) || (p = 6) || (p = 128)
#endif

/// Determines if the current system is a MacOs system
let isMacOS =
#if NETSTANDARD1_6 || NETSTANDARD2_0
    System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
        System.Runtime.InteropServices.OSPlatform.OSX)
#else
    (Environment.OSVersion.Platform = PlatformID.MacOSX) ||
        // osascript is the AppleScript interpreter on OS X
        File.Exists "/usr/bin/osascript"
#endif

/// Determines if the current system is a Linux system
let isLinux =
#if NETSTANDARD1_6 || NETSTANDARD2_0
    System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
        System.Runtime.InteropServices.OSPlatform.Linux)
#else
    isUnix && not isMacOS
#endif

/// Determines if the current system is a Windows system
let isWindows =
#if NETSTANDARD1_6 || NETSTANDARD2_0
    System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
        System.Runtime.InteropServices.OSPlatform.Windows)
#else
    match Environment.OSVersion.Platform with
    | PlatformID.Win32NT | PlatformID.Win32S | PlatformID.Win32Windows | PlatformID.WinCE -> true
    | _ -> false
#endif


/// Determines if the current system is a mono system
/// Todo: Detect mono on windows
[<Obsolete("use either isMonoRuntime or isUnix, this flag is always false when compiled for NETSTANDARD")>]
let isMono =
#if NETSTANDARD1_6 || NETSTANDARD2_0
    false
#else
    isUnix
#endif

let monoPath =
    if isMacOS && File.Exists "/Library/Frameworks/Mono.framework/Commands/mono" then
        "/Library/Frameworks/Mono.framework/Commands/mono"
    else
        "mono"

let isMatchingOperatingSystem (operatingSystemFilter : string option) =
    let aliasesForOs =
        match isMacOS, isUnix, isWindows with
        | true, true, false -> [ "osx"; "mac" ]
        | false, true, false -> [ "linux"; "unix"; "un*x" ]
        | false, false, true -> [ "win"; "w7"; "w8"; "w10" ]
        | _ -> []

    match operatingSystemFilter with
    | None -> true
    | Some filter -> aliasesForOs |> List.exists (fun alias -> filter.ToLower().Contains(alias))

let isMatchingPlatform (operatingSystemFilter : string option) =
    match operatingSystemFilter with
    | None -> true
#if NETSTANDARD1_6 || NETSTANDARD2_0
    | Some filter when filter = "mono" -> isMacOS || isUnix
    | Some filter when filter = "windows" -> isWindows
#else
    | Some filter when filter = "mono" -> isMonoRuntime
    | Some filter when filter = "windows" -> not isMonoRuntime
#endif
    | _ -> isMatchingOperatingSystem operatingSystemFilter

/// [omit]
let inline normalizeXml (doc:XmlDocument) =
    use stringWriter = new StringWriter()
    let settings = XmlWriterSettings (Indent=true)

    use xmlTextWriter = XmlWriter.Create (stringWriter, settings)
    doc.WriteTo xmlTextWriter
    xmlTextWriter.Flush()
    stringWriter.GetStringBuilder() |> string

let saveNormalizedXml (fileName:string) (doc:XmlDocument) =
    // combination of saveFile and normalizeXml which ensures that the
    // file encoding matches the one listed in the XML itself.
    tracefn "Saving xml %s" fileName
    let settings = XmlWriterSettings (Indent=true)

    try
        use fstream = File.Create(fileName)
        use xmlTextWriter = XmlWriter.Create(fstream, settings)
        doc.WriteTo(xmlTextWriter) |> ok
    with _ ->
        FileSaveError fileName |> fail

let mutable autoAnswer = None
let readAnswer() =
    match autoAnswer with
    | Some true -> "y"
    | Some false -> "n"
    | None -> System.Console.ReadLine().Trim()

/// If the guard is true then a [Y]es / [N]o question will be ask.
/// Until the user pressed y or n.
let askYesNo question =
    let rec getAnswer() =
        Logging.tracefn "%s" question
        Logging.tracef "    [Y]es/[N]o => "
        let answer = readAnswer()
        Logging.tracefn ""
        match answer.ToLower() with
        | "y" -> true
        | "n" -> false
        | _ -> getAnswer()

    getAnswer()


let dirSeparator = Path.DirectorySeparatorChar.ToString()

let inline normalizeHomeDirectory (path : string) =
    let homeDirectory = "~"
    if path.StartsWith homeDirectory then
        let path = path.Substring(1)
        let path = if path.StartsWith "\\" || path.StartsWith "/" then path.Substring(1) else path
        Path.Combine(GetHomeDirectory(),path)
    else
        path

let inline normalizePath(path:string) =
    (normalizeHomeDirectory path)
      .Replace("\\",dirSeparator)
      .Replace("/",dirSeparator).TrimEnd(Path.DirectorySeparatorChar)
      .Replace(dirSeparator + "." + dirSeparator, dirSeparator)

let inline windowsPath (path:string) = path.Replace(Path.DirectorySeparatorChar, '\\')
/// Gets all files with the given pattern
let inline FindAllFiles(folder, pattern) = DirectoryInfo(folder).GetFiles(pattern, SearchOption.AllDirectories)

type ResolvedPackagesFolder =
    /// No "packages" folder for the current package
    | NoPackagesFolder
    /// the /packages/group/ExtractedPackage.X.Y.Z folder
    | SymbolicLink of string
    | ResolvedFolder of string
    member x.Path =
        match x with
        | NoPackagesFolder -> None
        | SymbolicLink f
        | ResolvedFolder f -> Some f

type PackagesFolderGroupConfig =
    | NoPackagesFolder
    | GivenPackagesFolder of string
    | SymbolicLink
    | DefaultPackagesFolder
    member x.ResolveGroupDir root groupName =
        match x with
        | NoPackagesFolder -> None
        | GivenPackagesFolder p ->
            // relative to root
            Some p
        | SymbolicLink
        | DefaultPackagesFolder ->
            let groupDir =
                if groupName = Constants.MainDependencyGroup then
                    Path.Combine(root, Constants.DefaultPackagesFolderName)
                else
                    Path.Combine(root, Constants.DefaultPackagesFolderName, groupName.CompareString)
            Some groupDir
    member x.Resolve root groupName (packageName:PackageName) version includeVersionInPath  =
        let parentPath () =
            let groupDir = x.ResolveGroupDir root groupName |> Option.get
            let packageFolder = string packageName + (if includeVersionInPath then "." + string version else "")
            Path.Combine(groupDir, packageFolder)

        match x with
        | NoPackagesFolder -> ResolvedPackagesFolder.NoPackagesFolder
        | GivenPackagesFolder p ->
            // relative to root
            ResolvedPackagesFolder.ResolvedFolder p
        | SymbolicLink ->
            parentPath () |> ResolvedPackagesFolder.SymbolicLink
        | DefaultPackagesFolder ->
            parentPath () |> ResolvedPackagesFolder.ResolvedFolder
    static member Default = DefaultPackagesFolder

let runDotnet workingDir arguments =
    let result =
        let p = new System.Diagnostics.Process()
        p.StartInfo.WorkingDirectory <- workingDir
        p.StartInfo.FileName <- "dotnet"
        p.StartInfo.Arguments <- arguments
        p.Start() |> ignore
        p.WaitForExit()
        p.ExitCode

    if result <> 0 then
        failwithf "dotnet %s failed" arguments

let RunInLockedAccessMode(lockedFolder,lockedAction: unit -> bool) =
    if not (Directory.Exists lockedFolder) then
        Directory.CreateDirectory lockedFolder |> ignore

    let rootFolder = DirectoryInfo(lockedFolder).Parent

    let currentProcess = System.Diagnostics.Process.GetCurrentProcess()
    let fileName = Path.Combine(lockedFolder,Constants.AccessLockFileName)
    let pid = string currentProcess.Id

    // Checks the lockedFolder for a paket.processlock file or waits until it get access to it.
    let acquireLock (startTime:DateTime) (timeOut:TimeSpan) trials =
        let mutable skip = false
        let mutable trials = trials
        while not skip do
            try
                let mutable skipUnlock = false
                while not skipUnlock do
                    if File.Exists fileName then
                        let content = File.ReadAllText fileName
                        if content = pid then
                            skipUnlock <- true
                        else
                            let hasRunningPaketProcess =
                                Process.GetProcessesByName currentProcess.ProcessName
                                |> Array.filter (fun p -> string p.Id <> pid)
                                |> Array.exists (fun p -> content = string p.Id && (not p.HasExited))

                            if hasRunningPaketProcess then
                                if startTime + timeOut <= DateTime.Now then
                                    failwith "timeout"
                                else
                                    Thread.Sleep 100
                            else
                                skipUnlock <- true
                    else
                        skipUnlock <- true

                File.WriteAllText(fileName, pid)
                skip <- true
            with
            | exn when exn.Message = "timeout" ->
                failwithf "Could not acquire lock to '%s'.%sThe process timed out." fileName Environment.NewLine
            | exn ->
                if trials > 0 then
                    trials <- trials - 1
                    if verbose then
                        tracefn "Could not acquire lock to %s.%s%s%sTrials left: %d." fileName Environment.NewLine exn.Message Environment.NewLine trials
                else
                    failwithf "Could not acquire lock to '%s'. No more trials left. Message.: %s" fileName exn.Message

    let releaseLock trials =
        let mutable skip = false
        let mutable trials = trials
        while not skip do
            try
                if File.Exists fileName then
                    let content = File.ReadAllText fileName
                    if content = pid then
                        File.Delete fileName
                skip <- true
            with
            | exn when trials > 0 ->
                Thread.Sleep 100
                trials <- trials - 1
                if verbose then
                    tracefn "Could not release lock to %s.%s%s%sTrials left: %d." fileName Environment.NewLine exn.Message Environment.NewLine trials
            | _ ->
                skip <- true

    try
        acquireLock DateTime.Now (TimeSpan.FromMinutes 10.) 100

        let runDotNetRestore = lockedAction()

        releaseLock 5
        if runDotNetRestore then
            let slnFiles = rootFolder.GetFiles("*.sln", SearchOption.TopDirectoryOnly)
            if Array.isEmpty slnFiles then
                let projFiles = rootFolder.GetFiles("*.*proj", SearchOption.TopDirectoryOnly)

                for proj in projFiles do
                    tracefn "Calling dotnet restore on %s" proj.Name
                    runDotnet rootFolder.FullName (sprintf "restore \"%s\"" proj.Name)
            else
                for sln in slnFiles do
                    tracefn "Calling dotnet restore on %s" sln.Name
                    runDotnet rootFolder.FullName (sprintf "restore \"%s\"" sln.Name)
    with
    | _ ->
        releaseLock 5
        reraise()



[<RequireQualifiedAccess>]
module String =

    /// Match if 'text' starts with the 'prefix' string case
    let (|StartsWith|_|) (prefix: string) (input: string) =
        if input.StartsWith prefix then Some () else None

    /// Match if 'text' starts with the 'prefix' and return the text with the prefix removed
    let (|RemovePrefix|_|) (prefix: string) (input: string) =
        if input.StartsWith prefix then Some (input.Substring prefix.Length)
        else None

    let getLines (str: string) =
        use reader = new StringReader(str)
        [|  let mutable line = reader.ReadLine()
            while isNotNull line do
                yield line
                line <- reader.ReadLine()
            if str.EndsWith "\n" then   // last trailing space not returned
                yield String.Empty      // http://stackoverflow.com/questions/19365404/stringreader-omits-trailing-linebreak
        |]

    /// Check if the two strings are equal ignoring case
    let inline equalsIgnoreCase str1 str2 =
        String.Compare(str1,str2,StringComparison.OrdinalIgnoreCase) = 0

    /// Match if the strings are equal ignoring case
    let (|EqualsIC|_|) (str1:string) (str2:String) =
        if equalsIgnoreCase str1 str2 then Some () else None

    /// Check if 'text' includes the 'target' string case insensitive
    let inline containsIgnoreCase (target:string) (text:string) =
        text.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0

    /// Match if 'text' includes the 'target' string case insensitive
    let (|ContainsIC|_|) (target:string) (str2:String) =
        if containsIgnoreCase target str2 then Some () else None

    /// Check if 'text' starts with the 'prefix' string case insensitive
    let inline startsWithIgnoreCase (prefix:string) (text:string) =
        text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) = 0

    /// Match if 'text' starts with the 'prefix' string case insensitive
    let (|StartsWithIC|_|) (prefix:string) (text:String) =
        if startsWithIgnoreCase prefix text then Some () else None

    /// Check if 'text' ends with the 'suffix' string case insensitive
    let inline endsWithIgnoreCase (suffix:string) (text:string) =
        suffix.Length <= text.Length &&
        text.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase) >= text.Length - suffix.Length

    /// Match if 'text' ends with the 'suffix' string case insensitive
    let (|EndsWithIC|_|) (suffix:string) (text:String) =
        if endsWithIgnoreCase suffix text then  Some () else None

    let quoted (text:string) = (if text.Contains(" ") then "\"" + text + "\"" else text)

    let inline trim (text:string) = text.Trim()
    let inline trimChars (chs: char[]) (text:string) = text.Trim chs
    let inline trimStart (pre: char[]) (text:string) = text.TrimStart pre
    let inline split sep (text:string) = text.Split sep

// MonadPlus - "or else"
let inline (++) x y =
    match x with
    | None -> y
    | _ -> x

let parseKeyValuePairs (s:string) : Dictionary<string,string> =
    let s = s.Trim()
    try
        let l = List<_>()
        let add key value =
            if String.IsNullOrWhiteSpace key |> not then
                let x = key,value
                l.Add x |> ignore

        let quoted = ref false
        let lastKey = Text.StringBuilder(50)
        let lastValue = Text.StringBuilder(50)
        let isKey = ref true
        for pos in 0..s.Length - 1 do
            let x = s.[pos]
            let restHasKey() =
                let rest = s.Substring(pos + 1)
                if String.IsNullOrEmpty(rest.Trim()) then true else
                match rest.IndexOf ',' with
                | -1 -> rest.Contains(":")
                | p ->
                    let s = rest.Substring(0,p)
                    s.Contains(":")

            if x = '"' then
                quoted := not !quoted
            elif x = ',' && not !quoted && restHasKey() then
                add (lastKey.ToString()) (lastValue.ToString())
                lastKey.Clear() |> ignore
                lastValue.Clear() |> ignore
                isKey := true
            elif x = ':' && not !quoted then
                if not !isKey then
                    failwithf "invalid delimiter at position %d" pos
                isKey := false
            else
                if !isKey then
                    lastKey.Append(x) |> ignore
                else
                    lastValue.Append(x) |> ignore

        add (lastKey.ToString()) (lastValue.ToString())

        let d = Dictionary<_,_>()
        for k,v in l do
            d.Add(k.Trim().ToLower(),v.Trim())
        d
    with
    | exn ->
        raise (Exception(sprintf "Could not parse '%s' as key/value pairs." s, exn))

let saveFile (fileName : string) (contents : string) =
    verbosefn "Saving file %s" fileName
    try
        File.WriteAllText (fileName, contents) |> ok
    with _ ->
        FileSaveError fileName |> fail

let removeFile (fileName : string) =
    if File.Exists fileName then
        tracefn "Removing file %s" fileName
        try
            File.Delete fileName |> ok
        with _ ->
            FileDeleteError fileName |> fail
    else ok ()

let normalizeLineEndings (text : string) =
    text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine)

let removeComment (text:string) =
    let rec stripComment pos =
        if pos = 0 || (Char.IsWhiteSpace text.[pos-1]) then text.Substring(0,pos).Trim()
        else remove (pos+1)
    and remove (startAt: int) =
        match text.IndexOf( "//", startAt ), text.IndexOf( "#", startAt ) with
        | -1 , -1 -> text
        | -1, p | p , -1 -> stripComment p
        | p1, p2 -> stripComment (min p1 p2)
    remove 0

let getSha512Stream (stream:Stream) =
    use hasher = System.Security.Cryptography.SHA512.Create() :> System.Security.Cryptography.HashAlgorithm
    Convert.ToBase64String(hasher.ComputeHash(stream))

let getSha512File (filePath:string) =
    use stream = File.OpenRead(filePath)
    getSha512Stream stream

let fixDatesInArchive fileName =
    try
        use zipToOpen = new FileStream(fileName, FileMode.Open)
        use archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update)
        let maxTime = DateTimeOffset.Now

        for e in archive.Entries do
            try
                let d = min maxTime e.LastWriteTime
                e.LastWriteTime <- d
            with
            | _ -> e.LastWriteTime <- maxTime
    with
    | exn -> traceWarnfn "Could not fix timestamps in %s. Error: %s" fileName exn.Message

let fixArchive fileName =
    if isMonoRuntime then
        fixDatesInArchive fileName

let extractZipToDirectory (zipFileName:string) (directoryName:string) =
    Directory.CreateDirectory directoryName |> ignore
    try
        fixArchive zipFileName
        ZipFile.ExtractToDirectory(zipFileName, directoryName)
    with
    | exn ->
        let targetPath = FileInfo(directoryName).FullName |> normalizePath
        traceWarnfn "Package %s couldn't be extracted to \"%s\". %s: %s. Trying to extract files individually." zipFileName directoryName (exn.GetType().FullName) exn.Message
        use archive = ZipFile.OpenRead zipFileName
        for entry in archive.Entries do
            let destinationPath = Path.GetFullPath(Path.Combine(targetPath, entry.FullName.Trim([| '/'; '\\'|])))

            let fi = FileInfo(destinationPath)
            let folder = fi.Directory.FullName |> normalizePath
            let isSubFolder =
                let comparer =
                    if isLinux then
                        System.StringComparison.Ordinal
                    else
                        System.StringComparison.OrdinalIgnoreCase

                folder.StartsWith(targetPath,comparer)

            if not isSubFolder then
                raise (Exception(sprintf "Error during extraction of %s.%sPackage is corrupted or possible zip leak attack in entry \"%s\"." zipFileName Environment.NewLine entry.FullName))

            if not fi.Directory.Exists then
                fi.Directory.Create()
            if fi.Exists then
                try fi.Delete() with | _ -> ()
            entry.ExtractToFile(destinationPath)



// adapted from MiniRx
// http://minirx.codeplex.com/
[<AutoOpen>]
module ObservableExtensions =

    let private synchronize f =
        let ctx = System.Threading.SynchronizationContext.Current
        f (fun g arg ->
            let nctx = System.Threading.SynchronizationContext.Current
            if ctx <> null && ctx <> nctx then
                ctx.Post((fun _ -> g arg), null)
            else
                g arg)

    type Microsoft.FSharp.Control.Async with
      static member AwaitObservable(ev1:IObservable<'a>) =
        synchronize (fun f ->
          Async.FromContinuations(fun (cont,_econt,_ccont) ->
           let rec callback = (fun value ->
             remover.Dispose()
             f cont value )
           and remover : IDisposable  = ev1.Subscribe callback
           () ))

    [<RequireQualifiedAccess>]
    module Observable =
        open System.Collections.Generic

        /// Creates an observable that calls the specified function after someone
        /// subscribes to it (useful for waiting using 'let!' when we need to start
        /// operation after 'let!' attaches handler)
        let guard f (e:IObservable<'Args>) =
          { new IObservable<'Args> with
              member __.Subscribe observer =
                let rm = e.Subscribe observer in f(); rm }

        let sample (milliseconds: int) source =
            let relay (observer:IObserver<'T>) =
                let rec loop () = async {
                    let! value = Async.AwaitObservable source
                    observer.OnNext value
                    do! Async.Sleep milliseconds
                    return! loop()
                }
                loop ()

            { new IObservable<'T> with
                member __.Subscribe(observer:IObserver<'T>) =
                    let cts = new System.Threading.CancellationTokenSource()
                    Async.Start (relay observer, cts.Token)
                    { new IDisposable with
                        member __.Dispose() = cts.Cancel()
                    }
            }

        let ofSeq s =
            let evt = new Event<_>()
            evt.Publish |> guard (fun _ ->
                for n in s do evt.Trigger(n))

        let private oneAndDone (obs : IObserver<_>) value =
            obs.OnNext value
            obs.OnCompleted()

        let ofAsync a : IObservable<'a> =
            { new IObservable<'a> with
                member __.Subscribe obs =
                    let oneAndDone' = oneAndDone obs
                    let token = new CancellationTokenSource()
                    Async.StartWithContinuations (a,oneAndDone',obs.OnError,obs.OnError,token.Token)
                    { new IDisposable with
                        member __.Dispose() =
                            token.Cancel |> ignore
                            token.Dispose() } }

        let ofAsyncWithToken (token : CancellationToken) a : IObservable<'a> =
            { new IObservable<'a> with
                  member __.Subscribe obs =
                      let oneAndDone' = oneAndDone obs
                      Async.StartWithContinuations (a,oneAndDone',obs.OnError,obs.OnError,token)
                      { new IDisposable with
                            member __.Dispose() = () } }

        let flatten (input: IObservable<#seq<'a>>): IObservable<'a> =
            { new IObservable<'a> with
                member __.Subscribe obs =
                    let cts = new CancellationTokenSource()
                    let sub =
                        input.Subscribe
                          { new IObserver<#seq<'a>> with
                             member __.OnNext values = values |> Seq.iter obs.OnNext
                             member __.OnCompleted() =
                               cts.Cancel()
                               obs.OnCompleted()
                             member __.OnError e =
                               cts.Cancel()
                               obs.OnError e }

                    { new IDisposable with
                        member __.Dispose() =
                            sub.Dispose()
                            cts.Cancel() }}

        let distinct (a: IObservable<'a>): IObservable<'a> =
            let seen = HashSet()
            Observable.filter seen.Add a

type StringBuilder with

    member self.AddLine text =
        self.AppendLine text |> ignore

    member self.AppendLinef text = Printf.kprintf self.AppendLine text

[<RequireQualifiedAccess>]
module Seq =
    let tryExactlyOne (s:#seq<_>) =
        let mutable i = 0
        let mutable first = Unchecked.defaultof<_>
        use e = s.GetEnumerator()
        while (i < 2 && e.MoveNext()) do
            i <- i + 1
            first <- e.Current
        if i = 1 then Some first
        else None

    /// Unzip a seq by mapping the elements that satisfy the predicate
    /// into the first seq and mapping the elements that fail to satisfy the predicate
    /// into the second seq
    let partitionAndChoose predicate choosefn1 choosefn2 sqs =
        (([],[]),sqs)
        ||> Seq.fold (fun (xs,ys) elem ->
            if predicate elem then
                match choosefn1 elem with
                | Some x ->  (x::xs,ys)
                | None -> xs,ys
            else
                match choosefn2 elem with
                | Some y -> xs,y::ys
                | None -> xs,ys
        ) |> fun (xs,ys) ->
            List.rev xs :> seq<_>, List.rev ys :> seq<_>

    let tryTake n (s:#seq<_>) =
        let mutable i = 0
        seq {
            use e = s.GetEnumerator()
            while (i < n && e.MoveNext()) do
                i <- i + 1
                yield e.Current
        }

[<RequireQualifiedAccess>]
module List =
    // Try to find an element in a list.
    // If found, return the element and the list WITHOUT the element.
    // If not found, return None and the whole original list.
    let tryExtractOne fn values =
        match List.tryFindIndex fn values with
        | Some i ->
            let v = values.[i]
            Some v, (values.[0 .. i - 1 ] @ values.[i + 1 .. ])
        | None -> None, values

[<RequireQualifiedAccess>]
module Task =

    let Map<'TIn,'TOut> (mapper : 'TIn -> 'TOut) (t:Task<'TIn>) : Task<'TOut> =
        t.ContinueWith (fun (t:Task<'TIn>) -> mapper(t.Result))
