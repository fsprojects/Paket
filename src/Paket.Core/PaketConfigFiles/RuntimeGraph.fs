namespace Paket

open Domain

// See https://github.com/NuGet/NuGet.Client/blob/85731166154d0818d79a19a6d2417de6aa851f39/src/NuGet.Core/NuGet.Packaging/RuntimeModel/RuntimeGraph.cs
// See http://www.natemcmaster.com/blog/2016/05/19/nuget3-rid-graph/

// Example files can be found in the packages "Microsoft.NETCore.Platforms 1.1.0" and "Microsoft.NETCore.Targets 1.1.0"

/// The runtime identifiert for a specific runtime. Should be treated as a black box in combination with the operations defined on the RuntimeGraph
type Rid =
    private { Rid : string }
    static member Of s = { Rid = s }
    static member Parse s = Rid.Of s
    override x.ToString () = x.Rid
    static member Any = { Rid = "any" }

type CompatibilityProfileName = string

/// A compatibility profile identifies which RIDs are compatible with what FrameworkIdentifier (TargetFrameworkMoniker/tfm in NuGet world)
type CompatibilityProfile =
  { Name : CompatibilityProfileName
    Supported : Map<FrameworkIdentifier, Rid list> }

/// The description for a particular RID, indicates inherited (compatible) RIDs and runtime dependencies for this RID.
type RuntimeDescription =
  { Rid : Rid
    InheritedRids : Rid list
    RuntimeDependencies : Map<PackageName, (PackageName * VersionRequirement) list> }

/// The runtime graph consolidating compatibility informations across the different RIDs
type RuntimeGraph =
   { Supports : Map<CompatibilityProfileName, CompatibilityProfile>
     Runtimes : Map<Rid, RuntimeDescription> }
   static member Empty = { Supports = Map.empty; Runtimes = Map.empty }

open Newtonsoft.Json
open Newtonsoft.Json.Linq

