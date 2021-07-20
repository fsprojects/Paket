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
    let rec followODataLinkSafe (knownVersions:Set<_>) (url:string) =
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
                |> List.choose (fun a ->
                    match getAttribute "href" a with
                    | Some data ->
                        let newUrl = Uri.UnescapeDataString data
                        if newUrl <> url then Some newUrl else None
                    | _ -> None )

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
                let mutable uri = null // warn once per specific API endpoint, but try to cut the query
                let baseUrl = if Uri.TryCreate(url, UriKind.Absolute, &uri) then uri.AbsolutePath else url
                traceWarnIfNotBefore baseUrl
                    "At least one 'next' link (index %d) returned a empty result (noticed on '%O'): ['%s']" 
                    i url (System.String.Join("' ; '", linksToFollow))
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

let private tryGetAllVersionsFromNugetODataWithFilterWarnings = System.Collections.Concurrent.ConcurrentDictionary<_,_>()

let tryGetAllVersionsFromNugetODataWithFilter (auth, nugetURL, package:PackageName) =
    let url = sprintf "%s/Packages?semVerLevel=2.0.0&$filter=Id eq '%O'" nugetURL package
    NuGetRequestGetVersions.ofSimpleFunc url (fun _ ->
        async {
            try
                let! result = followODataLink auth url
                return SuccessResponse result
            with exn ->
                match tryGetAllVersionsFromNugetODataWithFilterWarnings.TryGetValue nugetURL with
                | true, true -> ()
                | _, _ ->
                    traceWarnfn "Possible Performance degradation, could not retrieve '%s', ignoring further warnings for this source" url
                    tryGetAllVersionsFromNugetODataWithFilterWarnings.TryAdd(nugetURL, true) |> ignore
                if verbose then
                    printfn "Error while retrieving data from '%s': %O" url exn
                let url = sprintf "%s/Packages?semVerLevel=2.0.0&$filter=tolower(Id) eq '%s'" nugetURL package.CompareString
                try
                    let! result = followODataLink auth url
                    return SuccessResponse result
                with exn ->
                    let cap = ExceptionDispatchInfo.Capture exn
                    return UnknownError cap
        })


let tryGetAllVersionsFromNugetODataFindById (auth, nugetURL, package:PackageName) =
    let url = sprintf "%s/FindPackagesById()?semVerLevel=2.0.0&id='%O'" nugetURL package
    NuGetRequestGetVersions.ofSimpleFunc url (fun _ ->
        async {
            try
                let! result = followODataLink auth url
                return SuccessResponse result
            with exn ->
                let cap = ExceptionDispatchInfo.Capture exn
                return UnknownError cap
        })

let tryGetAllVersionsFromNugetODataFindByIdNewestFirst (auth, nugetURL, package:PackageName) =
    let url = sprintf "%s/FindPackagesById()?semVerLevel=2.0.0&id='%O'&$orderby=Published desc" nugetURL package
    NuGetRequestGetVersions.ofSimpleFunc url (fun _ ->
        async {
            try
                let! result = followODataLink auth url
                return SuccessResponse result
            with exn ->
                let cap = ExceptionDispatchInfo.Capture exn
                return UnknownError cap
        })

let private getXmlDoc url raw =
    let doc = XmlDocument()
    try
        doc.LoadXml raw
    with
    | e -> raise (Exception(sprintf "Could not parse response from %s as OData.%sData:%s%s" url Environment.NewLine Environment.NewLine raw, e))
    doc

let private handleODataEntry nugetURL packageName version entry =
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
                match PlatformMatching.extractPlatforms false restriction with
                | Some p ->
                    Some p
                | None ->
                    if not (restriction.StartsWith "_") then 
                        Logging.traceWarnIfNotBefore ("Package", restriction, packageName, version) "Could not detect any platforms from '%s' in package %O %O, please tell the package authors" restriction packageName version
                    None
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

    let dependencies, warnings = addFrameworkRestrictionsToDependencies cleanedPackages frameworks

    for warning in warnings do
        let message = warning.Format officialName version
        Logging.traceWarnIfNotBefore message "%s" message

    { PackageName = officialName
      DownloadUrl = downloadLink
      SerializedDependencies = []
      SourceUrl = nugetURL
      CacheVersion = NuGetPackageCache.CurrentCacheVersion
      LicenseUrl = licenseUrl
      Version = (SemVer.Parse v).Normalize()
      Unlisted = publishDate = Constants.MagicUnlistingDate }
        .WithDependencies dependencies

// parse search results.
let parseODataListDetails (url,nugetURL,packageName:PackageName,version:SemVerInfo,doc) : ODataSearchResult =
    let feedNode =
        match doc |> getNode "feed" with
        | Some node -> node
        | None ->
            failwithf "Could not find 'entry' node for package %O %O" packageName version
    match feedNode |> getNodes "entry" with
    | [] ->
        // When no entry node is found our search did not yield anything.
        EmptyResult
    | entry :: t ->
        if t.Length > 0 then
            traceWarnfn "Got multiple results in OData query ('%s') for package %O %O, the feed might be broken" url packageName version
        handleODataEntry nugetURL packageName version entry
        |> ODataSearchResult.Match

