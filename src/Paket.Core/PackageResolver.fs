/// Contains logic which helps to resolve the dependency graph.
module Paket.PackageResolver

open Paket
open Paket.Requirements
open Paket.Logging
open System.Collections.Generic
open System
open Paket.PackageSources

/// Represents package details
type PackageDetails =
    { Name : string
      Source : PackageSource
      DownloadLink : string
      Unlisted : bool
      DirectDependencies :  (string * VersionRequirement * (FrameworkIdentifier option)) Set }

/// Represents data about resolved packages
type ResolvedPackage =
    { Name : string
      Version : SemVerInfo
      Dependencies : (string * VersionRequirement * (FrameworkIdentifier option)) Set
      Unlisted : bool
      Source : PackageSource }

    override this.ToString() = sprintf "%s %s" this.Name (this.Version.ToString())

type PackageResolution = Map<string , ResolvedPackage>

[<RequireQualifiedAccess>]
type ResolvedPackages =
| Ok of PackageResolution
| Conflict of Set<PackageRequirement> * Set<PackageRequirement>
    with
    member this.GetModelOrFail() =
        match this with
        | ResolvedPackages.Ok model -> model
        | ResolvedPackages.Conflict(closed,stillOpen) ->

            let errorText = ref ""

            let addToError text = errorText := !errorText + Environment.NewLine + text

            let traceUnresolvedPackage (x : PackageRequirement) =
                match x.Parent with
                | DependenciesFile _ ->
                    sprintf "    - %s %s" x.Name (x.VersionRequirement.ToString())
                | Package(name,version) ->
                    sprintf "    - %s %s%s       - from %s %s" x.Name (x.VersionRequirement.ToString()) Environment.NewLine 
                        name (version.ToString())
                |> addToError

            addToError "Error in resolution."

            if not closed.IsEmpty then
                addToError "  Resolved:"
                for x in closed do
                   traceUnresolvedPackage x

            addToError "  Can't resolve:"
            stillOpen
            |> Seq.head
            |> traceUnresolvedPackage

            addToError " Please try to relax some conditions."
            failwith !errorText


type Resolved = {
    ResolvedPackages : ResolvedPackages
    ResolvedSourceFiles : ModuleResolver.ResolvedSourceFile list }

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
                  Unlisted = packageDetails.Unlisted
                  Source = packageDetails.Source }
            exploredPackages.Add((packageName.ToLower(),version),explored)
            explored

    let getAllVersions(sources,packageName:string) =
        match allVersions.TryGetValue(packageName.ToLower()) with
        | false,_ ->
            tracefn "  - fetching versions for %s" packageName
            let versions = getVersionsF(sources,packageName)
            allVersions.Add(packageName.ToLower(),versions)
            versions
        | true,versions -> versions

    let isIncluded (vr1 : VersionRange, vr2 : VersionRange) =         
        match vr1, vr2 with
        | Minimum v1, Minimum v2 when v1 <= v2 -> true
        | Minimum v1, Specific v2 when v1 <= v2 -> true
        | Specific v1, Specific v2 when v1 = v2 -> true
        | Range(_, min1, max1, _), Specific v2 when min1 <= v2 && max1 >= v2 -> true
        | GreaterThan v1, GreaterThan v2 when v1 < v2 -> true
        | GreaterThan v1, Specific v2 when v1 < v2 -> true
        | _ -> false

    let rec improveModel (filteredVersions:Map<string , (SemVerInfo list * bool)>,packages:ResolvedPackage list,closed:Set<PackageRequirement>,stillOpen:Set<PackageRequirement>) =
        if Set.isEmpty stillOpen then
            let isOk =
                filteredVersions
                |> Map.forall (fun _ v ->
                    match v with
                    | [_],_ -> true
                    | _ -> false)

            if isOk then
                ResolvedPackages.Ok(packages |> Seq.fold (fun map p -> Map.add (p.Name.ToLower()) p map) Map.empty)
            else
                ResolvedPackages.Conflict(closed,stillOpen)
        else
            let dependency = Seq.head stillOpen
            let rest = stillOpen |> Set.remove dependency
     
            let allVersions,compatibleVersions,globalOverride = 
                match Map.tryFind dependency.Name filteredVersions with
                | None ->
                    let versions = 
                        match dependency.VersionRequirement.Range with
                        | Specific v -> 
                            let versions = [v]
                            allVersions.Add(dependency.Name.ToLower(),versions)
                            versions
                        | _ -> getAllVersions(dependency.Sources,dependency.Name)
                    if Seq.isEmpty versions then
                        failwithf "Couldn't retrieve versions for %s." dependency.Name
                    if dependency.VersionRequirement.Range.IsGlobalOverride then
                        versions,List.filter dependency.VersionRequirement.IsInRange versions,true
                    else
                        versions,List.filter dependency.VersionRequirement.IsInRange versions,false
                | Some(versions,globalOverride) -> 
                    if globalOverride then versions,versions,true else versions,List.filter dependency.VersionRequirement.IsInRange versions,false

            if compatibleVersions = [] then
                match dependency.Parent with
                | PackageRequirementSource.DependenciesFile _ ->                     
                    let versionText = String.Join(Environment.NewLine + "     - ",List.sort allVersions)
                    failwithf "Could not find compatible versions for top level dependency:%s     %A%s   Available versions:%s     - %s%s   Try to relax the dependency or allow prereleases." 
                        Environment.NewLine (dependency.ToString()) Environment.NewLine Environment.NewLine versionText Environment.NewLine
                | _ -> ()

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
                | ResolvedPackages.Conflict _ ->
                    let exploredPackage = getExploredPackage(dependency.Sources,dependency.Name,versionToExplore)    
                    if exploredPackage.Unlisted then state else                
                    let newFilteredVersion = Map.add dependency.Name ([versionToExplore],globalOverride) filteredVersions
                    let newDependencies =
                        exploredPackage.Dependencies
                        |> Set.map (fun (n,v,_) -> {dependency with Name = n; VersionRequirement = v; Parent = Package(dependency.Name,versionToExplore) })
                        |> Set.filter (fun d -> Set.contains d closed |> not)
                        |> Set.filter (fun d -> Set.contains d stillOpen |> not)
                        |> Set.filter (fun d ->
                            closed 
                            |> Seq.filter (fun x -> x.Name = d.Name)
                            |> Seq.exists (fun otherDep -> isIncluded (d.VersionRequirement.Range,otherDep.VersionRequirement.Range))
                            |> not)
                        |> Set.filter (fun d ->
                            rest 
                            |> Seq.filter (fun x -> x.Name = d.Name)
                            |> Seq.exists (fun otherDep -> isIncluded (d.VersionRequirement.Range,otherDep.VersionRequirement.Range))
                            |> not)

                    improveModel (newFilteredVersion,exploredPackage::packages,Set.add dependency closed,Set.union rest newDependencies)
                | ResolvedPackages.Ok _ -> state)
                    (ResolvedPackages.Conflict(closed,stillOpen))

    match improveModel (Map.empty, [], Set.empty, Set.ofList rootDependencies) with
    | ResolvedPackages.Conflict(_) as c -> c
    | ResolvedPackages.Ok model -> 
        // cleanup names
        ResolvedPackages.Ok(model |> Seq.fold (fun map x -> 
                                         let package = x.Value
                                         let cleanup = 
                                             { package with Dependencies = 
                                                                package.Dependencies 
                                                                |> Set.map (fun (name, v, d) -> model.[name.ToLower()].Name, v, d) }
                                         Map.add package.Name cleanup map) Map.empty)
