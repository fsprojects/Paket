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
                let fullPath = Path.GetFullPath file.FullName;
                { UnparsedPackageFile.FullPath = fullPath; UnparsedPackageFile.PathWithinPackage = fullPath.Substring(dirFullPath.Length + 1).Replace("\\", "/") })
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
let GetLibFiles(targetFolder) =
    let libs = getFiles targetFolder "lib" "libraries"
    let refs = getFiles targetFolder "ref" "libraries"
    let runtimeLibs = getFiles targetFolder "runtimes" "libraries"
    refs
    |> Array.append libs
    |> Array.append runtimeLibs

/// Finds all targets files in a nuget package.
let GetTargetsFiles(targetFolder, (pkg : PackageName)) =
    let packageId = pkg.CompareString
    getFiles targetFolder "build" ".targets files"
    |> Array.filter (fun p ->
        let name = System.IO.Path.GetFileName p.FullPath
        name.Equals(packageId + ".targets", StringComparison.OrdinalIgnoreCase) || name.Equals(packageId + ".props", StringComparison.OrdinalIgnoreCase))

/// Finds all analyzer files in a nuget package.
let GetAnalyzerFiles(targetFolder) = getFilesMatching targetFolder "*.dll" "analyzers" "analyzer dlls"


let tryNuGetV3 (auth, nugetV3Url, package:PackageName) =
    async {
        try
            return! NuGetV3.findVersionsForPackage(nugetV3Url, auth, package)
        with exn -> return None
    }


