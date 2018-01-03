/// Contains NuGet support.
module Paket.NuGetV3

open Newtonsoft.Json
open System.IO
open System.Collections.Generic

open System
open System.Threading.Tasks
open Paket.Domain
open Paket.NuGetCache
open Paket.Utils
open Paket.Xml
open Paket.PackageSources
open Paket.Requirements
open Paket.Logging
open Paket.PlatformMatching


type NugetV3SourceResourceJSON =
    { [<JsonProperty("@type")>]
      Type : string
      [<JsonProperty("@id")>]
      ID : string }

type NugetV3SourceRootJSON =
    { [<JsonProperty("resources")>]
      Resources : NugetV3SourceResourceJSON [] }

//type NugetV3Source =
//    { Url : string
//      Authentication : NugetSourceAuthentication option }

type NugetV3ResourceType =
    | AutoComplete
    | AllVersionsAPI
    //| Registration
    | PackageIndex

    member this.AsString =
        match this with
        | AutoComplete -> "SearchAutoCompleteService"
        //| Registration -> "RegistrationsBaseUrl"
        | AllVersionsAPI -> "PackageBaseAddress/3.0.0"
        | PackageIndex -> "PackageDisplayMetadataUriTemplate"

// Cache for nuget indices of sources
type ResourceIndex = Map<NugetV3ResourceType,string>
let private nugetV3Resources = System.Collections.Concurrent.ConcurrentDictionary<NugetV3Source,Task<ResourceIndex>>()

let getNuGetV3Resource (source : NugetV3Source) (resourceType : NugetV3ResourceType) : Async<string> =
    let key = source
    let getResourcesRaw () =
        async {
            let basicAuth = source.Authentication |> Option.map toCredentials
            let! rawData = safeGetFromUrl(basicAuth, source.Url, acceptJson)
            let rawData =
                match rawData with
                | NotFound ->
                    raise (new Exception(sprintf "Could not load resources (404) from '%s'" source.Url))
                | UnknownError e ->
                    raise (new Exception(sprintf "Could not load resources from '%s'" source.Url, e.SourceException))
                | SuccessResponse x -> x

            let json = JsonConvert.DeserializeObject<NugetV3SourceRootJSON>(rawData)
            let resources =
                json.Resources
                |> Seq.distinctBy(fun x -> x.Type.ToLower())
                |> Seq.map(fun x -> x.Type.ToLower(), x.ID)
            let map =
                resources
                |> Seq.choose (fun (res, value) ->
                    let resType =
                        match res.ToLower() with
                        | "searchautocompleteservice" -> Some AutoComplete
                        //| "registrationsbaseurl" -> Some Registration
                        | s when s.StartsWith "packagedisplaymetadatauritemplate" -> Some PackageIndex
                        | "packagebaseaddress/3.0.0" -> Some AllVersionsAPI
                        | _ -> None
                    match resType with
                    | None -> None
                    | Some k ->
                        Some (k, value))
                |> Seq.distinctBy fst
                |> Map.ofSeq
            return map
        } |> Async.StartAsTask

    async {
        let t = nugetV3Resources.GetOrAdd(key, (fun _ -> getResourcesRaw()))
        let! res = t |> Async.AwaitTask
        return
            match res.TryFind resourceType with
            | Some s -> s
            | None -> failwithf "could not find an %s endpoint for %s" (resourceType.ToString()) source.Url
    }

/// [omit]
type JSONResource =
    { Type : string;
      ID: string }

/// [omit]
type JSONVersionData =
    { Data : string []
      Versions : string [] }

/// [omit]
type JSONRootData =
    { Resources : JSONResource [] }

/// [omit]
let private searchDict = new System.Collections.Concurrent.ConcurrentDictionary<_,System.Threading.Tasks.Task<_>>()

/// [omit]
let private allVersionsDict = new System.Collections.Concurrent.ConcurrentDictionary<_,System.Threading.Tasks.Task<_>>()

