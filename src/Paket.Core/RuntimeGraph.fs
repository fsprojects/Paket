namespace Paket

open Domain

// See https://github.com/NuGet/NuGet.Client/blob/85731166154d0818d79a19a6d2417de6aa851f39/src/NuGet.Core/NuGet.Packaging/RuntimeModel/RuntimeGraph.cs
// See http://www.natemcmaster.com/blog/2016/05/19/nuget3-rid-graph/

// Example files can be found in the packages "Microsoft.NETCore.Platforms 1.1.0" and "Microsoft.NETCore.Targets 1.1.0"

type Rid =
    private { Rid : string }
    static member Of s = { Rid = s }
    static member Parse s = Rid.Of s
    override x.ToString () = x.Rid

type CompatibilityProfileName = string

type CompatibilityProfile =
  { Name : CompatibilityProfileName
    Supported : Map<FrameworkIdentifier, Rid list> }

type RuntimeDescription =
  { Rid : Rid
    InheritedRids : Rid list
    RuntimeDependencies : Map<PackageName, (PackageName * VersionRequirement) list> }

type RuntimeGraph =
   { Supports : Map<CompatibilityProfileName, CompatibilityProfile>
     Runtimes : Map<Rid, RuntimeDescription> }

open Newtonsoft.Json
open Newtonsoft.Json.Linq

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

module RuntimeGraph =
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
    let merge (r1:RuntimeGraph) (r2:RuntimeGraph) =
       { Supports = Map.merge mergeCompatibility r1.Supports r2.Supports
         Runtimes = Map.merge mergeDescription r1.Runtimes r2.Runtimes }

