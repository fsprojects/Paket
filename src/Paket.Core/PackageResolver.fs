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
        let restrictions = restrictions |> getRestrictionList
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
                | FrameworkRestriction.AtLeast v2 -> true
                | FrameworkRestriction.Between(v2,v3) when v1 < v3 -> true
                | _ -> false)
        | FrameworkRestriction.Between (min, max) ->
            restrictions 
            |> Seq.filter (fun r2 -> restriction.IsSameCategoryAs(r2) = Some(true))
            |> Seq.exists (fun r2 ->
                match r2 with
                | FrameworkRestriction.Exactly v when v >= min && v < max -> true
                | FrameworkRestriction.AtLeast v when v < max -> true
                | FrameworkRestriction.Between(min',max') when max' >= min && min' < max -> true
                | _ -> false)
        | _ -> true

    let filterByRestrictions (restrictions:FrameworkRestrictions) (dependencies:DependencySet) : DependencySet = 
        match getRestrictionList restrictions with
        | [] -> dependencies
        | restrictions ->
            dependencies 
            |> Set.filter (fun dependency ->
                restrictions |> List.exists (fun r -> isIncluded r dependency))

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
| Conflict of Map<PackageName,ResolvedPackage> * Set<PackageRequirement> * Set<PackageRequirement> * Set<PackageRequirement> * PackageRequirement * (PackageName -> (SemVerInfo * PackageSource list) seq)
    with

    member this.GetConflicts() =
        match this with
        | Resolution.Ok(_) -> []
        | Resolution.Conflict(resolved,closed,stillOpen,conflicts,lastPackageRequirement,getVersionF) ->
            closed
            |> Set.union stillOpen
            |> Set.add lastPackageRequirement
            |> Seq.filter (fun x -> x.Name = lastPackageRequirement.Name)
            |> Seq.sortBy (fun x -> x.Parent)
            |> Seq.toList

    member this.GetErrorText(showResolvedPackages) =
        match this with
        | Resolution.Ok(_) -> ""
        | Resolution.Conflict(resolved,closed,stillOpen,conflicts,lastPackageRequirement,getVersionF) ->
            let errorText = System.Text.StringBuilder()

            let addToError text = errorText.AppendLine text |> ignore
           
            if showResolvedPackages && not resolved.IsEmpty then
                addToError "  Resolved packages:"
                for kv in resolved do
                    let resolvedPackage = kv.Value
                    sprintf "   - %O %O" resolvedPackage.Name resolvedPackage.Version |> addToError

            let reportConflicts (conflicts:PackageRequirement list) =
                let r = Seq.head conflicts
                addToError <| sprintf "  Could not resolve package %O:" r.Name
                let hasPrereleases = conflicts |> Seq.exists (fun r -> r.VersionRequirement.PreReleases <> PreReleaseStatus.No)
                conflicts
                |> List.iter (fun x ->
                        let vr = x.VersionRequirement.ToString() |> fun s -> if String.IsNullOrWhiteSpace s then ">= 0" else s
                        let pr = if hasPrereleases && x.VersionRequirement.PreReleases = PreReleaseStatus.No then " (no prereleases)" else ""

                        match x.Parent with
                        | DependenciesFile _ ->
                            sprintf "   - Dependencies file requested package %O: %s%s" r.Name vr pr |> addToError
                        | Package(parentName,version,_) ->
                            sprintf "   - %O %O requested package %O: %s%s" parentName version r.Name vr pr |> addToError)

            match this.GetConflicts() with
            | [] -> addToError <| sprintf "  Could not resolve package %O. Unknown resolution error." (Seq.head stillOpen)
            | [c] ->
                reportConflicts [c]
                match getVersionF c.Name |> Seq.toList with
                | [] -> sprintf "   - No versions available." |> addToError
                | avalaibleVersions ->
                    sprintf "   - Available versions:" |> addToError
                    for v in avalaibleVersions do 
                        sprintf "     - %O" v |> addToError
            | conflicts ->
                reportConflicts conflicts
            
            errorText.ToString()

    member this.GetModelOrFail() = 
        match this with
        | Resolution.Ok model -> model
        | Resolution.Conflict(_) -> 
            "There was a version conflict during package resolution." + Environment.NewLine +
                this.GetErrorText(true)  + Environment.NewLine +
                "  Please try to relax some conditions."
            |> failwithf "%s"