/// Calculates the NuGet v3 URL from a NuGet v2 URL.
let calculateNuGet3Path(nugetUrl:string) =
    match nugetUrl.TrimEnd([|'/'|]) with
    | "http://nuget.org/api/v2" -> Some "http://api.nuget.org/v3/index.json"
    | "https://nuget.org/api/v2" -> Some "https://api.nuget.org/v3/index.json"
    | "http://www.nuget.org/api/v2" -> Some "http://api.nuget.org/v3/index.json"
    | "https://www.nuget.org/api/v2" -> Some "https://api.nuget.org/v3/index.json"
    | url when url.EndsWith("/nuget/v2") && url.Contains("pkgs.visualstudio.com") -> Some (url.Replace("/nuget/v2","/nuget/v3/index.json"))
    | url when url.EndsWith("/nuget/v2") && url.Contains("/_packaging/") -> Some (url.Replace("/nuget/v2","/nuget/v3/index.json"))  // TFS
    | url when url.EndsWith("api/v2") && url.Contains("visualstudio.com") -> Some (url.Replace("api/v2","api/v3/index.json"))
    | url when url.EndsWith("api/v2") && url.Contains("myget.org") -> Some (url.Replace("api/v2","api/v3/index.json"))
    | url when url.EndsWith("v3/index.json") -> Some url
    | _ -> None

/// Calculates the NuGet v3 URL from a NuGet v2 URL.
let calculateNuGet2Path(nugetUrl:string) =
    match nugetUrl.TrimEnd([|'/'|]) with
    | "http://api.nuget.org/v3/index.json" -> Some "http://nuget.org/api/v2"
    | "https://api.nuget.org/v3/index.json" -> Some "https://nuget.org/api/v2"
    | "http://api.nuget.org/v3/index.json" -> Some "http://www.nuget.org/api/v2"
    | "https://api.nuget.org/v3/index.json" -> Some "https://www.nuget.org/api/v2"
    | url when url.EndsWith("/nuget/v3/index.json") -> Some (url.Replace("/nuget/v3/index.json","/nuget/v2"))
    | url when url.EndsWith("/api/v3/index.json") && url.Contains("visualstudio.com") -> Some (url.Replace("/api/v3/index.json",""))
    | url when url.EndsWith("/api/v3/index.json") && url.Contains("myget.org") -> Some (url.Replace("/api/v3/index.json",""))
    | url when url.EndsWith("v2") -> Some url
    | _ -> None


/// [omit]
let getSearchAPI(auth,nugetUrl) =
    searchDict.GetOrAdd(nugetUrl, fun nugetUrl ->
        async {
            match calculateNuGet3Path nugetUrl with
            | None -> return None
            | Some v3Path ->
                let source = { Url = v3Path; Authentication = auth }
                let! v3res = getNuGetV3Resource source AutoComplete |> Async.Catch
                return
                    match v3res with
                    | Choice1Of2 s -> Some s
                    | Choice2Of2 ex ->
                        if verbose then traceWarnfn "getSearchAPI: %s" (ex.ToString())
                        None
        } |> Async.StartAsTask)

/// [omit]
let getAllVersionsAPI(auth,nugetUrl) =
    allVersionsDict.GetOrAdd(nugetUrl, fun nugetUrl ->
        async {
            match calculateNuGet3Path nugetUrl with
            | None -> return None
            | Some v3Path ->
                let source = { Url = v3Path; Authentication = auth }
                let! v3res = getNuGetV3Resource source AllVersionsAPI |> Async.Catch
                return
                    match v3res with
                    | Choice1Of2 s -> Some s
                    | Choice2Of2 ex ->
                        if verbose then traceWarnfn "getAllVersionsAPI: %s" (ex.ToString())
                        None
        } |> Async.StartAsTask)


/// [omit]
let extractAutoCompleteVersions(response:string) =
    JsonConvert.DeserializeObject<JSONVersionData>(response).Data

/// [omit]
let extractVersions(response:string) =
    JsonConvert.DeserializeObject<JSONVersionData>(response).Versions


