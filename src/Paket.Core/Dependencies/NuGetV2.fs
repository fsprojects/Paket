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
    let url = sprintf "%s/Packages?semVerLevel=2.0.0&$filter=Id eq '%O'" nugetURL package
    NuGetRequestGetVersions.ofSimpleFunc url (fun _ ->
        async {
            try
                let! result = followODataLink auth url
                return SuccessResponse result
            with _ ->
                let url = sprintf "%s/Packages?semVerLevel=2.0.0&$filter=tolower(Id) eq '%s'" nugetURL (package.CompareString)
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
    | e -> raise <| Exception(sprintf "Could not parse response from %s as OData.%sData:%s%s" url Environment.NewLine Environment.NewLine raw, e)
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


let getDetailsFromNuGetViaODataFast auth nugetURL (packageName:PackageName) (version:SemVerInfo) =
    async {
        let normalizedVersion = version.Normalize()
        let fallback6 () =
            async {
                let url = sprintf "%s/Packages(Id='%s',Version='%O')" nugetURL (packageName.CompareString) normalizedVersion
                let! raw = getFromUrl(auth,url,acceptXml)
                if verbose then
                    tracefn "Response from %s:" url
                    tracefn ""
                    tracefn "%s" raw
                let doc = getXmlDoc url raw
                return parseODataEntryDetails(url,nugetURL,packageName,version,doc) |> ODataSearchResult.Match
            }

        let fallback5 () =
            async {
                let url = sprintf "%s/Packages(Id='%s',Version='%O')" nugetURL (packageName.CompareString) version
                let! raw = getFromUrl(auth,url,acceptXml)
                if verbose then
                    tracefn "Response from %s:" url
                    tracefn ""
                    tracefn "%s" raw
                let doc = getXmlDoc url raw
                match parseODataEntryDetails(url,nugetURL,packageName,version,doc) |> ODataSearchResult.Match with
                | EmptyResult ->
                    if verbose then tracefn "No results, trying again with direct detail access and normalizedVersion."
                    return! fallback6()
                | res -> return res
            }

        let fallback4 () =
            async {
                let url = sprintf "%s/Packages?$filter=(tolower(Id) eq '%s') and (Version eq '%O')" nugetURL (packageName.CompareString) normalizedVersion
                let! raw = getFromUrl(auth,url,acceptXml)
                if verbose then
                    tracefn "Response from %s:" url
                    tracefn ""
                    tracefn "%s" raw
                let doc = getXmlDoc url raw
                match parseODataListDetails(url,nugetURL,packageName,version,doc) with
                | EmptyResult ->
                    if verbose then tracefn "No results, trying again with direct detail access."
                    return! fallback5()
                | res -> return res
            }

        let fallback3 () =
            async {
                try
                    let url = sprintf "%s/Packages?$filter=(tolower(Id) eq '%s') and (Version eq '%O')" nugetURL (packageName.CompareString) version
                    let! raw = getFromUrl(auth,url,acceptXml)
                    if verbose then
                        tracefn "Response from %s:" url
                        tracefn ""
                        tracefn "%s" raw
                    let doc = getXmlDoc url raw
                    match parseODataListDetails(url,nugetURL,packageName,version,doc) with
                    | EmptyResult ->
                        if verbose then tracefn "No results, trying again with NormalizedVersion as Version."
                        return! fallback4()
                    | res -> return res
                with ex ->
                    return! fallback4()
            }

        let fallback2 () =
            async {
                try
                    let url = sprintf "%s/Packages?$filter=(tolower(Id) eq '%s') and (NormalizedVersion eq '%O')" nugetURL (packageName.CompareString) version
                    let! raw = getFromUrl(auth,url,acceptXml)
                    if verbose then
                        tracefn "Response from %s:" url
                        tracefn ""
                        tracefn "%s" raw
                    let doc = getXmlDoc url raw
                    match parseODataListDetails(url,nugetURL,packageName,version,doc) with
                    | EmptyResult ->
                        if verbose then tracefn "No results, trying again with Version instead of NormalizedVersion."
                        return! fallback3()
                    | res -> return res
                with ex ->
                    return! fallback3()
            }
       
        let fallback () =
            async {
                try
                    let url = sprintf "%s/Packages?$filter=(tolower(Id) eq '%s') and (NormalizedVersion eq '%O')" nugetURL (packageName.CompareString) normalizedVersion
                    let! raw = getFromUrl(auth,url,acceptXml)
                    if verbose then
                        tracefn "Response from %s:" url
                        tracefn ""
                        tracefn "%s" raw
                    let doc = getXmlDoc url raw
                    match parseODataListDetails(url,nugetURL,packageName,version,doc) with
                    | EmptyResult ->
                        if verbose then tracefn "No results, trying again with Version as NormalizedVersion."
                        return! fallback2()
                    | res -> return res
                with ex ->
                    return! fallback2()
            }

        let firstUrl = sprintf "%s/Packages?$filter=(Id eq '%s') and (NormalizedVersion eq '%s')" nugetURL (packageName.ToString()) normalizedVersion
        try
            let! raw = getFromUrl(auth,firstUrl,acceptXml)
            if verbose then
                tracefn "Response from %s:" firstUrl
                tracefn ""
                tracefn "%s" raw
            let doc = getXmlDoc firstUrl raw
            match parseODataListDetails(firstUrl,nugetURL,packageName,version,doc) with
            | EmptyResult ->
                if verbose then tracefn "No results, trying again with case-insensitive version."
                return! fallback()
            | res -> return res
        with ex ->
            // TODO: Remove this 'with' eventually when this warning is no longer reported
            traceWarnfn "Failed to getDetailsFromNuGetViaODataFast '%s'. Trying with Version instead of NormalizedVersion (Please report this warning!): %O" firstUrl ex
            return! fallback()
    }


/// Gets package details from NuGet via OData
let getDetailsFromNuGetViaOData auth nugetURL (packageName:PackageName) (version:SemVerInfo) =
    let queryPackagesProtocol (packageName:PackageName) =
        async {
            let url = sprintf "%s/Packages(Id='%O',Version='%O')" nugetURL packageName version
            let! response = safeGetFromUrl(auth,url,acceptXml)

            let! raw =
                match response with
                | SafeWebResult.SuccessResponse r -> async { return Some r }
                | SafeWebResult.NotFound -> async { return None }
                | SafeWebResult.UnknownError err when
                        urlIsMyGet nugetURL ||
                        urlIsNugetGallery nugetURL ||
                        urlSimilarToTfsOrVsts nugetURL ->
                    raise <|
                        System.Exception(
                            sprintf "Could not get package details for %O from %s" packageName nugetURL,
                            err.SourceException)
                | SafeWebResult.UnknownError err ->
                    traceWarnfn "Failed to find defails '%s' from '%s'. trying again with /odata/Packages. Please report this." err.SourceException.Message url
                    if verbose then
                        tracefn "Details of last error (%s): %O" url err.SourceException
                    async {
                        try
                            let url = sprintf "%s/odata/Packages(Id='%O',Version='%O')" nugetURL packageName version
                            let! raw = getXmlFromUrl(auth,url)
                            return Some raw
                        with e ->
                            return raise <| System.AggregateException(err.SourceException, e)
                    }

            if verbose then
                tracefn "Response from %s:" url
                tracefn ""
                tracefn "%s" (match raw with Some s -> s | _ -> "NOTFOUND 404")
            match raw with
            | Some raw ->
                let doc = getXmlDoc url raw
                return parseODataEntryDetails(url,nugetURL,packageName,version,doc) |> ODataSearchResult.Match
            | None -> return ODataSearchResult.EmptyResult }

    async {
        try
            let! result =
                // See https://github.com/fsprojects/Paket/issues/2213
                // TODO: There is a bug in VSTS, so we can't trust this protocol. Remove when VSTS is fixed
                // TODO: TFS has the same bug
                if urlSimilarToTfsOrVsts nugetURL then queryPackagesProtocol packageName
                else getDetailsFromNuGetViaODataFast auth nugetURL packageName version
            return result
        with e when not (urlSimilarToTfsOrVsts nugetURL) ->
            traceWarnfn "Failed to get package details '%s'. This feeds implementation might be broken." e.Message
            if verbose then tracefn "Details: %O" e
            return! queryPackagesProtocol packageName
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


