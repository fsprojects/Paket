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
open Paket.NuGetCache
open Paket.Utils
open Paket.Xml
open Paket.PackageSources
open Paket.Requirements
open FSharp.Polyfill
open System.Runtime.ExceptionServices

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
            let url = sprintf "%s/Packages?semVerLevel=2.0.0&$filter=tolower(Id) eq '%s'" nugetURL (package.CompareString)
            if verbose then
                verbosefn "getAllVersionsFromNugetODataWithFilter from url '%s'" url
            let! result = followODataLink auth url
            return Some result
        with _ -> return None
    }

let tryGetAllVersionsFromNugetODataFindById (auth, nugetURL, package:PackageName) =
    async {
        try
            let url = sprintf "%s/FindPackagesById()?semVerLevel=2.0.0&id='%O'" nugetURL package
            if verbose then
                verbosefn "getAllVersionsFromNugetODataFindById from url '%s'" url
            let! result = followODataLink auth url
            return Some result
        with _ -> return None
    }

let tryGetAllVersionsFromNugetODataFindByIdNewestFirst (auth, nugetURL, package:PackageName) =
    async {
        try
            let url = sprintf "%s/FindPackagesById()?semVerLevel=2.0.0id='%O'&$orderby=Published desc" nugetURL package
            if verbose then
                verbosefn "getAllVersionsFromNugetODataFindByIdNewestFirst from url '%s'" url
            let! result = followODataLink auth url
            return Some result
        with _ -> return None
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
    |> NuGetPackageCache.withDependencies dependencies

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
            if urlSimilarToTfsOrVsts nugetURL && result |> NuGetPackageCache.getDependencies |> List.isEmpty then
                // TODO: There is a bug in VSTS, so we can't trust this protocol. Remove when VSTS is fixed
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


