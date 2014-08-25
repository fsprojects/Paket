module Paket.DependencyGraph

open System

type VersionRange = 
    | AtLeast of string
    | Exactly of string
    | Between of string * string
    | Conflict of VersionRange
    static member Parse(text : string) : VersionRange = 
        // TODO: Make this pretty
        if text.StartsWith "~> " then 
            let min = text.Replace("~> ", "")
            let parts = min.Split('.')
            let major = Int32.Parse parts.[0]
            
            let newParts = 
                (major + 1).ToString() :: Seq.toList (parts
                                                      |> Seq.skip 1
                                                      |> Seq.map (fun _ -> "0"))
            VersionRange.Between(min, String.Join(".", newParts))
        else if text.StartsWith "= " then VersionRange.Exactly(text.Replace("= ", ""))
        else VersionRange.AtLeast text

    /// Checks wether the given version is in the version range
    member this.IsInRange version =
        match this with
        | AtLeast v -> v <= version
        | Exactly v -> v = version
        | Between(min,max) -> version >= min && version < max
        | Conflict _ -> false



/// Calculates the logical conjunction of the given version requirements
let Shrink(version1, version2) = 
    match version1, version2 with
    | AtLeast v1, AtLeast v2 -> VersionRange.AtLeast(max v1 v2)
    | AtLeast v1, Exactly v2 when v2 >= v1 -> VersionRange.Exactly v2
    | Exactly v1, AtLeast v2 when v1 >= v2 -> VersionRange.Exactly v1
    | Exactly v1, Exactly v2 when v1 = v2 -> VersionRange.Exactly v1
    | Between(min1, max1), Exactly v2 when min1 <= v2 && max1 > v2 -> VersionRange.Exactly v2
    | Exactly v1, Between(min2, max2) when min2 <= v1 && max2 > v1 -> VersionRange.Exactly v1
    | Between(min1, max1), Between(min2, max2) -> VersionRange.Between(max min1 min2, min max1 max2)

let filterVersions (version:VersionRange) versions =
    versions
    |> List.filter version.IsInRange

type Dependencies = Map<string, VersionRange>

type ConfigValue = 
    { Source : string
      Version : VersionRange }
    
type Config = Map<string, ConfigValue>

// TODO make this correct        
let merge (config1 : Config) (config2 : Config) = 
    config2 |> Seq.fold (fun m x -> 
                   match Map.tryFind x.Key m with
                   | Some v -> 
                       if v.Version > x.Value.Version then m
                       else Map.add x.Key x.Value m
                   | None -> Map.add x.Key x.Value m) config1

type IDiscovery =
   abstract member GetDirectDependencies : string * string -> Map<string, VersionRange>
   abstract member GetVersions : string -> string seq

let analyzeNode (discovery:IDiscovery) (package,versionRange:VersionRange) =
    let maxVersion = 
        discovery.GetVersions package
        |> Seq.filter versionRange.IsInRange
        |> Seq.max

    maxVersion,discovery.GetDirectDependencies(package,maxVersion)

let mergeDependencies (d1:Dependencies) (d2:Dependencies) =
    let mutable dependencies = d1
    for dep in d2 do
        dependencies <-
            match Map.tryFind dep.Key dependencies with
            | Some d -> Map.add dep.Key (Shrink(d,dep.Value)) dependencies
            | None -> Map.add dep.Key dep.Value dependencies

    dependencies

let AnalyzeGraph (discovery:IDiscovery) (package,versionRange:VersionRange) : Dependencies =
    let rec analyzeGraph (package,versionRange) =
        let _,startDependencies = analyzeNode discovery (package,versionRange)
        let mutable dependencies = startDependencies

        for node in startDependencies do
            dependencies <- mergeDependencies dependencies (analyzeGraph (node.Key,node.Value))

        dependencies

    let maxVersion = 
        discovery.GetVersions package
        |> Seq.filter versionRange.IsInRange
        |> Seq.max

    Map.add package (VersionRange.Exactly maxVersion) Map.empty
    |> mergeDependencies (analyzeGraph (package,versionRange))