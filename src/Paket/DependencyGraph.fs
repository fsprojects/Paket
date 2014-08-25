module Paket.DependencyGraph

open System

type VersionRange = 
    | MinVersion of string
    | SpecificVersion of string
    | VersionRange of string * string
    | Conflict of VersionRange
    static member Between(min, max) : VersionRange = VersionRange(min, max)
    static member Exactly version : VersionRange = SpecificVersion version
    static member AtLeast version : VersionRange = MinVersion version
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
        | MinVersion v -> v <= version
        | SpecificVersion v -> v = version
        | VersionRange(min,max) -> version >= min && version < max
        | Conflict _ -> false



/// Calculates the logical conjunction of the given version requirements
let Shrink(version1, version2) = 
    match version1, version2 with
    | MinVersion v1, MinVersion v2 -> VersionRange.AtLeast(max v1 v2)
    | MinVersion v1, SpecificVersion v2 when v2 >= v1 -> VersionRange.Exactly v2
    | SpecificVersion v1, MinVersion v2 when v1 >= v2 -> VersionRange.Exactly v1
    | VersionRange(min1, max1), SpecificVersion v2 when min1 <= v2 && max1 > v2 -> VersionRange.Exactly v2
    | SpecificVersion v1, VersionRange(min2, max2) when min2 <= v1 && max2 > v1 -> VersionRange.Exactly v1
    | VersionRange(min1, max1), VersionRange(min2, max2) -> VersionRange.VersionRange(max min1 min2, min max1 max2)

let filterVersions (version:VersionRange) versions =
    versions
    |> List.filter version.IsInRange
    


type VersionNode = {
    Package : string
    Version : string
    Dependencies : Map<string, VersionRange>
}

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

let analyzeNode (discovery:IDiscovery) (package,versionRange:VersionRange) : VersionNode =
    let maxVersion = 
        discovery.GetVersions package
        |> Seq.filter versionRange.IsInRange
        |> Seq.max

    { Package = package; Version = maxVersion; Dependencies = discovery.GetDirectDependencies(package,maxVersion) }