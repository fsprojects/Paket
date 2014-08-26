module Paket.DependencyGraph

open System

type VersionRange = 
    | AtLeast of string
    | Exactly of string
    | Between of string * string
    | Conflict of VersionRange * VersionRange
    
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
        | Between(min, max) -> version >= min && version < max
        | Conflict _ -> false

type DefindedDependency = {
    DefiningPackage : string
    DefiningVersion : string
    ReferencedPackage : string
    ReferencedVersion : VersionRange }

/// Calculates the logical conjunction of the given version requirements
let Shrink(version1:DefindedDependency, version2:DefindedDependency) = 
    match version1.ReferencedVersion, version2.ReferencedVersion with
    | AtLeast v1, AtLeast v2 when v1 >= v2 -> version1
    | AtLeast _, AtLeast _ -> version2
    | AtLeast v1, Exactly v2 when v2 >= v1 -> version2
    | Exactly v1, AtLeast v2 when v1 >= v2 -> version1
    | Exactly v1, Exactly v2 when v1 = v2 -> version1
    | Between(min1, max1), Exactly v2 when min1 <= v2 && max1 > v2 -> version2
    | Exactly v1, Between(min2, max2) when min2 <= v1 && max2 > v1 -> version1
    | Between(min1, max1), Between(min2, max2) -> { version1 with ReferencedVersion = VersionRange.Between(max min1 min2, min max1 max2) } // TODO:
    | _ -> { version1 with ReferencedVersion = VersionRange.Conflict(version1.ReferencedVersion, version2.ReferencedVersion)} // TODO:

let filterVersions (version : VersionRange) versions = versions |> List.filter version.IsInRange

type Dependency = string * VersionRange
type Dependencies = Map<string,VersionRange>

type IDiscovery = 
    abstract GetDirectDependencies : string * string -> Dependency list
    abstract GetVersions : string -> string seq

let DictionaryDiscovery(graph : seq<string * string * Dependency list>) = 
    { new IDiscovery with
          member __.GetDirectDependencies(package, version) = 
            graph 
            |> Seq.filter (fun (p,v,_) -> p = package && v = version) 
            |> Seq.map (fun (_,_,d) -> d) 
            |> Seq.head 

          member __.GetVersions package = 
              graph              
              |> Seq.filter (fun (p,_,_) -> p = package)
              |> Seq.map (fun (_,v,_) -> v) }

type ResolvedVersion =
| Resolved of string
| ResolvingConflict of DefindedDependency

let Resolve(discovery : IDiscovery, dependencies:Dependency seq) =      
    let rec analyzeGraph fixedDependencies  (dependencies:Map<string,DefindedDependency>) =
        if Map.isEmpty dependencies then fixedDependencies else
        let current = Seq.head dependencies

        match current.Value.ReferencedVersion with
        | Conflict _  -> analyzeGraph (Map.add current.Key (ResolvedVersion.ResolvingConflict current.Value) fixedDependencies) (Map.remove current.Key dependencies)
        | _ ->
            match Map.tryFind current.Key fixedDependencies with
            | Some (Resolved fixedVersion) -> if current.Value.ReferencedVersion.IsInRange fixedVersion then fixedDependencies else failwith "Conflict"
            | _ ->            
                let maxVersion = 
                    discovery.GetVersions current.Key
                    |> Seq.filter current.Value.ReferencedVersion.IsInRange
                    |> Seq.max

                let mutable newDependencies = dependencies

                for package,version in discovery.GetDirectDependencies(current.Key, maxVersion) do
                    let newDependency = { DefiningPackage = current.Key; DefiningVersion = maxVersion; ReferencedPackage = package; ReferencedVersion = version}
                    newDependencies <- 
                        match Map.tryFind package newDependencies with
                        | Some oldDependency -> Map.add package (Shrink(oldDependency,newDependency)) newDependencies
                        | None -> Map.add package newDependency newDependencies

                newDependencies <- Map.remove current.Key newDependencies

                analyzeGraph (Map.add current.Key (ResolvedVersion.Resolved maxVersion) fixedDependencies) newDependencies

    dependencies
    |> Seq.map (fun (p,v) -> p,{ DefiningPackage = ""; DefiningVersion = "";  ReferencedPackage = p; ReferencedVersion = v})
    |> Map.ofSeq
    |> analyzeGraph Map.empty