let rec private getPackageDetails alternativeProjectRoot root force (sources:PackageSource list) packageName (version:SemVerInfo) : Async<PackageResolver.PackageDetails> =
    async {
        let tryV2 source (nugetSource:NugetSource) force = async {
            let! result =
                NuGetV2.getDetailsFromNuGet
                    force
                    (nugetSource.Authentication |> Option.map toBasicAuth)
                    nugetSource.Url
                    packageName
                    version
            return Choice1Of2(source,result)  }

        let tryV3 source nugetSource force = async {
            if nugetSource.Url.Contains("myget.org") || nugetSource.Url.Contains("nuget.org") || nugetSource.Url.Contains("visualstudio.com") || nugetSource.Url.Contains("/nuget/v3/") then
                match NuGetV3.calculateNuGet2Path nugetSource.Url with
                | Some url ->
                    let! result =
                        NuGetV2.getDetailsFromNuGet
                            force
                            (nugetSource.Authentication |> Option.map toBasicAuth)
                            url
                            packageName
                            version
                    return Choice1Of2(source,result)
                | _ ->
                    let! result = NuGetV3.GetPackageDetails force nugetSource packageName version
                    return Choice1Of2(source,result)
            else
                let! result = NuGetV3.GetPackageDetails force nugetSource packageName version
                return Choice1Of2(source,result) }

        let getPackageDetails force =
            // helper to work through the list sequentially
            let rec trySelectFirst errors workLeft =
                async {
                    match workLeft with
                    | work :: rest ->
                        let! r = work
                        match r with
                        | Choice1Of2 result -> return Choice1Of2 result
                        | Choice2Of2 error -> return! trySelectFirst (error::errors) rest
                    | [] -> return Choice2Of2 errors
                }
            sources
            |> List.sortBy (fun source ->
                match source with  // put local caches to the end
                | LocalNuGet(_,Some _) -> true
                | _ -> false)
            |> List.map (fun source -> async {
                try
                    match source with
                    | NuGetV2 nugetSource ->
                        return! tryV2 source nugetSource force
                    | NuGetV3 nugetSource when NuGetV2.urlSimilarToTfsOrVsts nugetSource.Url  ->
                        match NuGetV3.calculateNuGet2Path nugetSource.Url with
                        | Some url ->
                            let nugetSource : NugetSource =
                                { Url = url
                                  Authentication = nugetSource.Authentication }
                            return! tryV2 source nugetSource force
                        | _ ->
                            return! tryV3 source nugetSource force
                    | NuGetV3 nugetSource ->
                        try
                            return! tryV3 source nugetSource force
                        with
                        | exn ->
                            match NuGetV3.calculateNuGet2Path nugetSource.Url with
                            | Some url ->
                                let nugetSource : NugetSource =
                                    { Url = url
                                      Authentication = nugetSource.Authentication }
                                return! tryV2 source nugetSource force
                            | _ ->
                                raise exn
                                return! tryV3 source nugetSource force

                    | LocalNuGet(path,Some _) ->
                        let! result = NuGetLocal.getDetailsFromLocalNuGetPackage true alternativeProjectRoot root path packageName version
                        return Choice1Of2(source,result)
                    | LocalNuGet(path,None) ->
                        let! result = NuGetLocal.getDetailsFromLocalNuGetPackage false alternativeProjectRoot root path packageName version
                        return Choice1Of2(source,result)
                with e ->
                    if verbose then
                        verbosefn "Source '%O' exception: %O" source e
                    let capture = ExceptionDispatchInfo.Capture e
                    return Choice2Of2 capture })
            |> trySelectFirst []

        let! maybePackageDetails = getPackageDetails force
        let! source,nugetObject =
            async {
                let fallback () =
                    match sources |> List.map (fun (s:PackageSource) -> s.ToString()) with
                    | [source] ->
                        failwithf "Couldn't get package details for package %O %O on %O." packageName version source
                    | [] ->
                        failwithf "Couldn't get package details for package %O %O, because no sources were specified." packageName version
                    | sources ->
                        failwithf "Couldn't get package details for package %O %O on any of %A." packageName version sources
                    
                match maybePackageDetails with
                | Choice2Of2 ([]) -> return fallback()
                | Choice2Of2 (h::restError) ->
                    for error in restError do
                        if not verbose then
                            // Otherwise the error was already mentioned above
                            traceWarnfn "Ignoring: %s" error.SourceException.Message
                    h.Throw()
                    return fallback()
                | Choice1Of2 packageDetails -> return packageDetails
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

let getVersionsCached key f (source, auth, nugetURL, package) =
    async {
        match protocolCache.TryGetValue(source) with
        | true, v when v <> key -> return None
        | true, v when v = key ->
            let! result = f (auth, nugetURL, package)
            match result with
            | Some x -> return Some x
            | _ -> return None
        | _ ->
            let! result = f (auth, nugetURL, package)
            match result with
            | Some x ->
                protocolCache.TryAdd(source, key) |> ignore
                return Some x
            | _ -> return None
    }

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
                       if (not force) && errorFileExists then [] else
                       match nugetSource with
                       | NuGetV2 source ->
                            let auth = source.Authentication |> Option.map toBasicAuth
                            if not force && (String.containsIgnoreCase "nuget.org" source.Url || String.containsIgnoreCase "myget.org" source.Url || String.containsIgnoreCase "visualstudio.com" source.Url) then
                                [getVersionsCached "Json" NuGetV2.tryGetPackageVersionsViaJson (nugetSource, auth, source.Url, packageName) ]
                            elif String.containsIgnoreCase "artifactory" source.Url then
                                [getVersionsCached "ODataNewestFirst" NuGetV2.tryGetAllVersionsFromNugetODataFindByIdNewestFirst (nugetSource, auth, source.Url, packageName) ]
                            else
                                let v2Feeds =
                                    [ yield getVersionsCached "OData" NuGetV2.tryGetAllVersionsFromNugetODataFindById (nugetSource, auth, source.Url, packageName)
                                      yield getVersionsCached "ODataWithFilter" NuGetV2.tryGetAllVersionsFromNugetODataWithFilter (nugetSource, auth, source.Url, packageName)
                                      if not (String.containsIgnoreCase "teamcity" source.Url || String.containsIgnoreCase"feedservice.svc" source.Url  ) then
                                        yield getVersionsCached "Json" NuGetV2.tryGetPackageVersionsViaJson (nugetSource, auth, source.Url, packageName) ]

                                let apiV3 = NuGetV3.getAllVersionsAPI(source.Authentication,source.Url) |> Async.AwaitTask
                                match apiV3 |> Async.RunSynchronously with
                                | None -> v2Feeds
                                | Some v3Url -> (getVersionsCached "V3" tryNuGetV3 (nugetSource, auth, v3Url, packageName)) :: v2Feeds
                       | NuGetV3 source ->
                            let resp =
                                async {
                                    let! versionsAPI = PackageSources.getNuGetV3Resource source AllVersionsAPI
                                    return!
                                        tryNuGetV3
                                            (source.Authentication |> Option.map toBasicAuth,
                                             versionsAPI,
                                             packageName)
                                }

                            [ resp ]
                       | LocalNuGet(path,Some _) -> [ NuGetLocal.getAllVersionsFromLocalPath (true, path, packageName, alternativeProjectRoot, root) ]
                       | LocalNuGet(path,None) -> [ NuGetLocal.getAllVersionsFromLocalPath (false, path, packageName, alternativeProjectRoot, root) ])
            |> Seq.toArray
            |> Array.map Async.Choice
            |> Async.Parallel

        let! result = versionResponse
        return
            result
            |> Array.zip sources
            |> Array.choose (fun ((_,s),v) ->
                match v with
                | Some v when Array.isEmpty v |> not ->
                    try
                        let errorFile = getVersionsFailedCacheFileName s
                        if errorFile.Exists then
                            File.Delete(errorFile.FullName)
                    with _ -> ()
                    Some (s,v)
                | _ ->
                    try
                        let errorFile = getVersionsFailedCacheFileName s
                        if errorFile.Exists |> not then
                            File.WriteAllText(errorFile.FullName,DateTime.Now.ToString())
                    with _ -> ()
                    None)
            |> Array.collect (fun (s,versions) -> 
                if packageName = PackageName "FSharp.Core" then
                    versions |> Array.filter (fun v -> v <> "4.2.0") |> Array.map (fun v -> v,s) // HACK: since version was leftpadded
                else
                    versions |> Array.map (fun v -> v,s)) }
    let! versions = async {
        let! trial1 = trial force
        match trial1 with
        | versions when Array.isEmpty versions |> not -> return versions
        | _ ->
            let! trial2 = trial true
            match trial2 with
            | versions when Array.isEmpty versions |> not -> return versions
            | _ ->
                match sources |> Seq.map (fun s -> s.ToString()) |> List.ofSeq with
                | [source] ->
                    return failwithf "Could not find versions for package %O on %O." packageName source
                | [] ->
                    return failwithf "Could not find versions for package %O, because no sources were specified." packageName
                | sources ->
                    return failwithf "Could not find versions for package %O on any of %A." packageName sources }
    return
        versions
        |> Seq.toList
        |> List.map (fun (v,s) -> SemVer.Parse v,v,s)
        |> List.groupBy (fun (v,_,_) -> v.Normalize())
        |> List.map (fun (_,s) ->
            let sorted = s |> List.sortByDescending (fun (_,_,s) -> s.IsLocalFeed)

            let _,v,_ = List.head sorted
            SemVer.Parse v,sorted |> List.map (fun (_,_,x) -> x)) }




