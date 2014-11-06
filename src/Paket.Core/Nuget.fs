/// Contains NuGet support.
module Paket.Nuget

open System
open System.IO
open System.Net
open Newtonsoft.Json
open Ionic.Zip
open System.Xml
open System.Text.RegularExpressions
open Paket.Logging
open System.Text

open Paket.PackageSources

/// Represents type of NuGet packages.config file
type NugetPackagesConfigType = ProjectLevel | SolutionLevel

/// Represents NuGet packages.config file
type NugetPackagesConfig = {
    File: FileInfo;
    Packages: (string*SemVerInfo) list
    Type: NugetPackagesConfigType
}

let private loadNuGetOData raw =
    let doc = XmlDocument()
    doc.LoadXml raw
    let manager = new XmlNamespaceManager(doc.NameTable)
    manager.AddNamespace("ns", "http://www.w3.org/2005/Atom")
    manager.AddNamespace("d", "http://schemas.microsoft.com/ado/2007/08/dataservices")
    manager.AddNamespace("m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata")
    doc,manager

type NugetPackageCache =
    { Dependencies : (string * VersionRequirement * (FrameworkIdentifier option)) list
      Name : string
      SourceUrl: string
      Unlisted : bool
      DownloadUrl : string}

let rec private followODataLink auth url = 
    async { 
        let! raw = getFromUrl (auth, url)
        let doc, manager = loadNuGetOData raw
        
        let currentPage = 
            [ for node in doc.SelectNodes("//ns:feed/ns:entry/m:properties/d:Version", manager) -> node.InnerText ]

        return [ for linkNode in doc.SelectNodes("//ns:feed/ns:link[@rel=\"next\"]", manager) -> 
                     linkNode.Attributes.["href"].Value ]
               |> List.map (followODataLink auth)
               |> Async.Parallel
               |> Async.RunSynchronously
               |> Seq.concat
               |> Seq.append currentPage
    }

/// Gets versions of the given package via OData via /Packages?$filter=Id eq 'packageId'
let getAllVersionsFromNugetODataWithFilter (auth, nugetURL, package) = 
    // we cannot cache this
    followODataLink auth (sprintf "%s/Packages?$filter=Id eq '%s'" nugetURL package)

/// Gets versions of the given package via OData via /FindPackagesById()?id='packageId'.
let getAllVersionsFromNugetOData (auth, nugetURL, package) = 
    async { 
        // we cannot cache this
        try 
            return! followODataLink auth (sprintf "%s/FindPackagesById()?id='%s'" nugetURL package)
        with _ -> return! getAllVersionsFromNugetODataWithFilter (auth, nugetURL, package)
    }

/// Gets all versions no. of the given package.
let getAllVersions(auth,nugetURL, package) = 
    // we cannot cache this
    async { 
        let! raw = safeGetFromUrl(auth,sprintf "%s/package-versions/%s?includePrerelease=true" nugetURL package)
        match raw with
        | None -> let! result = getAllVersionsFromNugetOData(auth,nugetURL, package)
                  return result
        | Some data -> 
            try 
                try 
                    let result = JsonConvert.DeserializeObject<string []>(data) |> Array.toSeq
                    return result
                with _ -> let! result = getAllVersionsFromNugetOData(auth,nugetURL, package)
                          return result
            with exn -> 
                failwithf "Could not get data from %s for package %s.%s Message: %s" nugetURL package 
                    Environment.NewLine exn.Message
                return Seq.empty
    }

/// Gets versions of the given package from local Nuget feed.
let getAllVersionsFromLocalPath (localNugetPath, package) =
    async {
        return Directory.EnumerateFiles(localNugetPath,"*.nupkg",SearchOption.AllDirectories)
               |> Seq.choose (fun fileName ->
                                   let fi = FileInfo(fileName)
                                   let _match = Regex(sprintf @"^%s\.(\d.*)\.nupkg" package, RegexOptions.IgnoreCase).Match(fi.Name)
                                   if _match.Groups.Count > 1 then Some _match.Groups.[1].Value else None)
    }


