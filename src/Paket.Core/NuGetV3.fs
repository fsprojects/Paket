/// Contains NuGet support.
module Paket.NuGetV3

open Newtonsoft.Json
open System.IO
open System.Collections.Generic

open Paket.Domain
open Paket.NuGet
open Paket.Utils
open Paket.Xml
open Paket.PackageSources
open Paket.Requirements

/// [omit]
type JSONResource = 
    { Type : string;
      ID: string }

/// [omit]
type JSONVersionData = 
    { Data : string [] }

/// [omit]
type JSONRootData = 
    { Resources : JSONResource [] }

/// [omit]
let getSearchAutocompleteService (data : string) =
    JsonConvert.DeserializeObject<JSONRootData>(data.Replace("@id","ID").Replace("@type","Type")).Resources
    |> Array.tryFind (fun x -> (isNull x.Type |> not) && x.Type.ToLower() = "searchautocompleteservice")
    |> Option.map (fun x -> x.ID)

/// [omit]
let private searchDict = new System.Collections.Concurrent.ConcurrentDictionary<_,_>()

/// Calculates the NuGet v3 URL from a NuGet v2 URL.
let calculateNuGet3Path(nugetUrl:string) = 
    match nugetUrl.TrimEnd([|'/'|]) with
    | "http://nuget.org/api/v2" -> Some "http://api.nuget.org/v3/index.json"
    | "https://nuget.org/api/v2" -> Some "https://api.nuget.org/v3/index.json"
    | "http://www.nuget.org/api/v2" -> Some "http://api.nuget.org/v3/index.json"
    | "https://www.nuget.org/api/v2" -> Some "https://api.nuget.org/v3/index.json"
    | url when url.EndsWith("api/v2") && url.Contains("myget.org") -> Some (url.Replace("api/v2","api/v3/index.json"))
    | url when url.EndsWith("api/v3/index.json") -> Some url
    | _ -> None

/// [omit]
let getSearchAPI(auth,nugetUrl) = 
    match searchDict.TryGetValue nugetUrl with
    | true,v -> v
    | _ ->
        let result = 
            match calculateNuGet3Path nugetUrl with
            | None -> None
            | Some v3Path ->
                let serviceData =
                    safeGetFromUrl(auth,v3Path,acceptJson)
                    |> Async.RunSynchronously

                match serviceData with
                | None -> None
                | Some data -> getSearchAutocompleteService data

        searchDict.[nugetUrl] <- result
        result

/// [omit]
let extractVersions(response:string) =
    JsonConvert.DeserializeObject<JSONVersionData>(response).Data

let internal findVersionsForPackage(v3Url, auth, packageName:Domain.PackageName, includingPrereleases, maxResults) =
    async {
        let! response = safeGetFromUrl(auth,sprintf "%s?id=%O&take=%d%s" v3Url packageName (max maxResults 100000) (if includingPrereleases then "&prerelease=true" else ""), acceptXml) // Nuget is showing old versions first
        match response with
        | Some text ->
            let versions =
                let extracted = extractVersions text
                if extracted.Length > maxResults then
                    extracted |> Seq.take maxResults |> Seq.toArray
                else
                    extracted

            return Some(SemVer.SortVersions versions)
        | None -> return None
    }

/// Uses the NuGet v3 autocomplete service to retrieve all package versions for the given package.
let FindVersionsForPackage(auth, nugetURL, package, includingPrereleases, maxResults) =
    async {
        let! raw = findVersionsForPackage(auth, nugetURL, package, includingPrereleases, maxResults)
        match raw with 
        | Some versions -> return versions
        | None -> return [||]
    }

/// [omit]
let extractPackages(response:string) =
    JsonConvert.DeserializeObject<JSONVersionData>(response).Data

let private getPackages(auth, nugetURL, packageNamePrefix, maxResults) = async {
    match getSearchAPI(auth,nugetURL) with
    | Some url -> 
        let query = sprintf "%s?q=%s&take=%d" url packageNamePrefix maxResults
        let! response = safeGetFromUrl(auth,query,acceptJson)
        match response with
        | Some text -> return extractPackages text
        | None -> return [||]
    | None -> return [||]
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
        let! registrationUrl = PackageSources.getNugetV3Resource source Registration
        let url = sprintf "%s%s/%s.json" registrationUrl (packageName.ToString().ToLower()) version.AsString
        let! rawData = safeGetFromUrl (source.Authentication |> Option.map toBasicAuth, url, acceptJson)
        return
            match rawData with
            | None -> failwithf "could not get registration data from %s" url
            | Some x -> JsonConvert.DeserializeObject<Registration>(x)
    }

let getCatalog url auth =
    async {
        let! rawData = safeGetFromUrl (auth, url, acceptJson)
        return
            match rawData with
            | None -> failwithf "could not get catalog data from %s" url
            | Some x -> JsonConvert.DeserializeObject<Catalog>(x)
    }

let getPackageDetails (source:NugetV3Source) (packageName:PackageName) (version:SemVerInfo) : Async<NuGet.NugetPackageCache> =
    async {
        let! registrationData = getRegistration source packageName version
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
                        | null -> []
                        | x -> Requirements.parseRestrictions x
                    (PackageName dep.Id), (VersionRequirement.Parse dep.Range), targetFramework)
                |> Seq.toList
        let unlisted =
            if catalogData.Listed.HasValue then
               not catalogData.Listed.Value 
            else
                false

        let optimized = Requirements.optimizeDependencies dependencies 
        return 
            { Dependencies = optimized
              PackageName = packageName.ToString()
              SourceUrl = source.Url
              Unlisted = unlisted
              DownloadUrl = registrationData.PackageContent
              LicenseUrl = catalogData.LicenseUrl
              CacheVersion = NuGet.NugetPackageCache.CurrentCacheVersion }
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
                let cachedObject = JsonConvert.DeserializeObject<NuGet.NugetPackageCache> json
                if cachedObject.CacheVersion <> NugetPackageCache.CurrentCacheVersion then
                    let! details = getPackageDetails source packageName version
                    return true,details
                else
                    return false,cachedObject
            with _ -> 
                let! details = getPackageDetails source packageName version
                return true,details
        else
            let! details = getPackageDetails source packageName version
            return true,details
    }
    
/// Uses the NuGet v3 registration endpoint to retrieve package details .
let GetPackageDetails (force:bool) (source:NugetV3Source) (packageName:PackageName) (version:SemVerInfo) : Async<NuGet.NugetPackageCache> =
    getDetailsFromCacheOr
        force
        source.Url
        packageName
        version
        (fun () ->
            getPackageDetails source packageName version)