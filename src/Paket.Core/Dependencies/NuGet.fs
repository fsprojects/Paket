module Paket.NuGet

// Type forwards, this way we only need to open "Paket.NuGet" thoughout paket code (others are implementation detail)
type UnparsedPackageFile = Paket.NuGetCache.UnparsedPackageFile
type NuGetPackageCache = Paket.NuGetCache.NuGetPackageCache

open System
open System.Diagnostics
open System.IO
open Paket.Logging
open Paket.Utils
open Paket.Domain
open Paket.PackageSources
open System.Net
open System.Runtime.ExceptionServices
open System.Text
open System.Threading.Tasks
open FSharp.Polyfill
open Paket.NuGetCache
open Paket.PackageResolver
open System.Net.Http
open System.Threading

type NuGetContent =
    | NuGetDirectory of name:string * contents:NuGetContent list
    | NuGetFile of name:string
    member x.Contents =
        match x with
        | NuGetDirectory (_, c) -> c
        | NuGetFile _ -> []
    member x.Name =
        match x with
        | NuGetDirectory(n, _)
        | NuGetFile n -> n

module NuGetContent =
    let toSeq ignoreRoot x =
        let rec toSeqImpl prefix x = seq {
            yield prefix, x
            match x with
            | NuGetDirectory (name, contents) ->
                let newPrefix = sprintf "%s/%s" prefix name
                yield! contents |> Seq.collect (toSeqImpl newPrefix)
            | NuGetFile name -> () }
        if not ignoreRoot then toSeqImpl "" x
        else
            match x with
            | NuGetFile name -> ["", x] :> _
            | NuGetDirectory (name, contents) ->
                contents |> Seq.collect (toSeqImpl "")

    let rec iter f x =
        f x
        match x with
        | NuGetDirectory (name, contents) -> contents |> Seq.iter f
        | NuGetFile _ -> ()

    let mapName f x =
        match x with
        | NuGetDirectory (name, contents) -> NuGetDirectory(f name, contents)
        | NuGetFile name -> NuGetFile (f name)

let inline ofGivenList rootName (directories: string seq) (files: string seq) =
    let folders = System.Collections.Generic.Dictionary<_,NuGetContent list>()
    let l = obj()
    let inline getCurrent n = match folders.TryGetValue n with | true, l -> l | _ -> []
    let inline append n item =
        lock l (fun () -> folders.[n] <- item :: getCurrent n)

    directories
    |> Seq.iter (fun di ->
        let parent = Path.GetDirectoryName di
        let name = Path.GetFileName di
        append parent (NuGetDirectory (name, [])))

    files
    |> Seq.iter (fun fi ->
        let parent = Path.GetDirectoryName fi
        let name = Path.GetFileName fi
        append parent (NuGetFile name))

    let rec fixContent path (c:NuGetContent) =
        match c with
        | NuGetFile _ -> c
        | NuGetDirectory (n, _) ->
            let newPath = Path.Combine(path, n)
            NuGetDirectory(n, getCurrent newPath |> List.map (fixContent newPath) |> List.rev)

    NuGetDirectory("", [])
    |> fixContent ""
    |> NuGetContent.mapName (fun _ -> rootName)

type NuGetPackageContent =
    { Content : NuGetContent list
      Path : string
      Spec : Nuspec }


let rec private ofDirectorySlow targetFolder =
    let dir = DirectoryInfo(targetFolder)
    let subDirs =
        if dir.Exists then
            dir.EnumerateDirectories()
            |> Seq.map (fun di -> ofDirectorySlow di.FullName)
            |> Seq.toList
        else []

    let files =
        if dir.Exists then
            dir.EnumerateFiles()
            |> Seq.map (fun fi -> NuGetFile(fi.Name))
            |> Seq.toList
        else []

    NuGetDirectory(dir.Name, subDirs @ files)


let ofDirectory targetFolder =
    let spec = Path.Combine(targetFolder, "paket-installmodel.cache")
    let readFromDisk () =
        let result = ofDirectorySlow targetFolder
        let text =
            result
            |> NuGetContent.toSeq true
            |> Seq.map (function
                | prefix, NuGetDirectory (name, _) -> sprintf "D: %s/%s" prefix name
                | prefix, NuGetFile name -> sprintf "F: %s/%s" prefix name)
            |> fun s -> String.Join("\n", s)
        try File.WriteAllText(spec, text)
        with e -> eprintf "Error writing '%s': %O" spec e
        result
    if File.Exists spec then
        // read from file if possible
        try
            let rootDirName = Path.GetFileName(targetFolder)
            let spec =
                try File.ReadAllText(spec)
                with
                | :? System.IO.FileNotFoundException -> reraise()
                | :? System.IO.IOException ->
                    // maybe not yet completely written, wait and try again
                    Thread.Sleep 300
                    File.ReadAllText(spec)
            let readLines =
                spec.Split('\n')
                |> Seq.map (fun line ->
                    if line.StartsWith "D: " then
                      Some (line.Substring 4), None
                    elif line.StartsWith "F: " then
                      None, Some (line.Substring 4)
                    else None, None)
                |> Seq.toList
            let directories = readLines |> Seq.choose fst
            let files = readLines |> Seq.choose snd
            ofGivenList rootDirName directories files
        with :? System.IO.IOException as e ->
            eprintf "Error reading '%s', falling back to slow mode. Error was: %O" spec e
            readFromDisk()
    else
        readFromDisk()