let parseODataEntryDetails (url,nugetURL,packageName:PackageName,version:SemVerInfo,doc) =
    match doc |> getNode "entry" with
    | Some entry ->
        handleODataEntry nugetURL packageName version entry
    | None ->
        // allow feed node, see https://github.com/fsprojects/Paket/issues/2539
        // TODO: traceWarnfn "Consider updating your NuGet server, see https://github.com/fsprojects/Paket/issues/2539"
        match parseODataListDetails (url,nugetURL,packageName,version,doc) with
        | EmptyResult ->
            failwithf "Could not find 'entry' node for package %O %O" packageName version
        | ODataSearchResult.Match entry -> entry


let getDetailsFromNuGetViaODataFast isVersionAssumed nugetSource (packageName:PackageName) (version:SemVerInfo) =
    let doBlacklist = not isVersionAssumed
    async {
        let normalizedVersion = version.Normalize()
        let urls =
            [ // NuGet feeds should support this.
              // By ID needs to be first because TFS/VSTS https://github.com/fsprojects/Paket/issues/2213
              UrlToTry.From
                (UrlId.GetVersion_ById { LoweredPackageId = true; NormalizedVersion = false })
                "1_%s/Packages(Id='%s',Version='%O')"
                nugetSource.Url
                packageName.CompareString
                version
              // DevExpress needs normalized versions? https://github.com/fsprojects/Paket/issues/2599
              UrlToTry.From
                (UrlId.GetVersion_ById { LoweredPackageId = true; NormalizedVersion = true })
                "1_%s/Packages(Id='%s',Version='%O')"
                nugetSource.Url
                packageName.CompareString
                normalizedVersion
              // Use original casing.
              UrlToTry.From
                (UrlId.GetVersion_ById { LoweredPackageId = false; NormalizedVersion = true })
                "1_%s/Packages(Id='%s',Version='%O')"
                nugetSource.Url
                (packageName.ToString())
                normalizedVersion
              UrlToTry.From
                (UrlId.GetVersion_ById { LoweredPackageId = false; NormalizedVersion = false })
                "1_%s/Packages(Id='%s',Version='%O')"
                nugetSource.Url
                (packageName.ToString())
                version
              // We couldn't find by ID, try to search via filter.
              // Start without toLower because of ProGet performance https://github.com/fsprojects/Paket/issues/2466
              UrlToTry.From
                (UrlId.GetVersion_Filter
                    ({ LoweredPackageId = false; NormalizedVersion = true },
                     { ToLower = false; NormalizedVersion = true }))
                "2_%s/Packages?$filter=(Id eq '%s') and (NormalizedVersion eq '%s')"
                nugetSource.Url
                (packageName.ToString())
                normalizedVersion
              // Try to find with all normalized
              UrlToTry.From
                (UrlId.GetVersion_Filter
                    ({ LoweredPackageId = true; NormalizedVersion = true },
                     { ToLower = true; NormalizedVersion = true }))
                "2_%s/Packages?$filter=(tolower(Id) eq '%s') and (NormalizedVersion eq '%s')"
                nugetSource.Url
                packageName.CompareString
                normalizedVersion
              // SonarType does not support NormalizedVersion, see https://issues.sonatype.org/browse/NEXUS-6159
              // and https://github.com/fsprojects/Paket/issues/2320
              UrlToTry.From
                (UrlId.GetVersion_Filter({ LoweredPackageId = false; NormalizedVersion = true }, { ToLower = false; NormalizedVersion = false }))
                "2_%s/Packages?$filter=(Id eq '%s') and (Version eq '%s')"
                nugetSource.Url
                (packageName.ToString())
                normalizedVersion
              // Not sure
              UrlToTry.From
                (UrlId.GetVersion_Filter({ LoweredPackageId = true; NormalizedVersion = false }, { ToLower = true; NormalizedVersion = false }))
                "2_%s/Packages?$filter=(tolower(Id) eq '%s') and (Version eq '%O')"
                nugetSource.Url
                packageName.CompareString
                version
              // Not sure
              UrlToTry.From
                (UrlId.GetVersion_Filter({ LoweredPackageId = true; NormalizedVersion = true }, { ToLower = true; NormalizedVersion = false }))
                "2_%s/Packages?$filter=(tolower(Id) eq '%s') and (Version eq '%O')"
                nugetSource.Url
                packageName.CompareString
                normalizedVersion
            ]
        let handleEntryUrl url =
            async {
                try
                    let! raw = getFromUrl(nugetSource.Authentication,url,acceptXml)
                    if verbose then
                        tracefn "Response from %s:" url
                        tracefn ""
                        tracefn "%s" raw
                    let doc = getXmlDoc url raw
                    let res = parseODataEntryDetails(url,nugetSource.Url,packageName,version,doc)
                    return Choice1Of2 (res |> ODataSearchResult.Match)
                with ex ->
                    return Choice2Of2 ex
            }
        let handleListUrl url =
            async {
                try
                    let! raw = getFromUrl(nugetSource.Authentication,url,acceptXml)
                    if verbose then
                        tracefn "Response from %s:" url
                        tracefn ""
                        tracefn "%s" raw
                    let doc = getXmlDoc url raw
                    match parseODataListDetails(url,nugetSource.Url,packageName,version,doc) with
                    | EmptyResult ->
                        return Choice2Of2 (exn "Empty response is not trusted")
                    | res -> return Choice1Of2 res
                with ex ->
                    return Choice2Of2 ex
            }
        let handleUrl (url:string) =
            let realUrl = url.Substring(2)
            if url.StartsWith "1_"
            then handleEntryUrl realUrl
            else handleListUrl realUrl
        let tryAgain c =
            match c with
            | Choice1Of2 _ -> false
            | _ -> true

        let! result = NuGetCache.tryAndBlacklistUrl doBlacklist true nugetSource tryAgain handleUrl urls
        match result with
        | Choice1Of2 res -> return res
        | Choice2Of2 ex -> return raise (exn("error", ex))
    }