let internal findAutoCompleteVersionsForPackage(v3Url, auth, packageName:Domain.PackageName, includingPrereleases, maxResults) =
    async {
        let url = sprintf "%s?semVerLevel=2.0.0&id=%O&take=%d%s" v3Url packageName (max maxResults 100000) (if includingPrereleases then "&prerelease=true" else "")

        let! response = safeGetFromUrl(auth,url,acceptJson) // NuGet is showing old versions first
        return
            response
            |> SafeWebResult.map (fun text ->
                let versions =
                    let extracted = extractAutoCompleteVersions text
                    if extracted.Length > maxResults then
                        SemVer.SortVersions extracted |> Array.take maxResults
                    else
                        SemVer.SortVersions extracted
                versions)
    }

/// Uses the NuGet v3 autocomplete service to retrieve all package versions for the given package.
let FindAutoCompleteVersionsForPackage(nugetURL, auth, package, includingPrereleases, maxResults) =
    async {
        let! raw = findAutoCompleteVersionsForPackage(nugetURL, auth, package, includingPrereleases, maxResults)
        return raw
    }


let internal findVersionsForPackage(v3Url, auth, packageName:Domain.PackageName) =
    // Comment from http://api.nuget.org/v3/index.json
    // explicitely says
    // Base URL of Azure storage where NuGet package registration info for NET Core is stored, in the format https://api.nuget.org/v3-flatcontainer/{id-lower}/{id-lower}.{version-lower}.nupkg
    // so I guess we need to take "id-lower" here -> myget actually needs tolower
    let url = sprintf "%s%s/index.json?semVerLevel=2.0.0" v3Url (packageName.CompareString)
    NuGetRequestGetVersions.ofSimpleFunc url (fun _ ->
        async {
            let! response = safeGetFromUrl(auth,url,acceptJson) // NuGet is showing old versions first
            return
                response
                |> SafeWebResult.map (fun text ->
                    let versions = extractVersions text

                    SemVer.SortVersions versions)
        })

/// Uses the NuGet v3 service to retrieve all package versions for the given package.
let FindVersionsForPackage(nugetURL, auth, package) =
    findVersionsForPackage(nugetURL, auth, package)

/// [omit]
let extractPackages(response:string) =
    JsonConvert.DeserializeObject<JSONVersionData>(response).Data

let private getPackages(auth, nugetURL, packageNamePrefix, maxResults) = async {
    let! apiRes = getSearchAPI(auth,nugetURL) |> Async.AwaitTask
    match apiRes with
    | Some url ->
        let query = sprintf "%s?q=%s&take=%d" url packageNamePrefix maxResults
        let! response = safeGetFromUrl(auth |> Option.map toCredentials,query,acceptJson)
        match SafeWebResult.asResult response with
        | Result.Ok text -> return  Result.Ok (extractPackages text)
        | Result.Error err -> return Result.Error err
    | None ->
        if verbose then tracefn "Could not calculate search api from %s" nugetURL
        return Result.Ok [||]
}

/// Uses the NuGet v3 autocomplete service to retrieve all packages with the given prefix.
let FindPackages(auth, nugetURL, packageNamePrefix, maxResults) =
    async {
        return! getPackages(auth, nugetURL, packageNamePrefix, maxResults)
    }


type CatalogDependency =
    { [<JsonProperty("id")>]
      Id : string
      [<JsonProperty("range")>]
      Range : string }
type CatalogDependencyGroup =
    { [<JsonProperty("targetFramework")>]
      TargetFramework : string
      [<JsonProperty("dependencies")>]
      Dependencies : CatalogDependency [] }
type Catalog =
    { [<JsonProperty("licenseUrl")>]
      LicenseUrl : string
      [<JsonProperty("listed")>]
      Listed : System.Nullable<bool>
      [<JsonProperty("version")>]
      Version : string
      [<JsonProperty("dependencyGroups")>]
      DependencyGroups : CatalogDependencyGroup [] }


type PackageIndexPackage =
    { [<JsonProperty("@type")>]
      Type: string
      [<JsonProperty("packageContent")>]
      DownloadLink: string
      [<JsonProperty("catalogEntry")>]
      PackageDetails: Catalog }

