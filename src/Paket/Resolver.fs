module Paket.Resolver

open Paket

type private Shrinked =
| Ok of Dependency
| Conflict of Dependency * Dependency


let private shrink (s1 : Shrinked, s2 : Shrinked) = 
    match s1, s2 with
    | Ok version1, Ok version2 -> 
        match version1.DependentPackage.VersionRange, version2.DependentPackage.VersionRange with
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
                    { version1.DependentPackage with VersionRange = VersionRange.Between(newMin, newMax) }
                Shrinked.Ok(match version1 with
                            | RootDependency _ -> RootDependency shrinkedDependency
                            | PackageDependency d -> 
                                PackageDependency { DefiningPackage = d.DefiningPackage
                                                    DependentPackage = shrinkedDependency })
        | _ -> Shrinked.Conflict(version1, version2)
    | _ -> s1

let private addDependency package dependencies newDependency =
    let newDependency = Shrinked.Ok newDependency    
    match Map.tryFind package dependencies with
    | Some oldDependency -> Map.add package (shrink(oldDependency,newDependency)) dependencies
    | None -> Map.add package newDependency dependencies
    
let private mergeDependencies (discovery : IDiscovery) (definingPackage:Package) version dependencies = 
    let mutable newDependencies = dependencies
    for p in discovery.GetDirectDependencies(definingPackage.SourceType, definingPackage.Source, definingPackage.Name, version) do
        let newDependency = 
            PackageDependency { DefiningPackage = { definingPackage with VersionRange = Exactly version}
                                DependentPackage = 
                                    { Name = p.Name
                                      VersionRange = p.VersionRange
                                      SourceType = p.SourceType
                                      Source = p.Source } }
        newDependencies <- addDependency p.Name newDependencies newDependency
    newDependencies

let Resolve(discovery : IDiscovery, dependencies:Package seq) =      
    let rec analyzeGraph fixedDependencies (dependencies:Map<string,Shrinked>) =
        if Map.isEmpty dependencies then fixedDependencies else
        let current = Seq.head dependencies
        let resolvedName = current.Key

        match current.Value with
        | Shrinked.Conflict(c1,c2) -> analyzeGraph (Map.add resolvedName (ResolvedVersion.Conflict(c1,c2)) fixedDependencies) (Map.remove resolvedName dependencies)
        | Ok referencedPackage -> 
            match Map.tryFind resolvedName fixedDependencies with
            | Some (Resolved dependency) -> 
                match dependency.DependentPackage.VersionRange with
                | Exactly fixedVersion -> if referencedPackage.DependentPackage.VersionRange.IsInRange fixedVersion then fixedDependencies else failwith "Conflict"
                | _ -> failwith "Not allowed"
            | _ ->            
                let maxVersion = 
                    discovery.GetVersions resolvedName
                    |> Seq.filter referencedPackage.DependentPackage.VersionRange.IsInRange
                    |> Seq.max

                let resolvedPackage =
                    { Name = resolvedName
                      VersionRange = Exactly maxVersion
                      SourceType = referencedPackage.DependentPackage.SourceType
                      Source = referencedPackage.DependentPackage.Source }
                let resolvedDependency = 
                    match referencedPackage with
                    | RootDependency p -> 
                        RootDependency resolvedPackage
                    | PackageDependency d -> 
                        PackageDependency { DefiningPackage = d.DefiningPackage
                                            DependentPackage = resolvedPackage }

                dependencies
                |> mergeDependencies discovery referencedPackage.DependentPackage maxVersion
                |> Map.remove resolvedName
                |> analyzeGraph (Map.add resolvedName (ResolvedVersion.Resolved resolvedDependency) fixedDependencies)

    dependencies
    |> Seq.map (fun p -> 
           p.Name, 
           RootDependency { Name = p.Name
                            VersionRange = p.VersionRange
                            SourceType = p.SourceType
                            Source = p.Source })
    |> Seq.fold (fun m (p, d) -> addDependency p m d) Map.empty
    |> analyzeGraph Map.empty