let getODataDetails nugetURL raw = 
    let doc,manager = loadNuGetOData raw
            
    let getAttribute name = 
        seq { 
                for node in doc.SelectNodes(sprintf "//ns:entry/m:properties/d:%s" name, manager) do
                    yield node.InnerText
            }
            |> Seq.head


    let officialName = 
        match [ for node in doc.SelectNodes("//ns:entry/m:properties/d:Id", manager) -> node.InnerText] with
        | id::_ -> id
        | [] ->
            seq { 
                    for node in doc.SelectNodes("//ns:entry/ns:title", manager) do
                        yield node.InnerText
                }
                |> Seq.head

    let publishDate = 
        match [ for node in doc.SelectNodes("//ns:entry/m:properties/d:Published", manager) -> node.InnerText] with
        | id::_ -> 
            match DateTime.TryParse id with
            | true,date -> date
            | _ -> DateTime.MinValue
        | [] -> DateTime.MinValue

    let downloadLink = 
        seq { 
                for node in doc.SelectNodes("//ns:entry/ns:content", manager) do
                    let downloadType = node.Attributes.["type"].Value
                    if downloadType = "application/zip" || downloadType = "binary/octet-stream" then
                        yield node.Attributes.["src"].Value
            }
            |> Seq.head


    let packages = 
        getAttribute "Dependencies"
        |> fun s -> s.Split([| '|' |], System.StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun d -> d.Split ':')
        |> Array.filter (fun d -> Array.isEmpty d |> not && d.[0] <> "")
        |> Array.map (fun a -> a.[0],(if a.Length > 1 then a.[1] else "0"),(if a.Length > 2 && a.[2] <> "" then FrameworkIdentifier.Extract a.[2] else None))
        |> Array.map (fun (name, version, restricted) -> name, NugetVersionRangeParser.parse version, restricted)
        |> Array.toList

    { Name = officialName
      DownloadUrl = downloadLink
      Dependencies = packages
      SourceUrl = nugetURL
      Unlisted = publishDate = Constants.MagicUnlistingDate }

/// Gets package details from Nuget via OData
let getDetailsFromNugetViaOData auth nugetURL package version = 
    async { 
        let! raw = getFromUrl(auth,sprintf "%s/Packages?$filter=Id eq '%s' and Version eq '%s'" nugetURL package version)
        return getODataDetails nugetURL raw
    }


/// The NuGet cache folder.
let CacheFolder = 
    let appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
    let di = DirectoryInfo(Path.Combine(Path.Combine(appData, "NuGet"), "Cache"))
    if not di.Exists then
        di.Create()
    di.FullName

let private loadFromCacheOrOData force fileName auth nugetURL package version = 
    async {
        if not force && File.Exists fileName then
            try 
                let json = File.ReadAllText(fileName)
                let cachedObject = JsonConvert.DeserializeObject<NugetPackageCache>(json)                
                if cachedObject.Name = null || cachedObject.DownloadUrl = null || cachedObject.SourceUrl = null then
                    let! details = getDetailsFromNugetViaOData auth nugetURL package version
                    return true,details
                else
                    return false,cachedObject
            with _ -> 
                let! details = getDetailsFromNugetViaOData auth nugetURL package version
                return true,details
        else
            let! details = getDetailsFromNugetViaOData auth nugetURL package version
            return true,details
    }

/// Tries to get download link and direct dependencies from Nuget
/// Caches calls into json file
let getDetailsFromNuget force auth nugetURL package version = 
    async {
        try            
            let fi = FileInfo(Path.Combine(CacheFolder,sprintf "%s.%s.json" package version))
            let! (invalidCache,details) = loadFromCacheOrOData force fi.FullName auth nugetURL package version
            if details.SourceUrl <> nugetURL then
                return! getDetailsFromNugetViaOData auth nugetURL package version 
            else
                if invalidCache then
                    File.WriteAllText(fi.FullName,JsonConvert.SerializeObject(details))
                return details
        with
        | _ -> return! getDetailsFromNugetViaOData auth nugetURL package version 
    }    
    
