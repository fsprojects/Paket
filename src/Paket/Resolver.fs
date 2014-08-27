module Paket.Resolver

open Paket

type private Shrinked =
| Ok of DefindedDependency
| Conflict of DefindedDependency * DefindedDependency


let private shrink(s1:Shrinked, s2:Shrinked) =
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
        | Between(min1, max1), Between(min2, max2) ->
            let newMin = max min1 min2
            let newMax = min max1 max2
            if newMin > newMax then Shrinked.Conflict(version1, version2) else
            Shrinked.Ok { version1 with ReferencedVersion = VersionRange.Between(newMin, newMax) } // TODO:
        | _ -> Shrinked.Conflict(version1, version2)
    | _ -> s1

let private addDependency package dependencies newDependency =
    let newDependency = Shrinked.Ok newDependency    
    match Map.tryFind package dependencies with
    | Some oldDependency -> Map.add package (shrink(oldDependency,newDependency)) dependencies
    | None -> Map.add package newDependency dependencies
    
let private mergeDependencies (discovery : IDiscovery) definingPackage definingVersion dependencies =
    let mutable newDependencies = dependencies

    for package,version in discovery.GetDirectDependencies(definingPackage, definingVersion) do
        let newDependency = { DefiningPackage = definingPackage; DefiningVersion = definingVersion; ReferencedPackage = package; ReferencedVersion = version}
        newDependencies <- addDependency package newDependencies newDependency            

    newDependencies

let Resolve(discovery : IDiscovery, dependencies:(string * VersionRange) seq) =      
    let rec analyzeGraph fixedDependencies (dependencies:Map<string,Shrinked>) =
        if Map.isEmpty dependencies then fixedDependencies else
        let current = Seq.head dependencies
        let definingPackage = current.Key

        match current.Value with
        | Shrinked.Conflict(c1,c2) -> analyzeGraph (Map.add definingPackage (ResolvedVersion.Conflict(c1,c2)) fixedDependencies) (Map.remove definingPackage dependencies)
        | Ok c -> 
            match Map.tryFind definingPackage fixedDependencies with
            | Some (Resolved fixedVersion) -> if c.ReferencedVersion.IsInRange fixedVersion then fixedDependencies else failwith "Conflict"
            | _ ->            
                let maxVersion = 
                    discovery.GetVersions definingPackage
                    |> Seq.filter c.ReferencedVersion.IsInRange
                    |> Seq.max


                dependencies
                |> mergeDependencies discovery definingPackage maxVersion
                |> Map.remove definingPackage
                |> analyzeGraph (Map.add definingPackage (ResolvedVersion.Resolved maxVersion) fixedDependencies)

    dependencies
    |> Seq.map (fun (p,v) -> p,{ DefiningPackage = ""; DefiningVersion = "";  ReferencedPackage = p; ReferencedVersion = v})
    |> Seq.fold (fun m (p,d) -> addDependency p m d) Map.empty
    |> analyzeGraph Map.empty