let calcOpenRequirements (exploredPackage:ResolvedPackage,globalFrameworkRestrictions,(versionToExplore,_),dependency,closed:Set<PackageRequirement>,stillOpen:Set<PackageRequirement>) =
    let dependenciesByName =
        // there are packages which define multiple dependencies to the same package
        // we just take the latest one - see #567
        let hashSet = new HashSet<_>()
        exploredPackage.Dependencies
        |> Set.filter (fun (name,_,_) -> hashSet.Add name)

    let rest = 
        stillOpen
        |> Set.remove dependency
    
    dependenciesByName
    |> Set.map (fun (n, v, restriction) -> 
        let newRestrictions = 
            filterRestrictions restriction exploredPackage.Settings.FrameworkRestrictions
            |> filterRestrictions globalFrameworkRestrictions
        { dependency with Name = n
                          VersionRequirement = v
                          Parent = Package(dependency.Name, versionToExplore, exploredPackage.Source)
                          Graph = [dependency] @ dependency.Graph
                          Settings = { dependency.Settings with FrameworkRestrictions = newRestrictions } })
    |> Set.filter (fun d ->
        closed
        |> Seq.exists (fun x ->
            x.Name = d.Name && 
               x.Settings.FrameworkRestrictions = d.Settings.FrameworkRestrictions &&
                (x = d ||
                 x.VersionRequirement.Range.IsIncludedIn d.VersionRequirement.Range ||
                 x.VersionRequirement.Range.IsGlobalOverride))
        |> not)
    |> Set.filter (fun d ->
        stillOpen
        |> Seq.exists (fun x -> x.Name = d.Name && (x = d || x.VersionRequirement.Range.IsGlobalOverride) && x.Settings.FrameworkRestrictions = d.Settings.FrameworkRestrictions)
        |> not)
    |> Set.union rest


type Resolved = {
    ResolvedPackages : Resolution
    ResolvedSourceFiles : ModuleResolver.ResolvedSourceFile list }

let getResolverStrategy globalStrategyForDirectDependencies globalStrategyForTransitives (allRequirementsOfCurrentPackage:Set<PackageRequirement>) (currentRequirement:PackageRequirement) =
    if currentRequirement.Parent.IsRootRequirement() && Set.count allRequirementsOfCurrentPackage = 1 then
        let combined = currentRequirement.ResolverStrategyForDirectDependencies ++ globalStrategyForDirectDependencies

        defaultArg combined ResolverStrategy.Max
    else
        let combined =
            (allRequirementsOfCurrentPackage
                |> List.ofSeq
                |> List.filter (fun x -> x.Depth > 0)
                |> List.sortBy (fun x -> x.Depth, x.ResolverStrategyForTransitives <> globalStrategyForTransitives, x.ResolverStrategyForTransitives <> Some ResolverStrategy.Max)
                |> List.map (fun x -> x.ResolverStrategyForTransitives)
                |> List.fold (++) None)
                ++ globalStrategyForTransitives
                    
        defaultArg combined ResolverStrategy.Max

type UpdateMode =
    | UpdateGroup of GroupName
    | UpdateFiltered of GroupName * PackageFilter
    | Install
    | UpdateAll