/// Downloads the given package to the NuGet Cache folder
let DownloadPackage(alternativeProjectRoot, root, (source : PackageSource), caches:Cache list, groupName, packageName:PackageName, version:SemVerInfo, includeVersionInPath, force, detailed) =
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
                        tracefn "Downloading %O %O%s" packageName version (if groupName = Constants.MainDependencyGroup then "" else sprintf " (%O)" groupName)
                    let! nugetPackage = GetPackageDetails alternativeProjectRoot root force [source] groupName packageName version

                    let encodeURL (url:string) = url.Replace("+","%2B")
                    let downloadUri =
                        if Uri.IsWellFormedUriString(nugetPackage.DownloadLink, UriKind.Absolute) then
                            Uri nugetPackage.DownloadLink
                        else
                            let sourceUrl =
                                if nugetPackage.Source.Url.EndsWith("/") then nugetPackage.Source.Url
                                else nugetPackage.Source.Url + "/"
                            Uri(Uri sourceUrl, nugetPackage.DownloadLink)

                    downloadUrl := downloadUri.ToString()

                    if authenticated && verbose then
                        tracefn "  from %O" !downloadUrl
                        tracefn "  to %s" targetFileName
                    
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
                | exn when String.IsNullOrWhiteSpace !downloadUrl -> failwithf "Could not download %O %O.%s    %s" packageName version Environment.NewLine exn.Message
                | exn -> failwithf "Could not download %O %O from %s.%s    %s" packageName version !downloadUrl Environment.NewLine exn.Message }

    async {
        do! download true 0
        let! files = NuGetCache.CopyFromCache(root, groupName, targetFile.FullName, licenseFileName, packageName, version, includeVersionInPath, force, detailed)
        return targetFileName,files
    }
