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
open Paket.Utils
open Paket.Xml
open Paket.PackageSources
open Paket.Requirements

type NugetPackageCache =
    { Dependencies : (PackageName * VersionRequirement * FrameworkRestrictions) list
      PackageName : string
      SourceUrl: string
      Unlisted : bool
      DownloadUrl : string
      LicenseUrl : string
      CacheVersion: string }

    static member CurrentCacheVersion = "2.0"

let rec private followODataLink getUrlContents url = 
    async { 
        let! raw = getUrlContents url
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
            |> List.map (followODataLink getUrlContents)
            |> Async.Parallel

        return
            linksVersions
            |> Seq.collect id
            |> Seq.append entriesVersions
    }

/// Gets versions of the given package via OData via /Packages?$filter=Id eq 'packageId'
let getAllVersionsFromNugetODataWithFilter (getUrlContents, nugetURL, package) = 
    // we cannot cache this
    let url = sprintf "%s/Packages?$filter=Id eq '%s'" nugetURL package
    verbosefn "getAllVersionsFromNugetODataWithFilter from url '%s'" url
    followODataLink getUrlContents url

/// Gets versions of the given package via OData via /FindPackagesById()?id='packageId'.
let getAllVersionsFromNugetOData (getUrlContents, nugetURL, package) = 
    async {
        // we cannot cache this
        try 
            let url = sprintf "%s/FindPackagesById()?id='%s'" nugetURL package
            verbosefn "getAllVersionsFromNugetOData from url '%s'" url
            return! followODataLink getUrlContents url
        with _ -> return! getAllVersionsFromNugetODataWithFilter (getUrlContents, nugetURL, package)
    }

/// Gets all versions no. of the given package.
let getAllVersionsFromNuGet2(auth,nugetURL,package) = 
    // we cannot cache this
    async { 
        let url = sprintf "%s/package-versions/%s?includePrerelease=true" nugetURL package
        verbosefn "getAllVersionsFromNuGet2 from url '%s'" url
        let! raw = safeGetFromUrl(auth, url)
        let getUrlContents url = getFromUrl(auth, url)
        match raw with
        | None -> let! result = getAllVersionsFromNugetOData(getUrlContents, nugetURL, package)
                  return result
        | Some data -> 
            try 
                try 
                    let result = JsonConvert.DeserializeObject<string []>(data) |> Array.toSeq
                    return result
                with _ -> verbosefn "exn when deserialising data '%s'" data
                          let! result = getAllVersionsFromNugetOData(getUrlContents, nugetURL, package)
                          return result
            with exn -> 
                return! failwithf "Could not get data from %s for package %s.%s Message: %s" nugetURL package 
                    Environment.NewLine exn.Message
    }


let getAllVersions(auth, nugetURL, package) = async {
        try
            let! versions = getAllVersionsFromNuGet2(auth,nugetURL,package) 
            return Some versions
        with
        | exn -> return None
    }

//    async { 
//        let! raw = getViaNuGet3(auth, nugetURL, package)
//
//        match raw with
//        | None -> let! result = getAllVersionsFromNuGet2(auth,nugetURL,package)
//                  return result
//        | Some data -> 
//            try 
//                try 
//                    let result = getJSONLDDetails data
//                    return (Array.toSeq result)
//                with _ -> let! result = getAllVersionsFromNuGet2(auth,nugetURL, package)
//                          return result
//            with exn ->
//                return! failwithf "Could not get data from %s for package %s.%s Message: %s" nugetURL package
//                    Environment.NewLine exn.Message
//    }

