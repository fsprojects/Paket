/// Contains NuGet support.
module Paket.Nuget

open System
open System.IO
open System.Net
open Newtonsoft.Json
open Ionic.Zip
open System.Xml

let loadNuGetOData raw =
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
    
/// Gets versions of the given package.
let getAllVersions(nugetURL,package) = 
    // we cannot cache this
    async { 
        let! raw = sprintf "%s/package-versions/%s" nugetURL package |> getFromUrl
        if raw = "" then 
            let! first = getAllVersionsFromNugetOData(nugetURL,package)
            return first
        else 
            return JsonConvert.DeserializeObject<string []>(raw) |> Array.toSeq
    }

/// Parses NuGet version ranges.
let parseVersionRange (text:string) = 
    if text = null then nullArg "text" 
    let failParse() = failwithf "unable to parse %s" text

    let parseBound  = function
        | '[' | ']' -> Closed
        | '(' | ')' -> Open
        | _         -> failParse()

    if text = "" then Latest
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
let getDetailsFromNugetViaOData nugetURL package version = 
    async { 
        let! raw = sprintf "%s/Packages(Id='%s',Version='%s')" nugetURL package version |> getFromUrl
        let doc,manager = loadNuGetOData raw
            
        let getAttribute name = 
            seq { 
                   for node in doc.SelectNodes(sprintf "//ns:entry/m:properties/d:%s" name, manager) do
                       yield node.InnerText
               }
               |> Seq.head

        let downloadLink = 
            seq { 
                   for node in doc.SelectNodes("//ns:entry/ns:content", manager) do
                       if node.Attributes.["type"].Value = "application/zip" then
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
                   if a.Length > 1 then a.[1]
                   else "")
            |> Array.map (fun (name, version) -> 
                   { Name = name
                     // TODO: Parse nuget version ranges - see http://docs.nuget.org/docs/reference/versioning
                     VersionRange = parseVersionRange version
                     SourceType = "nuget"
                     Source = nugetURL })
            |> Array.toList

        return downloadLink,packages
    }

/// The NuGet cache folder.
let CacheFolder = 
    let appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
    Path.Combine(Path.Combine(appData, "NuGet"), "Cache")

let private loadFromCacheOrOData force fileName nugetURL package version = 
    async {
        if not force && File.Exists fileName then
            try 
                let json = File.ReadAllText(fileName)
                return false,JsonConvert.DeserializeObject<PackageDetails>(json)
            with _ -> 
                let! details = getDetailsFromNugetViaOData nugetURL package version
                return true,details
        else
            let! details = getDetailsFromNugetViaOData nugetURL package version
            return true,details
    }


let getDetailsFromNuget force nugetURL package version = 
    async {
        try            
            let fi = FileInfo(Path.Combine(CacheFolder,sprintf "%s.%s.json" package version))
            let! (invalidCache,details) = loadFromCacheOrOData force fi.FullName nugetURL package version 
            if invalidCache then
                File.WriteAllText(fi.FullName,JsonConvert.SerializeObject(details))
            return details
        with
        | _ -> return! getDetailsFromNugetViaOData nugetURL package version 
    }    

/// Downloads the given package to the NuGet Cache folder
let DownloadPackage(source, name, version, force) = async { 
        let targetFileName = Path.Combine(CacheFolder,name + "." + version + ".nupkg")
        let targetFile = FileInfo targetFileName
        if not force && targetFile.Exists && targetFile.Length > 0L then 
            tracefn "%s %s already downloaded" name version
            return targetFileName 
        else
            // discover the link on the fly
            let! (link,_) = getDetailsFromNuget force source name version
        
            use client = new WebClient()
            tracefn "Downloading %s %s to %s" name version targetFileName
            // TODO: Set credentials
            do! client.DownloadFileTaskAsync(Uri link, targetFileName)
                |> Async.AwaitIAsyncResult
                |> Async.Ignore

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

/// Nuget Discovery API.
let NugetDiscovery = 
    { new IDiscovery with
          
          member __.GetPackageDetails(force, sourceType, source, package, version) = 
              if sourceType <> "nuget" then failwithf "invalid sourceType %s" sourceType
              getDetailsFromNuget force source package version
          
          member __.GetVersions(sourceType, source, package) = 
              if sourceType <> "nuget" then failwithf "invalid sourceType %s" sourceType
              getAllVersions (source, package) }