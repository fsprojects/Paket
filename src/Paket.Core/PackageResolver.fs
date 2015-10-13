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
            |> Seq.exists (fun r2 ->
                match r2 with
                | FrameworkRestriction.Exactly v2 when v1 = v2 -> true
                | FrameworkRestriction.AtLeast v2 when v1 >= v2 -> true
                | FrameworkRestriction.Between(v2,v3) when v1 >= v2 && v1 < v3 -> true
                | _ -> false)
        | FrameworkRestriction.AtLeast v1 -> 
            restrictions 
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

let createPackageRequirement parent (packageName, version, restrictions) = 
    { Name = packageName
      VersionRequirement = version
      ResolverStrategy = ResolverStrategy.Max
      Settings = parent.Settings
      Parent = Package(parent.Name, parent.Version) }

let rec getDependencyGraph packages package =
    let requirements =
        package.Dependencies
        |> Seq.map (createPackageRequirement package)
        |> List.ofSeq

    requirements @
    (requirements
    |> List.collect (fun r -> 
        packages
        |> List.filter (fun p -> p.Name = r.Name)
        |> List.collect (getDependencyGraph packages)))

let createPackageRequirements exclude resolution =
    let packages =
        resolution
        |> Map.toSeq
        |> Seq.map snd
        |> List.ofSeq

    let contains list package = list |> List.contains package.Name

    let transitive = 
        packages
        |> Seq.collect (fun d -> d.Dependencies |> Seq.map (fun (n,_,_) -> n))
        |> List.ofSeq

    packages
    |> List.filter ((contains transitive) >> not)
    |> List.filter ((contains exclude) >> not)
    |> List.collect (getDependencyGraph packages)

type PackageResolution = Map<PackageName, ResolvedPackage>

let allPrereleases versions = versions |> List.filter (fun v -> v.PreRelease <> None) = versions

let cleanupNames (model : PackageResolution) : PackageResolution = 
    model |> Seq.fold (fun map x -> 
                 let package = x.Value
                 let cleanup = 
                     { package with Dependencies = 
                                        package.Dependencies 
                                        |> Set.map (fun (name, v, d) -> model.[name].Name, v, d) }
                 Map.add package.Name cleanup map) Map.empty

[<RequireQualifiedAccess>]
type Resolution =
| Ok of PackageResolution
| Conflict of Set<PackageRequirement> * Set<PackageRequirement> * Set<PackageRequirement>
    with
    member this.GetModelOrFail() =
        match this with
        | Resolution.Ok model -> model
        | Resolution.Conflict(closed,stillOpen,requirements) ->

            let errorText = ref ""

            let addToError text = errorText := !errorText + Environment.NewLine + text

            let traceUnresolvedPackage (r : PackageRequirement) =
                addToError <| sprintf "  Could not resolve package %O:" r.Name

                closed
                |> Set.union requirements
                |> Seq.filter (fun x -> x.Name = r.Name)
                |> Seq.iter (fun x ->
                        let (PackageName name) = x.Name
                        match x.Parent with
                        | DependenciesFile _ ->
                            sprintf "   - Dependencies file requested %O" x.VersionRequirement |> addToError
                        | Package(PackageName parentName,version) ->
                            sprintf "   - %s %O requested %O" parentName version x.VersionRequirement
                            |> addToError)

                let (PackageName name) = r.Name
                match r.Parent with
                | DependenciesFile _ ->
                    sprintf "   - Dependencies file requested %O" r.VersionRequirement |> addToError
                | Package(PackageName parentName,version) ->
                    sprintf "   - %s %O requested %O" parentName version r.VersionRequirement
                    |> addToError

            addToError "Error in resolution."

            if not closed.IsEmpty then
                addToError "  Resolved:"
                for x in closed do
                    sprintf "   - %O %O" x.Name x.VersionRequirement |> addToError

            stillOpen
            |> Seq.head
            |> traceUnresolvedPackage

            addToError " Please try to relax some conditions."
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
        if Set.contains d stillOpen then false else
        if Set.contains d closed then false else
        if closed |> Seq.exists (fun x -> x.Name = d.Name && x.VersionRequirement.Range.IsIncludedIn d.VersionRequirement.Range) then false else
        rest |> Seq.exists (fun x -> x.Name = d.Name && x.VersionRequirement.Range.IsIncludedIn d.VersionRequirement.Range) |> not)
    |> Set.union rest

type Resolved = {
    ResolvedPackages : Resolution
    ResolvedSourceFiles : ModuleResolver.ResolvedSourceFile list }

type UpdateMode =
    | UpdatePackage of  GroupName * PackageName
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

    let rec step (filteredVersions:Map<PackageName, (SemVerInfo list * bool)>,selectedPackageVersions:ResolvedPackage list,closedRequirements:Set<PackageRequirement>,openRequirements:Set<PackageRequirement>) =
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

                Resolution.Ok(resolution)
            else
                Resolution.Conflict(closedRequirements,openRequirements,rootDependencies)
        else
            let packageCount = selectedPackageVersions |> List.length
            verbosefn "  %d packages in resolution. %d requirements left" packageCount openRequirements.Count
            
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
                        if allPrereleases prereleases then
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
                if currentRequirement.Parent.IsRootRequirement() then
                    let versionText = 
                        let versions = getVersionsF sources resolverStrategy groupName currentRequirement.Name |> Seq.toList

                        String.Join(Environment.NewLine + "     - ",List.sortDescending versions)
                    failwithf "Could not find compatible versions for top level dependency:%s     %A%s   Available versions:%s     - %s%s   Try to relax the dependency%s." 
                        Environment.NewLine (String.Join(Environment.NewLine + "     ", currentRequirements |> Seq.map string)) Environment.NewLine Environment.NewLine versionText Environment.NewLine
                          (if currentRequirement.VersionRequirement.PreReleases = PreReleaseStatus.No then " or allow prereleases" else "")
                else
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
                let state = ref (Resolution.Conflict(closedRequirements,openRequirements,openRequirements))
                let isOk() = 
                    match !state with
                    | Resolution.Ok _ -> true
                    | _ -> false
                let versionsToExplore = ref !compatibleVersions

                while not (isOk()) && not (Seq.isEmpty !versionsToExplore) do
                    let versionToExplore = Seq.head !versionsToExplore
                    versionsToExplore := Seq.tail !versionsToExplore
                    let exploredPackage = getExploredPackage(currentRequirement,versionToExplore)
                    if exploredPackage.Unlisted && not useUnlisted then () else
                    let newFilteredVersions = Map.add currentRequirement.Name ([versionToExplore],!globalOverride) filteredVersions
                        
                    let newOpen = calcOpenRequirements(exploredPackage,globalFrameworkRestrictions,versionToExplore,currentRequirement,closedRequirements,openRequirements)
                    let newPackages =
                        exploredPackage::(selectedPackageVersions |> List.filter (fun p -> p.Name <> exploredPackage.Name || p.Version <> exploredPackage.Version))

                    state := step (newFilteredVersions,newPackages,Set.add currentRequirement closedRequirements,newOpen)
                    allUnlisted := exploredPackage.Unlisted && !allUnlisted

                !allUnlisted,!state

            match tryToImprove false with
            | true,Resolution.Conflict(_) -> tryToImprove true |> snd
            | _,x-> x

    match step (Map.empty, [], Set.empty, rootDependencies) with
    | Resolution.Conflict(_) as c -> c
    | Resolution.Ok model -> Resolution.Ok(cleanupNames model)