(*
let perfCompare f1 f2 =
    let baseDir = @"C:\Users\matth\.nuget\packages"
    let packages =
        Directory.EnumerateDirectories(baseDir)
        |> Seq.collect (fun dir -> Directory.EnumerateDirectories(dir))
        |> Seq.toList
    let inline time f =
        packages |> List.map (fun dir ->
            let w = Stopwatch.StartNew()
            let result = f dir
            w.Stop()
            w.Elapsed, result)
    let lines =
        [1..3]
        |> Seq.map (fun _ -> time f1, time f2)
        |> Seq.toList
    let times1 = lines |> List.map fst
    let times2 = lines |> List.map snd

    Seq.zip times1.Head times2.Head
    |> Seq.iter (fun ((_, left), (_, right)) ->
        if left <> right then eprintf "%A <> %A" left right)

    printfn "f1: %A" (times1 |> Seq.map (fun tl -> tl |> Seq.fold (fun (t1:TimeSpan) t2 -> t1.Add(fst t2)) (new TimeSpan(0L))))
    printfn "f2: %A" (times2 |> Seq.map (fun tl -> tl |> Seq.fold (fun (t1:TimeSpan) t2 -> t1.Add(fst t2)) (new TimeSpan(0L))))

do perfCompare ofDirectory ofDirectoryWithCache*)

let rec createEntry (content:string) =
    let dirName, rest =
        match content.IndexOf "/" with
        | -1 -> None, content
        | i -> Some (content.Substring(0, i)), content.Substring(i+1)
    match dirName with
    | Some name -> NuGetDirectory(name, [ createEntry rest ])
    | None -> NuGetFile(rest)

let rec addContent (content:string) contents =
    let dirName, rest =
        match content.IndexOf "/" with
        | -1 -> None, content
        | i -> Some (content.Substring(0, i)), content.Substring(i+1)
    let wasAdded, contents =
        contents
        |> List.fold (fun (wasAdded, contents) (c: NuGetContent) ->
            match wasAdded, dirName, c with
            | true, _, _ -> true, c :: contents
            | false, Some dir, NuGetDirectory(name, oldContents) when dir = name ->
                true, NuGetDirectory(name, addContent rest oldContents) :: contents
            | false, None, NuGetFile(name) when name = rest ->
                failwithf "File '%s' is already in the tree" name
            | _ -> false, c :: contents) (false, [])
    if wasAdded then
        contents
    else
        createEntry content :: contents

let ofFiles filesList =
    filesList
    |> Seq.fold (fun state file -> addContent file state) []

let GetContentWithNuSpec spec dir =
    lazy { Content = (ofDirectory dir).Contents
           Path = dir
           Spec = spec }

let GetContent dir = lazy (
    let di = DirectoryInfo(dir)
    if not di.Exists then
        failwithf "%s doesn't exist. nuspec file can't be loaded." di.FullName

    let spec =
        di.EnumerateFiles("*.nuspec", SearchOption.TopDirectoryOnly)
        |> Seq.tryExactlyOne
        |> Option.map (fun f -> Nuspec.Load(f.FullName))

    match spec with
    | Some spec -> (GetContentWithNuSpec spec dir).Force()
    | None -> failwithf "Could not find nuspec in '%s', try deleting the directory and restoring again." dir)

let tryFindFolder folder (content:NuGetPackageContent) =
    let rec collectItems prefixFull (prefixInner:string) (content:NuGetContent) =
        let fullPath = Path.Combine(prefixFull, content.Name)
        let relPath = sprintf "%s/%s" prefixInner content.Name
        match content with
        | NuGetDirectory (_, contents) ->
            contents
            |> List.collect (collectItems fullPath relPath)
        | NuGetFile _ ->
            [ {UnparsedPackageFile.FullPath = fullPath
               UnparsedPackageFile.PathWithinPackage = relPath } ]

    content.Content
    |> List.tryPick (fun c ->
        match c with
        | NuGetDirectory(name,contents) when String.equalsIgnoreCase name folder ->
            Some(name,contents)
        | _ -> None)
    |> Option.map (fun (name,item) ->
        item
        |> List.collect (collectItems (Path.Combine(content.Path, name)) name))

let tryFindFile file (content:NuGetPackageContent) =
    content.Content
    |> List.tryPick (fun c ->
        match c with
        | NuGetFile _ when String.equalsIgnoreCase c.Name file ->
            Some {UnparsedPackageFile.FullPath = Path.Combine(content.Path, c.Name)
                  UnparsedPackageFile.PathWithinPackage = c.Name }
        | _ -> None)

let DownloadLicense(root,force,packageName:PackageName,version:SemVerInfo,licenseUrl,targetFileName) =
    let verboseRequest = verbose || isRequestEnvVarSet
    async {
        let targetFile = FileInfo targetFileName
        if not force && targetFile.Exists && targetFile.Length > 0L then
            if verbose then
                verbosefn "License for %O %O already downloaded" packageName version
        else
            try
                if verbose then
                    verbosefn "Downloading license for %O %O to %s" packageName version targetFileName

                let request = HttpWebRequest.Create(Uri licenseUrl) :?> HttpWebRequest
#if NETSTANDARD1_6 || NETSTANDARD2_0
                // Note: this code is not working on regular non-dotnetcore
                // "This header must be modified with the appropriate property."
                // But we don't have the UserAgent API available.
                // We should just switch to HttpClient everywhere.
                request.Headers.[HttpRequestHeader.UserAgent] <- "Paket"
#else
                request.UserAgent <- "Paket"
                request.AutomaticDecompression <- DecompressionMethods.GZip ||| DecompressionMethods.Deflate
                request.Timeout <- 3000
#endif

                request.UseDefaultCredentials <- true
                request.Proxy <- NetUtils.getDefaultProxyFor licenseUrl
                use _ = Profile.startCategory Profile.Category.NuGetRequest
                use! httpResponse = request.AsyncGetResponse()

                use httpResponseStream = httpResponse.GetResponseStream()

                let bufferSize = 4096
                let buffer : byte [] = Array.zeroCreate bufferSize
                let bytesRead = ref -1

                use fileStream = File.Create(targetFileName)

                while !bytesRead <> 0 do
                    let! bytes = httpResponseStream.AsyncRead(buffer, 0, bufferSize)
                    bytesRead := bytes
                    do! fileStream.AsyncWrite(buffer, 0, !bytesRead)

                if verbose then
                    verbosefn "License for %O %O downloaded from %s." packageName version licenseUrl
            with
            | exn ->
                if verboseRequest then
                    traceWarnfn "Could not download license for %O %O from %s.%s    %O" packageName version licenseUrl Environment.NewLine exn
    }