// parse search results.
let parseFindPackagesByIDODataListDetails (url,nugetURL,packageName:PackageName,version:SemVerInfo,doc) : ODataSearchResult =
    let feedNode =
        match doc |> getNode "feed" with
        | Some node -> node
        | None ->
            failwithf "Could not find 'entry' node for package %O %O" packageName version

    let getVersion (n:XmlNode) =
        match n |> getNode "properties" |> optGetNode "Version" with
        | Some v -> Some(SemVer.Parse(v.InnerText).Normalize())
        | None -> None

    let v = version.Normalize()

    match feedNode |> getNodes "entry" |> List.tryFind (fun n -> getVersion n = Some v) with
    | None ->
        // When no entry node is found our search did not yield anything.
        EmptyResult
    | Some entry ->
        handleODataEntry nugetURL packageName version entry
        |> ODataSearchResult.Match

let rec parseFindPackagesByIDODataEntryDetails (url,nugetSource:NuGetSource,packageName:PackageName,version:SemVerInfo) = async {
    let! raw = getFromUrl(nugetSource.Authentication,url,acceptXml)
    if verbose then
        tracefn "Response from %s:" url
        tracefn ""
        tracefn "%s" raw
    let doc = getXmlDoc url raw

    match parseFindPackagesByIDODataListDetails (url,nugetSource.Url,packageName,version,doc) with
    | EmptyResult ->
        let linksToFollow =
             doc
             |> getNodes "link"
             |> List.filter (fun node -> node |> getAttribute "rel" = Some "next")
             |> List.choose (fun a ->
                 match getAttribute "href" a with
                 | Some data ->
                     let newUrl = Uri.UnescapeDataString data
                     if newUrl <> url then Some newUrl else None
                 | _ -> None)
        let result = ref None

        for link in linksToFollow do
            if !result = None then
                let! r = parseFindPackagesByIDODataEntryDetails (link,nugetSource,packageName,version)
                match r with
                | EmptyResult -> ()
                | r -> result := Some r

        match !result with
        | Some r -> return r
        | _ -> return EmptyResult

    | ODataSearchResult.Match entry -> return ODataSearchResult.Match entry
}

/// Gets package details from NuGet via OData
let getDetailsFromNuGetViaOData isVersionAssumed nugetSource (packageName:PackageName) (version:SemVerInfo) = async {
    try
        return! getDetailsFromNuGetViaODataFast isVersionAssumed nugetSource packageName version
    with
    | _ ->
        let url = sprintf "%s/FindPackagesById()?semVerLevel=2.0.0&id='%O'" nugetSource.Url packageName
        return! parseFindPackagesByIDODataEntryDetails (url,nugetSource,packageName,version)
}

let getDetailsFromNuGet force isVersionAssumed nugetSource packageName version =
    getDetailsFromCacheOr
        force
        nugetSource.Url
        packageName
        version
        (fun () -> getDetailsFromNuGetViaOData isVersionAssumed nugetSource packageName version)

/// Uses the NuGet v2 API to retrieve all packages with the given prefix.
let FindPackages(auth, nugetURL, packageNamePrefix, maxResults) =
    let url = sprintf "%s/Packages()?$filter=IsLatestVersion and IsAbsoluteLatestVersion and substringof('%s',tolower(Id))" nugetURL ((packageNamePrefix:string).ToLowerInvariant())
    async {
        try
            let! raw = getFromUrl(auth,url,acceptXml)
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


