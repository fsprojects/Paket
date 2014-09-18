/// Contains NuGet support.
module Paket.Nuget

open System
open System.IO
open System.Net
open Newtonsoft.Json
open Ionic.Zip
open System.Xml
open System.Collections.Generic
open System.Text.RegularExpressions

let private loadNuGetOData raw =
    let doc = XmlDocument()
    doc.LoadXml raw
    let manager = new XmlNamespaceManager(doc.NameTable)
    manager.AddNamespace("ns", "http://www.w3.org/2005/Atom")
    manager.AddNamespace("d", "http://schemas.microsoft.com/ado/2007/08/dataservices")
    manager.AddNamespace("m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata")
    doc,manager

/// Gets versions of the given package via OData.
let getAllVersionsFromNugetOData (nugetURL, package) = 
    // we cannot cache this
    async { 
        let! raw = sprintf "%s/Packages?$filter=Id eq '%s'" nugetURL package |> getFromUrl
        let doc,manager = loadNuGetOData raw
        return seq { 
                   for node in doc.SelectNodes("//ns:feed/ns:entry/m:properties/d:Version", manager) do
                       yield node.InnerText
               }
    }
    
let private versionDataCache = Dictionary<_, _>()

/// Gets versions of the given package.
/// Caches calls in RAM - do not cache on disk!
let getAllVersions (nugetURL, package) =     
    // we cannot cache this
    async { 
        let key = nugetURL + package
        match versionDataCache.TryGetValue key with
        | true, data -> return data
        | _ -> 
            let! raw = sprintf "%s/package-versions/%s" nugetURL package |> safeGetFromUrl

            match raw with
            | None -> 
                let! result = getAllVersionsFromNugetOData (nugetURL, package)
                versionDataCache.Add(key, result)
                return result
            | Some data -> 
                try
                    try
                        let result = JsonConvert.DeserializeObject<string []>(data) |> Array.toSeq
                        versionDataCache.Add(key, result)
                        return result
                    with
                    | _ ->
                        let! result = getAllVersionsFromNugetOData (nugetURL, package)
                        versionDataCache.Add(key, result)
                        return result
                with 
                | exn -> 
                    failwithf "Could not get data from %s for package %s.%s Message: %s" nugetURL package Environment.NewLine exn.Message
                    return Seq.empty
    }

/// Gets versions of the given package from local Nuget feed.
let getAllVersionsFromLocalPath (localNugetPath, package) =
    async {
        return Directory.EnumerateFiles(localNugetPath,"*.nupkg",SearchOption.AllDirectories)
               |> Seq.choose (fun fileName -> 
                                   let _match = Regex(sprintf @"%s\.(\d.*)\.nupkg" package, RegexOptions.IgnoreCase).Match(fileName)
                                   if _match.Groups.Count > 1 then Some _match.Groups.[1].Value else None)
    }


/// Parses NuGet version ranges.
let parseVersionRange (text:string) = 
    if text = null then nullArg "text" 
    let failParse() = failwithf "unable to parse %s" text

    let parseBound  = function
        | '[' | ']' -> Closed
        | '(' | ')' -> Open
        | _         -> failParse()

    if text = "" || text = "null" then VersionRange.NoRestriction
    elif not <| text.Contains "," then
        if text.StartsWith "[" then Specific(text.Trim([|'['; ']'|]) |> SemVer.parse)
        else Minimum(SemVer.parse text)
    else
        let fromB = parseBound text.[0]
        let toB   = parseBound (Seq.last text)
        let versions = text
                        .Trim([|'['; ']';'(';')'|])
                        .Split([|','|], StringSplitOptions.RemoveEmptyEntries)
                        |> Array.map SemVer.parse
        match versions.Length with
        | 2 ->
            Range(fromB, versions.[0], versions.[1], toB)
        | 1 ->
            if text.[1] = ',' then
                match fromB, toB with
                | Open, Closed -> Maximum(versions.[0])
                | Open, Open -> LessThan(versions.[0])
                | _ -> failParse()
            else 
                match fromB, toB with
                | Open, Open -> GreaterThan(versions.[0])
                | _ -> failParse()
        | _ -> failParse()
            