let private getFilesMatching targetFolder searchPattern subFolderName filesDescriptionForVerbose =
    let files =
        let dir = DirectoryInfo(targetFolder)
        let dirFullPath = Path.GetFullPath targetFolder
        let path = Path.Combine(dir.FullName.ToLower(), subFolderName)
        if dir.Exists then
            dir.GetDirectories()
            |> Array.filter (fun fi -> String.equalsIgnoreCase fi.FullName path)
            |> Array.collect (fun dir -> dir.GetFiles(searchPattern, SearchOption.AllDirectories))
            |> Array.map (fun file ->
                let fullPath = Path.GetFullPath file.FullName
                { UnparsedPackageFile.FullPath = fullPath
                  UnparsedPackageFile.PathWithinPackage = fullPath.Substring(dirFullPath.Length + 1).Replace("\\", "/") })
        else
            [||]

    if Logging.verbose then
        if Array.isEmpty files then
            verbosefn "No %s found in %s matching %s" filesDescriptionForVerbose targetFolder searchPattern
        else
            let s = String.Join(Environment.NewLine + "  - ",files |> Array.map (fun l -> l.FullPath))
            verbosefn "%s found in %s matching %s:%s  - %s" filesDescriptionForVerbose targetFolder searchPattern Environment.NewLine s

    files

let private getFiles targetFolder subFolderName filesDescriptionForVerbose =
    getFilesMatching targetFolder "*.*" subFolderName filesDescriptionForVerbose

/// Finds all libraries in a nuget package.
[<Obsolete "Use GetContent instead">]
let GetLibFiles targetFolder =
    let libs = getFiles targetFolder "lib" "libraries"
    let refs = getFiles targetFolder "ref" "libraries"
    let runtimeLibs = getFiles targetFolder "runtimes" "libraries"
    refs
    |> Array.append libs
    |> Array.append runtimeLibs
    |> Array.filter (fun p ->
        let ext = System.IO.Path.GetExtension p.FullPath

        ".dll".Equals(ext, StringComparison.OrdinalIgnoreCase))

/// Finds all targets files in a nuget package.
[<Obsolete "Use GetContent instead">]
let GetTargetsFiles(targetFolder, pkg : PackageName) =
    let packageId = pkg.CompareString
    getFiles targetFolder "build" ".targets files"
    |> Array.filter (fun p ->
        let name = System.IO.Path.GetFileName p.FullPath
        name.Equals(packageId + ".targets", StringComparison.OrdinalIgnoreCase) || name.Equals(packageId + ".props", StringComparison.OrdinalIgnoreCase))

/// Finds all analyzer files in a nuget package.
[<Obsolete "Use GetContent instead">]
let GetAnalyzerFiles targetFolder = getFilesMatching targetFolder "*.dll" "analyzers" "analyzer dlls"

let tryNuGetV3 (auth, nugetV3Url, package:PackageName) =
    NuGetV3.findVersionsForPackage(nugetV3Url, auth, package)