/// A module for parsing runtime.json files contained in various packages.
module RuntimeGraphParser =
    open System.Collections.Generic

    (*
{ "uwp.10.0.app": {
      "uap10.0": [
        "win10-x86",
        "win10-x86-aot",
        "win10-x64",
        "win10-x64-aot",
        "win10-arm",
        "win10-arm-aot"
      ]
  }
}*)
    let readCompatiblityProfilesJ (json:JObject) =
      [ for t in json :> IEnumerable<KeyValuePair<string, JToken>> do
          yield
            { Name = t.Key
              Supported =
                [ for s in t.Value :?> JObject :> IEnumerable<KeyValuePair<string, JToken>> do
                    match FrameworkDetection.Extract s.Key with
                    | Some fid ->
                        yield fid, [ for rid in (s.Value :?> JArray) -> { Rid = string rid } ]
                    | None -> failwithf "could not detect framework-identifier '%s'" s.Key ]
                |> Map.ofSeq } ]
    (*{
    "win": {
      "#import": [ "any" ]
      "Microsoft.Win32.Primitives": {
        "runtime.win.Microsoft.Win32.Primitives": "4.3.0"
      },*)
    let readRuntimeDescriptionJ (json:JObject) =
      [ for t in json :> IEnumerable<KeyValuePair<string, JToken>> do
          let rid = { Rid = t.Key }
          match t.Value with
          | :? JObject as spec ->
              let imports =
                match spec.["#import"] with
                | :? JArray as j -> [ for t in j -> { Rid = string t } ]
                | null -> []
                | o -> failwithf "unknown stuff in '#import' value: %O" o
              let dependencies =
                spec :> IEnumerable<KeyValuePair<string, JToken>>
                |> Seq.filter (fun kv -> kv.Key <> "#import")
                |> Seq.map (fun kv ->
                    let packageName = PackageName kv.Key
                    let depsSpec =
                        match kv.Value with
                        | :? JObject as deps ->
                            deps :> IEnumerable<KeyValuePair<string, JToken>>
                            |> Seq.map (fun kv -> PackageName kv.Key, VersionRequirement.Parse (string kv.Value))
                            |> Seq.toList
                        | _ -> failwithf "unknown stuff in runtime-dependency: %O" kv.Value
                    packageName, depsSpec)
                |> Map.ofSeq
              yield
                  { Rid = rid
                    InheritedRids = imports
                    RuntimeDependencies = dependencies }
          | _ -> failwithf "unknwn stuff in runtime-description: %O" t.Value ]

    let readRuntimeGraphJ (json:JObject) =
       { Supports =
            match json.["supports"] with
            | :? JObject as supports ->
                readCompatiblityProfilesJ supports
                |> Seq.map (fun c -> c.Name, c)
                |> Map.ofSeq
            | null -> Map.empty
            | _ -> failwith "invalid data in supports field."
         Runtimes =
            match json.["runtimes"] with
            | :? JObject as runtimes ->
                readRuntimeDescriptionJ runtimes
                |> Seq.map (fun r -> r.Rid, r)
                |> Map.ofSeq
            | null -> Map.empty
            | _ -> failwith "invalid data in runtimes" }

    let readRuntimeGraph (s:string) =
        readRuntimeGraphJ (JObject.Parse(s))

module Map =
    let merge (f:'b -> 'b -> 'b) (m1:Map<'a,'b>) (m2:Map<'a,'b>) =
        m1
        |> Map.toSeq
        |> Seq.append (m2 |> Map.toSeq)
        |> Seq.groupBy (fst)
        |> Seq.map (fun (k, g) ->
            match g |> Seq.map snd |> Seq.toList with
            | [ a; b ] -> k, f a b
            | [ a ] -> k, a
            | _ -> failwithf "This should never happen")
        |> Map.ofSeq

// Defines common operations on the runtime graph
module RuntimeGraph =
    open PackageResolver

    let mergeCompatibility (s1:CompatibilityProfile) (s2:CompatibilityProfile) =
       assert (s1.Name = s2.Name)
       { Name = s1.Name
         Supported =
            Map.merge (@) s1.Supported s2.Supported
            |> Map.map (fun _ l -> List.distinct l) }
    let mergeDescription (d1:RuntimeDescription) (d2:RuntimeDescription) =
       assert (d1.Rid = d2.Rid)
       { Rid = d1.Rid
         InheritedRids = d1.InheritedRids @ d2.InheritedRids |> List.distinct
         RuntimeDependencies =
            Map.merge (@) d1.RuntimeDependencies d2.RuntimeDependencies
            |> Map.map (fun _ l -> List.distinct l) }
    /// merge two runtime graphs
    let merge (r1:RuntimeGraph) (r2:RuntimeGraph) =
       { Supports = Map.merge mergeCompatibility r1.Supports r2.Supports
         Runtimes = Map.merge mergeDescription r1.Runtimes r2.Runtimes }
    /// merge a sequence of runtime graphs
    let mergeSeq s =
        s |> Seq.fold merge RuntimeGraph.Empty
    /// get the list of compatible RIDs for the given RID. Most compatible are near the head. The list contains the given RID in the HEAD
    let getInheritanceList (rid:Rid) (g:RuntimeGraph) =
        let rec getListRec currentList toInspect =
            match toInspect with
            | rid :: rest ->
                if List.contains rid currentList then
                    getListRec currentList rest
                else
                    let desc = g.Runtimes.[rid]
                    let filtered = desc.InheritedRids |> List.filter (fun inh -> not (List.contains inh currentList))
                    let newList = currentList @ filtered
                    let toInspect = rest @ filtered
                    getListRec newList toInspect
            | _ -> currentList

        getListRec [rid] [rid]
    /// get a list of RIDs in no particular order which are part of this runtime graph
    let getKnownRids (g:RuntimeGraph) =
        g.Runtimes |> Map.toSeq |> Seq.map fst
    /// calculates whether the given assetRid is compatible with the given projectRid.
    /// consider a project targeting projectRid, this returns true if an asset with assetRid is comaptible.
    let areCompatible projectRid assetRid g =
        g
        |> getInheritanceList projectRid
        |> List.contains assetRid
    /// return runtime depenendencies for the given package and runtime
    let findRuntimeDependencies rid packageName g =
        getInheritanceList rid g
        |> Seq.choose (fun r ->
            match g.Runtimes |> Map.tryFind r with
            | Some desc ->
                let result = desc.RuntimeDependencies |> Map.tryFind packageName
                result
            | None -> None)
        |> Seq.tryHead
        |> Option.defaultValue []

    open System.IO
    open Pri.LongPath
    /// Downloads the given package into the nuget cache and read its runtime.json.
    let getRuntimeGraphFromNugetCache root groupName (package:ResolvedPackage) =
        // 1. downloading packages into cache
        let targetFileName, _ =
            NuGet.DownloadPackage (None, root, package.Source, [], groupName, package.Name, package.Version, package.IsCliTool, false, false, false)
            |> Async.RunSynchronously

        let extractedDir = NuGetCache.ExtractPackageToUserFolder (targetFileName, package.Name, package.Version, package.IsCliTool, null) |> Async.RunSynchronously
        // 2. Get runtime graph
        let runtime = Path.Combine(extractedDir, "runtime.json")
        if File.Exists runtime then Some (runtime) else None
        |> Option.map File.ReadAllText
        |> Option.map RuntimeGraphParser.readRuntimeGraph