/// Reads direct dependencies from a nupkg file
let getDetailsFromLocalFile path package version =
    async {
        let nupkg = FileInfo(Path.Combine(path, sprintf "%s.%s.nupkg" package version))
        let zip = ZipFile.Read(nupkg.FullName)
        let zippedNuspec = (zip |> Seq.find (fun f -> f.FileName.EndsWith ".nuspec"))

        zippedNuspec.Extract(Path.GetTempPath(), ExtractExistingFileAction.OverwriteSilently)

        let fileName = FileInfo(Path.Combine(Path.GetTempPath(), zippedNuspec.FileName)).FullName

        let nuspec = Nuspec.Load fileName        

        File.Delete(fileName)

        return 
            { Name = nuspec.OfficialName
              DownloadUrl = package
              Dependencies = nuspec.Dependencies
              SourceUrl = path
              Unlisted = false }
    }


let isExtracted fileName =
    let fi = FileInfo(fileName)
    if not fi.Exists then false else
    let di = fi.Directory
    di.EnumerateFileSystemInfos()
    |> Seq.exists (fun f -> f.FullName <>fi.FullName)    

/// Extracts the given package to the ./packages folder
let ExtractPackage(fileName:string, targetFolder, name, version) =    
    async {
        if  isExtracted fileName then
             verbosefn "%s %s already extracted" name version
        else
            use zip = ZipFile.Read(fileName)
            Directory.CreateDirectory(targetFolder) |> ignore
            for e in zip do
                try
                    e.Extract(targetFolder, ExtractExistingFileAction.OverwriteSilently)
                with
                | exn ->                    
                    failwithf "Error during unzipping %s in %s %s: %s" e.FileName name version exn.Message 

            // cleanup folder structure
            let rec cleanup (dir : DirectoryInfo) = 
                for sub in dir.GetDirectories() do
                    let newName = sub.FullName.Replace("%2B", "+")
                    if sub.FullName <> newName && not (Directory.Exists newName) then 
                        Directory.Move(sub.FullName, newName)
                        cleanup (DirectoryInfo newName)
                    else
                        cleanup sub
            cleanup (DirectoryInfo targetFolder)
            tracefn "%s %s unzipped to %s" name version targetFolder
        return targetFolder
    }

/// Extracts the given package to the ./packages folder
let CopyFromCache(cacheFileName, name, version, force) = 
    async { 
        let targetFolder = DirectoryInfo(Path.Combine("packages", name)).FullName
        let fi = FileInfo(cacheFileName)
        let targetFile = FileInfo(Path.Combine(targetFolder, fi.Name))
        if not force && targetFile.Exists then           
            verbosefn "%s %s already copied" name version        
        else
            CleanDir targetFolder
            File.Copy(cacheFileName, targetFile.FullName)            
        try
            return! ExtractPackage(targetFile.FullName,targetFolder,name,version)            
        with
        | exn -> 
            File.Delete targetFile.FullName
            Directory.Delete(targetFolder,true)
            raise exn
            return ""
    }

