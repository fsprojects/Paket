module Paket.NuGet

// Type forwards, this way we only need to open "Paket.NuGet" thoughout paket code (others are implementation detail)
type UnparsedPackageFile = Paket.NuGetCache.UnparsedPackageFile
type NuGetPackageCache = Paket.NuGetCache.NuGetPackageCache

open System
open System.IO
open Paket.Logging
open Paket.Utils
open Paket.Domain
open Paket.PackageSources
open System.Net
open System.Runtime.ExceptionServices
open System.Text
open FSharp.Polyfill
open Paket.NuGetCache

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

type NuGetPackageContent =
    { Content : NuGetContent list
      Path : string
      Spec : Nuspec }

let rec ofDirectory targetFolder =
    let dir = DirectoryInfo(targetFolder)
    let subDirs =
        if dir.Exists then
            dir.GetDirectories()
            |> Array.map (fun di -> ofDirectory di.FullName)
            |> Array.toList
        else []

    let files =
        if dir.Exists then
            dir.GetFiles()
            |> Array.map (fun fi -> NuGetFile(fi.Name))
            |> Array.toList
        else []

    NuGetDirectory(dir.Name, subDirs @ files)

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

let GetContent dir = lazy (
    let di = DirectoryInfo(dir)
    if not di.Exists then
        failwithf "%s doesn't exist. nuspec file can't be loaded." di.FullName

    let spec =
        di.EnumerateFiles("*.nuspec", SearchOption.TopDirectoryOnly)
        |> Seq.exactlyOne
        |> fun f -> Nuspec.Load(f.FullName)

    { Content = (ofDirectory dir).Contents
      Path = dir
      Spec = spec })

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

let DownloadLicense(root,force,packageName:PackageName,version:SemVerInfo,licenseUrl,targetFileName) =
    async {
        if String.IsNullOrWhiteSpace licenseUrl then return () else

        let targetFile = FileInfo targetFileName
        if not force && targetFile.Exists && targetFile.Length > 0L then
            if verbose then
                verbosefn "License for %O %O already downloaded" packageName version
        else
            try
                if verbose then
                    verbosefn "Downloading license for %O %O to %s" packageName version targetFileName

                let request = HttpWebRequest.Create(Uri licenseUrl) :?> HttpWebRequest
#if NETSTANDARD1_6
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
                request.Proxy <- Utils.getDefaultProxyFor licenseUrl
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
                if verbose then
                    traceWarnfn "Could not download license for %O %O from %s.%s    %s" packageName version licenseUrl Environment.NewLine exn.Message
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
let GetLibFiles(targetFolder) =
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
let GetTargetsFiles(targetFolder, (pkg : PackageName)) =
    let packageId = pkg.CompareString
    getFiles targetFolder "build" ".targets files"
    |> Array.filter (fun p ->
        let name = System.IO.Path.GetFileName p.FullPath
        name.Equals(packageId + ".targets", StringComparison.OrdinalIgnoreCase) || name.Equals(packageId + ".props", StringComparison.OrdinalIgnoreCase))

/// Finds all analyzer files in a nuget package.
[<Obsolete "Use GetContent instead">]
let GetAnalyzerFiles(targetFolder) = getFilesMatching targetFolder "*.dll" "analyzers" "analyzer dlls"

let tryNuGetV3 (auth, nugetV3Url, package:PackageName) =
    NuGetV3.findVersionsForPackage(nugetV3Url, auth, package)


let rec private getPackageDetails alternativeProjectRoot root force (sources:PackageSource list) packageName (version:SemVerInfo) : Async<PackageResolver.PackageDetails> =
    async {
        let inCache =
            sources
            |> Seq.choose(fun source ->
                NuGetCache.tryGetDetailsFromCache force source.Url packageName version
                |> Option.map (fun details -> source, details))
            |> Seq.tryHead

        let tryV2 (nugetSource:NugetSource) force =
            NuGetV2.getDetailsFromNuGet
                force
                nugetSource
                packageName
                version

        let tryV3 (nugetSource:NugetV3Source) force =
            //if nugetSource.Url.Contains("myget.org") || nugetSource.Url.Contains("nuget.org") || nugetSource.Url.Contains("visualstudio.com") || nugetSource.Url.Contains("/nuget/v3/") then
            //    match NuGetV3.calculateNuGet2Path nugetSource.Url with
            //    | Some url ->
            //        NuGetV2.getDetailsFromNuGet
            //            force
            //            { nugetSource with Url = url } //= .Authentication |> Option.map toBasicAuth)
            //            packageName
            //            version
            //    | _ ->
            //        NuGetV3.GetPackageDetails force nugetSource packageName version
            //else
                NuGetV3.GetPackageDetails force nugetSource packageName version

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
                            if verbose then
                                traceWarnfn "I/O error for source '%O': %O" source exn
                            else
                                traceWarnfn "I/O error for source '%O': %s" source exn.Message
                            return! trySelectFirst (exn :> exn :: errors) rest
                        | e ->
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
                    //| NuGetV3 nugetSource when urlSimilarToTfsOrVsts nugetSource.Url  ->
                        //match NuGetV3.calculateNuGet2Path nugetSource.Url with
                        //| Some url ->
                        //    let nugetSource : NugetSource =
                        //        { Url = url
                        //          Authentication = nugetSource.Authentication }
                        //    return! tryV2 nugetSource force
                        //| _ ->
                    //        return! tryV3 nugetSource force
                    | NuGetV3 nugetSource ->
                        try
                            return! tryV3 nugetSource force
                        with
                        | exn ->
                            eprintfn "Possible Performance degration, V3 was not working: %s" exn.Message
                            if verbose then
                                printfn "Error while using V3 API: %O" exn

                            match NuGetV3.calculateNuGet2Path nugetSource.Url with
                            | Some url ->
                                let nugetSource : NugetSource =
                                    { Url = url
                                      Authentication = nugetSource.Authentication }
                                return! tryV2 nugetSource force
                            | _ ->
                                raise exn
                                return! tryV3 nugetSource force

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
              DirectDependencies = NuGetPackageCache.getDependencies nugetObject |> Set.ofList } }

