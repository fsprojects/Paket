/// Contains NuGet support.
module Paket.NuGetV2

open System
open System.IO
open System.Net
open Newtonsoft.Json
open System.IO.Compression
open System.Xml
open System.Text.RegularExpressions
open Paket.Logging
open System.Text

open Paket.Domain
open Paket.NuGet
open Paket.Utils
open Paket.Xml
open Paket.PackageSources
open Paket.Requirements
open FSharp.Polyfill

let rec private followODataLink auth url = 
    async {
        let! raw = getFromUrl(auth, url, acceptXml)
        if String.IsNullOrWhiteSpace raw then return [||] else
        let doc = XmlDocument()
        doc.LoadXml raw
        let feed = 
            match doc |> getNode "feed" with
            | Some node -> node
            | None -> failwithf "unable to parse data from %s" url

        let readEntryVersion = Some 
                               >> optGetNode "properties"
                               >> optGetNode "Version"
                               >> Option.map (fun node -> node.InnerText)

        let entriesVersions = feed |> getNodes "entry" |> List.choose readEntryVersion

        let! linksVersions = 
            feed 
            |> getNodes "link"
            |> List.filter (fun node -> node |> getAttribute "rel" = Some "next")
            |> List.choose (getAttribute "href")
            |> List.map (followODataLink auth)
            |> Async.Parallel

        return
            linksVersions
            |> Seq.collect id
            |> Seq.append entriesVersions
            |> Seq.toArray
    }


let tryGetAllVersionsFromNugetODataWithFilter (auth, nugetURL, package:PackageName) = 
    async { 
        try 
            let url = sprintf "%s/Packages?$filter=Id eq '%O'" nugetURL package
            verbosefn "getAllVersionsFromNugetODataWithFilter from url '%s'" url
            let! result = followODataLink auth url
            return Some result
        with _ -> return None
    }

let tryGetPackageVersionsViaOData (auth, nugetURL, package:PackageName) = 
    async { 
        try 
            let url = sprintf "%s/FindPackagesById()?id='%O'" nugetURL package
            verbosefn "getAllVersionsFromNugetOData from url '%s'" url
            let! result = followODataLink auth url
            return Some result
        with _ -> return None
    }

let tryGetPackageVersionsViaJson (auth, nugetURL, package:PackageName) = 
    async { 
        let url = sprintf "%s/package-versions/%O?includePrerelease=true" nugetURL package
        let! raw = safeGetFromUrl (auth, url, acceptJson)
        
        match raw with
        | None -> return None
        | Some data -> 
            try 
                let versions = Some(JsonConvert.DeserializeObject<string []> data)
                return versions
            with _ -> return None
    }

let tryNuGetV3 (auth, nugetV3Url, package:PackageName) = 
    async { 
        try 
            return! NuGetV3.findVersionsForPackage(nugetV3Url, auth, package)
        with exn -> return None
    }

/// Gets versions of the given package from local NuGet feed.
let getAllVersionsFromLocalPath (localNugetPath, package:PackageName, root) =
    async {
        let localNugetPath = Utils.normalizeLocalPath localNugetPath
        let di = getDirectoryInfo localNugetPath root
        if not di.Exists then
            failwithf "The directory %s doesn't exist.%sPlease check the NuGet source feed definition in your paket.dependencies file." di.FullName Environment.NewLine

        let versions = 
            Directory.EnumerateFiles(di.FullName,"*.nupkg",SearchOption.AllDirectories)
            |> Seq.filter (fun fi -> fi.EndsWith ".symbols.nupkg" |> not)
            |> Seq.choose (fun fileName ->
                            let fi = FileInfo(fileName)
                            let _match = Regex(sprintf @"^%O\.(\d.*)\.nupkg" package, RegexOptions.IgnoreCase).Match(fi.Name)
                            if _match.Groups.Count > 1 then Some _match.Groups.[1].Value else None)
            |> Seq.toArray
        return Some(versions)
    }