type PackageIndexPage =
    { [<JsonProperty("@id")>]
      Id: string
      [<JsonProperty("@type")>]
      Type: string
      [<JsonProperty("items")>]
      Packages: PackageIndexPackage []
      [<JsonProperty("count")>]
      Count: int
      [<JsonProperty("lower")>]
      Lower: string
      [<JsonProperty("upper")>]
      Upper: string }

type PackageIndex =
    { [<JsonProperty("@id")>]
      Id: string
      [<JsonProperty("items")>]
      Pages: PackageIndexPage []
      [<JsonProperty("count")>]
      Count : int }

let private getPackageIndexRaw (source : NugetV3Source) (packageName:PackageName) =
    async {
        let! registrationUrl = getNuGetV3Resource source PackageIndex
        let url = registrationUrl.Replace("{id-lower}", packageName.ToString().ToLower())
        let! rawData = safeGetFromUrl (source.Authentication |> Option.map toCredentials, url, acceptJson)
        return
            match rawData with
            | NotFound -> None
            | UnknownError err ->
                raise (System.Exception(sprintf "could not get registration data from %s" url, err.SourceException))
            | SuccessResponse x -> Some (JsonConvert.DeserializeObject<PackageIndex>(x))
    }

let private getPackageIndexMemoized =
    memoizeAsync (fun (source, packageName) -> getPackageIndexRaw source packageName)
let getPackageIndex source packageName = getPackageIndexMemoized (source, packageName)


let private getPackageIndexPageRaw (source:NugetV3Source) (url:string) =
    async {
        let! rawData = safeGetFromUrl (source.Authentication |> Option.map toCredentials, url, acceptJson)
        return
            match rawData with
            | NotFound -> raise (System.Exception(sprintf "could not get registration data (404) from '%s'" url))
            | UnknownError err ->
                raise (System.Exception(sprintf "could not get registration data from %s" url, err.SourceException))
            | SuccessResponse x -> JsonConvert.DeserializeObject<PackageIndexPage>(x)
    }

let private getPackageIndexPageMemoized =
    memoizeAsync (fun (source, url) -> getPackageIndexPageRaw source url)
let getPackageIndexPage source (page:PackageIndexPage) = getPackageIndexPageMemoized (source, page.Id)


let getRelevantPage (source:NugetV3Source) (index:PackageIndex) (version:SemVerInfo) =
    async {
        let normalizedVersion = SemVer.Parse (version.ToString().ToLowerInvariant())
        let pages =
            index.Pages
            |> Seq.filter (fun p -> SemVer.Parse (p.Lower.ToLowerInvariant()) <= normalizedVersion && normalizedVersion <= SemVer.Parse (p.Upper.ToLowerInvariant()))
            |> Seq.toList

        let tryFindOnPage (page:PackageIndexPage) = async {
            let! page = async {
                if page.Count > 0 && (isNull page.Packages || page.Packages.Length = 0) then
                    return! getPackageIndexPage source page
                else return page }
            if page.Count > 0 && (isNull page.Packages || page.Packages.Length = 0) then
                failwithf "Page '%s' should contain packages!" page.Id

            let packages =
                page.Packages
                    // TODO: This might need to be part of SemVer itself?
                    // This is our favorite package: nlog/5.0.0-beta03-tryoutMutex
                    |> Seq.filter (fun p -> SemVer.Parse (p.PackageDetails.Version.ToLowerInvariant()) = normalizedVersion)
                    |> Seq.toList
            match packages with
            | [ package ] -> return Some package
            | [] -> return None
            | h :: _ ->
                // Can happen in theory when multiple versions differ only in casing...
                traceWarnfn "Multiple package versions matched with '%O' on page '%s'" version page.Id
                return Some h }
        match pages with
        | [ page ] ->
            let! package = tryFindOnPage page
            match package with
            | Some package -> return Some package
            | _ -> return failwithf "Version '%O' should be part of part of page '%s' but wasn't." version page.Id
        | [] ->
            return None
        | multiple ->
            // This can happen theoretically because of ToLower, if someone is really crasy enough to upload a package
            // with differently cased build strings and if nuget makes a page split exactly at that point.
            let mutable result = None
            for page in multiple do
                if result.IsNone then
                    let! package = tryFindOnPage page
                    match package with
                    | Some package -> result <- Some package
                    | None -> ()
            match result with
            | Some result ->
                traceWarnfn "Mulitple pages of V3 index '%s' match with version '%O'" index.Id version
                return Some result
            | None ->
                return failwithf "Mulitple pages of V3 index '%s' match with version '%O'" index.Id version
    }

