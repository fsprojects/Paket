/// Contains logic which helps to resolve the dependency graph.
module Paket.Resolver

open Paket
open System

type private Shrinked =
| Ok of Dependency
| Conflict of Dependency * Dependency


let private shrink (s1 : Shrinked, s2 : Shrinked) = 
    match s1, s2 with
    | Ok version1, Ok version2 -> 
        match version1.Referenced.VersionRange, version2.Referenced.VersionRange with
        | Minimum v1, Minimum v2 when v1 >= v2 -> s1
        | Minimum _, Minimum _ -> s2
        | Minimum v1, Specific v2 when v2 >= v1 -> s2
        | Specific v1, Minimum v2 when v1 >= v2 -> s1
        | Specific v1, Specific v2 when v1 = v2 -> s1
        | Range(min1, max1), Specific v2 when min1 <= v2 && max1 > v2 -> s2
        | Specific v1, Range(min2, max2) when min2 <= v1 && max2 > v1 -> s2
        | Range(min1, max1), Range(min2, max2) -> 
            let newMin = max min1 min2
            let newMax = min max1 max2
            if newMin > newMax then Shrinked.Conflict(version1, version2)
            else 
                let shrinkedDependency = 
                    { version1.Referenced with VersionRange = VersionRange.Range(newMin, newMax) }
                Shrinked.Ok(match version1 with
                            | FromRoot _ -> FromRoot shrinkedDependency
                            | FromPackage d -> 
                                FromPackage { Defining = d.Defining
                                              Referenced = shrinkedDependency })
        | _ -> Shrinked.Conflict(version1, version2)
    | _ -> s1

let private addDependency package dependencies newDependency =
    let newDependency = Shrinked.Ok newDependency    
    match Map.tryFind package dependencies with
    | Some oldDependency -> Map.add package (shrink(oldDependency,newDependency)) dependencies
    | None -> Map.add package newDependency dependencies   

/// Resolves all direct and indirect dependencies
let Resolve(force, discovery : IDiscovery, rootDependencies:Package seq) =    
    let rec analyzeGraph processed (dependencies:Map<string,Shrinked>) =
        if Map.isEmpty dependencies then processed else
        let current = Seq.head dependencies
        let resolvedName = current.Key

        match current.Value with
        | Shrinked.Conflict(c1,c2) -> 
            let resolved = { processed with ResolvedVersionMap = Map.add resolvedName (ResolvedDependency.Conflict(c1,c2)) processed.ResolvedVersionMap }
            analyzeGraph resolved (Map.remove resolvedName dependencies)
        | Ok dependency -> 
            match Map.tryFind resolvedName processed.ResolvedVersionMap with
            | Some (Resolved dependency) -> 
                match dependency.Referenced.VersionRange with
                | Specific fixedVersion -> 
                    if not <| dependency.Referenced.VersionRange.IsInRange fixedVersion then failwith "Conflict" else
                    
                    dependencies
                    |> Map.remove resolvedName
                    |> analyzeGraph processed
                | _ -> failwith "Not allowed"
            | _ ->
                let allVersions = 
                    discovery.GetVersions(dependency.Referenced.SourceType,dependency.Referenced.Source,resolvedName) 
                    |> Async.RunSynchronously
                    |> Seq.toList

                let versions =                
                    allVersions
                    |> List.filter dependency.Referenced.VersionRange.IsInRange
                    |> List.map SemVer.parse

                if versions = [] then
                    failwithf "No package found which matches %s %A.%sVersion available: %A" dependency.Referenced.Name dependency.Referenced.VersionRange Environment.NewLine allVersions

                let maxVersion = List.max versions

                let _,dependentPackages = 
                    discovery.GetPackageDetails(force, dependency.Referenced.SourceType, dependency.Referenced.Source, dependency.Referenced.Name, maxVersion.ToString()) 
                    |> Async.RunSynchronously

                let resolvedPackage =
                    { Name = resolvedName
                      VersionRange = VersionRange.Exactly(maxVersion.ToString())
                      SourceType = dependency.Referenced.SourceType
                      Source = dependency.Referenced.Source }

                let resolvedDependency = 
                    ResolvedDependency.Resolved(
                                            match dependency with
                                            | FromRoot _ -> FromRoot resolvedPackage
                                            | FromPackage d -> 
                                                FromPackage { Defining = d.Defining
                                                              Referenced = resolvedPackage })

                let mutable dependencies = dependencies

                for dependentPackage in dependentPackages do
                    let newDependency = 
                        FromPackage { Defining = { dependency.Referenced with VersionRange = VersionRange.Exactly(maxVersion.ToString()) }
                                      Referenced = 
                                          { Name = dependentPackage.Name
                                            VersionRange = dependentPackage.VersionRange
                                            SourceType = dependentPackage.SourceType
                                            Source = dependentPackage.Source } }
                    dependencies <- addDependency dependentPackage.Name dependencies newDependency

                let resolved = 
                    { ResolvedVersionMap = Map.add resolvedName resolvedDependency processed.ResolvedVersionMap
                      DirectDependencies = Map.add (dependency.Referenced.Name, maxVersion.ToString()) dependentPackages processed.DirectDependencies }
                
                dependencies
                |> Map.remove resolvedName
                |> analyzeGraph resolved

    
    rootDependencies
    |> Seq.map (fun p -> p.Name, FromRoot p)
    |> Seq.fold (fun m (p, d) -> addDependency p m d) Map.empty
    |> analyzeGraph { ResolvedVersionMap = Map.empty; DirectDependencies = Map.empty }
