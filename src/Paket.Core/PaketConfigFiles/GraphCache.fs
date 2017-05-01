/// Contains logic for saving and loading the graph cache.
module Paket.GraphCache

open Paket.Domain
open PackageResolver
open System.Collections.Generic
open System.Collections.Concurrent
open Newtonsoft.Json.Linq
open System


// Once this works properly I'd like to enable this by default.
let graphCacheFile = System.Environment.GetEnvironmentVariable("PAKET_GRAPH_CACHE")
let preventOnlineRead = System.Environment.GetEnvironmentVariable("PAKET_GRAPH_CACHE_PREVENT_ONLINE") = "true"
let isGraphCacheEnabled = not <| System.String.IsNullOrWhiteSpace(graphCacheFile)

type NuGetSourceUri = string
type RuntimeGraphCache =
  | NotJetCached
  | CachedData of RuntimeGraph option
module RuntimeGraphCache =
    let getData = function NotJetCached -> None | CachedData s -> Some s
    let simplify x = Option.defaultValue NotJetCached x
    let ofOption x =
        match x with
        | None -> NotJetCached
        | Some d -> CachedData d
type CachedInfo =
  { // Versions list must time-out somehow, maybe we even ignore that later - But we use it to make tests faster ...
    VersionListRetrieved : System.DateTime
    Details : Map<SemVerInfo, PackageDetails option * RuntimeGraphCache>
  }
  static member FromVersions versions =
    { VersionListRetrieved = System.DateTime.Now
      Details = versions |> Seq.map (fun v -> v, (None, NotJetCached)) |> Map.ofSeq }
  static member FromPackageDetails (ver:SemVerInfo) (details:PackageDetails) =
    { VersionListRetrieved = System.DateTime()
      Details = [ ver, (Some details, NotJetCached) ] |> Map.ofSeq }
  static member FromRuntimeGraph (ver:SemVerInfo) (graph:RuntimeGraph option) =
    { VersionListRetrieved = System.DateTime()
      Details = [ ver, (None, CachedData graph) ] |> Map.ofSeq }
module CachedInfo =
  let updateVersions versions x =
    let emptyMap = versions |> Seq.map (fun v -> v, (None, NotJetCached)) |> Map.ofSeq
    { x with
        VersionListRetrieved = System.DateTime.Now
        Details = Map.merge (fun _ right -> right) emptyMap x.Details }
  let updatePackageDetails ver packageDetail (x:CachedInfo) =
    let runtimeInfo = x.Details |> Map.tryFind ver |> Option.map snd |> RuntimeGraphCache.simplify
    { x with
        VersionListRetrieved = System.DateTime.Now
        Details = x.Details |> Map.add ver (Some packageDetail, runtimeInfo) }
  let updateRuntimeInfo ver runtimeInfo (x:CachedInfo) =
    let packageDetail = x.Details |> Map.tryFind ver |> Option.bind fst
    { x with
        VersionListRetrieved = System.DateTime.Now
        Details = x.Details |> Map.add ver (packageDetail, CachedData runtimeInfo) }
