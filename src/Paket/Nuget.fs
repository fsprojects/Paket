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

let getAllVersionsFromNugetOData (nugetURL, package) = 
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
    if text = "" then Latest else
    if text.StartsWith "[" then
        if text.EndsWith "]" then 
            VersionRange.Exactly(text.Replace("[","").Replace("]",""))
        else
            let parts = text.Replace("[","").Replace(")","").Split ','
            VersionRange.Between(parts.[0],parts.[1])
    else VersionRange.AtLeast(text)

/// Gets hash value and algorithm from Nuget.
let getDetailsFromNuget nugetURL name version = 
    async { 
        use wc = new WebClient()
        let! data = sprintf "%s/Packages(Id='%s',Version='%s')" nugetURL name version
                    |> wc.DownloadStringTaskAsync
                    |> Async.AwaitTask
        let data = XDocument.Parse data
let getDetailsFromNuget nugetURL package version = 
    async { 
        let! raw = sprintf "%s/Packages(Id='%s',Version='%s')" nugetURL package version |> getFromUrl
        let doc,manager = loadNuGetOData raw
            
        let getAttribute name = 
            seq { 
                   for node in doc.SelectNodes(sprintf "//ns:entry/m:properties/d:%s" name, manager) do
                       yield node.InnerText
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

        return (packages,getAttribute "PackageHash", getAttribute "PackageHashAlgorithm")
    }
    
/// The NuGet cache folder.
let CacheFolder = 
    let appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
    Path.Combine(Path.Combine(appData, "NuGet"), "Cache")

/// Downloads the given package to the NuGet Cache folder
let DownloadPackage(source, name, version, force) = 
    async { 
        let targetFileName = Path.Combine(CacheFolder,name + "." + version + ".nupkg")
        let targetFile = FileInfo targetFileName
        if not force && targetFile.Exists && targetFile.Length > 0L then 
            tracefn "%s %s already downloaded" name version
            return targetFileName 
        else
            let url = 
                match source with
                | "http://nuget.org/api/v2" -> sprintf "http://packages.nuget.org/v1/Package/Download/%s/%s" name version
                | _ -> 
                    // TODO: How can we discover the download link?
                    failwithf "unknown package source %s - can't download package %s %s" source name version
        
            use client = new WebClient()
            tracefn "Downloading %s %s to %s" name version targetFileName
            // TODO: Set credentials
            do! client.DownloadFileTaskAsync(Uri url, targetFileName)
                |> Async.AwaitIAsyncResult
                |> Async.Ignore
            let! _,hash,algorithm = getDetailsFromNuget source name version
            match (hash,algorithm) |> Hashing.compareWith name targetFile with
            | Some error -> 
                // TODO: File.Delete targetFileName
                traceError error
                return targetFileName
            | None -> return targetFileName
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
          
          member __.GetDirectDependencies(sourceType, source, package, version) = 
              if sourceType <> "nuget" then failwithf "invalid sourceType %s" sourceType
              async { let! dependencies, _, _ = getDetailsFromNuget source package version
                      return dependencies }
          
          member __.GetVersions(sourceType, source, package) = 
              if sourceType <> "nuget" then failwithf "invalid sourceType %s" sourceType
              getAllVersions (source, package) }