/// Gets versions of the given package from local Nuget feed.
let getAllVersionsFromLocalPath (localNugetPath, package, root) =
    async {        
        let localNugetPath = Utils.normalizeLocalPath localNugetPath
        let di = getDirectoryInfo localNugetPath root
        if not di.Exists then
            failwithf "The directory %s doesn't exist.%sPlease check the NuGet source feed definition in your paket.dependencies file." di.FullName Environment.NewLine

        let versions = 
            Directory.EnumerateFiles(di.FullName,"*.nupkg",SearchOption.AllDirectories)
            |> Seq.choose (fun fileName ->
                            let fi = FileInfo(fileName)
                            let _match = Regex(sprintf @"^%s\.(\d.*)\.nupkg" package, RegexOptions.IgnoreCase).Match(fi.Name)
                            if _match.Groups.Count > 1 then Some _match.Groups.[1].Value else None)
        return Some versions
    }


let parseODataDetails(nugetURL,packageName,version,raw) = 
    let doc = XmlDocument()
    doc.LoadXml raw
                
    let entry = 
        match (doc |> getNode "feed" |> optGetNode "entry" ) ++ (doc |> getNode "entry") with
        | Some node -> node
        | _ -> failwithf "unable to find entry node for package %s %O" packageName version

    let officialName =
        match (entry |> getNode "properties" |> optGetNode "Id") ++ (entry |> getNode "title") with
        | Some node -> node.InnerText
        | _ -> failwithf "Could not get official package name for package %s %O" packageName version
        
    let publishDate =
        match entry |> getNode "properties" |> optGetNode "Published" with
        | Some node -> 
            match DateTime.TryParse node.InnerText with
            | true, date -> date
            | _ -> DateTime.MinValue
        | _ -> DateTime.MinValue
    
    let downloadLink =
        match entry |> getNode "content" |> optGetAttribute "type", 
              entry |> getNode "content" |> optGetAttribute "src"  with
        | Some "application/zip", Some link -> link
        | Some "binary/octet-stream", Some link -> link
        | _ -> failwithf "unable to find downloadLink for package %s %O" packageName version
        
    let licenseUrl =
        match entry |> getNode "properties" |> optGetNode "LicenseUrl" with
        | Some node -> node.InnerText 
        | _ -> ""

    let dependencies =
        match entry |> getNode "properties" |> optGetNode "Dependencies" with
        | Some node -> node.InnerText
        | None -> failwithf "unable to find dependencies for package %s %O" packageName version

    let packages = 
        dependencies
        |> fun s -> s.Split([| '|' |], System.StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun d -> d.Split ':')
        |> Array.map (fun a -> 
                        a.[0],
                        (if a.Length > 1 then a.[1] else "0"),
                        (if a.Length > 2 && a.[2] <> "" then 
                            if a.[2].ToLower().StartsWith("portable") then [FrameworkRestriction.Portable(a.[2])] else
                            match FrameworkDetection.Extract a.[2] with
                            | Some x -> [FrameworkRestriction.Exactly x]
                            | None -> []
                         else 
                            []))
        |> Array.map (fun (name, version, restricted) -> PackageName name, VersionRequirement.Parse version, restricted)
        |> Array.toList

    
    { PackageName = officialName
      DownloadUrl = downloadLink
      Dependencies = Requirements.optimizeRestrictions packages
      SourceUrl = nugetURL
      CacheVersion = NugetPackageCache.CurrentCacheVersion
      LicenseUrl = licenseUrl
      Unlisted = publishDate = Constants.MagicUnlistingDate }


let getDetailsFromNuGetViaODataFast auth nugetURL package (version:SemVerInfo) = 
    async {         
        try 
            let url = sprintf "%s/Packages?$filter=Id eq '%s' and NormalizedVersion eq '%s'" nugetURL package (version.Normalize())
            let! raw = getFromUrl(auth,url)
            if verbose then
                tracefn "Response from %s:" url
                tracefn ""
                tracefn "%s" raw
            return parseODataDetails(nugetURL,package,version,raw)
        with _ ->         
            let url = sprintf "%s/Packages?$filter=Id eq '%s' and Version eq '%s'" nugetURL package (version.ToString())
            let! raw = getFromUrl(auth,url)
            if verbose then
                tracefn "Response from %s:" url
                tracefn ""
                tracefn "%s" raw
            return parseODataDetails(nugetURL,package,version,raw)
    }

