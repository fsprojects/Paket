module Paket.Resolver

open Paket

type private Shrinked =
| Ok of Dependency
| Conflict of Dependency * Dependency


let private shrink (s1 : Shrinked, s2 : Shrinked) = 
    match s1, s2 with
    | Ok version1, Ok version2 -> 
        match version1.Referenced.VersionRange, version2.Referenced.VersionRange with
        | AtLeast v1, AtLeast v2 when v1 >= v2 -> s1
        | AtLeast _, AtLeast _ -> s2
        | AtLeast v1, Exactly v2 when v2 >= v1 -> s2
        | Exactly v1, AtLeast v2 when v1 >= v2 -> s1
        | Exactly v1, Exactly v2 when v1 = v2 -> s1
        | Between(min1, max1), Exactly v2 when min1 <= v2 && max1 > v2 -> s2
        | Exactly v1, Between(min2, max2) when min2 <= v1 && max2 > v1 -> s2
        | Between(min1, max1), Between(min2, max2) -> 
            let newMin = max min1 min2
            let newMax = min max1 max2
            if newMin > newMax then Shrinked.Conflict(version1, version2)
            else 
                let shrinkedDependency = 
                    { version1.Referenced with VersionRange = VersionRange.Between(newMin, newMax) }
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

let Resolve(discovery : IDiscovery, dependencies:Package seq) =    
    let rec analyzeGraph processed (dependencies:Map<string,Shrinked>) =
        if Map.isEmpty dependencies then processed else
        let current = Seq.head dependencies
        let resolvedName = current.Key

        match current.Value with
        | Shrinked.Conflict(c1,c2) -> 
            let resolved =  Map.add resolvedName (ResolvedVersion.Conflict(c1,c2)) processed
            analyzeGraph resolved (Map.remove resolvedName dependencies)
        | Ok dependency -> 
            match Map.tryFind resolvedName processed with
            | Some (Resolved dependency) -> 
                match dependency.Referenced.VersionRange with
                | Exactly fixedVersion -> 
                    if not <| dependency.Referenced.VersionRange.IsInRange fixedVersion then failwith "Conflict" else
                    
                    dependencies
                    |> Map.remove resolvedName
                    |> analyzeGraph processed
                | _ -> failwith "Not allowed"
            | _ ->
                
                let maxVersion = 
                    discovery.GetVersions(dependency.Referenced.SourceType,dependency.Referenced.Source,resolvedName)
                    |> Async.RunSynchronously
                    |> Seq.filter dependency.Referenced.VersionRange.IsInRange
                    |> Seq.max

                let resolvedPackage =
                    { Name = resolvedName
                      VersionRange = Exactly maxVersion
                      SourceType = dependency.Referenced.SourceType
                      Source = dependency.Referenced.Source }
                let resolvedDependency = 
                    ResolvedVersion.Resolved(match dependency with
                                             | FromRoot _ -> FromRoot resolvedPackage
                                             | FromPackage d -> 
                                                 FromPackage { Defining = d.Defining
                                                               Referenced = resolvedPackage })

                let mutable dependencies = dependencies
                let dependentPackages = 
                    discovery.GetDirectDependencies(dependency.Referenced.SourceType, dependency.Referenced.Source, dependency.Referenced.Name, maxVersion) 
                    |> Async.RunSynchronously

                for dependentPackage in dependentPackages do
                    let newDependency = 
                        FromPackage { Defining = { dependency.Referenced with VersionRange = Exactly maxVersion }
                                      Referenced = 
                                          { Name = dependentPackage.Name
                                            VersionRange = dependentPackage.VersionRange
                                            SourceType = dependentPackage.SourceType
                                            Source = dependentPackage.Source } }
                    dependencies <- addDependency dependentPackage.Name dependencies newDependency

                let resolved = Map.add resolvedName resolvedDependency processed 
                
                dependencies
                |> Map.remove resolvedName
                |> analyzeGraph resolved

    
    dependencies
    |> Seq.map (fun p -> 
                    p.Name, 
                    FromRoot { Name = p.Name
                               VersionRange = p.VersionRange
                               SourceType = p.SourceType
                               Source = p.Source })
    |> Seq.fold (fun m (p, d) -> addDependency p m d) Map.empty
    |> analyzeGraph Map.empty