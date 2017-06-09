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
            |> List.filter (fun x -> x <> url)
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
            let url = sprintf "%s/Packages?$filter=tolower(Id) eq '%s'" nugetURL (package.CompareString)
            if verbose then
                verbosefn "getAllVersionsFromNugetODataWithFilter from url '%s'" url
            let! result = followODataLink auth url
            return Some result
        with _ -> return None
    }

let tryGetAllVersionsFromNugetODataFindById (auth, nugetURL, package:PackageName) =
    async {
        try
            let url = sprintf "%s/FindPackagesById()?id='%O'" nugetURL package
            if verbose then
                verbosefn "getAllVersionsFromNugetODataFindById from url '%s'" url
            let! result = followODataLink auth url
            return Some result
        with _ -> return None
    }

let tryGetAllVersionsFromNugetODataFindByIdNewestFirst (auth, nugetURL, package:PackageName) =
    async {
        try
            let url = sprintf "%s/FindPackagesById()?id='%O'&$orderby=Published desc" nugetURL package
            if verbose then
                verbosefn "getAllVersionsFromNugetODataFindByIdNewestFirst from url '%s'" url
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
let getAllVersionsFromLocalPath (isCache, localNugetPath, package:PackageName, alternativeProjectRoot, root) =
    async {
        let localNugetPath = Utils.normalizeLocalPath localNugetPath
        let di = getDirectoryInfoForLocalNuGetFeed localNugetPath alternativeProjectRoot root

        if not di.Exists then
            if isCache then
                di.Create()
            else
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


let parseODataDetails(url,nugetURL,packageName:PackageName,version:SemVerInfo,raw) =
    let doc = XmlDocument()
    try
        doc.LoadXml raw
    with
    | _ -> failwithf "Could not parse response from %s as OData.%sData:%s%s" url Environment.NewLine Environment.NewLine raw

    let entry =
        match (doc |> getNode "feed" |> optGetNode "entry" ) ++ (doc |> getNode "entry") with
        | Some node -> node
        | _ -> failwithf "unable to find entry node for package %O %O in %s" packageName version raw

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

    let rawPackages =
        let split (d : string) =
            let a = d.Split ':'
            let name = PackageName a.[0]
            let version = VersionRequirement.Parse(if a.Length > 1 then a.[1] else "0")
            (if a.Length > 2 && a.[2] <> "" then
                let restriction = a.[2]
                PlatformMatching.extractPlatforms restriction
             else Some PlatformMatching.ParsedPlatformPath.Empty)
            |> Option.map (fun pp -> name, version, pp)

        dependencies
        |> fun s -> s.Split([| '|' |], System.StringSplitOptions.RemoveEmptyEntries)
        |> Array.choose split

    let frameworks =
        rawPackages
        |> Seq.map (fun (_,_,pp) -> pp)
        |> Seq.distinctBy (fun pp -> pp.Platforms |> List.sort)
        |> Seq.toList
    let cleanedPackages =
        rawPackages
        |> Seq.filter (fun (n,_,_) -> System.String.IsNullOrEmpty (n.ToString()) |> not)
        |> Seq.toList
    let dependencies =
        addFrameworkRestrictionsToDependencies cleanedPackages frameworks

    { PackageName = officialName
      DownloadUrl = downloadLink
      SerializedDependencies = []
      SourceUrl = nugetURL
      CacheVersion = NuGetPackageCache.CurrentCacheVersion
      LicenseUrl = licenseUrl
      Version = (SemVer.Parse v).Normalize()
      Unlisted = publishDate = Constants.MagicUnlistingDate }
    |> NuGet.NuGetPackageCache.withDependencies dependencies

let getDetailsFromNuGetViaODataFast auth nugetURL (packageName:PackageName) (version:SemVerInfo) =
    async {
        try
            let url = sprintf "%s/Packages?$filter=(tolower(Id) eq '%s') and (NormalizedVersion eq '%s')" nugetURL (packageName.CompareString) (version.Normalize())
            let! raw = getFromUrl(auth,url,acceptXml)
            if verbose then
                tracefn "Response from %s:" url
                tracefn ""
                tracefn "%s" raw
            return parseODataDetails(url,nugetURL,packageName,version,raw)
        with _ ->
            let url = sprintf "%s/Packages?$filter=(tolower(Id) eq '%s') and (Version eq '%O')" nugetURL (packageName.CompareString) version
            let! raw = getFromUrl(auth,url,acceptXml)
            if verbose then
                tracefn "Response from %s:" url
                tracefn ""
                tracefn "%s" raw
            return parseODataDetails(url,nugetURL,packageName,version,raw)
    }

let urlSimilarToTfsOrVsts url =
    String.containsIgnoreCase "visualstudio.com" url || (String.containsIgnoreCase "/_packaging/" url && String.containsIgnoreCase "/nuget/v" url)

/// Gets package details from NuGet via OData
let getDetailsFromNuGetViaOData auth nugetURL (packageName:PackageName) (version:SemVerInfo) =
    let queryPackagesProtocol (packageName:PackageName) =
        async {
            let url = sprintf "%s/Packages(Id='%O',Version='%O')" nugetURL packageName version
            let! response = safeGetFromUrl(auth,url,acceptXml)

            let! raw =
                match response with
                | Some(r) -> async { return r }
                | _  when
                        String.containsIgnoreCase "myget.org" nugetURL ||
                        String.containsIgnoreCase "nuget.org" nugetURL ||
                        String.containsIgnoreCase "visualstudio.com" nugetURL ->
                    failwithf "Could not get package details for %O from %s" packageName nugetURL
                | _ ->
                    let url = sprintf "%s/odata/Packages(Id='%O',Version='%O')" nugetURL packageName version
                    getXmlFromUrl(auth,url)

            if verbose then
                tracefn "Response from %s:" url
                tracefn ""
                tracefn "%s" raw
            return parseODataDetails(url,nugetURL,packageName,version,raw) }

    async {
        try
            let! result = getDetailsFromNuGetViaODataFast auth nugetURL packageName version
            if urlSimilarToTfsOrVsts nugetURL && result |> NuGet.NuGetPackageCache.getDependencies |> List.isEmpty then
                // TODO: There is a bug in VSTS, so we can't trust this protocol. Remvoe when VSTS is fixed
                // TODO: TFS has the same bug
                return! queryPackagesProtocol packageName
            else
                return result
        with _ ->
            try
                return! queryPackagesProtocol packageName
            with _ ->
                // try uppercase version as workaround for https://github.com/fsprojects/Paket/issues/2145 - Bad!
                let name = PackageName.ToString()
                let uppercase = PackageName.ToString().[0].ToString().ToUpper() + name.Substring(1)
                return! queryPackagesProtocol (PackageName uppercase)
    }

let getDetailsFromNuGet force auth nugetURL packageName version =
    getDetailsFromCacheOr
        force
        nugetURL
        packageName
        version
        (fun () -> getDetailsFromNuGetViaOData auth nugetURL packageName version)


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

let findLocalPackage directory (packageName:PackageName) (version:SemVerInfo) =
    if not <| Directory.Exists directory then
        failwithf "The package %O %O can't be found in %s. (The directory doesn't exist.)%sPlease check the feed definition in your paket.dependencies file." packageName version directory Environment.NewLine
    let v1 = FileInfo(Path.Combine(directory, sprintf "%O.%O.nupkg" packageName version))
    if v1.Exists then v1 else
    let normalizedVersion = version.Normalize()
    let v2 = FileInfo(Path.Combine(directory, sprintf "%O.%s.nupkg" packageName normalizedVersion))
    if v2.Exists then v2 else

    let v3 =
        Directory.EnumerateFiles(directory,"*.nupkg",SearchOption.AllDirectories)
        |> Seq.map (fun x -> FileInfo(x))
        |> Seq.filter (fun fi -> String.containsIgnoreCase (packageName.CompareString)  fi.Name)
        |> Seq.filter (fun fi -> fi.Name.Contains(normalizedVersion) || fi.Name.Contains(version.ToString()))
        |> Seq.tryHead

    match v3 with
    | None -> failwithf "The package %O %O can't be found in %s.%sPlease check the feed definition in your paket.dependencies file." packageName version directory Environment.NewLine
    | Some x -> x

/// Reads package name from a nupkg file
let getPackageNameFromLocalFile fileName =
    use __ = Profile.startCategory Profile.Category.FileIO
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
let getDetailsFromLocalNuGetPackage isCache alternativeProjectRoot root localNuGetPath (packageName:PackageName) (version:SemVerInfo) =
    async {
        let localNugetPath = Utils.normalizeLocalPath localNuGetPath
        let di = getDirectoryInfoForLocalNuGetFeed localNugetPath alternativeProjectRoot root
        let nupkg = findLocalPackage di.FullName packageName version
        
        use _ = Profile.startCategory Profile.Category.FileIO
        fixArchive nupkg.FullName
        use zipToCreate = new FileStream(nupkg.FullName, FileMode.Open, FileAccess.Read)
        use zip = new ZipArchive(zipToCreate,ZipArchiveMode.Read)

        let zippedNuspec = zip.Entries |> Seq.find (fun f -> f.FullName.EndsWith ".nuspec")
        let fileName = FileInfo(Path.Combine(Path.GetTempPath(), zippedNuspec.Name)).FullName

        zippedNuspec.ExtractToFile(fileName, true)

        let nuspec = Nuspec.Load fileName

        File.Delete(fileName)

        return
            { PackageName = nuspec.OfficialName
              DownloadUrl = packageName.ToString()
              SerializedDependencies = []
              SourceUrl = di.FullName
              CacheVersion = NuGetPackageCache.CurrentCacheVersion
              LicenseUrl = nuspec.LicenseUrl
              Version = version.Normalize()
              Unlisted = isCache }
            |> NuGet.NuGetPackageCache.withDependencies nuspec.Dependencies
    }


let inline isExtracted (directory:DirectoryInfo) fileName =
    let fi = FileInfo(fileName)
    if not fi.Exists then false else
    if not directory.Exists then false else
    directory.EnumerateFileSystemInfos()
    |> Seq.exists (fun f -> f.FullName <> fi.FullName)

let IsPackageVersionExtracted(root, groupName, packageName:PackageName, version:SemVerInfo, includeVersionInPath) =
    let targetFolder = DirectoryInfo(getTargetFolder root groupName packageName version includeVersionInPath)
    let targetFileName = packageName.ToString() + "." + version.Normalize() + ".nupkg"
    isExtracted targetFolder targetFileName

// cleanup folder structure
let rec private cleanup (dir : DirectoryInfo) =
    for sub in dir.GetDirectories() do
        let newName = Uri.UnescapeDataString(sub.FullName).Replace("%2B","+")
        let di = DirectoryInfo newName
        if sub.FullName <> newName && not di.Exists then
            if not di.Parent.Exists then
                di.Parent.Create()
            try
                Directory.Move(sub.FullName, newName)
            with
            | exn -> failwithf "Could not move %s to %s%sMessage: %s" sub.FullName newName Environment.NewLine exn.Message

            cleanup (DirectoryInfo newName)
        else
            cleanup sub

    for file in dir.GetFiles() do
        let newName = Uri.UnescapeDataString(file.Name).Replace("%2B","+")
        if newName.Contains "..\\" || newName.Contains "../" then
          failwithf "Relative paths are not supported. Please tell the package author to fix the package to not use relative paths. The invalid file was '%s'" file.FullName
        if newName.Contains "\\" || newName.Contains "/" then
          traceWarnfn "File '%s' contains back- or forward-slashes, probably because it wasn't properly packaged (for example with windows paths in nuspec on a unix like system). Please tell the package author to fix it." file.FullName
        let newFullName = Path.Combine(file.DirectoryName, newName)
        if file.Name <> newName && not (File.Exists newFullName) then
            let dir = Path.GetDirectoryName newFullName
            if not <| Directory.Exists dir then
                Directory.CreateDirectory dir |> ignore

            File.Move(file.FullName, newFullName)


/// Extracts the given package to the user folder
let ExtractPackageToUserFolder(fileName:string, packageName:PackageName, version:SemVerInfo, detailed) =
    async {
        let targetFolder = DirectoryInfo(Path.Combine(Constants.UserNuGetPackagesFolder,packageName.ToString(),version.Normalize()))
        
        use _ = Profile.startCategory Profile.Category.FileIO
        if isExtracted targetFolder fileName |> not then
            Directory.CreateDirectory(targetFolder.FullName) |> ignore
            let fi = FileInfo fileName
            let targetPackageFileName = Path.Combine(targetFolder.FullName,fi.Name)
            File.Copy(fileName,targetPackageFileName)

            ZipFile.ExtractToDirectory(fileName, targetFolder.FullName)

            let cachedHashFile = Path.Combine(Constants.NuGetCacheFolder,fi.Name + ".sha512")
            if not <| File.Exists cachedHashFile then
                use stream = File.OpenRead(fileName)
                let packageSize = stream.Length
                use hasher = System.Security.Cryptography.SHA512.Create() :> System.Security.Cryptography.HashAlgorithm
                let packageHash = Convert.ToBase64String(hasher.ComputeHash(stream))
                File.WriteAllText(cachedHashFile,packageHash)

            File.Copy(cachedHashFile,targetPackageFileName + ".sha512")
            cleanup targetFolder
        return targetFolder.FullName
    }

/// Extracts the given package to the ./packages folder
let ExtractPackage(fileName:string, targetFolder, packageName:PackageName, version:SemVerInfo, detailed) =
    async {
        use _ = Profile.startCategory Profile.Category.FileIO
        let directory = DirectoryInfo(targetFolder)
        if isExtracted directory fileName then
             if verbose then
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


            cleanup directory
            if verbose then
                verbosefn "%O %O unzipped to %s" packageName version targetFolder
        let! _ = ExtractPackageToUserFolder(fileName, packageName, version, detailed)
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
                    if verbose then
                       verbosefn "License %O %O already copied" packageName version
                else
                    use _ = Profile.startCategory Profile.Category.FileIO
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
            if verbose then
                verbosefn "%O %O already copied" packageName version
        else
            use _ = Profile.startCategory Profile.Category.FileIO
            CleanDir targetFolder
            File.Copy(cacheFileName, targetFile.FullName)
        try
            let! extracted = ExtractPackage(targetFile.FullName,targetFolder,packageName,version,detailed)
            do! CopyLicenseFromCache(root, groupName, licenseCacheFile, packageName, version, includeVersionInPath, force)
            return extracted
        with
        | exn ->
            use _ = Profile.startCategory Profile.Category.FileIO
            File.Delete targetFile.FullName
            Directory.Delete(targetFolder,true)
            return! raise exn
    }

/// Puts the package into the cache
let CopyToCache(cache:Cache, fileName, force) =
    try
        use __ = Profile.startCategory Profile.Category.FileIO
        if Cache.isInaccessible cache then
            if verbose then
                verbosefn "Cache %s is inaccessible, skipping" cache.Location
        else
            let targetFolder = DirectoryInfo(cache.Location)
            if not targetFolder.Exists then
                targetFolder.Create()

            let fi = FileInfo(fileName)
            let targetFile = FileInfo(Path.Combine(targetFolder.FullName, fi.Name))

            if not force && targetFile.Exists then
                if verbose then
                    verbosefn "%s already in cache %s" fi.Name targetFolder.FullName
            else
                File.Copy(fileName, targetFile.FullName, force)
    with
    | _ ->
        Cache.setInaccessible cache
        reraise()

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

let rec private getPackageDetails alternativeProjectRoot root force (sources:PackageSource list) packageName (version:SemVerInfo) : Async<PackageResolver.PackageDetails> =
    async {
        let tryV2 source (nugetSource:NugetSource) force = async {
            let! result =
                getDetailsFromNuGet
                    force
                    (nugetSource.Authentication |> Option.map toBasicAuth)
                    nugetSource.Url
                    packageName
                    version
            return Some(source,result)  }

        let tryV3 source nugetSource force = async {
            if nugetSource.Url.Contains("myget.org") || nugetSource.Url.Contains("nuget.org") || nugetSource.Url.Contains("visualstudio.com") || nugetSource.Url.Contains("/nuget/v3/") then
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
            // helper to work through the list sequentially
            let rec trySelectFirst workLeft =
                async {
                    match workLeft with
                    | work :: rest ->
                        let! r = work
                        match r with
                        | Some result -> return Some result
                        | None -> return! trySelectFirst rest
                    | [] -> return None
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
                    | NuGetV3 nugetSource when urlSimilarToTfsOrVsts nugetSource.Url  ->
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
                        let! result = getDetailsFromLocalNuGetPackage true alternativeProjectRoot root path packageName version
                        return Some(source,result)
                    | LocalNuGet(path,None) ->
                        let! result = getDetailsFromLocalNuGetPackage false alternativeProjectRoot root path packageName version
                        return Some(source,result)
                with e ->
                    if verbose then
                        verbosefn "Source '%O' exception: %O" source e
                    return None })
            |> trySelectFirst

        let! maybePackageDetails = getPackageDetails force
        let! source,nugetObject =
            async {
                match maybePackageDetails with
                | None ->
                    let! m = getPackageDetails true
                    match m with
                    | None ->
                        match sources |> List.map (fun (s:PackageSource) -> s.ToString()) with
                        | [source] ->
                            return failwithf "Couldn't get package details for package %O %O on %O." packageName version source
                        | [] ->
                            return failwithf "Couldn't get package details for package %O %O, because no sources were specified." packageName version
                        | sources ->
                            return failwithf "Couldn't get package details for package %O %O on any of %A." packageName version sources
                    | Some packageDetails -> return packageDetails
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
              DirectDependencies = NuGet.NuGetPackageCache.getDependencies nugetObject |> Set.ofList } }

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
let GetVersions force alternativeProjectRoot root (sources, packageName:PackageName) = async {
    let trial force = async {
        let getVersionsFailedCacheFileName (source:PackageSource) =
            let h = source.Url |> normalizeUrl |> hash |> abs
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
                                [getVersionsCached "Json" tryGetPackageVersionsViaJson (nugetSource, auth, source.Url, packageName) ]
                            elif String.containsIgnoreCase "artifactory" source.Url then
                                [getVersionsCached "ODataNewestFirst" tryGetAllVersionsFromNugetODataFindByIdNewestFirst (nugetSource, auth, source.Url, packageName) ]
                            else
                                let v2Feeds =
                                    [ yield getVersionsCached "OData" tryGetAllVersionsFromNugetODataFindById (nugetSource, auth, source.Url, packageName)
                                      yield getVersionsCached "ODataWithFilter" tryGetAllVersionsFromNugetODataWithFilter (nugetSource, auth, source.Url, packageName)
                                      if not (String.containsIgnoreCase "teamcity" source.Url || String.containsIgnoreCase"feedservice.svc" source.Url  ) then
                                        yield getVersionsCached "Json" tryGetPackageVersionsViaJson (nugetSource, auth, source.Url, packageName) ]

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
                       | LocalNuGet(path,Some _) -> [ getAllVersionsFromLocalPath (true, path, packageName, alternativeProjectRoot, root) ]
                       | LocalNuGet(path,None) -> [ getAllVersionsFromLocalPath (false, path, packageName, alternativeProjectRoot, root) ])
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
                    let nupkg = findLocalPackage di.FullName packageName version
                    
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
        let! files = CopyFromCache(root, groupName, targetFile.FullName, licenseFileName, packageName, version, includeVersionInPath, force, detailed)
        return targetFileName,files
    }