let rec private getPackageDetails alternativeProjectRoot root force (parameters:GetPackageDetailsParameters) : Async<PackageResolver.PackageDetails> =
    let sources = parameters.Package.Sources
    let packageName = parameters.Package.PackageName
    let version = parameters.Version
    let verboseRequest = verbose || isRequestEnvVarSet
    async {
        let inCache =
            sources
            |> Seq.choose(fun source ->
                NuGetCache.tryGetDetailsFromCache force source.Url packageName version
                |> Option.map (fun details -> source, details))
            |> Seq.tryHead

        let tryV2 (nugetSource:NuGetSource) force =
            NuGetV2.getDetailsFromNuGet
                force
                parameters.VersionIsAssumed
                nugetSource
                packageName
                version


        let tryV3 (nugetSource:NuGetV3Source) force =
            NuGetV3.GetPackageDetails force nugetSource packageName version

        let v3AndFallBack (nugetSource:NuGetV3Source) force = async {
            try
                return! tryV3 nugetSource force
            with
            | exn ->
                traceWarnfn "Possible Performance degradation, V3 was not working: %s" exn.Message
                if verboseRequest then
                    printfn "Error while using V3 API: %O" exn

                match NuGetV3.calculateNuGet2Path nugetSource.Url with
                | Some url ->
                    let nugetSource : NuGetSource =
                        { Url = url
                          Authentication = nugetSource.Authentication }
                    return! tryV2 nugetSource force
                | _ ->
                    raise exn
                    return! tryV3 nugetSource force

        }

        let getPackageDetails force =
            // helper to work through the list sequentially
            let rec trySelectFirst (errors:exn list) workLeft  =
                async {
                    match workLeft with
                    | (source, work) :: rest ->
                        try
                            let! r = work
                            match r with
                            | ODataSearchResult.Match result -> return Some (source, result), errors
                            | ODataSearchResult.EmptyResult -> return! trySelectFirst errors rest
                        with
                        | :? System.IO.IOException as exn ->
                            // Handling IO exception here for less noise in output: https://github.com/fsprojects/Paket/issues/2480
                            if verboseRequest then
                                traceWarnfn "I/O error for source '%O': %O" source exn
                            else
                                traceWarnfn "I/O error for source '%O': %s" source exn.Message
                            return! trySelectFirst (exn :> exn :: errors) rest
                        | e ->
                            let mutable information = ""
                            match e.GetBaseException() with
                            | :? RequestFailedException as re ->
                                match re.Info with
                                | Some requestinfo -> if requestinfo.StatusCode = HttpStatusCode.NotFound then
                                                         information <- re.Message
                                | None -> ignore()
                            | _ -> ignore()
                            if not verboseRequest && information <> "" then
                                traceWarnIfNotBefore (source, information) "Source '%O' exception: %O" source information
                            else
                                traceWarnfn "Source '%O' exception: %O" source e
                            //let capture = ExceptionDispatchInfo.Capture e
                            return! trySelectFirst (e :: errors) rest
                    | [] -> return None, errors
                }
            match inCache with
            | Some (source, ODataSearchResult.Match result) -> async { return Some (source, result), [] }
            | _ ->
                sources
                |> List.sortBy (fun source ->
                    // put local caches to the end
                    // prefer nuget gallery
                    match source with
                    | LocalNuGet(_,Some _) -> 10
                    | s when s.NuGetType = KnownNuGetSources.OfficialNuGetGallery -> 1
                    | _ -> 3)
                |> List.map (fun source -> source, async {
                    match source with
                    | NuGetV2 nugetSource ->
                        return! tryV2 nugetSource force
                    | NuGetV3 nugetSource ->
                        return! v3AndFallBack nugetSource force
                    | LocalNuGet(path,hasCache) ->
                        return! NuGetLocal.getDetailsFromLocalNuGetPackage hasCache.IsSome alternativeProjectRoot root path packageName version
                })
                |> trySelectFirst []

        let! maybePackageDetails, errors = getPackageDetails force
        let! source,nugetObject =
            async {
                let fallback () =
                    let inner =
                        match errors with
                        | [e] -> e
                        | [] -> null
                        | l -> AggregateException(l) :> exn

                    match sources |> List.map (fun (s:PackageSource) -> s.ToString()) with
                    | [source] ->
                        rethrowf exn inner "Couldn't get package details for package %O %O on %O." packageName version source
                    | [] ->
                        rethrowf exn inner "Couldn't get package details for package %O %O, because no sources were specified." packageName version
                    | sources ->
                        rethrowf exn inner "Couldn't get package details for package %O %O on any of %A." packageName version sources

                match maybePackageDetails with
                | None -> return fallback()
                | Some packageDetails -> return packageDetails
            }

        let encodeURL (url:string) =
            if String.IsNullOrWhiteSpace url then url else
            let segments = url.Split [|'?'|]
            let baseUrl = segments.[0]
            Array.set segments 0 (baseUrl.Replace("+", "%2B"))
            System.String.Join("?", segments)

        let newName = PackageName nugetObject.PackageName
        if packageName <> newName then
            failwithf "Package details for %O are not matching requested package %O." newName packageName

        return
            { Name = PackageName nugetObject.PackageName
              Source = source
              DownloadLink = encodeURL nugetObject.DownloadUrl
              Unlisted = nugetObject.Unlisted
              LicenseUrl = nugetObject.LicenseUrl
              DirectDependencies = nugetObject.GetDependencies() } }

let rec GetPackageDetails alternativeProjectRoot root force (parameters:GetPackageDetailsParameters): Async<PackageResolver.PackageDetails> =
    let verboseRequest = verbose || isRequestEnvVarSet
    async {
        try
            return! getPackageDetails alternativeProjectRoot root force parameters
        with
        | exn when (not force) ->
            if verboseRequest then
                traceWarnfn "GetPackageDetails failed: %O" exn
            else
                traceWarnfn "Something failed in GetPackageDetails, trying again with force: %s" exn.Message
            return! getPackageDetails alternativeProjectRoot root true parameters
    }

let protocolCache = System.Collections.Concurrent.ConcurrentDictionary<_,_>()


type GetVersionError = NuGetCache.NuGetResponseGetVersionsFailure
type GetVersionRequest = NuGetCache.NuGetResponseGetVersions

type SourceResponseType =
    | SourceNoResult
    | SourceSuccess of version : string [] * fastest : int
type SourceRequest =
    { TaskResult : System.Threading.Tasks.Task<GetVersionRequest>
      Request : NuGetCache.NuGetRequestGetVersions }
type SourceResponse =
    { Source : PackageSource; Result : SourceResponseType; Requests : SourceRequest [] }
    member x.Versions =
        match x.Result with
        | SourceNoResult -> [||]
        | SourceSuccess (v, _) -> v
type GetVersionRequestResult =
    { Requests : SourceResponse [] }
    member x.Versions =
        x.Requests |> Array.collect (fun r -> r.Versions)

let getVersionsCached key f (source, auth, nugetURL, package) =
    let request:NuGetCache.NuGetRequestGetVersions = f (auth, nugetURL, package)
    NuGetCache.NuGetRequestGetVersions.ofFunc request.Url (fun _ ->
        async {
            match protocolCache.TryGetValue(source) with
            | true, v when v <> key -> return GetVersionRequest.ProtocolNotCached
            | true, v when v = key ->
                let! result = request.DoRequest()
                return result
            | _ ->
                let! result = request.DoRequest()
                match result with
                | GetVersionRequest.SuccessVersionResponse x ->
                    protocolCache.TryAdd(source, key) |> ignore
                    return GetVersionRequest.SuccessVersionResponse x
                | err -> return err
        })


