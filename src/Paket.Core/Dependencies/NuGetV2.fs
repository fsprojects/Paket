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

let private followODataLink auth url =
    let rec followODataLinkSafe (knownVersions:Set<_>) url =
        async {
            let! raw = getFromUrl(auth, url, acceptXml)
            if String.IsNullOrWhiteSpace raw then return true, [||] else
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
            let newSet = entriesVersions |> Set.ofList
            let noNewFound = newSet.IsSubsetOf knownVersions
            if noNewFound then
                return false, [||]
            else

            let linksToFollow =
                feed
                |> getNodes "link"
                |> List.filter (fun node -> node |> getAttribute "rel" = Some "next")
                |> List.choose (getAttribute "href")
                |> List.filter (fun x -> x <> url)

            let! linksVersions =
                linksToFollow
                |> List.map (followODataLinkSafe (Set.union newSet knownVersions))
                |> Async.Parallel

            let atLeastOneFailed =
                linksVersions
                |> Seq.map fst
                |> Seq.tryFindIndex not

            let collected =
                linksVersions
                |> Seq.map snd
                |> Seq.collect id
                |> Seq.toArray

            match atLeastOneFailed with
            | Some i ->
                traceWarnfn "At least one 'next' link (index %d) returned a empty result (noticed on '%O'): ['%s']" i url (System.String.Join("' ; '", linksToFollow))
            | None -> ()
            return
                true,
                collected
                |> Array.append (entriesVersions |> List.toArray)
        }
    async {
        let! _, res = followODataLinkSafe Set.empty url
        return res
    }

let tryGetAllVersionsFromNugetODataWithFilter (auth, nugetURL, package:PackageName) =
    let url = sprintf "%s/Packages?semVerLevel=2.0.0&$filter=tolower(Id) eq '%s'" nugetURL (package.CompareString)
    NuGetRequestGetVersions.ofSimpleFunc url (fun _ ->
        async {
            try
                let! result = followODataLink auth url
                return Result.Ok result
            with exn ->
                let cap = ExceptionDispatchInfo.Capture exn
                return Result.Error cap
        })

let tryGetAllVersionsFromNugetODataFindById (auth, nugetURL, package:PackageName) =
    let url = sprintf "%s/FindPackagesById()?semVerLevel=2.0.0&id='%O'" nugetURL package
    NuGetRequestGetVersions.ofSimpleFunc url (fun _ ->
        async {
            try
                let! result = followODataLink auth url
                return Result.Ok  result
            with exn ->
                let cap = ExceptionDispatchInfo.Capture exn
                return Result.Error cap
        })

let tryGetAllVersionsFromNugetODataFindByIdNewestFirst (auth, nugetURL, package:PackageName) =
    let url = sprintf "%s/FindPackagesById()?semVerLevel=2.0.0&id='%O'&$orderby=Published desc" nugetURL package
    NuGetRequestGetVersions.ofSimpleFunc url (fun _ ->
        async {
            try
                let! result = followODataLink auth url
                return Result.Ok result
            with exn ->
                let cap = ExceptionDispatchInfo.Capture exn
                return Result.Error cap
        })

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
                | FSharp.Core.Result.Ok r -> async { return r }
                | FSharp.Core.Result.Error err when
                        String.containsIgnoreCase "myget.org" nugetURL ||
                        String.containsIgnoreCase "nuget.org" nugetURL ||
                        String.containsIgnoreCase "visualstudio.com" nugetURL ->
                    raise <|
                        System.Exception(
                            sprintf "Could not get package details for %O from %s" packageName nugetURL,
                            err.SourceException)
                | FSharp.Core.Result.Error err ->
                    try
                        let url = sprintf "%s/odata/Packages(Id='%O',Version='%O')" nugetURL packageName version
                        getXmlFromUrl(auth,url)
                    with e ->
                        raise <| System.AggregateException(err.SourceException, e)

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
        with e ->
            traceWarnfn "Failed to get package details '%s'. This feeds implementation might be broken." e.Message
            if verbose then tracefn "Details: %O" e
            try
                return! queryPackagesProtocol packageName
            with e ->
                traceWarnfn "Failed to get package details (again) '%s'. This feeds implementation might be broken." e.Message
                if verbose then tracefn "Details: %O" e
                // try uppercase version as workaround for https://github.com/fsprojects/Paket/issues/2145 - Bad!
                let name = packageName.ToString()
                let uppercase = packageName.ToString().[0].ToString().ToUpper() + name.Substring(1)
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
    let url = sprintf "%s/Packages()?$filter=IsLatestVersion and IsAbsoluteLatestVersion and substringof('%s',tolower(Id))" nugetURL ((packageNamePrefix:string).ToLowerInvariant())
    async {
        try
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
                    |> FSharp.Core.Result.Ok
                | _ ->  [||] |> FSharp.Core.Result.Ok
        with e ->
            return FSharp.Core.Result.Error (ExceptionDispatchInfo.Capture e)
    }