let getPackageDetails (source:NugetV3Source) (packageName:PackageName) (version:SemVerInfo) : Async<ODataSearchResult> =
    async {
        let! pageIndex = getPackageIndex source packageName// version
        match pageIndex with
        | None -> return EmptyResult
        | Some pageIndex ->
        let! relevantPage = getRelevantPage source pageIndex version
        match relevantPage with
        | None -> return EmptyResult
        | Some relevantPage ->
        let catalogData = relevantPage.PackageDetails
        let dependencyGroups, dependencies =
            if catalogData.DependencyGroups = null then
                [], []
            else
                let detect x =
                    match extractPlatforms false x with
                    | Some p -> p
                    | None ->
                        if not (x.StartsWith "_") then
                            Logging.traceErrorIfNotBefore ("Package", x, packageName, version) "Could not detect any platforms from '%s' in %O %O, please tell the package authors" x packageName version
                        ParsedPlatformPath.Empty
                catalogData.DependencyGroups |> Seq.map (fun group -> detect group.TargetFramework) |> Seq.toList,

                catalogData.DependencyGroups
                |> Seq.map(fun group ->
                    if group.Dependencies = null then
                        Seq.empty
                    else
                        group.Dependencies
                        |> Seq.map(fun dep -> dep, group.TargetFramework))
                |> Seq.concat
                |> Seq.map(fun (dep, targetFramework) ->
                    let targetFramework =
                        match targetFramework with
                        | null -> ParsedPlatformPath.Empty
                        | x -> detect x
                    (PackageName dep.Id), (VersionRequirement.Parse dep.Range), targetFramework)
                |> Seq.toList
        let unlisted =
            if catalogData.Listed.HasValue then
               not catalogData.Listed.Value
            else
                false

        let optimized, warnings =
            addFrameworkRestrictionsToDependencies dependencies dependencyGroups
        for warning in warnings do
            Logging.traceWarnfn "%s" (warning.Format packageName version)

        return
            { SerializedDependencies = []
              PackageName = packageName.ToString()
              SourceUrl = source.Url
              Unlisted = unlisted
              DownloadUrl = relevantPage.DownloadLink
              LicenseUrl = catalogData.LicenseUrl
              Version = version.Normalize()
              CacheVersion = NuGetPackageCache.CurrentCacheVersion }
            |> NuGetPackageCache.withDependencies optimized
            |> ODataSearchResult.Match
    }

let loadFromCacheOrGetDetails (force:bool)
                              (cacheFileName:string)
                              (source:NugetV3Source)
                              (packageName:PackageName)
                              (version:SemVerInfo) =
    async {
        if not force && File.Exists cacheFileName then
            try
                let json = File.ReadAllText(cacheFileName)
                let cachedObject = JsonConvert.DeserializeObject<NuGetPackageCache> json
                if cachedObject.CacheVersion <> NuGetPackageCache.CurrentCacheVersion then
                    let! details = getPackageDetails source packageName version
                    return true,details
                else
                    return false,ODataSearchResult.Match cachedObject
            with exn ->
                if verboseWarnings then
                    eprintfn "Possible Performance degradation, could not retrieve '%O' from cache: %O" packageName exn
                else
                    eprintfn "Possible Performance degradation, could not retrieve '%O' from cache: %s" packageName exn.Message
                let! details = getPackageDetails source packageName version
                return true,details
        else
            let! details = getPackageDetails source packageName version
            return true,details
    }

/// Uses the NuGet v3 registration endpoint to retrieve package details .
let GetPackageDetails (force:bool) (source:NugetV3Source) (packageName:PackageName) (version:SemVerInfo) : Async<ODataSearchResult> =
    getDetailsFromCacheOr
        force
        source.Url
        packageName
        version
        (fun () ->
            getPackageDetails source packageName version)