/// Allows to retrieve all version no. for a package from the given sources.
let GetVersions force alternativeProjectRoot root (parameters:GetPackageVersionsParameters) = async {
    let packageName = parameters.Package.PackageName
    let sources = parameters.Package.Sources
    let verboseRequest = verbose || isRequestEnvVarSet
    let trial force = async {
        let getVersionsFailedCacheFileName (source:PackageSource) =
            let h = source.Url |> NuGetCache.normalizeUrl |> hash |> abs
            let packageUrl = sprintf "Versions.%O.s%d.failed" packageName h
            let fileName = Path.Combine(Constants.NuGetCacheFolder,packageUrl)
            try
                FileInfo fileName
            with
            | inner -> rethrowf exn inner "'%s' is not a valid file name" fileName

        let sources =
            sources
            |> Array.ofSeq
            |> Array.map (fun nugetSource ->
                let errorFile = getVersionsFailedCacheFileName nugetSource
                errorFile.Exists,nugetSource)

        let force = force || Array.forall fst sources

        let versionResponse =
            sources
            |> Seq.map (fun (errorFileExists,nugetSource) ->
                async {
                       if (not force) && errorFileExists then return [] else
                       match nugetSource with
                       | NuGetV2 source ->
                            let auth = source.Authentication
                            if String.containsIgnoreCase "artifactory" source.Url then
                                return [getVersionsCached "ODataNewestFirst" NuGetV2.tryGetAllVersionsFromNugetODataFindByIdNewestFirst (nugetSource, auth, source.Url, packageName) ]
                            else
                                let v2Feeds =
                                    [ yield getVersionsCached "OData" NuGetV2.tryGetAllVersionsFromNugetODataFindById (nugetSource, auth, source.Url, packageName)
                                      yield getVersionsCached "ODataWithFilter" NuGetV2.tryGetAllVersionsFromNugetODataWithFilter (nugetSource, auth, source.Url, packageName) ]

                                return v2Feeds
                       | NuGetV3 source ->
                            let! versionsAPI = NuGetV3.getNuGetV3Resource source NuGetV3.AllVersionsAPI
                            return [ getVersionsCached "V3" tryNuGetV3 (nugetSource, source.Authentication, versionsAPI, packageName) ]
                       | LocalNuGet(path,Some _) ->
                            return [ NuGetLocal.getAllVersionsFromLocalPath (true, path, packageName, alternativeProjectRoot, root) ]
                       | LocalNuGet(path,None) ->
                            return [ NuGetLocal.getAllVersionsFromLocalPath (false, path, packageName, alternativeProjectRoot, root) ]
                })
            |> Seq.toArray
            |> Array.map (fun a ->
                async {
                    let! requests = a
                    let! runningTasks, result =
                        requests
                        |> List.map NuGetCache.NuGetRequestGetVersions.run
                        |> Async.tryFindSequential (fun req -> req.IsSuccess)
                    let zippedTasks =
                        Array.zip (requests |> List.toArray) runningTasks
                        |> Array.map (fun (req, task) ->
                            { TaskResult = task; Request = req })
                    return zippedTasks,result
                })
            |> Async.Parallel

        let! result = versionResponse
        let allResults =
            result
            |> Array.zip sources
            |> Array.map (fun ((_,s),(tasks, index)) ->
                let result =
                    match index with
                    | Some index (* when Array.isEmpty tasks.[index].Result.Versions |> not *) ->
                        if Array.isEmpty tasks.[index].TaskResult.Result.Versions |> not then
                            try
                                let errorFile = getVersionsFailedCacheFileName s
                                if errorFile.Exists then
                                    File.Delete(errorFile.FullName)
                            with e ->
                                traceWarnfn "Error while deleting error file: %O" e
                        let takeResult = tasks.[index].TaskResult.Result
                        SourceSuccess (takeResult.Versions, index)
                    | _ ->
                        try
                            let errorFile = getVersionsFailedCacheFileName s
                            if errorFile.Exists |> not then
                                File.WriteAllText(errorFile.FullName,DateTime.Now.ToString())
                        with _ -> ()
                        SourceNoResult
                { Source = s; Result = result; Requests = tasks })
        return { Requests = allResults } }

    let! versions = async {
        let! trial1 = trial force
        let reportRequests withDetails (trial:GetVersionRequestResult) =
            let sb = new StringBuilder()
            let add s = sb.AddLine(s) |> ignore
            for sourceResult in trial.Requests do
                match sourceResult.Result with
                | SourceNoResult ->
                    add(sprintf "Source '%s' yielded no results" sourceResult.Source.Url)
                | SourceSuccess (s, i) ->
                    add(sprintf "Source '%s' yielded (%d): [%s]" sourceResult.Source.Url i (System.String.Join(" ; ", s)))
                if withDetails then
                    for req in sourceResult.Requests do
                        if req.TaskResult.IsCompleted then
                            if req.TaskResult.IsCanceled then
                                add(sprintf " - Request '%s' was cancelled (another one was faster)" req.Request.Url)
                            elif req.TaskResult.IsFaulted then
                                if verboseRequest then
                                    add(sprintf " - Request '%s' errored: %O" req.Request.Url req.TaskResult.Exception)
                                else
                                    add(sprintf " - Request '%s' errored: %s" req.Request.Url req.TaskResult.Exception.Message)
                            else
                                match req.TaskResult.Result with
                                | NuGetCache.NuGetResponseGetVersions.FailedVersionRequest err ->
                                    if verboseRequest then
                                        add(sprintf " - Request '%s' finished with: %O" req.Request.Url err.Error.SourceException)
                                    else
                                        add(sprintf " - Request '%s' finished with: %s" req.Request.Url err.Error.SourceException.Message)
                                | NuGetCache.NuGetResponseGetVersions.ProtocolNotCached ->
                                    add(sprintf " - Request '%s' was skipped because 'ProtocolNotCached'" req.Request.Url)
                                | NuGetCache.NuGetResponseGetVersions.SuccessVersionResponse versions ->
                                    add(sprintf " - Request '%s' finished with: [%s]" req.Request.Url (System.String.Join(" ; ", versions)))
                        else
                            add(sprintf " - Request '%s' is not finished yet" req.Request.Url)

            sb.ToString()
        let getException (trial:GetVersionRequestResult) message =
            trial.Requests
            |> Seq.map (fun sourceResult ->
                let innerExns =
                    sourceResult.Requests
                    |> Seq.map (fun req ->
                        if req.TaskResult.IsCompleted then
                            if req.TaskResult.IsCanceled then
                                Exception(sprintf "Request '%s' was cancelled (another one was faster)" req.Request.Url)
                            elif req.TaskResult.IsFaulted then
                                Exception(sprintf "Request '%s' errored" req.Request.Url, req.TaskResult.Exception)
                            else
                                match req.TaskResult.Result with
                                | NuGetCache.NuGetResponseGetVersions.FailedVersionRequest err ->
                                    Exception(sprintf "Request '%s' finished with error" req.Request.Url, err.Error.SourceException)
                                | NuGetCache.NuGetResponseGetVersions.ProtocolNotCached ->
                                    Exception(sprintf "Request '%s' was skipped because 'ProtocolNotCached'" req.Request.Url)
                                | NuGetCache.NuGetResponseGetVersions.SuccessVersionResponse versions ->
                                    Exception(sprintf "Request '%s' finished with: [%s]" req.Request.Url (System.String.Join(" ; ", versions)))
                        else
                            Exception(sprintf "Request '%s' is not finished yet" req.Request.Url))

                match sourceResult.Result with
                | SourceNoResult ->
                    AggregateException(sprintf "Source '%s' yielded no results" sourceResult.Source.Url, innerExns) :> exn
                | SourceSuccess (s, i) ->
                    AggregateException(sprintf "Source '%s' yielded (%d): [%s]" sourceResult.Source.Url i (System.String.Join(" ; ", s)), innerExns) :> exn
            )
            |> fun exns -> AggregateException(message, exns) :> exn

        if verbose then
            reportRequests verbose trial1
            |> printfn "%s"
        match trial1 with
        | _ when Array.isEmpty trial1.Versions |> not ->
            return trial1.Requests
        | _ ->
            let requested = trial1.Requests |> Seq.collect (fun i -> i.Requests) |> Seq.map (fun r -> "   " + r.Request.Url)
            traceWarnfn "Trial1 (NuGet.GetVersions) did not yield any results, trying again.%s%O" Environment.NewLine (String.Join(Environment.NewLine, requested))
            if verboseRequest then
                reportRequests verboseRequest trial1
                |> printfn "%s"
            let! trial2 = trial true
            match trial2 with
            | _ when Array.isEmpty trial2.Versions |> not ->
                return trial2.Requests
            | _ ->
                let errorMsg =
                    match sources |> Seq.map (fun s -> s.ToString()) |> List.ofSeq with
                    | [source] -> sprintf "Could not find versions for package %O on %O." packageName source
                    | [] -> sprintf "Could not find versions for package %O, because no sources were specified." packageName
                    | sources -> sprintf "Could not find versions for package %O on any of %A." packageName sources
                return raise (getException trial2 errorMsg) }

    let mergedResults =
        versions
        |> Seq.toList
        |> List.collect (fun sr ->
            sr.Versions
            |> Array.toList
            |> List.map (fun v -> v, sr.Source))
        |> List.map (fun (v,s) -> SemVer.Parse v,v,s)
        |> List.groupBy (fun (v,_,_) -> v.Normalize())
        |> List.map (fun (_,s) ->
            let sorted = s |> List.sortByDescending (fun (_,_,s) -> s.IsLocalFeed)

            let v,_,_ = List.head sorted
            v,sorted |> List.map (fun (_,_,x) -> x))

    return mergedResults }