let rec GetPackageDetails alternativeProjectRoot root force (sources:PackageSource list) groupName packageName (version:SemVerInfo) : Async<PackageResolver.PackageDetails> =
    async {
        try
            return! getPackageDetails alternativeProjectRoot root force sources packageName version
        with
        | exn ->
            if verbose then
                traceWarnfn "GetPackageDetails failed: %O" exn
            else
                traceWarnfn "Something failed in GetPackageDetails, trying again with force: %s" exn.Message
            return! getPackageDetails alternativeProjectRoot root true sources packageName version
    }

let protocolCache = System.Collections.Concurrent.ConcurrentDictionary<_,_>()


type GetVersionError = NuGetCache.NuGetResponseGetVersionsFailure
    //{ Url : string; Error : ExceptionDispatchInfo }
    //static member ofTuple (url,err) =
    //    { Url = url; Error = err }
    //static member ofFailure (f:NuGetCache.NuGetResponseGetVersionsFailure) =
    //    { Url = f.; Error = err }
type GetVersionRequest = NuGetCache.NuGetResponseGetVersions
    //| SuccessVersionResponse of string []
    //| ProtocolNotCached
    //| FailedVersionRequest of GetVersionError

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
let GetVersions force alternativeProjectRoot root (sources, packageName:PackageName) = async {
    let trial force = async {
        let getVersionsFailedCacheFileName (source:PackageSource) =
            let h = source.Url |> NuGetCache.normalizeUrl |> hash |> abs
            let packageUrl = sprintf "Versions.%O.s%d.failed" packageName h
            let fileName = Path.Combine(Constants.NuGetCacheFolder,packageUrl)
            try
                FileInfo fileName
            with
            | exn -> failwithf "%s is not a valid file name. Message: %s" fileName exn.Message

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
                            let auth = source.Authentication |> Option.map toBasicAuth
                            if String.containsIgnoreCase "artifactory" source.Url then
                                return [getVersionsCached "ODataNewestFirst" NuGetV2.tryGetAllVersionsFromNugetODataFindByIdNewestFirst (nugetSource, auth, source.Url, packageName) ]
                            else
                                let v2Feeds =
                                    [ yield getVersionsCached "OData" NuGetV2.tryGetAllVersionsFromNugetODataFindById (nugetSource, auth, source.Url, packageName)
                                      yield getVersionsCached "ODataWithFilter" NuGetV2.tryGetAllVersionsFromNugetODataWithFilter (nugetSource, auth, source.Url, packageName) ]

                                return v2Feeds
                                //let! apiV3 = NuGetV3.getAllVersionsAPI(source.Authentication,source.Url) |> Async.AwaitTask
                                //match apiV3 with
                                //| None -> return v2Feeds
                                //| Some v3Url -> return (getVersionsCached "V3" tryNuGetV3 (nugetSource, auth, v3Url, packageName)) :: v2Feeds
                       | NuGetV3 source ->
                            let! versionsAPI = PackageSources.getNuGetV3Resource source AllVersionsAPI
                            let auth = source.Authentication |> Option.map toBasicAuth
                            return [ getVersionsCached "V3" tryNuGetV3 (nugetSource, auth, versionsAPI, packageName) ]
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
            trial.Requests
            |> Seq.iter (fun sourceResult ->
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
                                if verbose then
                                    add(sprintf " - Request '%s' errored: %O" req.Request.Url req.TaskResult.Exception)
                                else
                                    add(sprintf " - Request '%s' errored: %s" req.Request.Url req.TaskResult.Exception.Message)
                            else
                                match req.TaskResult.Result with
                                | NuGetCache.NuGetResponseGetVersions.FailedVersionRequest err ->
                                    if verbose then
                                        add(sprintf " - Request '%s' finished with: %O" req.Request.Url err.Error.SourceException)
                                    else
                                        add(sprintf " - Request '%s' finished with: %s" req.Request.Url err.Error.SourceException.Message)
                                | NuGetCache.NuGetResponseGetVersions.ProtocolNotCached ->
                                    add(sprintf " - Request '%s' was skipped because 'ProtocolNotCached'" req.Request.Url)
                                | NuGetCache.NuGetResponseGetVersions.SuccessVersionResponse versions ->
                                    add(sprintf " - Request '%s' finished with: [%s]" req.Request.Url (System.String.Join(" ; ", versions)))
                        else
                            add(sprintf " - Request '%s' is not finished yet" req.Request.Url)
            )
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
            traceWarn "Trial1 (NuGet.GetVersions) did not yield any results, trying again."
            let! trial2 = trial true
            match trial2 with
            | _ when Array.isEmpty trial2.Versions |> not ->
                if verbose then
                    reportRequests verbose trial1
                    |> printfn "%s"
                return trial2.Requests
            | _ ->
                let errorMsg =
                    match sources |> Seq.map (fun s -> s.ToString()) |> List.ofSeq with
                    | [source] -> sprintf "Could not find versions for package %O on %O." packageName source
                    | [] -> sprintf "Could not find versions for package %O, because no sources were specified." packageName
                    | sources -> sprintf "Could not find versions for package %O on any of %A." packageName sources
                return raise <| getException trial2 errorMsg }
    return
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

            let _,v,_ = List.head sorted
            SemVer.Parse v,sorted |> List.map (fun (_,_,x) -> x)) }

