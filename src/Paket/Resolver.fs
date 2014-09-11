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
        | Range(_, min1, max1, _), Specific v2 when min1 <= v2 && max1 > v2 -> s2
        | Specific v1, Range(_, min2, max2, _) when min2 <= v1 && max2 > v1 -> s2
        | Range(_, min1, max1, _), Range(_, min2, max2, _) -> 
            let newMin = max min1 min2
            let newMax = min max1 max2
            if newMin > newMax then Shrinked.Conflict(version1, version2)
            else 
                let shrinkedDependency = 
                    { version1.Referenced with VersionRange = VersionRange.Range(Closed, newMin, newMax, Open) }
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
let Resolve(force, discovery : IDiscovery, rootDependencies:UnresolvedPackage seq) =    
    let rec analyzeGraph processed (openDependencies:Map<string,Shrinked>) =
        if Map.isEmpty openDependencies then processed else
        let current = Seq.head openDependencies
        let resolvedName = current.Key

        match current.Value with
        | Shrinked.Conflict(c1,c2) -> 
            let resolved = Map.add resolvedName (ResolvedDependency.Conflict(c1,c2)) processed
            analyzeGraph resolved (Map.remove resolvedName openDependencies)
        | Ok dependency -> 
            let originalPackage = dependency.Referenced
           
            tracefn "  %s %s"  originalPackage.Name (originalPackage.VersionRange.ToString())
            match Map.tryFind resolvedName processed with
            | Some (Resolved package') -> 
                if not <| dependency.Referenced.VersionRange.IsInRange package'.Version then
                    let resolved =
                        processed 
                        |> Map.remove resolvedName
                        |> Map.add resolvedName (ResolvedDependency.ResolvedConflict(package',dependency))
                        
                    openDependencies
                    |> Map.remove resolvedName
                    |> analyzeGraph resolved
                else                    
                    openDependencies
                    |> Map.remove resolvedName
                    |> analyzeGraph processed
            | _ ->
                let allVersions = 
                    discovery.GetVersions(originalPackage.Sources,resolvedName) 
                    |> Async.RunSynchronously
                    |> Seq.concat
                    |> Seq.toList
                    |> List.map SemVer.parse

                let versions =                
                    allVersions
                    |> List.filter originalPackage.VersionRange.IsInRange

                if versions = [] then
                    allVersions
                    |> List.map (fun v -> v.ToString())
                    |> fun xs -> String.Join(Environment.NewLine + "  ", xs)
                    |> failwithf "No package found that matches %s %A on %A.%sVersion available:%s  %s" originalPackage.Name (originalPackage.VersionRange.ToString()) originalPackage.Sources Environment.NewLine Environment.NewLine

                let resolvedVersion = 
                    match dependency with
                    | FromRoot _ -> List.max versions
                    | FromPackage d ->
                        match originalPackage.ResolverStrategy with
                        | ResolverStrategy.Max -> List.max versions
                        | ResolverStrategy.Min -> List.min versions

                let packageDetails = 
                    discovery.GetPackageDetails(force, originalPackage.Sources, originalPackage.Name, originalPackage.ResolverStrategy, resolvedVersion.ToString()) 
                    |> Async.RunSynchronously

                let resolvedPackage:ResolvedPackage =
                    { Name = resolvedName
                      Version = resolvedVersion
                      DirectDependencies = 
                        packageDetails.DirectDependencies 
                        |> List.map (fun p -> p.Name,p.VersionRange)
                      Source = packageDetails.Source }

                let resolvedDependency = ResolvedDependency.Resolved resolvedPackage
                                            
                let mutable dependencies = openDependencies

                for dependentPackage in packageDetails.DirectDependencies do
                    let newDependency = 
                        FromPackage { Defining = { originalPackage with VersionRange = VersionRange.Exactly(resolvedVersion.ToString()) }
                                      Referenced = 
                                          { Name = dependentPackage.Name
                                            VersionRange = dependentPackage.VersionRange
                                            DirectDependencies = []
                                            ResolverStrategy = originalPackage.ResolverStrategy
                                            Sources = dependentPackage.Sources } }
                    dependencies <- addDependency dependentPackage.Name dependencies newDependency

                let resolved = Map.add resolvedName resolvedDependency processed

                dependencies
                |> Map.remove resolvedName
                |> analyzeGraph resolved

    
    rootDependencies
    |> Seq.map (fun p -> p.Name, FromRoot p)
    |> Seq.fold (fun m (p, d) -> addDependency p m d) Map.empty
    |> analyzeGraph Map.empty