(*
{
    "http://nuget.org": {
        // Maybe sometime...
        "SourceType": "Local", // Or "V2" or "V3"
        "Packages": [
            {
                "PackageName": "<PackageName>",
                "VersionsRetrieved": "<datetIme>"
                "Versions": [
                {
                    "Version": "1.0.0",
                    "PackageDetails": {
                        "Name": "",
                        "DownloadLink": "",
                        "LicenseUrl": "",
                        "IsUnlisted": true,
                        "Dependencies":
                            [
                                { "PackageName": "", "VersionRequirement": "", "FrameworkRestrictions": "" },
                                { "PackageName": "", "VersionRequirement": "", "FrameworkRestrictions": "" }
                            ]
                    },
                    "RuntimeGraphData": {
                        "supports": {
                            "uwp.10.0.app": {
                              "uap10.0": [
                                "win10-x86",
                                "win10-x86-aot",
                                "win10-x64",
                                "win10-x64-aot",
                                "win10-arm",
                                "win10-arm-aot"
                              ]
                            }
                        },
                        "runtimes": {
                            "win": {
                              "#import": [ "any" ]
                              "Microsoft.Win32.Primitives": {
                                "runtime.win.Microsoft.Win32.Primitives": "4.3.0"
}}}

*)

let inline internal (!>) (x:^a) : ^b = ((^a or ^b) : (static member op_Implicit : ^a -> ^b) x)
let internal writeRuntimeGraph (r:RuntimeGraph option) =
    match r with
    | Some graph ->
        RuntimeGraphParser.writeRuntimeGraphJ graph
    | None ->
        let n = JObject()
        n
let internal writePackageDetails (pack:PackageDetails) =
    let n = JObject()
    n.Add("Name", !>pack.Name.Name)
    n.Add("DownloadLink", !>pack.DownloadLink)
    n.Add("LicenseUrl", !>pack.LicenseUrl)
    n.Add("IsUnlisted", !>pack.Unlisted)
    let deps = JArray()
    pack.DirectDependencies
    |> Seq.iter (fun (packName, versionReq, restriction) ->
        let dep = JObject()
        dep.Add("PackageName", !>packName.Name)
        dep.Add("VersionRequirement", !>versionReq.ToString())
        match Requirements.getRestrictionList restriction with
        | [] -> ()
        | list -> dep.Add("FrameworkRestrictions", !>(System.String.Join(", ",list)))
        deps.Add(dep))
    n.Add("Dependencies", deps)
    n
let internal writePackageVersion version details runtime =
    let n = JObject()
    n.Add("Version", !> version.ToString())
    details |> Option.iter (fun details ->
        n.Add("PackageDetails", writePackageDetails details))
    runtime |> Option.iter (fun runtime ->
        n.Add("RuntimeGraphData", writeRuntimeGraph runtime))
    n

let internal writePackage (name:PackageName) cache =
    let n = JObject()
    let versions = JArray()
    cache.Details
    |> Map.toSeq
    |> Seq.iter (fun (version, (details, runtime)) ->
        writePackageVersion version details (runtime |> RuntimeGraphCache.getData)
        |> fun s -> versions.Add(s))
    n.Add("VersionsRetrieved", !> cache.VersionListRetrieved)
    n.Add("PackageName", !> name.Name)
    n.Add("Versions", versions)
    n
let internal writeSource data =
    let s = JObject()
    let packages = JArray()
    data
    |> Seq.iter (fun ((name:PackageName), cache) ->
        let n = writePackage name cache
        packages.Add(n))
    s.Add("Packages", packages)
    //let sourceType =
    //s.Add("SourceType",  )
    s

let createGraphJson (data : seq<NuGetSourceUri * PackageName * CachedInfo>) : JObject =

    let j = JObject()
    data
    |> Seq.groupBy (fun (fst, _, _) -> fst)
    |> Seq.iter (fun (source, group) ->
        group
        |> Seq.map (fun (_, name, cache) -> name, cache)
        |> writeSource
        |> fun s -> j.Add(source, s))
    j

let internal readPackageDependency (j:JObject) : PackageName * VersionRequirement * Requirements.FrameworkRestrictions =
    let name =
        match j.["PackageName"] with
        | :? JValue as v -> v.ToString()
        | _ -> failwithf "Need Name in PackageDetails element"
        |> PackageName

    let versionRequirement =
        match j.["VersionRequirement"] with
        | :? JValue as v -> v.ToString()
        | _ -> failwithf "Need Name in PackageDetails element"
        |> DependenciesFileParser.parseVersionRequirement

    let frameworkRestrictions =
        match j.["FrameworkRestrictions"] with
        | :? JValue as v -> v.ToString()
        | _ -> ""
        |> fun s -> if System.String.IsNullOrWhiteSpace s then [] else Requirements.parseRestrictions true s
    name, versionRequirement, Requirements.FrameworkRestrictions.FrameworkRestrictionList frameworkRestrictions

let internal readPackageDetails source (j:JObject) : PackageDetails =
    let name =
        match j.["Name"] with
        | :? JValue as v -> v.ToString()
        | _ -> failwithf "Need Name in PackageDetails element"
        |> PackageName
    let downloadLink =
        match j.["DownloadLink"] with
        | :? JValue as v -> v.ToString()
        | _ -> failwithf "Need DownloadLink in PackageDetails element"
    let licenseUrl =
        match j.["LicenseUrl"] with
        | :? JValue as v -> v.ToString()
        | _ -> failwithf "Need LicenseUrl in PackageDetails element"
    let unlisted =
        match j.["IsUnlisted"] with
        | :? JValue as v -> System.Boolean.Parse(v.ToString())
        | _ -> failwithf "Need LicenseUrl in PackageDetails element"
    let dependencies =
        match j.["Dependencies"] with
        | :? JArray as deps -> deps
        | _ -> failwithf "Need LicenseUrl in PackageDetails element"
        :> IEnumerable<JToken>
        |> Seq.map (fun j -> j :?> JObject |> readPackageDependency)
        |> Set.ofSeq
    { Name  = name
      Source = Paket.PackageSources.PackageSource.NuGetV2Source source
      DownloadLink = downloadLink
      LicenseUrl =licenseUrl
      Unlisted = unlisted
      DirectDependencies = dependencies }

let internal readRuntimeGraph (j:JObject) : RuntimeGraph option =
    match j.["runtimes"] with
    | :? JObject as s ->
        RuntimeGraphParser.readRuntimeGraphJ false j |> Some
    | _ -> None

let internal readVersion source (jObject:JObject) : SemVerInfo * PackageDetails option * RuntimeGraphCache =
    let version =
        match jObject.["Version"] with
        | :? JValue as v -> v.ToString()
        | _ -> failwithf "Need PacakgeName in package element"
        |> SemVer.Parse
    let packageDetails =
        match jObject.["PackageDetails"] with
        | :? JObject as o -> readPackageDetails source o |> Some
        | _ -> None
    let runtimeGraph =
        match jObject.["RuntimeGraphData"] with
        | :? JObject as o -> readRuntimeGraph o |> Some
        | _ -> None
        |> RuntimeGraphCache.ofOption
    version, packageDetails, runtimeGraph

let internal readPackage source (jObject:JObject) : PackageName * CachedInfo =
    let versionsRetrieved =
        match jObject.["VersionsRetrieved"] with
        | :? JValue as v -> DateTime.Parse( v.ToString() )
        | _ ->
            // TODO: add logging?
            DateTime()

    let packageName =
        match jObject.["PackageName"] with
        | :? JValue as v -> v.ToString()
        | _ -> failwithf "Need PacakgeName in package element"
        |> PackageName
    let details =
        match jObject.["Versions"] with
        | :? JArray as ar -> ar
        | _ -> failwithf "Need Versions as JArray element"
        :> IEnumerable<JToken>
        |> Seq.map (fun j -> j :?> JObject |> readVersion source)
        |> Seq.map (fun (ver, packageDetails, runtimeGraph) -> ver, (packageDetails, runtimeGraph))
        |> Map.ofSeq

    packageName, { VersionListRetrieved = versionsRetrieved; Details = details }

let internal readSource sourceName (jObject:JObject) : seq<PackageName * CachedInfo> =
    let packages =
        match jObject.["Packages"] with
        | :? JArray as packages -> packages
        | null -> failwithf "Packages element was missing in source"
        | _ -> failwithf "Invalid data in Packages element."
        :> IEnumerable<JToken>
        |> Seq.map (fun j ->
            readPackage sourceName (j :?> JObject))
    //let sourceType =
    //    match jObject.["SourceType"] with
    //    | :? JValue as v -> v.ToString()
    //    | _ -> failwithf "Need PacakgeName in package element"

    packages


let readGraphJson (j:JObject) : seq<NuGetSourceUri * PackageName * CachedInfo> =

    j :> IEnumerable<KeyValuePair<string, JToken>>
    |> Seq.collect (fun kv ->
        readSource kv.Key (kv.Value :?> JObject)
        |> Seq.map (fun (packName, cache) -> kv.Key, packName, cache))

// Notes:
// - No entry means: Version list was never retrieved
// - We need to be carefull when caching the list of versions...
let private tree = ConcurrentDictionary<NuGetSourceUri * PackageName, CachedInfo>()
let updateCacheFile () =
    tree
    |> Seq.toList
    |> Seq.map (fun kv ->
        let uri, name = kv.Key
        uri, name,kv.Value)
    |> createGraphJson
    |> fun j -> System.IO.File.WriteAllText(graphCacheFile, j.ToString())

let readCacheFile () =
    if System.IO.File.Exists graphCacheFile then
        System.IO.File.ReadAllText(graphCacheFile)
        |> JObject.Parse
        |> readGraphJson
        |> Seq.iter (fun (source, packName, cache) ->
            tree.AddOrUpdate((source,packName), cache, (fun _ oldCache ->
                // TODO merge...
                cache))
            |> ignore)


let tryGetCacheEntry (source:Paket.PackageSources.PackageSource) name =
    let fixSource (cache:CachedInfo) =
        { cache with
            Details =
                cache.Details
                |> Map.map (fun _ (packageDetails, runtimeGraph) ->
                    match packageDetails with
                    | Some detail -> Some { detail with Source = source }, runtimeGraph
                    | None -> packageDetails, runtimeGraph) }

    match tree.TryGetValue((source.Url,name)) with
    | true, cache ->
        Some (fixSource cache)
    | _ -> None

do
    if isGraphCacheEnabled then
        readCacheFile()

let updateVersions (source:Paket.PackageSources.PackageSource) packageName versions =
    tree.AddOrUpdate((source.Url,packageName), CachedInfo.FromVersions versions, (fun _ oldVal ->
        oldVal |> CachedInfo.updateVersions versions))
    |> ignore
    updateCacheFile()

let updatePackageDetails packageName (ver:SemVerInfo) (packageDetails:PackageDetails) =
    tree.AddOrUpdate((packageDetails.Source.Url,packageName), CachedInfo.FromPackageDetails ver packageDetails, (fun _ oldVal ->
        oldVal |> CachedInfo.updatePackageDetails ver packageDetails))
    |> ignore
    updateCacheFile()

let updateRuntimeGraph (source:Paket.PackageSources.PackageSource) packageName (ver:SemVerInfo) (graph:RuntimeGraph option) =
    tree.AddOrUpdate((source.Url,packageName), CachedInfo.FromRuntimeGraph ver graph, (fun _ oldVal ->
        oldVal |> CachedInfo.updateRuntimeInfo ver graph))
    |> ignore
    updateCacheFile()

type GetVersionF = PackageSources.PackageSource list -> ResolverStrategy -> GroupName -> PackageName -> seq<SemVerInfo * PackageSources.PackageSource list>
let liftGetVersionsF (getVersionF:GetVersionF) : GetVersionF =
  if not isGraphCacheEnabled then getVersionF else
    let cachedGetVersionF sources resolver group name =
        sources
        |> Seq.map (fun (source:PackageSources.PackageSource) ->
            match tryGetCacheEntry source name with
            | Some cache ->
                cache.Details
                |> Map.toSeq
                |> Seq.map fst
                |> Seq.map (fun v -> v, source)
                |> Some
            | None -> None)
        |> Seq.fold (fun (s) item ->
            s |> Option.bind (fun curList -> item |> Option.map (fun versions -> versions :: curList))) (Some [])
        |> Option.map (fun items ->
            items
            |> Seq.concat
            |> Seq.groupBy fst
            |> Seq.map (fun (k, g) -> k, g |> Seq.map snd |> Seq.toList)
            |> Seq.sortByDescending (fun (v,_) -> v))
        |> Option.defaultWith (fun () ->
            // At least one source was not cached -> retrieve from server
            if preventOnlineRead then failwithf "At least one package source of %A was not found locally, and 'Prevent Online Read' was request." sources
            let onlineData = getVersionF sources resolver group name
            // update cache
            onlineData
            |> Seq.collect (fun (ver, sources) -> sources |> Seq.map (fun s -> s, ver))
            |> Seq.groupBy fst
            |> Seq.map (fun (k, g) -> k, g |> Seq.map snd |> Seq.toList)
            |> Seq.iter (fun (source, versions) -> updateVersions source name versions)

            onlineData)

    cachedGetVersionF

type GetPackageDetailsF = PackageSources.PackageSource list -> GroupName -> PackageName -> SemVerInfo -> PackageDetails
let liftGetPackageDetailsF (getPackageDetailsF:GetPackageDetailsF) : GetPackageDetailsF =
  if not isGraphCacheEnabled then getPackageDetailsF else
    let cachedGetPackageDetailsF sources group name semVer =
        sources
        |> Seq.choose (fun (source:PackageSources.PackageSource) ->
            match tryGetCacheEntry source name with
            | Some cache -> cache.Details |> Map.tryFind semVer
            | None -> None)
        |> Seq.tryHead
        |> Option.bind fst
        |> Option.defaultWith (fun _ ->
            // No package-details cached jet
            if preventOnlineRead then failwithf "No package details for package %O are cached jet in any of the sources %A, and 'Prevent Online Read' was request." name sources
            let details = getPackageDetailsF sources group name semVer
            // update cache
            updatePackageDetails name semVer details

            details)

    cachedGetPackageDetailsF

type GetRuntimeGraphF = GroupName -> ResolvedPackage -> RuntimeGraph option
let liftGetRuntimeGraphFromPackage (getRuntimeGraphFromPackage:GetRuntimeGraphF) : GetRuntimeGraphF =
  if not isGraphCacheEnabled then getRuntimeGraphFromPackage else
    let cachedGetRuntimeGraphFromPackage group resolvedPackage =
        match tryGetCacheEntry resolvedPackage.Source resolvedPackage.Name with
        | Some cache -> cache.Details |> Map.tryFind resolvedPackage.Version |> Option.map snd
        | None -> None
        |> RuntimeGraphCache.simplify
        |> RuntimeGraphCache.getData
        |> Option.defaultWith (fun _ ->
            // No package-details cached jet
            if preventOnlineRead then failwithf "No runtime graph data for package %O is cached jet, and 'Prevent Online Read' was request." resolvedPackage.Name
            let details = getRuntimeGraphFromPackage group resolvedPackage
            // update cache
            updateRuntimeGraph resolvedPackage.Source resolvedPackage.Name resolvedPackage.Version details

            details)

    cachedGetRuntimeGraphFromPackage