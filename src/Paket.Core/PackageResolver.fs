/// Contains logic which helps to resolve the dependency graph.
module Paket.PackageResolver

open Paket
open Paket.Domain
open Paket.Requirements
open Paket.Logging
open System.Collections.Generic
open System
open Paket.PackageSources

type DependencySet = Set<PackageName * VersionRequirement * FrameworkRestrictions>

module DependencySetFilter = 
    let isIncluded (restriction:FrameworkRestriction) (dependency:PackageName * VersionRequirement * FrameworkRestrictions) =
        let _,_,restrictions = dependency
        if Seq.isEmpty restrictions then true else
        match restriction with
        | FrameworkRestriction.Exactly v1 -> 
            restrictions 
            |> Seq.filter (fun r2 -> restriction.IsSameCategoryAs(r2) = Some(true))
            |> Seq.exists (fun r2 ->
                match r2 with
                | FrameworkRestriction.Exactly v2 when v1 = v2 -> true
                | FrameworkRestriction.AtLeast v2 when v1 >= v2 -> true
                | FrameworkRestriction.Between(v2,v3) when v1 >= v2 && v1 < v3 -> true
                | _ -> false)
        | FrameworkRestriction.AtLeast v1 -> 
            restrictions 
            |> Seq.filter (fun r2 -> restriction.IsSameCategoryAs(r2) = Some(true))
            |> Seq.exists (fun r2 ->
                match r2 with
                | FrameworkRestriction.Exactly v2 when v1 <= v2 -> true
                | FrameworkRestriction.AtLeast v2 when v1 <= v2 -> true
                | FrameworkRestriction.Between(v2,v3) when v1 <= v2 && v1 < v3 -> true
                | _ -> false)
        | _ -> true

    let filterByRestrictions (restrictions:FrameworkRestriction seq) (dependencies:DependencySet) : DependencySet = 
        restrictions
        |> Seq.fold (fun currentSet restriction -> 
            currentSet
            |> Set.filter (isIncluded restriction)) dependencies

/// Represents package details
type PackageDetails =
    { Name : PackageName
      Source : PackageSource
      DownloadLink : string
      LicenseUrl : string
      Unlisted : bool
      DirectDependencies : DependencySet }

/// Represents data about resolved packages
type ResolvedPackage =
    { Name : PackageName
      Version : SemVerInfo
      Dependencies : DependencySet
      Unlisted : bool
      Settings : InstallSettings
      Source : PackageSource }

    override this.ToString() = sprintf "%O %O" this.Name this.Version

type PackageResolution = Map<PackageName, ResolvedPackage>

let cleanupNames (model : PackageResolution) : PackageResolution = 
    model
    |> Map.map (fun _ package ->
        { package with 
            Dependencies = 
                package.Dependencies 
                |> Set.map (fun (name, v, d) -> model.[name].Name, v, d) })

[<RequireQualifiedAccess>]
type Resolution =
| Ok of PackageResolution
| Conflict of Set<ResolvedPackage> * Set<PackageRequirement> * Set<PackageRequirement> * (PackageName -> SemVerInfo seq)
    with
    member this.GetModelOrFail() =
        match this with
        | Resolution.Ok model -> model
        | Resolution.Conflict(resolved,closed,stillOpen,getVersionF) ->

            let errorText = ref ""

            let addToError text = errorText := !errorText + Environment.NewLine + text

            let traceUnresolvedPackage (r : PackageRequirement) =
                addToError <| sprintf "  Could not resolve package %O:" r.Name

                let conflicts =
                    closed
                    |> Set.union stillOpen
                    |> Set.add r
                    |> Seq.filter (fun x -> x.Name = r.Name)
                    |> Seq.sortBy (fun x -> x.Parent)
                    |> Seq.toList

                conflicts
                |> Seq.iter (fun x ->
                        match x.Parent with
                        | DependenciesFile _ ->
                            sprintf "   - Dependencies file requested: %O" x.VersionRequirement |> addToError
                        | Package(parentName,version) ->
                            sprintf "   - %O %O requested: %O" parentName version x.VersionRequirement
                            |> addToError)

                match conflicts with
                | [c] ->
                    match getVersionF c.Name |> Seq.toList with
                    | [] -> sprintf "   - No versions available." |> addToError
                    | avalaibleVersions ->
                        sprintf "   - Available versions:" |> addToError
                        for v in avalaibleVersions do 
                            sprintf "     - %O" v |> addToError
                | _ -> ()

            addToError "There was a version conflict during package resolution."

            if not resolved.IsEmpty then
                addToError "  Resolved packages:"
                for resolvedPackage in resolved do
                    sprintf "   - %O %O" resolvedPackage.Name resolvedPackage.Version |> addToError

            stillOpen
            |> Seq.head
            |> traceUnresolvedPackage

            addToError "  Please try to relax some conditions."
            failwith !errorText