let parseODataDetails(nugetURL,packageName:PackageName,version:SemVerInfo,raw) = 
    let doc = XmlDocument()
    doc.LoadXml raw
                
    let entry = 
        match (doc |> getNode "feed" |> optGetNode "entry" ) ++ (doc |> getNode "entry") with
        | Some node -> node
        | _ -> failwithf "unable to find entry node for package %O %O" packageName version

    let officialName =
        match (entry |> getNode "properties" |> optGetNode "Id") ++ (entry |> getNode "title") with
        | Some node -> node.InnerText
        | _ -> failwithf "Could not get official package name for package %O %O" packageName version
        
    let publishDate =
        match entry |> getNode "properties" |> optGetNode "Published" with
        | Some node -> 
            match DateTime.TryParse node.InnerText with
            | true, date -> date
            | _ -> DateTime.MinValue
        | _ -> DateTime.MinValue
    
    let v = 
        match entry |> getNode "properties" |> optGetNode "Version" with
        | Some node -> node.InnerText
        | _ -> failwithf "Could not get official version no. for package %O %O" packageName version
        
    let downloadLink =
        match entry |> getNode "content" |> optGetAttribute "type", 
              entry |> getNode "content" |> optGetAttribute "src"  with
        | Some "application/zip", Some link -> link
        | Some "binary/octet-stream", Some link -> link
        | _ -> failwithf "unable to find downloadLink for package %O %O" packageName version
        
    let licenseUrl =
        match entry |> getNode "properties" |> optGetNode "LicenseUrl" with
        | Some node -> node.InnerText 
        | _ -> ""

    let dependencies =
        match entry |> getNode "properties" |> optGetNode "Dependencies" with
        | Some node -> node.InnerText
        | None -> failwithf "unable to find dependencies for package %O %O" packageName version

    let packages = 
        let split (d : string) =
            let a = d.Split ':'
            PackageName a.[0], 
            VersionRequirement.Parse(if a.Length > 1 then a.[1] else "0"), 
            (if a.Length > 2 && a.[2] <> "" then 
                 if String.startsWithIgnoreCase "portable" a.[2] then [ FrameworkRestriction.Portable(a.[2]) ]
                 else 
                     match FrameworkDetection.Extract a.[2] with
                     | Some x -> [ FrameworkRestriction.Exactly x ]
                     | None -> []
             else [])

        dependencies
        |> fun s -> s.Split([| '|' |], System.StringSplitOptions.RemoveEmptyEntries)
        |> Array.map split
        |> Array.toList

    let dependencies = Requirements.optimizeDependencies packages
    
    { PackageName = officialName
      DownloadUrl = downloadLink
      Dependencies = dependencies
      SourceUrl = nugetURL
      CacheVersion = NuGetPackageCache.CurrentCacheVersion
      LicenseUrl = licenseUrl
      Version = (SemVer.Parse v).Normalize()
      Unlisted = publishDate = Constants.MagicUnlistingDate }


let getDetailsFromNuGetViaODataFast auth nugetURL (packageName:PackageName) (version:SemVerInfo) = 
    async {
        try 
            let url = sprintf "%s/Packages?$filter=(Id eq '%O') and (NormalizedVersion eq '%s')" nugetURL packageName (version.Normalize())
            let! raw = getFromUrl(auth,url,acceptXml)
            if verbose then
                tracefn "Response from %s:" url
                tracefn ""
                tracefn "%s" raw
            return parseODataDetails(nugetURL,packageName,version,raw)
        with _ ->
            let url = sprintf "%s/Packages?$filter=(Id eq '%O') and (Version eq '%O')" nugetURL packageName version
            let! raw = getFromUrl(auth,url,acceptXml)
            if verbose then
                tracefn "Response from %s:" url
                tracefn ""
                tracefn "%s" raw
            return parseODataDetails(nugetURL,packageName,version,raw)
    }