/// Gets package details from Nuget via OData
let getDetailsFromNuGetViaOData auth nugetURL package (version:SemVerInfo) = 
    async {         
        try 
            return! getDetailsFromNuGetViaODataFast auth nugetURL package version
        with _ ->         
            let url = sprintf "%s/Packages(Id='%s',Version='%s')" nugetURL package (version.ToString())
            let! response = safeGetFromUrl(auth,url)
                    
            let! raw =
                match response with
                | Some(r) -> async { return r }
                | None ->
                    let url = sprintf "%s/odata/Packages(Id='%s',Version='%s')" nugetURL package (version.ToString())
                    getXmlFromUrl(auth,url)

            if verbose then
                tracefn "Response from %s:" url
                tracefn ""
                tracefn "%s" raw
            return parseODataDetails(nugetURL,package,version,raw)
    }

/// The NuGet cache folder.
let CacheFolder = 
    let appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
    let di = DirectoryInfo(Path.Combine(Path.Combine(appData, "NuGet"), "Cache"))
    if not di.Exists then
        di.Create()
    di.FullName

let inline normalizeUrl(url:string) = url.Replace("https","http").Replace("www.","")

let private loadFromCacheOrOData force fileName auth nugetURL package version = 
    async {
        if not force && File.Exists fileName then
            try 
                let json = File.ReadAllText(fileName)
                let cachedObject = JsonConvert.DeserializeObject<NugetPackageCache>(json)                
                if cachedObject.CacheVersion <> NugetPackageCache.CurrentCacheVersion then
                    let! details = getDetailsFromNuGetViaOData auth nugetURL package version
                    return true,details
                else
                    return false,cachedObject
            with _ -> 
                let! details = getDetailsFromNuGetViaOData auth nugetURL package version
                return true,details
        else
            let! details = getDetailsFromNuGetViaOData auth nugetURL package version
            return true,details
    }

let deleteErrorFile (packageName:PackageName) =
    let di = DirectoryInfo(CacheFolder)
    for errorFile in di.GetFiles(sprintf "*%O*.failed" packageName) do
        try
            File.Delete(errorFile.FullName)
        with
        | _ -> ()

/// Tries to get download link and direct dependencies from Nuget
/// Caches calls into json file
let getDetailsFromNuget force auth nugetURL package (version:SemVerInfo) = 
    let cacheFile = 
        let h = nugetURL |> normalizeUrl |> hash |> abs
        let packageUrl = sprintf "%s.%s.s%d.json" package (version.Normalize()) h
        FileInfo(Path.Combine(CacheFolder,packageUrl))

    let errorFile = FileInfo(cacheFile.FullName + ".failed")

    async {
        try
            if not force && errorFile.Exists then
                failwithf "Error file for %s exists at %s" package errorFile.FullName

            let! (invalidCache,details) = loadFromCacheOrOData force cacheFile.FullName auth nugetURL package version

            verbosefn "loaded details for '%s@%O' from url '%s'" package version nugetURL

            errorFile.Delete()
            if invalidCache then
                File.WriteAllText(cacheFile.FullName,JsonConvert.SerializeObject(details))
            return details
        with
        | exn -> 
            File.AppendAllText(errorFile.FullName,exn.ToString())
            raise exn
            return! getDetailsFromNuGetViaOData auth nugetURL package version
    } 
    
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

let findLocalPackage directory (name:string) (version:SemVerInfo) = 
    let v1 = FileInfo(Path.Combine(directory, sprintf "%s.%s.nupkg" name (version.ToString())))
    if v1.Exists then v1 else
    let normalizedVersion = version.Normalize()
    let v2 = FileInfo(Path.Combine(directory, sprintf "%s.%s.nupkg" name normalizedVersion))
    if v2.Exists then v2 else

    let v3 =
        Directory.EnumerateFiles(directory,"*.nupkg",SearchOption.AllDirectories)
        |> Seq.map (fun x -> FileInfo(x))
        |> Seq.filter (fun fi -> fi.Name.ToLower().Contains(name.ToLower()))
        |> Seq.filter (fun fi -> fi.Name.Contains(normalizedVersion) || fi.Name.Contains(version.ToString()))
        |> Seq.tryHead

    match v3 with
    | None -> failwithf "The package %s %s can't be found in %s.%sPlease check the feed definition in your paket.dependencies file." name (version.ToString()) directory Environment.NewLine
    | Some x -> x