let calcOpenRequirements (exploredPackage:ResolvedPackage,globalFrameworkRestrictions,versionToExplore,dependency,closed:Set<PackageRequirement>,stillOpen:Set<PackageRequirement>) =
    let dependenciesByName =
        // there are packages which define multiple dependencies to the same package
        // we just take the latest one - see #567
        let hashSet = new HashSet<_>()
        exploredPackage.Dependencies
        |> Set.filter (fun (name,_,_) -> hashSet.Add name)

    let rest = Set.remove dependency stillOpen

    dependenciesByName
    |> Set.map (fun (n, v, restriction) -> 
         let newRestrictions = 
             filterRestrictions restriction exploredPackage.Settings.FrameworkRestrictions 
             |> filterRestrictions globalFrameworkRestrictions
         { dependency with Name = n
                           VersionRequirement = v
                           Parent = Package(dependency.Name, versionToExplore)
                           Settings = { dependency.Settings with FrameworkRestrictions = newRestrictions } })
    |> Set.filter (fun d ->
        stillOpen
        |> Seq.append closed
        |> Seq.exists (fun x ->
            x.Name = d.Name && 
                (x = d ||
                 x.VersionRequirement.Range.IsIncludedIn d.VersionRequirement.Range ||
                 x.VersionRequirement.Range.IsGlobalOverride))
        |> not)
    |> Set.union rest

type Resolved = {
    ResolvedPackages : Resolution
    ResolvedSourceFiles : ModuleResolver.ResolvedSourceFile list }

type UpdateMode =
    | UpdatePackage of GroupName * PackageName
    | UpdateGroup of GroupName
    | Install
    | UpdateAll