/// Gets package details from NuGet via OData
let getDetailsFromNuGetViaOData auth nugetURL (packageName:PackageName) (version:SemVerInfo) = 
    async {
        try 
            return! getDetailsFromNuGetViaODataFast auth nugetURL packageName version
        with _ ->
            let url = sprintf "%s/Packages(Id='%O',Version='%O')" nugetURL packageName version
            let! response = safeGetFromUrl(auth,url,acceptXml)
                    
            let! raw =
                match response with
                | Some(r) -> async { return r }
                | _  when  String.containsIgnoreCase "myget.org" nugetURL || String.containsIgnoreCase "nuget.org" nugetURL ->
                    failwithf "Could not get package details for %O from %s" packageName nugetURL
                | _ ->
                    let url = sprintf "%s/odata/Packages(Id='%O',Version='%O')" nugetURL packageName version
                    getXmlFromUrl(auth,url)

            if verbose then
                tracefn "Response from %s:" url
                tracefn ""
                tracefn "%s" raw
            return parseODataDetails(nugetURL,packageName,version,raw)
    }

let getDetailsFromNuGet force auth nugetURL packageName version = 
    getDetailsFromCacheOr
        force
        nugetURL
        packageName
        version
        (fun () ->
            getDetailsFromNuGetViaOData auth nugetURL packageName version)


let fixDatesInArchive fileName =
    try
        use zipToOpen = new FileStream(fileName, FileMode.Open)
        use archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update)
        
        for e in archive.Entries do
            try
                let d = e.LastWriteTime
                ()
            with
            | _ -> e.LastWriteTime <- DateTimeOffset.Now
    with
    | exn -> traceWarnfn "Could not fix timestamps in %s. Error: %s" fileName exn.Message

let fixArchive fileName =
    if isMonoRuntime then 
        fixDatesInArchive fileName

let findLocalPackage directory (packageName:PackageName) (version:SemVerInfo) = 
    let v1 = FileInfo(Path.Combine(directory, sprintf "%O.%O.nupkg" packageName version))
    if v1.Exists then v1 else
    let normalizedVersion = version.Normalize()
    let v2 = FileInfo(Path.Combine(directory, sprintf "%O.%s.nupkg" packageName normalizedVersion))
    if v2.Exists then v2 else

    let v3 =
        Directory.EnumerateFiles(directory,"*.nupkg",SearchOption.AllDirectories)
        |> Seq.map (fun x -> FileInfo(x))
        |> Seq.filter (fun fi -> String.containsIgnoreCase (packageName.GetCompareString())  fi.Name)
        |> Seq.filter (fun fi -> fi.Name.Contains(normalizedVersion) || fi.Name.Contains(version.ToString()))
        |> Seq.tryHead

    match v3 with
    | None -> failwithf "The package %O %O can't be found in %s.%sPlease check the feed definition in your paket.dependencies file." packageName version directory Environment.NewLine
    | Some x -> x

/// Reads package name from a nupkg file
let getPackageNameFromLocalFile fileName = 
    fixArchive fileName
    use zipToCreate = new FileStream(fileName, FileMode.Open, FileAccess.Read)
    use zip = new ZipArchive(zipToCreate, ZipArchiveMode.Read)
    let zippedNuspec = zip.Entries |> Seq.find (fun f -> f.FullName.EndsWith ".nuspec")
    let fileName = FileInfo(Path.Combine(Path.GetTempPath(), zippedNuspec.Name)).FullName
    zippedNuspec.ExtractToFile(fileName, true)
    let nuspec = Nuspec.Load fileName
    File.Delete(fileName)
    nuspec.OfficialName

/// Reads direct dependencies from a nupkg file
let getDetailsFromLocalNuGetPackage root localNugetPath (packageName:PackageName) (version:SemVerInfo) =
    async {
        let localNugetPath = Utils.normalizeLocalPath localNugetPath
        let di = getDirectoryInfo localNugetPath root
        let nupkg = findLocalPackage di.FullName packageName version
        
        fixArchive nupkg.FullName
        use zipToCreate = new FileStream(nupkg.FullName, FileMode.Open, FileAccess.Read)
        use zip = new ZipArchive(zipToCreate,ZipArchiveMode.Read)
        
        let zippedNuspec = zip.Entries |> Seq.find (fun f -> f.FullName.EndsWith ".nuspec")
        let fileName = FileInfo(Path.Combine(Path.GetTempPath(), zippedNuspec.Name)).FullName

        zippedNuspec.ExtractToFile(fileName, true)

        let nuspec = Nuspec.Load fileName

        File.Delete(fileName)
        let dependencies = 
            nuspec.Dependencies
            |> List.map (fun (a,b,c) -> a,b, getRestrictionList c)

        return 
            { PackageName = nuspec.OfficialName
              DownloadUrl = packageName.ToString()
              Dependencies = Requirements.optimizeDependencies dependencies
              SourceUrl = di.FullName
              CacheVersion = NuGetPackageCache.CurrentCacheVersion
              LicenseUrl = nuspec.LicenseUrl
              Version = version.Normalize()
              Unlisted = false }
    }