/// Reads direct dependencies from a nupkg file
let getDetailsFromLocalFile root localNugetPath (packageName:PackageName) (version:SemVerInfo) =
    async {        
        let localNugetPath = Utils.normalizeLocalPath localNugetPath
        let di = getDirectoryInfo localNugetPath root
        let package = packageName.ToString()
        let nupkg = findLocalPackage di.FullName package version
        
        fixDatesInArchive nupkg.FullName
        use zipToCreate = new FileStream(nupkg.FullName, FileMode.Open)
        use zip = new ZipArchive(zipToCreate,ZipArchiveMode.Read)
        
        let zippedNuspec = zip.Entries |> Seq.find (fun f -> f.FullName.EndsWith ".nuspec")
        let fileName = FileInfo(Path.Combine(Path.GetTempPath(), zippedNuspec.Name)).FullName

        zippedNuspec.ExtractToFile(fileName, true)

        let nuspec = Nuspec.Load fileName        

        File.Delete(fileName)

        return 
            { PackageName = nuspec.OfficialName
              DownloadUrl = package
              Dependencies = Requirements.optimizeRestrictions nuspec.Dependencies
              SourceUrl = di.FullName
              CacheVersion = NugetPackageCache.CurrentCacheVersion
              LicenseUrl = nuspec.LicenseUrl
              Unlisted = false }
    }


let inline isExtracted fileName =
    let fi = FileInfo(fileName)
    if not fi.Exists then false else
    let di = fi.Directory
    di.EnumerateFileSystemInfos()
    |> Seq.exists (fun f -> f.FullName <> fi.FullName)    

/// Extracts the given package to the ./packages folder
let ExtractPackage(fileName:string, targetFolder, name, version:SemVerInfo) =    
    async {
        if isExtracted fileName then
             verbosefn "%s %A already extracted" name version
        else
            Directory.CreateDirectory(targetFolder) |> ignore

            fixDatesInArchive fileName            
            ZipFile.ExtractToDirectory(fileName, targetFolder)

            // cleanup folder structure
            let rec cleanup (dir : DirectoryInfo) = 
                for sub in dir.GetDirectories() do
                    let newName = sub.FullName.Replace("%2B", "+").Replace("%20", " ")
                    if sub.FullName <> newName && not (Directory.Exists newName) then 
                        Directory.Move(sub.FullName, newName)
                        cleanup (DirectoryInfo newName)
                    else
                        cleanup sub
                for file in dir.GetFiles() do
                    let newName = file.Name.Replace("%2B", "+").Replace("%20", " ")
                    if file.Name <> newName && not (File.Exists <| Path.Combine(file.DirectoryName, newName)) then
                        File.Move(file.FullName, Path.Combine(file.DirectoryName, newName))

            cleanup (DirectoryInfo targetFolder)
            tracefn "%s %A unzipped to %s" name version targetFolder
        return targetFolder
    }

let CopyLicenseFromCache(root, cacheFileName, name, version:SemVerInfo, includeVersionInPath, force) = 
    async {
        try
            if String.IsNullOrWhiteSpace cacheFileName then return () else
            let cacheFile = FileInfo cacheFileName
            if cacheFile.Exists then
                let targetFile = FileInfo(Path.Combine(getTargetFolder root name version includeVersionInPath, "license.html"))
                if not force && targetFile.Exists then
                    verbosefn "License %s %A already copied" name version        
                else                    
                    File.Copy(cacheFile.FullName, targetFile.FullName, true)
        with
        | exn -> traceWarnfn "Could not copy license for %s %A from %s.%s    %s" name version cacheFileName Environment.NewLine exn.Message
    }

