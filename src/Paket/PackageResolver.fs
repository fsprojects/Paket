/// Contains logic which helps to resolve the dependency graph.
module Paket.PackageResolver

open Paket
open Paket.Logging
open System.Collections.Generic

/// Resolves all direct and indirect dependencies
let Resolve(getVersionsF, getPackageDetailsF, rootDependencies:PackageRequirement list) =
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
                  Dependencies = packageDetails.DirectDependencies 
                  Source = packageDetails.Source }
            exploredPackages.Add((packageName.ToLower(),version),explored)
            explored
        
    let getAllVersions(sources,packageName:string) =
        match allVersions.TryGetValue(packageName.ToLower()) with
        | false,_ ->
            tracefn "  - fetching versions for %s" packageName
            let versions = getVersionsF sources packageName
            allVersions.Add(packageName.ToLower(),versions)
            versions
        | true,versions -> versions

    let rec improveModel (filteredVersions,packages:ResolvedPackage list,closed:Set<PackageRequirement>,stillOpen:Set<PackageRequirement>) =
        if Set.isEmpty stillOpen then
            let isOk =
                filteredVersions
                |> Map.forall (fun _ v -> 
                    match v with
                    | [_] -> true
                    | _ -> false)
        
            if isOk then
                Ok(packages |> Seq.fold (fun map p -> Map.add (p.Name.ToLower()) p map) Map.empty) 
            else 
                Conflict(closed,stillOpen)
        else
            let dependency = Seq.head stillOpen
            let rest = stillOpen |> Set.remove dependency
     
            let compatibleVersions = 
                match Map.tryFind dependency.Name filteredVersions with
                | None -> getAllVersions(dependency.Sources,dependency.Name)
                | Some versions -> versions
                |> List.filter dependency.VersionRequirement.IsInRange
                    
            let sorted =                
                match dependency.Parent with
                | DependenciesFile _ ->
                    List.sort compatibleVersions |> List.rev
                | _ ->
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
                        exploredPackage.Dependencies
                        |> List.map (fun (n,v) -> {dependency with Name = n; VersionRequirement = v; Parent = Package(dependency.Name,versionToExplore) })
                        |> List.filter (fun d -> Set.contains d closed |> not)
                        |> Set.ofList
                    
                    improveModel (newFilteredVersion,exploredPackage::packages,Set.add dependency closed,Set.union rest newDependencies)
                | Ok _ -> state)
                  (Conflict(closed,stillOpen))

            
    match improveModel (Map.empty, [], Set.empty, Set.ofList rootDependencies) with
    | Conflict(_) as c -> c
    | ResolvedPackages.Ok model -> 
        // cleanup names
        Ok(model |> Seq.fold (fun map x -> 
                        let package = x.Value
                        let cleanup = 
                            { package with Dependencies = 
                                               package.Dependencies 
                                               |> List.map (fun (name, v) -> model.[name.ToLower()].Name, v) }
                        Map.add package.Name cleanup map) Map.empty)