let inline isExtracted fileName =
    let fi = FileInfo(fileName)
    if not fi.Exists then false else
    let di = fi.Directory
    di.EnumerateFileSystemInfos()
    |> Seq.exists (fun f -> f.FullName <> fi.FullName)

let IsPackageVersionExtracted(root, groupName, packageName:PackageName, version:SemVerInfo, includeVersionInPath) =
    let targetFolder = DirectoryInfo(getTargetFolder root groupName packageName version includeVersionInPath).FullName
    let targetFileName = Path.Combine(targetFolder, packageName.ToString() + "." + version.Normalize() + ".nupkg")
    isExtracted targetFileName

/// Extracts the given package to the ./packages folder
let ExtractPackage(fileName:string, targetFolder, packageName:PackageName, version:SemVerInfo, detailed) =
    async {
        if isExtracted fileName then
             verbosefn "%O %O already extracted" packageName version
        else
            Directory.CreateDirectory(targetFolder) |> ignore

            try
                fixArchive fileName
                ZipFile.ExtractToDirectory(fileName, targetFolder)
            with
            | exn ->
                
                let text = if detailed then sprintf "%s In rare cases a firewall might have blocked the download. Please look into the file and see if it contains text with further information." Environment.NewLine else ""
                failwithf "Error during extraction of %s.%sMessage: %s%s" (Path.GetFullPath fileName) Environment.NewLine exn.Message text

            // cleanup folder structure
            let rec cleanup (dir : DirectoryInfo) = 
                for sub in dir.GetDirectories() do
                    let newName = Uri.UnescapeDataString(sub.FullName)
                    if sub.FullName <> newName && not (Directory.Exists newName) then 
                        Directory.Move(sub.FullName, newName)
                        cleanup (DirectoryInfo newName)
                    else
                        cleanup sub
                for file in dir.GetFiles() do
                    let newName = Uri.UnescapeDataString(file.Name)
                    if file.Name <> newName && not (File.Exists <| Path.Combine(file.DirectoryName, newName)) then
                        File.Move(file.FullName, Path.Combine(file.DirectoryName, newName))

            cleanup (DirectoryInfo targetFolder)
            verbosefn "%O %O unzipped to %s" packageName version targetFolder
        return targetFolder
    }

let CopyLicenseFromCache(root, groupName, cacheFileName, packageName:PackageName, version:SemVerInfo, includeVersionInPath, force) = 
    async {
        try
            if String.IsNullOrWhiteSpace cacheFileName then return () else
            let cacheFile = FileInfo cacheFileName
            if cacheFile.Exists then
                let targetFile = FileInfo(Path.Combine(getTargetFolder root groupName packageName version includeVersionInPath, "license.html"))
                if not force && targetFile.Exists then
                    verbosefn "License %O %O already copied" packageName version
                else
                    File.Copy(cacheFile.FullName, targetFile.FullName, true)
        with
        | exn -> traceWarnfn "Could not copy license for %O %O from %s.%s    %s" packageName version cacheFileName Environment.NewLine exn.Message
    }