/// Downloads the given package to the NuGet Cache folder
let DownloadPackage(auth, url, name, version, force) = 
    async { 
        let targetFileName = Path.Combine(CacheFolder, name + "." + version + ".nupkg")
        let targetFile = FileInfo targetFileName
        if not force && targetFile.Exists && targetFile.Length > 0L then 
            verbosefn "%s %s already downloaded" name version            
        else 
            // discover the link on the fly
            let! nugetPackage = getDetailsFromNuget force auth url name version
            try
                tracefn "Downloading %s %s to %s" name version targetFileName

                let request = HttpWebRequest.Create(Uri nugetPackage.DownloadUrl) :?> HttpWebRequest
                request.AutomaticDecompression <- DecompressionMethods.GZip ||| DecompressionMethods.Deflate

                match auth with
                | None -> request.UseDefaultCredentials <- true
                | Some auth -> 
                    // htttp://stackoverflow.com/questions/16044313/webclient-httpwebrequest-with-basic-authentication-returns-404-not-found-for-v/26016919#26016919
                    //this works ONLY if the server returns 401 first
                    //client DOES NOT send credentials on first request
                    //ONLY after a 401
                    //client.Credentials <- new NetworkCredential(auth.Username,auth.Password)

                    //so use THIS instead to send credenatials RIGHT AWAY
                    let credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(auth.Username.Expanded + ":" + auth.Password.Expanded))
                    request.Headers.[HttpRequestHeader.Authorization] <- String.Format("Basic {0}", credentials)

                request.Proxy <- Utils.defaultProxy
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
            | exn -> failwithf "Could not download %s %s.%s    %s" name version Environment.NewLine exn.Message
        return! CopyFromCache(targetFile.FullName, name, version, force)
    }

/// Finds all libraries in a nuget package.
let GetLibFiles(targetFolder) =    
    let libs = 
        let dir = DirectoryInfo(targetFolder)
        let libPath = dir.FullName.ToLower() + Path.DirectorySeparatorChar.ToString() + "lib" + Path.DirectorySeparatorChar.ToString()
        if dir.Exists then
            dir.GetFiles("*.*",SearchOption.AllDirectories)
            |> Array.filter (fun fi -> fi.FullName.ToLower().Contains(libPath))
        else
            Array.empty

    if Logging.verbose then
        if Array.isEmpty libs then 
            verbosefn "No libraries found in %s" targetFolder 
        else
            let s = String.Join(Environment.NewLine + "  - ",libs |> Array.map (fun l -> l.FullName))
            verbosefn "Libraries found in %s:%s  - %s" targetFolder Environment.NewLine s

    libs

/// Lists packages defined in a NuGet packages.config
let ReadPackagesConfig(configFile : FileInfo) =
    let doc = XmlDocument()
    doc.Load configFile.FullName
    { File = configFile
      Type = if configFile.Directory.Name = ".nuget" then SolutionLevel else ProjectLevel
      Packages = [for node in doc.SelectNodes("//package") ->
                      node.Attributes.["id"].Value, node.Attributes.["version"].Value |> SemVer.Parse ]}

let GetPackageDetails force sources package version : PackageResolver.PackageDetails= 
    let rec tryNext xs = 
        match xs with
        | source :: rest -> 
            try 
                match source with
                | Nuget source -> 
                    getDetailsFromNuget force source.Auth source.Url package version |> Async.RunSynchronously
                | LocalNuget path -> 
                    getDetailsFromLocalFile path package version |> Async.RunSynchronously
                |> fun x -> source,x
            with _ -> tryNext rest
        | [] -> failwithf "Couldn't get package details for package %s on %A." package (sources |> List.map (fun (s:PackageSource) -> s.ToString()))
    
    let source,nugetObject = tryNext sources
    { Name = nugetObject.Name
      Source = source
      DownloadLink = nugetObject.DownloadUrl
      Unlisted = nugetObject.Unlisted
      DirectDependencies = nugetObject.Dependencies |> Set.ofList } 

/// Allows to retrieve all version no. for a package from the given sources.
let GetVersions(sources, packageName) = 
    sources
    |> Seq.map (fun source -> 
           match source with
           | Nuget source -> getAllVersions (source.Auth, source.Url, packageName)
           | LocalNuget path -> getAllVersionsFromLocalPath (path, packageName))
    |> Async.Parallel
    |> Async.RunSynchronously
    |> Seq.concat
    |> Seq.toList
    |> List.map SemVer.Parse