/// Resolves all direct and transitive dependencies
let Resolve(getVersionsF, getPackageDetailsF, groupName:GroupName, globalStrategyForDirectDependencies, globalStrategyForTransitives, globalFrameworkRestrictions, (rootDependencies:PackageRequirement Set), updateMode : UpdateMode) =
    tracefn "Resolving packages for group %O:" groupName
    let lastConflictReported = ref DateTime.Now

    let packageFilter =
        match updateMode with
        | UpdateFiltered (_, f) -> Some f
        | _ -> None

    let rootSettings =
        rootDependencies
        |> Seq.map (fun x -> x.Name,x.Settings)
        |> dict

    let exploredPackages = Dictionary<PackageName*SemVerInfo,ResolvedPackage>()
    let conflictHistory = Dictionary<PackageName,int>()
    let knownConflicts = HashSet<_>()
    let tryRelaxed = ref false

    let getExploredPackage(dependency:PackageRequirement,(version,packageSources)) =
        let key = dependency.Name,version
        match exploredPackages.TryGetValue key with
        | true,package -> 
            let newRestrictions = 
                if List.isEmpty (globalFrameworkRestrictions |> getRestrictionList) && (List.isEmpty (package.Settings.FrameworkRestrictions |> getRestrictionList) || List.isEmpty (dependency.Settings.FrameworkRestrictions |> getRestrictionList)) then [] else
                optimizeRestrictions ((package.Settings.FrameworkRestrictions  |> getRestrictionList) @ (dependency.Settings.FrameworkRestrictions |> getRestrictionList) @ (globalFrameworkRestrictions |> getRestrictionList))
            
            let package = { package with Settings = { package.Settings with FrameworkRestrictions = FrameworkRestrictionList newRestrictions } }
            exploredPackages.[key] <- package
            Some package
        | false,_ ->
            match updateMode with
            | Install -> tracefn  " - %O %A" dependency.Name version
            | _ ->
                match dependency.VersionRequirement.Range with
                | Specific _ when dependency.Parent.IsRootRequirement() -> traceWarnfn " - %O is pinned to %O" dependency.Name version
                | _ -> tracefn  " - %O %A" dependency.Name version

            let newRestrictions = filterRestrictions dependency.Settings.FrameworkRestrictions globalFrameworkRestrictions
            
            try
                let packageDetails : PackageDetails = getPackageDetailsF packageSources dependency.Name version

                let filteredDependencies = DependencySetFilter.filterByRestrictions newRestrictions packageDetails.DirectDependencies

                let settings =
                    match dependency.Parent with
                    | DependenciesFile(_) -> dependency.Settings
                    | Package(_) -> 
                        match rootSettings.TryGetValue packageDetails.Name with
                        | true, s -> s + dependency.Settings 
                        | _ -> dependency.Settings 

                let settings = settings.AdjustWithSpecialCases packageDetails.Name
                let explored =
                    { Name = packageDetails.Name
                      Version = version
                      Dependencies = filteredDependencies
                      Unlisted = packageDetails.Unlisted
                      Settings = settings
                      Source = packageDetails.Source }
                exploredPackages.Add(key,explored)
                Some explored
            with
            | _ -> None

    let getCompatibleVersions(relax,filteredVersions:Map<PackageName, ((SemVerInfo * PackageSource list) list * bool)>,openRequirements:Set<PackageRequirement>,currentRequirement:PackageRequirement) =
        verbosefn "  Trying to resolve %O" currentRequirement

        let availableVersions = ref Seq.empty
        let compatibleVersions = ref Seq.empty
        let globalOverride = ref false
       
        match Map.tryFind currentRequirement.Name filteredVersions with
        | None ->
            let allRequirementsOfCurrentPackage =
                openRequirements
                |> Set.filter (fun r -> currentRequirement.Name = r.Name)

            // we didn't select a version yet so all versions are possible
            let isInRange mapF (ver,_) =
                allRequirementsOfCurrentPackage
                |> Seq.forall (fun r -> (mapF r).VersionRequirement.IsInRange ver)

            let getSingleVersion v =
                match currentRequirement.Parent with
                | PackageRequirementSource.Package(_,_,parentSource) -> 
                    let sources = parentSource :: currentRequirement.Sources |> List.distinct
                    Seq.singleton (v,sources)
                | _ -> 
                    let sources : PackageSource list = currentRequirement.Sources |> List.sortBy (fun x -> String.containsIgnoreCase "nuget.org" x.Url |> not) 
                    Seq.singleton (v,sources)

            availableVersions :=
                match currentRequirement.VersionRequirement.Range with
                | OverrideAll v -> getSingleVersion v
                | Specific v -> getSingleVersion v
                | _ -> 
                    let resolverStrategy = getResolverStrategy globalStrategyForDirectDependencies globalStrategyForTransitives allRequirementsOfCurrentPackage currentRequirement
                    getVersionsF currentRequirement.Sources resolverStrategy groupName currentRequirement.Name
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
                    let allPrereleases = prereleases |> List.filter (fun (v,_) -> v.PreRelease <> None) = prereleases
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
                    Seq.filter (fun (v,_) -> currentRequirement.VersionRequirement.IsInRange(v,currentRequirement.Parent.IsRootRequirement() |> not)) versions

                if Seq.isEmpty !compatibleVersions then
                    let withPrereleases = Seq.filter (fun (v,_) -> currentRequirement.IncludingPrereleases().VersionRequirement.IsInRange(v,currentRequirement.Parent.IsRootRequirement() |> not)) versions
                    if relax then
                        compatibleVersions := withPrereleases
                    else
                        if Seq.isEmpty withPrereleases |> not then
                            tryRelaxed := true


        !availableVersions,!compatibleVersions,!globalOverride

    let getConflicts(filteredVersions:Map<PackageName, ((SemVerInfo * PackageSource list) list * bool)>,closedRequirements:Set<PackageRequirement>,openRequirements:Set<PackageRequirement>,currentRequirement:PackageRequirement) = 
        let allRequirements = 
            openRequirements
            |> Set.filter (fun r -> r.Graph |> List.contains currentRequirement |> not)
            |> Set.union closedRequirements

        knownConflicts
        |> Seq.map (fun (conflicts,selectedVersion) ->
            match selectedVersion with 
            | None when Set.isSubset conflicts allRequirements -> conflicts
            | Some(selectedVersion,_) ->
                let n = (Seq.head conflicts).Name
                match filteredVersions |> Map.tryFind n with
                | Some(v,_) when v = selectedVersion && Set.isSubset conflicts allRequirements -> conflicts
                | _ -> Set.empty
            | _ -> Set.empty)
        |> Set.unionMany

    let getCurrentRequirement (openRequirements:Set<PackageRequirement>) =
        let currentMin = ref (Seq.head openRequirements)
        let currentBoost = ref 0
        for d in openRequirements do
            let boost = 
                match conflictHistory.TryGetValue d.Name with
                | true,c -> -c
                | _ -> 0
            if PackageRequirement.Compare(d,!currentMin,packageFilter,boost,!currentBoost) = -1 then
                currentMin := d
                currentBoost := boost
        !currentMin

    let boostConflicts (filteredVersions:Map<PackageName, ((SemVerInfo * PackageSource list) list * bool)>,currentRequirement:PackageRequirement,conflictStatus:Resolution) = 
        // boost the conflicting package, in order to solve conflicts faster
        let isNewConflict =
            match conflictHistory.TryGetValue currentRequirement.Name with
            | true,count -> 
                conflictHistory.[currentRequirement.Name] <- count + 1
                false
            | _ -> 
                conflictHistory.Add(currentRequirement.Name, 1)
                true
                
        let conflicts = conflictStatus.GetConflicts() 
        match conflicts with
        | c::_  ->
            let selectedVersion = Map.tryFind c.Name filteredVersions
            let key = conflicts |> Set.ofList,selectedVersion
            knownConflicts.Add key |> ignore
            let reportThatResolverIsTakingLongerThanExpected = not isNewConflict && DateTime.Now - !lastConflictReported > TimeSpan.FromSeconds 10.
            if verbose then
                tracefn "%s" <| conflictStatus.GetErrorText(false)
                tracefn "    ==> Trying different resolution."
            if reportThatResolverIsTakingLongerThanExpected then
                traceWarnfn "%s" <| conflictStatus.GetErrorText(false)
                traceWarn "The process is taking longer than expected."
                traceWarn "Paket may still find a valid resolution, but this might take a while."
                lastConflictReported := DateTime.Now
        | _ -> ()
  

    let rec step (relax,filteredVersions:Map<PackageName, ((SemVerInfo * PackageSource list) list * bool)>,currentResolution:Map<PackageName,ResolvedPackage>,closedRequirements:Set<PackageRequirement>,openRequirements:Set<PackageRequirement>) =
        if Set.isEmpty openRequirements then 
            Resolution.Ok(cleanupNames currentResolution) 
        else
            verbosefn "  %d packages in resolution. %d requirements left" currentResolution.Count openRequirements.Count
        
            let currentRequirement = getCurrentRequirement openRequirements
            let conflicts = getConflicts(filteredVersions,closedRequirements,openRequirements,currentRequirement)
            if conflicts |> Set.isEmpty |> not then 
                Resolution.Conflict(currentResolution,closedRequirements,openRequirements,conflicts,Seq.head conflicts,getVersionsF currentRequirement.Sources ResolverStrategy.Max groupName) 
            else
                let availableVersions,compatibleVersions,globalOverride = getCompatibleVersions(relax,filteredVersions,openRequirements,currentRequirement)

                let conflictStatus = Resolution.Conflict(currentResolution,closedRequirements,openRequirements,Set.empty,currentRequirement,getVersionsF currentRequirement.Sources ResolverStrategy.Max groupName)
                if Seq.isEmpty compatibleVersions then
                    boostConflicts (filteredVersions,currentRequirement,conflictStatus) 

                let ready = ref false
                let state = ref conflictStatus
                let useUnlisted = ref false
                let allUnlisted = ref true

                while not !ready do
                    let trial = ref 0
                    let forceBreak = ref false
            
                    let isOk() = 
                        match !state with
                        | Resolution.Ok _ -> true
                        | _ -> false
                    let versionsToExplore = ref compatibleVersions

                    let shouldTryHarder trial =
                        if !forceBreak then false else
                        if isOk() || Seq.isEmpty !versionsToExplore then false else
                        if trial < 1 then true else
                        conflicts |> Set.isEmpty

                    while shouldTryHarder !trial do
                        trial := !trial + 1
                        let versionToExplore = Seq.head !versionsToExplore
                        versionsToExplore := Seq.tail !versionsToExplore
                        match getExploredPackage(currentRequirement,versionToExplore) with
                        | None -> ()
                        | Some exploredPackage ->
                            if exploredPackage.Unlisted && not !useUnlisted then 
                                () 
                            else
                                let newFilteredVersions = Map.add currentRequirement.Name ([versionToExplore],globalOverride) filteredVersions
                        
                                let newOpen = calcOpenRequirements(exploredPackage,globalFrameworkRestrictions,versionToExplore,currentRequirement,closedRequirements,openRequirements)
                                if newOpen = openRequirements then 
                                    failwithf "The resolver confused itself. The new open requirements are the same as the old ones. This will result in an endless loop.%sCurrent Requirement: %A%sRequirements: %A" Environment.NewLine currentRequirement Environment.NewLine newOpen

                                let newResolution = Map.add exploredPackage.Name exploredPackage currentResolution

                                let newClosed = Set.add currentRequirement closedRequirements

                                state := step (relax,newFilteredVersions,newResolution,newClosed,newOpen)

                                match !state with
                                | Resolution.Conflict(resolved,closed,stillOpen,conflicts,lastPackageRequirement,getVersionF)
                                    when
                                        (Set.isEmpty conflicts |> not) && 
                                          newResolution.Count > 1 &&
                                          (conflicts |> Set.exists (fun r -> r = currentRequirement || r.Graph |> List.contains currentRequirement) |> not) ->
                                        forceBreak := true
                                | _ -> ()

                                allUnlisted := exploredPackage.Unlisted && !allUnlisted

                    if not !useUnlisted && !allUnlisted && not (isOk()) then
                        useUnlisted := true
                    else
                        ready := true

                !state

    match step (false, Map.empty, Map.empty, Set.empty, rootDependencies) with
    | Resolution.Conflict(resolved,closed,stillOpen,_,_,_) as conflict ->
        if !tryRelaxed then
            conflictHistory.Clear()
            knownConflicts.Clear() |> ignore
            step (true, Map.empty, Map.empty, Set.empty, rootDependencies)
        else
            conflict
    | x -> x
