/// Contains logic which helps to resolve the dependency graph.
module Paket.PackageResolver

open Paket
open Paket.Logging
open System.Collections.Generic

type ExploredWorld(getVersionsF, getPackageDetailsF,rootDependencies:UnresolvedPackage list) =
    let exploredPackages = Dictionary<string*SemVerInfo,ResolvedPackage>()
    let allVersions = new Dictionary<string,SemVerInfo list>()

    member __.RootDependencies = rootDependencies

    member __.ExploredPackages = exploredPackages
    
    member __.GetExploredPackage(sources,packageName,version) =
        match exploredPackages.TryGetValue <| (packageName,version) with
        | true,package -> package
        | false,_ ->
            let packageDetails : PackageDetails = getPackageDetailsF sources packageName (version.ToString())
            let explored =
                { Name = packageDetails.Name
                  Version = version
                  DirectDependencies = packageDetails.DirectDependencies 
                  Source = packageDetails.Source }
            exploredPackages.Add((packageName,version),explored)
            explored
        

    member __.GetAllVersions(sources,packageName) =
        match allVersions.TryGetValue packageName with
        | false,_ ->
            verbosefn "Getting versions for %s" packageName
            let versions = getVersionsF sources packageName
            allVersions.Add(packageName,versions)
            versions
        | true,versions -> versions

    member this.Resolve() =
        let isOk (filteredVersions:FilteredVersions) =
            filteredVersions
            |> Map.forall (fun _ v -> 
                match v with
                | [x] -> true
                | _ -> false)
        
        let rec improveModel (filteredVersions,packages,closed:UnresolvedPackage list,stillOpen:UnresolvedPackage list) =
            match stillOpen with
            | dependency::rest ->
                let compatibleVersions = 
                    match Map.tryFind dependency.Name filteredVersions with
                    | None -> this.GetAllVersions(dependency.Sources,dependency.Name)
                    | Some versions -> versions
                    |> List.filter dependency.VersionRange.IsInRange
                    
                let sorted =                
                    match dependency.ResolverStrategy with
                    | Max -> List.sort compatibleVersions |> List.rev
                    | Min -> List.sort compatibleVersions
                
                let state = ref (Conflict(closed,stillOpen))
                for versionToExplore in sorted do
                    match !state with
                    | Conflict _ ->
                        let exploredPackage = this.GetExploredPackage(dependency.Sources,dependency.Name,versionToExplore)
                        let newFilteredVersion = Map.add dependency.Name [versionToExplore] filteredVersions
                        let newDependencies =
                            exploredPackage.DirectDependencies
                            |> List.map (fun (n,v) -> {dependency with Name = n; VersionRange = v })
                            |> List.filter (fun d -> List.exists ((=) d) closed |> not)
                    
                        state := improveModel (newFilteredVersion,exploredPackage::packages,dependency::closed,newDependencies @ rest)
                    | Ok _ -> ()
                !state
            | [] -> 
                if isOk filteredVersions then 
                    Ok(packages |> Seq.fold (fun map p -> Map.add p.Name p map) Map.empty) 
                else 
                    Conflict(closed,stillOpen)
            
        improveModel(Map.empty,[],[],rootDependencies)        


/// Resolves all direct and indirect dependencies
let Resolve(getVersionsF, getPackageDetailsF, rootDependencies:UnresolvedPackage seq) : Resolved =    
    let explored = ExploredWorld(getVersionsF, getPackageDetailsF, rootDependencies |> Seq.toList)
    
    explored.Resolve()