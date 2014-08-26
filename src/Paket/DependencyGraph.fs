module Paket.DependencyGraph

open System

type VersionRange = 
    | AtLeast of string
    | Exactly of string
    | Between of string * string
    
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

type DefindedDependency = {
    DefiningPackage : string
    DefiningVersion : string
    ReferencedPackage : string
    ReferencedVersion : VersionRange }

type Shrinked =
| Ok of DefindedDependency
| Conflict of DefindedDependency * DefindedDependency

/// Calculates the logical conjunction of the given version requirements
let Shrink(s1:Shrinked, s2:Shrinked) =
    match s1,s2 with
    | Ok version1,Ok version2 ->
        match version1.ReferencedVersion, version2.ReferencedVersion with
        | AtLeast v1, AtLeast v2 when v1 >= v2 -> s1
        | AtLeast _, AtLeast _ -> s2
        | AtLeast v1, Exactly v2 when v2 >= v1 -> s2
        | Exactly v1, AtLeast v2 when v1 >= v2 -> s1
        | Exactly v1, Exactly v2 when v1 = v2 ->  s1
        | Between(min1, max1), Exactly v2 when min1 <= v2 && max1 > v2 -> s2
        | Exactly v1, Between(min2, max2) when min2 <= v1 && max2 > v1 -> s2
        | Between(min1, max1), Between(min2, max2) -> Shrinked.Ok { version1 with ReferencedVersion = VersionRange.Between(max min1 min2, min max1 max2) } // TODO:
        | _ -> Shrinked.Conflict(version1, version2)
    | _ -> s1

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
| ResolvingConflict of DefindedDependency * DefindedDependency

let Resolve(discovery : IDiscovery, dependencies:Dependency seq) =      
    let rec analyzeGraph fixedDependencies (dependencies:Map<string,Shrinked>) =
        if Map.isEmpty dependencies then fixedDependencies else
        let current = Seq.head dependencies

        match current.Value with
        | Conflict(c1,c2) -> analyzeGraph (Map.add current.Key (ResolvedVersion.ResolvingConflict(c1,c2)) fixedDependencies) (Map.remove current.Key dependencies)
        | Ok c -> 
            match Map.tryFind current.Key fixedDependencies with
            | Some (Resolved fixedVersion) -> if c.ReferencedVersion.IsInRange fixedVersion then fixedDependencies else failwith "Conflict"
            | _ ->            
                let maxVersion = 
                    discovery.GetVersions current.Key
                    |> Seq.filter c.ReferencedVersion.IsInRange
                    |> Seq.max

                let mutable newDependencies = dependencies

                for package,version in discovery.GetDirectDependencies(current.Key, maxVersion) do
                    let newDependency = Shrinked.Ok { DefiningPackage = current.Key; DefiningVersion = maxVersion; ReferencedPackage = package; ReferencedVersion = version}
                    newDependencies <- 
                        match Map.tryFind package newDependencies with
                        | Some oldDependency -> Map.add package (Shrink(oldDependency,newDependency)) newDependencies
                        | None -> Map.add package newDependency newDependencies

                newDependencies <- Map.remove current.Key newDependencies

                analyzeGraph (Map.add current.Key (ResolvedVersion.Resolved maxVersion) fixedDependencies) newDependencies

    dependencies
    |> Seq.map (fun (p,v) -> p,Shrinked.Ok { DefiningPackage = ""; DefiningVersion = "";  ReferencedPackage = p; ReferencedVersion = v})
    |> Map.ofSeq
    |> analyzeGraph Map.empty