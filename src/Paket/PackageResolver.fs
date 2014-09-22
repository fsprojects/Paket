/// Contains logic which helps to resolve the dependency graph.
module Paket.PackageResolver

open Paket
open Paket.Logging
open System.Collections.Generic

/// Resolves all direct and indirect dependencies
let Resolve(getVersionsF, getPackageDetailsF, rootDependencies:UnresolvedPackage list) =
    tracefn "Resolving packages:" 
    let exploredPackages = Dictionary<string*SemVerInfo,ResolvedPackage>()
    let allVersions = new Dictionary<string,SemVerInfo list>()
    
    let getExploredPackage(sources,packageName:string,version) =
        match exploredPackages.TryGetValue <| (packageName.ToLower(),version) with
        | true,package -> package
        | false,_ ->
            tracefn "    - exploring %s %s" packageName (version.ToString())
            let packageDetails : PackageDetails = getPackageDetailsF sources packageName (version.ToString())
            let explored =
                { Name = packageDetails.Name
                  Version = version
                  DirectDependencies = packageDetails.DirectDependencies 
                  Source = packageDetails.Source }
            exploredPackages.Add((packageName.ToLower(),version),explored)
            explored
        
    let getAllVersions(sources,packageName) =
        match allVersions.TryGetValue packageName with
        | false,_ ->
            tracefn "  - fetching versions for %s" packageName
            let versions = getVersionsF sources packageName
            allVersions.Add(packageName,versions)
            versions
        | true,versions -> versions

    let rec improveModel (filteredVersions,packages,closed:Set<UnresolvedPackage>,stillOpen:UnresolvedPackage list) =
        match stillOpen with
        | dependency::rest ->            
            let compatibleVersions = 
                match Map.tryFind dependency.Name filteredVersions with
                | None -> getAllVersions(dependency.Sources,dependency.Name)
                | Some versions -> versions
                |> List.filter dependency.VersionRange.IsInRange
                    
            let sorted =                
                match dependency.ResolverStrategy with
                | Max -> List.sort compatibleVersions |> List.rev
                | Min -> List.sort compatibleVersions
                            
            sorted 
            |> List.fold (fun state versionToExplore ->
                match state with
                | Conflict _ ->
                    let exploredPackage = getExploredPackage(dependency.Sources,dependency.Name,versionToExplore)
                    let newFilteredVersion = Map.add dependency.Name [versionToExplore] filteredVersions
                    let newDependencies =
                        exploredPackage.DirectDependencies
                        |> List.map (fun (n,v) -> {dependency with Name = n; VersionRange = v })
                        |> List.filter (fun d -> Set.contains d closed |> not)
                    
                    improveModel (newFilteredVersion,exploredPackage::packages,Set.add dependency closed,newDependencies @ rest)
                | Ok _ -> state)
                  (Conflict(closed,stillOpen))
        | [] -> 
            let isOk =
                filteredVersions
                |> Map.forall (fun _ v -> 
                    match v with
                    | [_] -> true
                    | _ -> false)
        
            if isOk then
                Ok(packages |> Seq.fold (fun map p -> Map.add p.Name p map) Map.empty) 
            else 
                Conflict(closed,stillOpen)
            
    improveModel(Map.empty,[],Set.empty,rootDependencies)