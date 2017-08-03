/// Contains NuGet support.
module Paket.NuGetV3

open Newtonsoft.Json
open System.IO
open Pri.LongPath
open System.Collections.Generic

open Paket.Domain
open Paket.NuGetCache
open Paket.Utils
open Paket.Xml
open Paket.PackageSources
open Paket.Requirements
open Paket.Logging

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
                let! v3res = PackageSources.getNuGetV3Resource source AutoComplete |> Async.Catch
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
                let! v3res = PackageSources.getNuGetV3Resource source AllVersionsAPI |> Async.Catch
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
        let! response = safeGetFromUrl(auth |> Option.map toBasicAuth,query,acceptJson)
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

type Registration = 
    { [<JsonProperty("catalogEntry")>]
      CatalogEntry : string
      
      [<JsonProperty("packageContent")>]
      PackageContent : string }

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
      
      [<JsonProperty("dependencyGroups")>]
      DependencyGroups : CatalogDependencyGroup [] }

let getRegistration (source : NugetV3Source) (packageName:PackageName) (version:SemVerInfo) =
    async {
        let! registrationUrl = PackageSources.getNuGetV3Resource source Registration
        let url = sprintf "%s%s/%s.json" registrationUrl (packageName.ToString().ToLower()) (version.Normalize())
        let! rawData = safeGetFromUrl (source.Authentication |> Option.map toBasicAuth, url, acceptJson)
        return
            match rawData with
            | NotFound -> None //raise <| System.Exception(sprintf "could not get registration data (404) from '%s'" url)
            | UnknownError err ->
                raise <| System.Exception(sprintf "could not get registration data from %s" url, err.SourceException)
            | SuccessResponse x -> Some (JsonConvert.DeserializeObject<Registration>(x))
    }

let getCatalog url auth =
    async {
        let! rawData = safeGetFromUrl (auth, url, acceptJson)
        return
            match rawData with
            | NotFound ->
                raise <| System.Exception(sprintf "could not get catalog data (404) from '%s'" url)
            | UnknownError err ->
                raise <| System.Exception(sprintf "could not get catalog data from %s" url, err.SourceException)
            | SuccessResponse x -> JsonConvert.DeserializeObject<Catalog>(x)
    }

let getPackageDetails (source:NugetV3Source) (packageName:PackageName) (version:SemVerInfo) : Async<ODataSearchResult> =
    async {
        let! registrationData = getRegistration source packageName version
        match registrationData with
        | None -> return EmptyResult
        | Some registrationData ->
        let! catalogData = getCatalog registrationData.CatalogEntry (source.Authentication |> Option.map toBasicAuth)

        let dependencies = 
            if catalogData.DependencyGroups = null then
                []
            else
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
                        | null -> FrameworkRestriction.NoRestriction
                        | x -> Requirements.parseRestrictionsLegacy false x
                    (PackageName dep.Id), (VersionRequirement.Parse dep.Range), targetFramework)
                |> Seq.toList
        let unlisted =
            if catalogData.Listed.HasValue then
               not catalogData.Listed.Value 
            else
                false
        // TODO: We probably need our new restriction logic here because I guess what nuget gives us is not enough...
        let optimized = 
            dependencies |> List.map (fun (m,v,r) -> m,v, ExplicitRestriction r)
        return 
            { SerializedDependencies = []
              PackageName = packageName.ToString()
              SourceUrl = source.Url
              Unlisted = unlisted
              DownloadUrl = registrationData.PackageContent
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
            with _ -> 
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