/// Extracts the given package to the ./packages folder
let CopyFromCache(root, cacheFileName, licenseCacheFile, name, version:SemVerInfo, includeVersionInPath, force) = 
    async { 
        let targetFolder = DirectoryInfo(getTargetFolder root name version includeVersionInPath).FullName
        let fi = FileInfo(cacheFileName)
        let targetFile = FileInfo(Path.Combine(targetFolder, fi.Name))
        if not force && targetFile.Exists then           
            verbosefn "%s %A already copied" name version        
        else
            CleanDir targetFolder
            File.Copy(cacheFileName, targetFile.FullName)            
        try 
            let! extracted = ExtractPackage(targetFile.FullName,targetFolder,name,version)
            do! CopyLicenseFromCache(root, licenseCacheFile, name, version, includeVersionInPath, force)
            return extracted
        with
        | exn -> 
            File.Delete targetFile.FullName
            Directory.Delete(targetFolder,true)
            return! raise exn
    }

let DownloadLicense(root,force,name,version:SemVerInfo,licenseUrl,targetFileName) =
    async { 
        if String.IsNullOrWhiteSpace licenseUrl then return () else
        
        let targetFile = FileInfo targetFileName
        if not force && targetFile.Exists && targetFile.Length > 0L then 
            verbosefn "License for %s %A already downloaded" name version
        else             
            try
                verbosefn "Downloading license for %s %A to %s" name version targetFileName

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
                    traceWarnfn "Could not download license for %s %A from %s.%s    %s" name version licenseUrl Environment.NewLine exn.Message
    }

/// Downloads the given package to the NuGet Cache folder
let DownloadPackage(root, auth, url, name, version:SemVerInfo, includeVersionInPath, force) = 
    async { 
        let targetFileName = Path.Combine(CacheFolder, name + "." + version.Normalize() + ".nupkg")
        let targetFile = FileInfo targetFileName
        let licenseFileName = Path.Combine(CacheFolder, name + "." + version.Normalize() + ".license.html")
        if not force && targetFile.Exists && targetFile.Length > 0L then 
            verbosefn "%s %A already downloaded" name version            
        else 
            // discover the link on the fly
            let! nugetPackage = getDetailsFromNuget force auth url name version
            try
                let! license = Async.StartChild(DownloadLicense(root,force,name,version,nugetPackage.LicenseUrl,licenseFileName), 5000)

                tracefn "Downloading %s %A to %s" name version targetFileName

                let request = HttpWebRequest.Create(Uri nugetPackage.DownloadUrl) :?> HttpWebRequest
                request.AutomaticDecompression <- DecompressionMethods.GZip ||| DecompressionMethods.Deflate
                request.UserAgent <- "Paket"

                match auth with
                | None -> request.UseDefaultCredentials <- true
                | Some auth -> 
                    // htttp://stackoverflow.com/questions/16044313/webclient-httpwebrequest-with-basic-authentication-returns-404-not-found-for-v/26016919#26016919
                    //this works ONLY if the server returns 401 first
                    //client DOES NOT send credentials on first request
                    //ONLY after a 401
                    //client.Credentials <- new NetworkCredential(auth.Username,auth.Password)

                    //so use THIS instead to send credentials RIGHT AWAY
                    let credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(auth.Username + ":" + auth.Password))
                    request.Headers.[HttpRequestHeader.Authorization] <- String.Format("Basic {0}", credentials)

                request.Proxy <- Utils.getDefaultProxyFor url
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

                try
                    do! license
                with
                | exn ->
                    if verbose then
                        traceWarnfn "Could not download license for %s %A from %s.%s    %s" name version nugetPackage.LicenseUrl Environment.NewLine exn.Message 
            with
            | exn -> failwithf "Could not download %s %A.%s    %s" name version Environment.NewLine exn.Message
                
        return! CopyFromCache(root, targetFile.FullName, licenseFileName, name, version, includeVersionInPath, force)
    }