/// Extracts the given package to the ./packages folder
let CopyFromCache(root, groupName, cacheFileName, licenseCacheFile, packageName:PackageName, version:SemVerInfo, includeVersionInPath, force, detailed) = 
    async { 
        let targetFolder = DirectoryInfo(getTargetFolder root groupName packageName version includeVersionInPath).FullName
        let fi = FileInfo(cacheFileName)
        let targetFile = FileInfo(Path.Combine(targetFolder, fi.Name))
        if not force && targetFile.Exists then
            verbosefn "%O %O already copied" packageName version
        else
            CleanDir targetFolder
            File.Copy(cacheFileName, targetFile.FullName)
        try 
            let! extracted = ExtractPackage(targetFile.FullName,targetFolder,packageName,version,detailed)
            do! CopyLicenseFromCache(root, groupName, licenseCacheFile, packageName, version, includeVersionInPath, force)
            return extracted
        with
        | exn -> 
            File.Delete targetFile.FullName
            Directory.Delete(targetFolder,true)
            return! raise exn
    }

let DownloadLicense(root,force,packageName:PackageName,version:SemVerInfo,licenseUrl,targetFileName) =
    async { 
        if String.IsNullOrWhiteSpace licenseUrl then return () else
        
        let targetFile = FileInfo targetFileName
        if not force && targetFile.Exists && targetFile.Length > 0L then 
            verbosefn "License for %O %O already downloaded" packageName version
        else
            try
                verbosefn "Downloading license for %O %O to %s" packageName version targetFileName

                let request = HttpWebRequest.Create(Uri licenseUrl) :?> HttpWebRequest
                request.AutomaticDecompression <- DecompressionMethods.GZip ||| DecompressionMethods.Deflate
                request.UserAgent <- "Paket"
                request.UseDefaultCredentials <- true
                request.Proxy <- Utils.getDefaultProxyFor licenseUrl
                request.Timeout <- 3000
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


let private getFiles targetFolder subFolderName filesDescriptionForVerbose =
    let files = 
        let dir = DirectoryInfo(targetFolder)
        let path = Path.Combine(dir.FullName.ToLower(), subFolderName)
        if dir.Exists then
            dir.GetDirectories()
            |> Array.filter (fun fi -> String.equalsIgnoreCase fi.FullName path)
            |> Array.collect (fun dir -> dir.GetFiles("*.*", SearchOption.AllDirectories))
        else
            [||]

    if Logging.verbose then
        if Array.isEmpty files then 
            verbosefn "No %s found in %s" filesDescriptionForVerbose targetFolder 
        else
            let s = String.Join(Environment.NewLine + "  - ",files |> Array.map (fun l -> l.FullName))
            verbosefn "%s found in %s:%s  - %s" filesDescriptionForVerbose targetFolder Environment.NewLine s

    files

/// Finds all libraries in a nuget package.
let GetLibFiles(targetFolder) = getFiles targetFolder "lib" "libraries"

/// Finds all targets files in a nuget package.
let GetTargetsFiles(targetFolder) = getFiles targetFolder "build" ".targets files"

/// Finds all analyzer files in a nuget package.
let GetAnalyzerFiles(targetFolder) = getFiles targetFolder "analyzers" "analyzer dlls"

let rec private getPackageDetails root force (sources:PackageSource list) packageName (version:SemVerInfo) : PackageResolver.PackageDetails = 
   
    let tryV2 source (nugetSource:NugetSource)  = async {
        let! result = 
            getDetailsFromNuGet 
                force 
                (nugetSource.Authentication |> Option.map toBasicAuth)
                nugetSource.Url 
                packageName
                version
        return Some(source,result)  }

    let tryV3 source nugetSource = async {
        if nugetSource.Url.Contains("myget.org") || nugetSource.Url.Contains("nuget.org") then
            match NuGetV3.calculateNuGet2Path nugetSource.Url with
            | Some url -> 
                let! result = 
                    getDetailsFromNuGet 
                        force 
                        (nugetSource.Authentication |> Option.map toBasicAuth)
                        url
                        packageName
                        version
                return Some(source,result)
            | _ ->
                let! result = NuGetV3.GetPackageDetails force nugetSource packageName version
                return Some(source,result)
        else
            let! result = NuGetV3.GetPackageDetails force nugetSource packageName version
            return Some(source,result) }

    let getPackageDetails force =
        sources
        |> List.map (fun source -> async {
            try 
                match source with
                | NuGetV2 nugetSource ->
                    return! tryV2 source nugetSource
                | NuGetV3 nugetSource when nugetSource.Url.Contains("pkgs.visualstudio.com")  ->
                    match NuGetV3.calculateNuGet2Path nugetSource.Url with
                    | Some url ->
                        let nugetSource : NugetSource = 
                            { Url = url
                              Authentication = nugetSource.Authentication }
                        return! tryV2 source nugetSource
                    | _ -> 
                        return! tryV3 source nugetSource
                | NuGetV3 nugetSource ->
                    try
                        return! tryV3 source nugetSource
                    with
                    | exn -> 
                        match NuGetV3.calculateNuGet2Path nugetSource.Url with
                        | Some url ->
                            let nugetSource : NugetSource = 
                                { Url = url
                                  Authentication = nugetSource.Authentication }
                            return! tryV2 source nugetSource
                        | _ -> 
                            raise exn
                            return! tryV3 source nugetSource

                | LocalNuGet path -> 
                    let! result = getDetailsFromLocalNuGetPackage root path packageName version
                    return Some(source,result)
            with e ->
                verbosefn "Source '%O' exception: %O" source e
                return None })
        |> List.tryPick Async.RunSynchronously

    let source,nugetObject = 
        match getPackageDetails force with
        | None ->
            match getPackageDetails true with
            | None -> 
                match sources |> List.map (fun (s:PackageSource) -> s.ToString()) with
                | [source] ->
                    failwithf "Couldn't get package details for package %O %O on %O." packageName version source
                | [] ->
                    failwithf "Couldn't get package details for package %O %O because no sources where specified." packageName version
                | sources ->
                    failwithf "Couldn't get package details for package %O %O on any of %A." packageName version sources
            | Some packageDetails -> packageDetails
        | Some packageDetails -> packageDetails

    let newName = PackageName nugetObject.PackageName
    if packageName <> newName then
        failwithf "Package details for %O are not matching requested package %O." newName packageName

    { Name = PackageName nugetObject.PackageName
      Source = source
      DownloadLink = nugetObject.DownloadUrl
      Unlisted = nugetObject.Unlisted
      LicenseUrl = nugetObject.LicenseUrl
      DirectDependencies = nugetObject.Dependencies |> Set.ofList }

let rec GetPackageDetails root force (sources:PackageSource list) packageName (version:SemVerInfo) : PackageResolver.PackageDetails = 
    try
        getPackageDetails root force sources packageName version
    with
    | _ -> getPackageDetails root true sources packageName version

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

/// Uses the NuGet v2 API to retrieve all packages with the given prefix.
let FindPackages(auth, nugetURL, packageNamePrefix, maxResults) =
    async {
        try 
            let url = sprintf "%s/Packages()?$filter=IsLatestVersion and IsAbsoluteLatestVersion and substringof('%s',tolower(Id))" nugetURL ((packageNamePrefix:string).ToLowerInvariant()) 
            let! raw = getFromUrl(auth |> Option.map toBasicAuth,url,acceptXml)
            let doc = XmlDocument()
            doc.LoadXml raw
            return
                match doc |> getNode "feed" with
                | Some n ->
                    [| for entry in n |> getNodes "entry" do
                        match (entry |> getNode "properties" |> optGetNode "Id") ++ (entry |> getNode "title") with
                        | Some node -> yield node.InnerText
                        | _ -> () |]
                | _ ->  [||]
        with _ -> return [||]
    }

/// Allows to retrieve all version no. for a package from the given sources.
let GetVersions force root (sources, packageName:PackageName) = 
    let trial force =
        let getVersionsFailedCacheFileName (source:PackageSource) =
            let h = source.Url |> normalizeUrl |> hash |> abs
            let packageUrl = sprintf "Versions.%O.s%d.failed" packageName h
            FileInfo(Path.Combine(CacheFolder,packageUrl))

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
                            if not force && (String.containsIgnoreCase "nuget.org" source.Url || String.containsIgnoreCase "myget.org" source.Url) then
                                [getVersionsCached "Json" tryGetPackageVersionsViaJson (nugetSource, auth, source.Url, packageName) ]
                            else
                                let v2Feeds =
                                    [ yield getVersionsCached "OData" tryGetPackageVersionsViaOData (nugetSource, auth, source.Url, packageName)
                                      yield getVersionsCached "ODataWithFilter" tryGetAllVersionsFromNugetODataWithFilter (nugetSource, auth, source.Url, packageName)
                                      if not (String.containsIgnoreCase "teamcity" source.Url || String.containsIgnoreCase"feedservice.svc" source.Url  ) then
                                        yield getVersionsCached "Json" tryGetPackageVersionsViaJson (nugetSource, auth, source.Url, packageName) ]

                                match NuGetV3.getAllVersionsAPI(source.Authentication,source.Url) with
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
                       | LocalNuGet path -> [ getAllVersionsFromLocalPath (path, packageName, root) ])
            |> Seq.toArray
            |> Array.map Async.Choice
            |> Async.Parallel
            |> Async.RunSynchronously
    
        versionResponse
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
        |> Array.map (fun (s,versions) -> versions |> Array.map (fun v -> v,s))
        |> Array.concat

    let versions = 
        match trial force with
        | versions when Array.isEmpty versions |> not -> versions
        | _ ->
            match trial true with
            | versions when Array.isEmpty versions |> not -> versions
            | _ ->
                match sources |> Seq.map (fun s -> s.ToString()) |> List.ofSeq with
                | [source] ->
                    failwithf "Could not find versions for package %O on %O." packageName source
                | [] ->
                    failwithf "Could not find versions for package %O because no sources where specified." packageName 
                | sources ->
                    failwithf "Could not find versions for package %O on any of %A." packageName sources

    versions
    |> Seq.toList
    |> List.groupBy fst
    |> List.map (fun (v,s) -> SemVer.Parse v,s |> List.map snd)