/// Downloads the given package to the NuGet Cache folder
let DownloadPackage(alternativeProjectRoot, root, (source : PackageSource), caches:Cache list, groupName, packageName:PackageName, version:SemVerInfo, isCliTool, includeVersionInPath, force, detailed) =
    let nupkgName = packageName.ToString() + "." + version.ToString() + ".nupkg"
    let normalizedNupkgName = packageName.ToString() + "." + version.Normalize() + ".nupkg"
    let targetFileName = Path.Combine(Constants.NuGetCacheFolder, normalizedNupkgName)
    let targetFile = FileInfo targetFileName
    let licenseFileName = Path.Combine(Constants.NuGetCacheFolder, packageName.ToString() + "." + version.Normalize() + ".license.html")

    let rec getFromCache (caches:Cache list) =
        match caches with
        | cache::rest ->
            try
                let cacheFolder = DirectoryInfo(cache.Location).FullName
                let cacheFile = FileInfo(Path.Combine(cacheFolder,normalizedNupkgName))
                if cacheFile.Exists && cacheFile.Length > 0L then
                    tracefn "Copying %O %O from cache %s" packageName version cache.Location
                    File.Copy(cacheFile.FullName,targetFileName)
                    true
                else
                    let cacheFile = FileInfo(Path.Combine(cacheFolder,nupkgName))
                    if cacheFile.Exists && cacheFile.Length > 0L then
                        tracefn "Copying %O %O from cache %s" packageName version cache.Location
                        File.Copy(cacheFile.FullName,targetFileName)
                        true
                    else
                        getFromCache rest
            with
            | _ -> getFromCache rest
        | [] -> false

    let rec download authenticated attempt =
        async {
            if not force && targetFile.Exists && targetFile.Length > 0L then
                if verbose then
                    verbosefn "%O %O already downloaded." packageName version
            elif not force && getFromCache caches then
                ()
            else
                match source with
                | LocalNuGet(path,_) ->
                    let path = Utils.normalizeLocalPath path
                    let di = Utils.getDirectoryInfoForLocalNuGetFeed path alternativeProjectRoot root
                    let nupkg = NuGetLocal.findLocalPackage di.FullName packageName version

                    use _ = Profile.startCategory Profile.Category.FileIO
                    File.Copy(nupkg.FullName,targetFileName)
                | _ ->
                // discover the link on the fly
                let downloadUrl = ref ""
                try
                    if authenticated then
                        let group = if groupName = Constants.MainDependencyGroup then "" else sprintf " (%O)" groupName
                        tracefn "Downloading %O %O%s" packageName version group

                    let! nugetPackage = GetPackageDetails alternativeProjectRoot root force [source] groupName packageName version

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

                    use trackDownload = Profile.startCategory Profile.Category.NuGetDownload
                    let! license = Async.StartChild(DownloadLicense(root,force,packageName,version,nugetPackage.LicenseUrl,licenseFileName), 5000)

                    let request = HttpWebRequest.Create(downloadUri) :?> HttpWebRequest
#if NETSTANDARD1_6
                    // Note: this code is not working on regular non-dotnetcore
                    // "This header must be modified with the appropriate property."
                    // But we don't have the UserAgent API available.
                    // We should just switch to HttpClient everywhere.
                    request.Headers.[HttpRequestHeader.UserAgent] <- "Paket"
#else
                    request.UserAgent <- "Paket"
                    request.AutomaticDecompression <- DecompressionMethods.GZip ||| DecompressionMethods.Deflate
#endif

                    if authenticated then
                        match source.Auth |> Option.map toBasicAuth with
                        | None | Some(Token _) -> request.UseDefaultCredentials <- true
                        | Some(Credentials(username, password)) ->
                            // htttp://stackoverflow.com/questions/16044313/webclient-httpwebrequest-with-basic-authentication-returns-404-not-found-for-v/26016919#26016919
                            //this works ONLY if the server returns 401 first
                            //client DOES NOT send credentials on first request
                            //ONLY after a 401
                            //client.Credentials <- new NetworkCredential(auth.Username,auth.Password)

                            //so use THIS instead to send credentials RIGHT AWAY
                            let credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(username + ":" + password))
                            request.Headers.[HttpRequestHeader.Authorization] <- String.Format("Basic {0}", credentials)
                    else
                        request.UseDefaultCredentials <- true

                    request.Proxy <- Utils.getDefaultProxyFor source.Url
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

                    match (httpResponse :?> HttpWebResponse).StatusCode with
                    | HttpStatusCode.OK -> ()
                    | statusCode -> failwithf "HTTP status code was %d - %O" (int statusCode) statusCode

                    if verbose then
                        verbosefn "Downloaded %O %O from %s." packageName version !downloadUrl

                    try
                        do! license
                    with
                    | exn ->
                        if verbose then
                            traceWarnfn "Could not download license for %O %O from %s.%s    %s" packageName version nugetPackage.LicenseUrl Environment.NewLine exn.Message
                with
                | :? System.Net.WebException as exn when
                    attempt < 5 &&
                    exn.Status = WebExceptionStatus.ProtocolError &&
                     (match source.Auth |> Option.map toBasicAuth with
                      | Some(Credentials(_)) -> true
                      | _ -> false)
                        -> do! download false (attempt + 1)
                | exn when String.IsNullOrWhiteSpace !downloadUrl ->
                    raise <| Exception(sprintf "Could not download %O %O." packageName version, exn)
                | exn -> raise <| Exception(sprintf "Could not download %O %O from %s." packageName version !downloadUrl, exn) }

    async {
        do! download true 0
        let! files = NuGetCache.CopyFromCache(root, groupName, targetFile.FullName, licenseFileName, packageName, version, isCliTool, includeVersionInPath, force, detailed)
        return targetFileName,files
    }