/// Finds all libraries in a nuget package.
let GetLibFiles(targetFolder) = 
    let libs = 
        let dir = DirectoryInfo(targetFolder)
        let libPath = dir.FullName.ToLower() + Path.DirectorySeparatorChar.ToString() + "lib" 
        if dir.Exists then
            dir.GetDirectories()
            |> Array.filter (fun fi -> fi.FullName.ToLower() = libPath)
            |> Array.collect (fun dir -> dir.GetFiles("*.*",SearchOption.AllDirectories))
        else
            [||]

    if Logging.verbose then
        if Array.isEmpty libs then 
            verbosefn "No libraries found in %s" targetFolder 
        else
            let s = String.Join(Environment.NewLine + "  - ",libs |> Array.map (fun l -> l.FullName))
            verbosefn "Libraries found in %s:%s  - %s" targetFolder Environment.NewLine s

    libs

/// Finds all targets files in a nuget package.
let GetTargetsFiles(targetFolder) = 
    let targetsFiles = 
        let dir = DirectoryInfo(targetFolder)
        let path = dir.FullName.ToLower() + Path.DirectorySeparatorChar.ToString() + "build" 
        if dir.Exists then
            dir.GetDirectories()
            |> Array.filter (fun fi -> fi.FullName.ToLower() = path)
            |> Array.collect (fun dir -> dir.GetFiles("*.*",SearchOption.AllDirectories))
        else
            [||]

    if Logging.verbose then
        if Array.isEmpty targetsFiles then
            verbosefn "No .targets files found in %s" targetFolder 
        else
            let s = String.Join(Environment.NewLine + "  - ",targetsFiles |> Array.map (fun l -> l.FullName))
            verbosefn ".targets files found in %s:%s  - %s" targetFolder Environment.NewLine s

    targetsFiles

let GetPackageDetails root force sources packageName (version:SemVerInfo) : PackageResolver.PackageDetails = 
    let package = packageName.ToString()
    let rec tryNext xs = 
        match xs with
        | source :: rest -> 
            verbosefn "Trying source '%O'" source
            try 
                match source with
                | Nuget source -> 
                    getDetailsFromNuget 
                        force 
                        (source.Authentication |> Option.map toBasicAuth)
                        source.Url 
                        (package.ToString())
                        version
                    |> Async.RunSynchronously
                | LocalNuget path -> 
                    getDetailsFromLocalFile root path packageName version 
                    |> Async.RunSynchronously
                |> fun x -> source,x
            with e ->
              verbosefn "Source '%O' exception: %O" source e
              tryNext rest
        | [] -> 
            deleteErrorFile packageName
            match sources with
            | [source] ->
                failwithf "Couldn't get package details for package %s %s on %s." package (version.ToString()) (source.ToString())
            | _ ->
                failwithf "Couldn't get package details for package %s %s on any of %A." package (version.ToString()) (sources |> List.map (fun (s:PackageSource) -> s.ToString()))
    
    let source,nugetObject = tryNext sources
    { Name = PackageName nugetObject.PackageName
      Source = source
      DownloadLink = nugetObject.DownloadUrl
      Unlisted = nugetObject.Unlisted
      LicenseUrl = nugetObject.LicenseUrl
      DirectDependencies = 
        nugetObject.Dependencies
        |> Requirements.optimizeRestrictions
        |> Set.ofList }

/// Allows to retrieve all version no. for a package from the given sources.
let GetVersions root (sources, PackageName packageName) = 
    let versions =
        sources
        |> Seq.map (fun source -> 
               match source with
               | Nuget source -> getAllVersions (
                                    source.Authentication |> Option.map toBasicAuth, 
                                    source.Url, 
                                    packageName)
               | LocalNuget path -> getAllVersionsFromLocalPath (path, packageName, root))
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Seq.choose id

    if Seq.isEmpty versions then
        failwithf "Could not find versions for package %s in any of the sources in %A." packageName sources

    versions
    |> Seq.concat
    |> Seq.toList
    |> List.map SemVer.Parse