let private getLicenseFile (packageName:PackageName) version =
    Path.Combine(NuGetCache.GetTargetUserFolder packageName version, NuGetCache.GetLicenseFileName packageName version)

/// Downloads the given package to the NuGet Cache folder
let private downloadAndExtractPackage(alternativeProjectRoot, root, isLocalOverride:bool, config:PackagesFolderGroupConfig, source : PackageSource, caches:Cache list, groupName, packageName:PackageName, version:SemVerInfo, kind, includeVersionInPath, downloadLicense, force, detailed) =
    let nupkgName = packageName.ToString() + "." + version.ToString() + ".nupkg"
    let normalizedNupkgName = NuGetCache.GetPackageFileName packageName version
    let configResolved = config.Resolve root groupName packageName version includeVersionInPath
    let verboseRequest = verbose || isRequestEnvVarSet
    let targetFileName =
        if not isLocalOverride then
            NuGetCache.GetTargetUserNupkg packageName version
        else
            match configResolved.Path with
            | Some p -> Path.Combine(p, nupkgName)
            | None -> failwithf "paket.local in combination with storage:none is not supported"

    if isLocalOverride && not force then
        failwithf "internal error: when isLocalOverride is specified then force needs to be specified as well"
    let targetFile = FileInfo targetFileName
    let licenseFileName = getLicenseFile packageName version

    if force then
        match configResolved.Path with
        | Some p ->
            if verbose then
                verbosefn "Cleaning %s" p
            CleanDir p
        | _ -> ()

    let ensureDir (fileName: string) =
        let parent = Path.GetDirectoryName fileName
        if not (Directory.Exists parent) then Directory.CreateDirectory parent |> ignore

    let rec getFromCache (caches:Cache list) =
        match caches with
        | cache::rest ->
            try
                let cacheFolder = DirectoryInfo(cache.Location).FullName
                let cacheFile = FileInfo(Path.Combine(cacheFolder,normalizedNupkgName))
                if cacheFile.Exists && cacheFile.Length > 0L then
                    tracefn "Copying %O %O from cache %s" packageName version cache.Location
                    ensureDir targetFileName
                    File.Copy(cacheFile.FullName,targetFileName,true)
                    true
                else
                    let cacheFile = FileInfo(Path.Combine(cacheFolder,nupkgName))
                    if cacheFile.Exists && cacheFile.Length > 0L then
                        tracefn "Copying %O %O from cache %s" packageName version cache.Location
                        ensureDir targetFileName
                        File.Copy(cacheFile.FullName,targetFileName,true)
                        true
                    else
                        getFromCache rest
            with
            | _ -> getFromCache rest
        | [] -> false

    let getFromFallbackFolder () =
        match NuGetCache.TryGetFallbackNupkg packageName version with
        | Some fileName ->
            verbosefn "Copying %O %O from SDK cache" packageName version
            use __ = Profile.startCategory Profile.Category.FileIO
            ensureDir targetFileName
            File.Copy(fileName,targetFileName,true)
            true
        | None -> false

    let rec download authenticated attempt =
        async {
            if not force && targetFile.Exists && targetFile.Length > 0L then
                if verbose then
                    verbosefn "%O %O already downloaded." packageName version
                return false
            elif getFromFallbackFolder () || (not force && getFromCache caches) then
                return true
            else
                match source with
                | LocalNuGet(path,_) ->
                    let path = Utils.normalizeLocalPath path
                    let di = Utils.getDirectoryInfoForLocalNuGetFeed path alternativeProjectRoot root
                    let nupkg = NuGetLocal.findLocalPackage di.FullName packageName version

                    use _ = Profile.startCategory Profile.Category.FileIO
                    ensureDir targetFileName
                    File.Copy(nupkg.FullName,targetFileName,true)
                    return true
                | _ ->
                // discover the link on the fly
                let downloadUrl = ref ""
                let mutable didDownload = false
                try
                    let sw = System.Diagnostics.Stopwatch.StartNew()
                    let groupString = if groupName = Constants.MainDependencyGroup then "" else sprintf " (%O)" groupName
                    if authenticated then
                        tracefn "Downloading %O %O%s" packageName version groupString

                    let! nugetPackage = GetPackageDetails alternativeProjectRoot root force (GetPackageDetailsParameters.ofParams [source] groupName packageName version)

                    let encodeURL (url:string) = url.Replace("+","%2B")
                    let downloadUri =
                        if Uri.IsWellFormedUriString(nugetPackage.DownloadLink, UriKind.Absolute) then
                            if verbose then
                                printfn "Downloading directly from DownloadLink: %s" nugetPackage.DownloadLink
                            Uri nugetPackage.DownloadLink
                        else
                            let sourceUrl =
                                if nugetPackage.Source.Url.EndsWith("/") then nugetPackage.Source.Url
                                else nugetPackage.Source.Url + "/"
                            let uri = Uri(Uri sourceUrl, nugetPackage.DownloadLink)
                            if verbose then
                                printfn "Downloading with combined url. Source: %s DownloadLink: %s Combined: %s" sourceUrl nugetPackage.DownloadLink (uri.ToString())
                            uri

                    downloadUrl := downloadUri.ToString()

                    if authenticated && verbose then
                        tracefn "Downloading from %O to %s" !downloadUrl targetFileName

                    ensureDir targetFileName

                    use _trackDownload = Profile.startCategory Profile.Category.NuGetDownload
                    let! cancellationToken = Async.CancellationToken

                    // default of three minute timeout
                    let defaultTimeout = 180000
                    let getTimeoutOrDefaultValue registryEntry defaultValue =
                        match Environment.GetEnvironmentVariable registryEntry with
                        | a when System.String.IsNullOrWhiteSpace a -> defaultValue
                        | a ->
                            match System.Int32.TryParse a with
                            | true, v -> v
                            | _ -> traceWarnfn "%s is not set to an interval in milliseconds, ignoring the value and defaulting to %d" registryEntry defaultValue
                                   defaultValue

                    // timeout for the request of the package to be downloaded
                    let requestTimeout =
                        getTimeoutOrDefaultValue "PAKET_REQUEST_TIMEOUT" defaultTimeout
                    // timeout for response of the stream for the package to be downloaded
                    let responseStreamTimeout =
                        getTimeoutOrDefaultValue "PAKET_RESPONSE_STREAM_TIMEOUT" defaultTimeout
                    // timeout for the read and write stream operations on the package to be downloaded
                    let streamReadWriteTimeout =
                        getTimeoutOrDefaultValue "PAKET_STREAMREADWRITE_TIMEOUT" defaultTimeout

                    let requestTokenSource = System.Threading.CancellationTokenSource.CreateLinkedTokenSource cancellationToken
                    requestTokenSource.CancelAfter requestTimeout

                    let client = NetUtils.createHttpClient(!downloadUrl, source.Auth.Retrieve (attempt <> 0))

                    let lastSpeedMeasure = Stopwatch.StartNew()
                    let mutable readSinceLastMeasure = 0L

                    let requestMsg = new HttpRequestMessage(HttpMethod.Get, downloadUri)
                    let! responseMsg = client.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead, requestTokenSource.Token) |> Async.AwaitTask
                    match responseMsg.StatusCode with
                    | HttpStatusCode.OK -> ()
                    | statusCode -> failwithf "HTTP status code was %d - %O" (int statusCode) statusCode

                    let! httpResponseStream = responseMsg.Content.ReadAsStreamAsync() |> Async.AwaitTask

                    if httpResponseStream.CanTimeout
                    then httpResponseStream.ReadTimeout <- responseStreamTimeout

                    let bufferSize = 1024 * 10
                    let buffer : byte [] = Array.zeroCreate bufferSize
                    let bytesRead = ref -1

                    let tmpFile = Path.GetDirectoryName targetFileName + Path.GetRandomFileName() + ".paket_tmp"
                    try
                        let mutable pos = 0L
                        do! async {
                            use fileStream = File.Create(tmpFile)
                            let printProgress = not Console.IsOutputRedirected

                            let length = try Some httpResponseStream.Length with :? System.NotSupportedException -> None
                            while !bytesRead <> 0 do
                                if printProgress && lastSpeedMeasure.Elapsed > TimeSpan.FromSeconds(10.) then
                                    // report speed and progress
                                    let speed = int (float readSinceLastMeasure * 8. / float lastSpeedMeasure.ElapsedMilliseconds)
                                    let percent =
                                        match length with
                                        | Some l -> sprintf "%d" (int (pos * 100L / l))
                                        | None -> "Unknown"
                                    tracefn "Still downloading from %O to %s (%d kbit/s, %s %%)" !downloadUrl tmpFile speed percent
                                    readSinceLastMeasure <- 0L
                                    lastSpeedMeasure.Restart()

                                // recreate token every time to continue as long as there is progress...
                                let streamReadWriteTokenSource = System.Threading.CancellationTokenSource.CreateLinkedTokenSource cancellationToken
                                streamReadWriteTokenSource.CancelAfter streamReadWriteTimeout
                                // if there is no response for streamReadWriteTimeout milliseconds -> abort
                                let! bytes = httpResponseStream.ReadAsync(buffer, 0, bufferSize, streamReadWriteTokenSource.Token) |> Async.AwaitTaskWithoutAggregate
                                bytesRead := bytes
                                do! fileStream.WriteAsync(buffer, 0, !bytesRead, streamReadWriteTokenSource.Token) |> Async.AwaitTaskWithoutAggregate
                                readSinceLastMeasure <- readSinceLastMeasure + int64 bytes
                                pos <- pos + int64 bytes }
                        // close/dispose filestream such that we can move

                        if not (File.Exists targetFileName) then
                            try File.Move(tmpFile, targetFileName)
                                didDownload <- true
                            with | :? System.IO.IOException as e ->
                                traceWarnfn "Error while moving temp file as '%s' already exists (maybe some other instance downloaded it as well): %O" targetFileName e
                                ()
                        else
                            tracefn "Not moving as '%s' already exists (maybe some other instance downloaded it as well)" targetFileName

                        let speed = int (float pos * 8. / float sw.ElapsedMilliseconds)
                        let size = pos / (1024L * 1024L)
                        tracefn "Download of %O %O%s done in %s. (%d kbit/s, %d MB)" packageName version groupString (Utils.TimeSpanToReadableString sw.Elapsed) speed size
                    finally
                        if File.Exists tmpFile then
                            try File.Delete(tmpFile)
                            with | :? System.IO.IOException ->
                                traceWarnfn "Error while removing temp file '%s'" tmpFile
                                ()

                    try
                        if downloadLicense && not (String.IsNullOrWhiteSpace nugetPackage.LicenseUrl) then
                            do! DownloadLicense(root,force,packageName,version,nugetPackage.LicenseUrl,licenseFileName)
                        return didDownload
                    with
                    | exn ->
                        if verbose then
                            traceWarnfn "Could not download license for %O %O from %s.%s    %s" packageName version nugetPackage.LicenseUrl Environment.NewLine exn.Message
                        return didDownload
                with
                | :? System.Net.WebException as exn when
                    attempt < 5 &&
                    exn.Status = WebExceptionStatus.ProtocolError &&
                     (match source.Auth.Retrieve (attempt <> 0) with
                      | Some(Credentials _) -> true
                      | _ -> false)
                        ->  traceWarnfn "Could not download %O %O.%s    %s.%sRetry." packageName version Environment.NewLine exn.Message Environment.NewLine
                            return! download false (attempt + 1)
                | inner when String.IsNullOrWhiteSpace !downloadUrl ->
                    return rethrowf exn inner "Could not download %O %O." packageName version
                | inner -> return rethrowf exn inner "Could not download %O %O from %s." packageName version !downloadUrl }

    async {
        configResolved.Path |> Option.iter SymlinkUtils.delete

        let! didDownload = download true 0

        match isLocalOverride, configResolved with
        | true, ResolvedPackagesFolder.NoPackagesFolder -> return failwithf "paket.local in combination with storage:none is not supported (use storage: symlink instead)"
        | true, ResolvedPackagesFolder.SymbolicLink directory
        | true, ResolvedPackagesFolder.ResolvedFolder directory ->
            let! folder = ExtractPackage(targetFile.FullName, directory, packageName, version, detailed)
            return targetFileName,folder
        | false, ResolvedPackagesFolder.SymbolicLink folder ->
            folder |> Utils.DirectoryInfo |> Utils.deleteDir
            ensureDir folder

            let! extractedUserFolder = async {
                if not didDownload then
                    return GetPackageUserFolderDir (packageName, version, kind)
                else return! ExtractPackageToUserFolder(source, targetFile.FullName, packageName, version, kind)
            }

            SymlinkUtils.makeDirectoryLink folder extractedUserFolder

            let packageFilePath = Path.Combine(extractedUserFolder, NuGetCache.GetPackageFileName packageName version)
            return packageFilePath, folder
        | false, otherConfig ->
            otherConfig.Path |> Option.iter SymlinkUtils.delete

            let! extractedUserFolder = async {
                if not didDownload then
                    return GetPackageUserFolderDir (packageName, version, kind)
                else return! ExtractPackageToUserFolder(source, targetFile.FullName, packageName, version, kind)
            }

            let! files = NuGetCache.CopyFromCache(otherConfig, targetFile.FullName, licenseFileName, packageName, version, force, detailed)

            let finalFolder =
                match files with
                | Some f -> f
                | None -> extractedUserFolder

            return targetFileName,finalFolder
    }


let DownloadAndExtractPackage(alternativeProjectRoot, root, isLocalOverride:bool, config:PackagesFolderGroupConfig, source : PackageSource, caches:Cache list, groupName, packageName:PackageName, version:SemVerInfo, kind, includeVersionInPath, downloadLicense, force, detailed) =
    downloadAndExtractPackage(alternativeProjectRoot, root, isLocalOverride, config, source , caches, groupName, packageName, version, kind, includeVersionInPath, downloadLicense, force, detailed)