/// Resolves all direct and transitive dependencies
let Resolve(groupName:GroupName, sources, getVersionsF, getPackageDetailsF, globalFrameworkRestrictions, (rootDependencies:PackageRequirement Set), updateMode : UpdateMode) =
    tracefn "Resolving packages for group %O:" groupName
    let startWithPackage = 
        match updateMode with
        | UpdatePackage(_,p) -> Some p
        | _ -> None

    let rootSettings =
        rootDependencies
        |> Seq.map (fun x -> x.Name,x.Settings)
        |> dict

    let exploredPackages = Dictionary<PackageName*SemVerInfo,ResolvedPackage>()
    let conflictHistory = Dictionary<PackageName,int>()

    let getExploredPackage(dependency:PackageRequirement,version) =
        let newRestrictions = filterRestrictions dependency.Settings.FrameworkRestrictions globalFrameworkRestrictions
        match exploredPackages.TryGetValue <| (dependency.Name,version) with
        | true,package -> 
            match dependency.Parent with
            | PackageRequirementSource.DependenciesFile(_) -> 
                let package = { package with Settings = { package.Settings with FrameworkRestrictions = newRestrictions } }
                exploredPackages.[(dependency.Name,version)] <- package
                package
            | _ -> package
        | false,_ ->
            match updateMode with
            | Install -> tracefn  " - %O %A" dependency.Name version
            | _ ->
                match dependency.VersionRequirement.Range with
                | Specific _ when dependency.Parent.IsRootRequirement() -> traceWarnfn " - %O is pinned to %O" dependency.Name version
                | _ -> tracefn  " - %O %A" dependency.Name version

            let packageDetails : PackageDetails = getPackageDetailsF sources dependency.Name version
            let restrictedDependencies = DependencySetFilter.filterByRestrictions newRestrictions packageDetails.DirectDependencies
            let settings =
                match dependency.Parent with
                | DependenciesFile(_) -> dependency.Settings
                | Package(_) -> 
                    match rootSettings.TryGetValue packageDetails.Name with
                    | true, s -> s + dependency.Settings 
                    | _ -> dependency.Settings 

            let explored =
                { Name = packageDetails.Name
                  Version = version
                  Dependencies = restrictedDependencies
                  Unlisted = packageDetails.Unlisted
                  Settings = settings.AdjustWithSpecialCases packageDetails.Name
                  Source = packageDetails.Source }
            exploredPackages.Add((dependency.Name,version),explored)
            explored

    let rec step (filteredVersions:Map<PackageName, (SemVerInfo list * bool)>,selectedPackageVersions:Set<ResolvedPackage>,closedRequirements:Set<PackageRequirement>,openRequirements:Set<PackageRequirement>) =
        if Set.isEmpty openRequirements then
            // we're done. re-check if we have a valid resolution and return it
            let isOk =
                filteredVersions
                |> Map.forall (fun _ v ->
                    match v with
                    | [_],_ -> true
                    | _ -> false)

            if isOk then
                let resolution =
                    selectedPackageVersions 
                    |> Seq.fold (fun map p -> Map.add p.Name p map) Map.empty
                    |> cleanupNames

                Resolution.Ok(resolution)
            else
                Resolution.Conflict(selectedPackageVersions,closedRequirements,openRequirements,getVersionsF sources ResolverStrategy.Max groupName)
        else
            verbosefn "  %d packages in resolution. %d requirements left" selectedPackageVersions.Count openRequirements.Count
            
            let currentRequirement =
                let currentMin = ref (Seq.head openRequirements)
                let currentBoost = ref 0
                for d in openRequirements do
                    let boost = 
                        match conflictHistory.TryGetValue d.Name with
                        | true,c -> -c
                        | _ -> 0
                    if PackageRequirement.Compare(d,!currentMin,startWithPackage,boost,!currentBoost) = -1 then
                        currentMin := d
                        currentBoost := boost
                !currentMin

            verbosefn "  Trying to resolve %O" currentRequirement

            let availableVersions = ref Seq.empty
            let compatibleVersions = ref Seq.empty
            let globalOverride = ref false
            let resolverStrategy =
                if currentRequirement.Parent.IsRootRequirement() then
                    ResolverStrategy.Max 
                else
                    currentRequirement.ResolverStrategy

            let currentRequirements =
                openRequirements
                |> Seq.filter (fun r -> currentRequirement.Name = r.Name)
                |> Seq.toList
     
            match Map.tryFind currentRequirement.Name filteredVersions with
            | None ->
                // we didn't select a version yet so all versions are possible

                let isInRange mapF ver =
                    (mapF currentRequirement).VersionRequirement.IsInRange ver &&
                    (currentRequirements |> Seq.forall (fun r -> (mapF r).VersionRequirement.IsInRange ver))

                availableVersions := 
                    match currentRequirement.VersionRequirement.Range with
                    | OverrideAll v -> Seq.singleton v
                    | Specific v -> Seq.singleton v
                    | _ -> getVersionsF sources resolverStrategy groupName currentRequirement.Name
                    |> Seq.cache

                let preRelease v =
                    v.PreRelease = None
                    || currentRequirement.VersionRequirement.PreReleases <> PreReleaseStatus.No
                    || match currentRequirement.VersionRequirement.Range with
                        | Specific v -> v.PreRelease <> None
                        | OverrideAll v -> v.PreRelease <> None
                        | _ -> false

                compatibleVersions := Seq.filter (isInRange id) (!availableVersions) |> Seq.cache
                if currentRequirement.VersionRequirement.Range.IsGlobalOverride then
                    globalOverride := true
                else
                    if Seq.isEmpty !compatibleVersions then
                        let prereleases = Seq.filter (isInRange (fun r -> r.IncludingPrereleases())) (!availableVersions) |> Seq.toList
                        let allPrereleases = prereleases |> List.filter (fun v -> v.PreRelease <> None) = prereleases
                        if allPrereleases then
                            availableVersions := Seq.ofList prereleases
                            compatibleVersions := Seq.ofList prereleases
            | Some(versions,globalOverride') -> 
                // we already selected a version so we can't pick a different
                globalOverride := globalOverride'
                availableVersions := List.toSeq versions
                if globalOverride' then
                    compatibleVersions := List.toSeq versions
                else
                    compatibleVersions := 
                        Seq.filter (fun v -> currentRequirement.VersionRequirement.IsInRange(v,currentRequirement.Parent.IsRootRequirement() |> not)) versions

            if Seq.isEmpty !compatibleVersions then
                // boost the conflicting package, in order to solve conflicts faster
                match conflictHistory.TryGetValue currentRequirement.Name with
                | true,count -> conflictHistory.[currentRequirement.Name] <- count + 1
                | _ -> conflictHistory.Add(currentRequirement.Name, 1)
                    
                if verbose then
                    tracefn "  Conflicts with:"
                    
                    closedRequirements
                    |> Set.union openRequirements
                    |> Seq.filter (fun d -> d.Name = currentRequirement.Name)
                    |> fun xs -> String.Join(Environment.NewLine + "    ",xs)
                    |> tracefn "    %s"

                    match filteredVersions |> Map.tryFind currentRequirement.Name with
                    | Some (v,_) -> tracefn "    Package %O was already pinned to %O" currentRequirement.Name v
                    | None -> ()

                    tracefn "    ==> Trying different resolution."

            let tryToImprove useUnlisted =
                let allUnlisted = ref true
                let state = ref (Resolution.Conflict(selectedPackageVersions,closedRequirements,openRequirements,getVersionsF sources ResolverStrategy.Max groupName))
                let isOk() = 
                    match !state with
                    | Resolution.Ok _ -> true
                    | _ -> false
                let versionsToExplore = ref !compatibleVersions

                while not (isOk()) && not (Seq.isEmpty !versionsToExplore) do
                    let versionToExplore = Seq.head !versionsToExplore
                    versionsToExplore := Seq.tail !versionsToExplore
                    let exploredPackage = getExploredPackage(currentRequirement,versionToExplore)

                    if exploredPackage.Unlisted && not useUnlisted then 
                        () 
                    else
                        let newFilteredVersions = Map.add currentRequirement.Name ([versionToExplore],!globalOverride) filteredVersions
                        
                        let newOpen = calcOpenRequirements(exploredPackage,globalFrameworkRestrictions,versionToExplore,currentRequirement,closedRequirements,openRequirements)
                        let selectedVersions =
                            selectedPackageVersions 
                            |> Set.filter (fun p -> p.Name <> exploredPackage.Name || p.Version <> exploredPackage.Version) // do we really need this line?
                        let newSelectedVersions = Set.add exploredPackage selectedVersions

                        state := step (newFilteredVersions,newSelectedVersions,Set.add currentRequirement closedRequirements,newOpen)
                        allUnlisted := exploredPackage.Unlisted && !allUnlisted

                !allUnlisted,!state

            match tryToImprove false with
            | true,Resolution.Conflict(_) -> tryToImprove true |> snd
            | _,x-> x

    step (Map.empty, Set.empty, Set.empty, rootDependencies)