/// Downloads the given package to the NuGet Cache folder
let DownloadPackage(root, (source : PackageSource), groupName, packageName:PackageName, version:SemVerInfo, includeVersionInPath, force, detailed) = 
    let targetFileName = Path.Combine(CacheFolder, packageName.ToString() + "." + version.Normalize() + ".nupkg")
    let targetFile = FileInfo targetFileName
    let licenseFileName = Path.Combine(CacheFolder, packageName.ToString() + "." + version.Normalize() + ".license.html")

    let rec download authenticated =
        async {
            if not force && targetFile.Exists && targetFile.Length > 0L then 
                verbosefn "%O %O already downloaded." packageName version
            else
                if authenticated then
                    tracefn "Downloading %O %O%s" packageName version (if groupName = Constants.MainDependencyGroup then "" else sprintf " (%O)" groupName)
                    verbosefn "  to %s" targetFileName

                // discover the link on the fly
                let nugetPackage = GetPackageDetails root force [source] packageName version
                try 
                    let! license = Async.StartChild(DownloadLicense(root,force,packageName,version,nugetPackage.LicenseUrl,licenseFileName), 5000)

                    let downloadUri = 
                        if Uri.IsWellFormedUriString(nugetPackage.DownloadLink, UriKind.Absolute) then
                            Uri nugetPackage.DownloadLink
                        else
                            let sourceUrl =
                                if nugetPackage.Source.Url.EndsWith("/") then nugetPackage.Source.Url
                                else nugetPackage.Source.Url + "/"
                            Uri(Uri sourceUrl,  nugetPackage.DownloadLink)

                    let request = HttpWebRequest.Create(downloadUri) :?> HttpWebRequest
                    request.AutomaticDecompression <- DecompressionMethods.GZip ||| DecompressionMethods.Deflate
                    request.UserAgent <- "Paket"

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
                    exn.Status = WebExceptionStatus.ProtocolError &&
                     (match source.Auth |> Option.map toBasicAuth with
                      | Some(Credentials(_)) -> true
                      | _ -> false)
                        -> do! download false
                | exn -> failwithf "Could not download %O %O from %s.%s    %s" packageName version nugetPackage.DownloadLink Environment.NewLine exn.Message }

    async {
        do! download true
        return! CopyFromCache(root, groupName, targetFile.FullName, licenseFileName, packageName, version, includeVersionInPath, force, detailed)
    }