/// Gets package details from Nuget via OData
let getDetailsFromNugetViaOData nugetURL package sources resolverStrategy version = 
    async { 
        let! raw = sprintf "%s/Packages(Id='%s',Version='%s')" nugetURL package version |> getFromUrl
        let doc,manager = loadNuGetOData raw
            
        let getAttribute name = 
            seq { 
                   for node in doc.SelectNodes(sprintf "//ns:entry/m:properties/d:%s" name, manager) do
                       yield node.InnerText
               }
               |> Seq.head

        let officialName = 
            seq { 
                   for node in doc.SelectNodes("//ns:entry/ns:title", manager) do
                       yield node.InnerText
               }
               |> Seq.head


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
            |> Array.filter (fun d -> Array.isEmpty d
                                      |> not && d.[0] <> "")
            |> Array.map (fun a -> 
                   a.[0], 
                   if a.Length > 1 then a.[1] else "0")
            |> Array.map (fun (name, version) ->
                   { Name = name
                     VersionRange =  parseVersionRange version
                     Sources = sources
                     ResolverStrategy = resolverStrategy })
            |> Array.toList

        return officialName,downloadLink,packages
    }

/// The NuGet cache folder.
let CacheFolder = 
    let appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
    Path.Combine(Path.Combine(appData, "NuGet"), "Cache")

let private loadFromCacheOrOData force fileName nugetURL package sources resolverStrategy version = 
    async {
        if not force && File.Exists fileName then
            try 
                let json = File.ReadAllText(fileName)
                return false,JsonConvert.DeserializeObject<string * string * UnresolvedPackage list>(json)
            with _ -> 
                let! details = getDetailsFromNugetViaOData nugetURL package sources resolverStrategy version
                return true,details
        else
            let! details = getDetailsFromNugetViaOData nugetURL package sources resolverStrategy version
            return true,details
    }


/// Tries to get download link and direct dependencies from Nuget
/// Caches calls into json file
let getDetailsFromNuget force nugetURL package sources resolverStrategy version = 
    async {
        try            
            let fi = FileInfo(Path.Combine(CacheFolder,sprintf "%s.%s.json" package version))
            let! (invalidCache,details) = loadFromCacheOrOData force fi.FullName nugetURL package sources resolverStrategy version 
            if invalidCache then
                File.WriteAllText(fi.FullName,JsonConvert.SerializeObject(details))
            return details
        with
        | _ -> return! getDetailsFromNugetViaOData nugetURL package sources resolverStrategy version 
    }    
    
/// Reads direct dependencies from a nupkg file
let getDetailsFromLocalFile path package sources resolverStrategy version =
    async {
        let nupkg = FileInfo(Path.Combine(path, sprintf "%s.%s.nupkg" package version))
        let zip = ZipFile.Read(nupkg.FullName)
        let zippedNuspec = (zip |> Seq.find (fun f -> f.FileName.EndsWith ".nuspec"))

        zippedNuspec.Extract(Path.GetTempPath(), ExtractExistingFileAction.OverwriteSilently)

        let nuspec = FileInfo(Path.Combine(Path.GetTempPath(), zippedNuspec.FileName))
        
        let xmlDoc = XmlDocument()
        nuspec.FullName |> xmlDoc.Load

        let ns = new XmlNamespaceManager(xmlDoc.NameTable);
        ns.AddNamespace("x", "http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd");
        ns.AddNamespace("y", "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd");
        
        let nsUri = xmlDoc.LastChild.NamespaceURI
        let pfx = ns.LookupPrefix(nsUri)

        let dependencies = 
            xmlDoc.SelectNodes(sprintf "/%s:package/%s:metadata/%s:dependencies/%s:dependency" pfx pfx pfx pfx, ns)
            |> Seq.cast<XmlNode>
            |> Seq.map (fun node -> 
                            let name = node.Attributes.["id"].Value                            
                            let version = 
                                if node.Attributes.["version"] <> null then 
                                    parseVersionRange node.Attributes.["version"].Value 
                                else 
                                    parseVersionRange "0"
                            {Name = name
                             VersionRange = version
                             Sources = sources
                             ResolverStrategy = resolverStrategy }) 
            |> Seq.toList

        let officialName = 
            xmlDoc.SelectNodes(sprintf "/%s:package/%s:metadata/%s:id" pfx pfx pfx, ns)
            |> Seq.cast<XmlNode>
            |> Seq.head
            |> fun node -> node.InnerText

        File.Delete(nuspec.FullName)

        return officialName,package,dependencies
    }

