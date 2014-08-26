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
    | _ -> VersionRange.Conflict(version1,version2)

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


let Resolve(discovery : IDiscovery, dependencies:Dependency seq) =      
    let rec analyzeGraph (fixedDependencies:Dependencies)  (dependencies:Dependencies) =
        if Map.isEmpty dependencies then fixedDependencies else
        let current = Seq.head dependencies

        match current.Value with
        | Conflict _ -> analyzeGraph (Map.add current.Key current.Value fixedDependencies) (Map.remove current.Key dependencies)
        | _ ->
            match Map.tryFind current.Key fixedDependencies with
            | Some (Exactly fixedVersion) -> if current.Value.IsInRange fixedVersion then fixedDependencies else failwith "Conflict"
            | _ ->            
                let maxVersion = 
                    discovery.GetVersions current.Key
                    |> Seq.filter current.Value.IsInRange
                    |> Seq.max

                let mutable newDependencies = dependencies

                for package,version in discovery.GetDirectDependencies(current.Key, maxVersion) do
                    newDependencies <- 
                        match Map.tryFind package newDependencies with
                        | Some oldDependency -> 
    //                        match Shrink(oldDependency, version) with
    //                        | VersionRange.Conflict(v1,v2) -> failwithf "Version conflict for package %s. Package %s %s wants %s %A but package %s %s wants %s %A." package current.Key maxVersion current.Key v1 package maxVersion current.Key v2
    //                        | correctVersion -> Map.add package correctVersion newDependencies

                              Map.add package (Shrink(oldDependency, version)) newDependencies
                        | None -> Map.add package version newDependencies

                newDependencies <- Map.remove current.Key newDependencies

                analyzeGraph (Map.add current.Key (VersionRange.Exactly maxVersion) fixedDependencies) newDependencies
    analyzeGraph Map.empty (Map.ofSeq dependencies)