/// Downloads the given package to the NuGet Cache folder
let DownloadPackage(url, name, sources, version, force) = 
    async { 
        let targetFileName = Path.Combine(CacheFolder, name + "." + version + ".nupkg")
        let targetFile = FileInfo targetFileName
        if not force && targetFile.Exists && targetFile.Length > 0L then 
            tracefn "%s %s already downloaded" name version
            return targetFileName
        else 
            // discover the link on the fly
            let! (_,link, _) = getDetailsFromNuget force url name sources ResolverStrategy.Max version
            use client = new WebClient()
            tracefn "Downloading %s %s to %s" name version targetFileName
            // TODO: Set credentials
            client.DownloadFileAsync(Uri link, targetFileName)
            let! _ = Async.AwaitEvent(client.DownloadFileCompleted)
            return targetFileName
    }


/// Extracts the given package to the ./packages folder
let ExtractPackage(fileName, name, version, force) = 
    async { 
        let targetFolder = DirectoryInfo(Path.Combine("packages", name)).FullName
        let fi = FileInfo(fileName)
        let targetFile = FileInfo(Path.Combine(targetFolder, fi.Name))
        if not force && targetFile.Exists then 
            tracefn "%s %s already extracted" name version
            return targetFolder
        else 
            CleanDir targetFolder
            File.Copy(fileName, targetFile.FullName)
            let zip = ZipFile.Read(fileName)
            Directory.CreateDirectory(targetFolder) |> ignore
            for e in zip do
                e.Extract(targetFolder, ExtractExistingFileAction.OverwriteSilently)

            // cleanup folder structure
            let rec cleanup (dir : DirectoryInfo) = 
                for sub in dir.GetDirectories() do
                    let newName = sub.FullName.Replace("%2B", "+")
                    if sub.FullName <> newName then 
                        Directory.Move(sub.FullName, newName)
                        cleanup (DirectoryInfo newName)
                    else
                        cleanup sub
            cleanup (DirectoryInfo targetFolder)
            tracefn "%s %s unzipped to %s" name version targetFolder
            return targetFolder
    }

/// Finds all libraries in a nuget packge.
let GetLibraries(targetFolder) =
    let dir = DirectoryInfo(Path.Combine(targetFolder,"lib"))
    if dir.Exists then
        dir.GetFiles("*.dll",SearchOption.AllDirectories)
    else
        Array.empty

/// Lists packages defined in a NuGet packages.config
let ReadPackagesFromFile(configFile : FileInfo) =
    let doc = XmlDocument()
    doc.Load configFile.FullName
    [for node in doc.SelectNodes("//package") ->
        node.Attributes.["id"].Value, node.Attributes.["version"].Value |> SemVer.parse ]

/// Nuget Discovery API.
let NugetDiscovery = 
    { new IDiscovery with
          
          //TODO: Should we really be able to call these methods with invalid arguments?
          member __.GetPackageDetails(force, sources, package, resolverStrategy, version) = async { 
                  let rec tryNext xs = 
                      async { 
                          match xs with
                          | source :: rest -> 
                              try 
                                  match source with
                                  | Nuget url -> 
                                    let! details = getDetailsFromNuget force url package sources resolverStrategy version                                  
                                    return source,details
                                  | LocalNuget path -> 
                                    let! details = getDetailsFromLocalFile path package sources resolverStrategy version                                    
                                    return source,details
                              with _ ->
                                return! tryNext rest
                          | [] -> 
                              failwithf "Couldn't get package details for package %s on %A" package sources
                              return! tryNext []
                      }

                  let! source,(name,link,packages) = tryNext sources
                  return 
                      { Name = name
                        Source = source
                        DownloadLink = link
                        DirectDependencies =
                        packages |> List.map (fun package -> {package with Sources = source :: (List.filter ((<>) source) sources) })}
              }
          
          member __.GetVersions(sources, package) =
              sources
              |> Seq.map (fun source -> 
                            match source with
                            | Nuget url -> getAllVersions (url, package)
                            | LocalNuget path -> getAllVersionsFromLocalPath (path, package))
              |> Async.Parallel }