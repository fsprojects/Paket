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

    let filterByRestrictions (restrictions:FrameworkRestriction list) (dependencies:DependencySet) : DependencySet = 
        match restrictions with
        | [] -> dependencies
        | _ ->
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
| Conflict of Map<PackageName,ResolvedPackage> * Set<PackageRequirement> * Set<PackageRequirement> * PackageRequirement * (PackageName -> (SemVerInfo * PackageSource list) seq)
    with

    member this.GetConflicts() =
        match this with
        | Resolution.Ok(_) -> []
        | Resolution.Conflict(resolved,closed,stillOpen,lastPackageRequirement,getVersionF) ->
            closed
            |> Set.union stillOpen
            |> Set.add lastPackageRequirement
            |> Seq.filter (fun x -> x.Name = lastPackageRequirement.Name)
            |> Seq.sortBy (fun x -> x.Parent)
            |> Seq.toList

    member this.GetErrorText(showResolvedPackages) =
        match this with
        | Resolution.Ok(_) -> ""
        | Resolution.Conflict(resolved,closed,stillOpen,lastPackageRequirement,getVersionF) ->
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
                            sprintf "   - Dependencies file requested: %s%s" vr pr |> addToError
                        | Package(parentName,version,_) ->
                            sprintf "   - %O %O requested: %s%s" parentName version vr pr |> addToError)

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

let calcOpenRequirements (exploredPackage:ResolvedPackage,globalFrameworkRestrictions,(versionToExplore,_,_),dependency,closed:Set<PackageRequirement>,stillOpen:Set<PackageRequirement>) =
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

type UpdateMode =
    | UpdateGroup of GroupName
    | UpdateFiltered of GroupName * PackageFilter
    | Install
    | UpdateAll

/// Resolves all direct and transitive dependencies
let Resolve(groupName:GroupName, sources, getVersionsF, getPackageDetailsF, strategy, globalFrameworkRestrictions, (rootDependencies:PackageRequirement Set), updateMode : UpdateMode) =
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

    let getExploredPackage(dependency:PackageRequirement,(version,preferredSource,packageSources)) =
        let key = dependency.Name,version
        match exploredPackages.TryGetValue key with
        | true,package -> 
            let newRestrictions = 
                if List.isEmpty globalFrameworkRestrictions && (List.isEmpty package.Settings.FrameworkRestrictions || List.isEmpty dependency.Settings.FrameworkRestrictions) then [] else
                optimizeRestrictions (package.Settings.FrameworkRestrictions @ dependency.Settings.FrameworkRestrictions @ globalFrameworkRestrictions)
            
            let package = { package with Settings = { package.Settings with FrameworkRestrictions = newRestrictions } }
            exploredPackages.[key] <- package
            package
        | false,_ ->
            match updateMode with
            | Install -> tracefn  " - %O %A" dependency.Name version
            | _ ->
                match dependency.VersionRequirement.Range with
                | Specific _ when dependency.Parent.IsRootRequirement() -> traceWarnfn " - %O is pinned to %O" dependency.Name version
                | _ -> tracefn  " - %O %A" dependency.Name version

            let newRestrictions = filterRestrictions dependency.Settings.FrameworkRestrictions globalFrameworkRestrictions
            
            let packageDetails : PackageDetails = 
                match preferredSource with
                | None -> getPackageDetailsF packageSources dependency.Name version
                | Some preferredSource ->
                    try
                        getPackageDetailsF [preferredSource] dependency.Name version
                    with
                    | _ -> getPackageDetailsF (List.filter (fun x -> x <> preferredSource) packageSources) dependency.Name version

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
            explored

    let rec step (filteredVersions:Map<PackageName, ((SemVerInfo * PackageSource option * PackageSource list) list * bool)>,currentResolution:Map<PackageName,ResolvedPackage>,closedRequirements:Set<PackageRequirement>,openRequirements:Set<PackageRequirement>) =
        if Set.isEmpty openRequirements then Resolution.Ok(cleanupNames currentResolution) else
        verbosefn "  %d packages in resolution. %d requirements left" currentResolution.Count openRequirements.Count
        
        let currentRequirement =
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

        let conflictStatus = Resolution.Conflict(currentResolution,closedRequirements,openRequirements,currentRequirement,getVersionsF sources ResolverStrategy.Max groupName)
        let getConflicts() = 
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

        let conflicts = getConflicts()
        if conflicts |> Set.isEmpty |> not then Resolution.Conflict(currentResolution,closedRequirements,conflicts,Seq.head conflicts,getVersionsF sources ResolverStrategy.Max groupName) else

        verbosefn "  Trying to resolve %O" currentRequirement

        let availableVersions = ref Seq.empty
        let compatibleVersions = ref Seq.empty
        let globalOverride = ref false
       
        match Map.tryFind currentRequirement.Name filteredVersions with
        | None ->
            let currentRequirements =
                openRequirements
                |> Set.filter (fun r -> currentRequirement.Name = r.Name)

            let resolverStrategy =
                let combined =
                    (currentRequirements
                    |> List.ofSeq
                    |> List.filter (fun x -> x.Depth > 0)
                    |> List.sortBy (fun x -> x.Depth, x.ResolverStrategy <> strategy, x.ResolverStrategy <> Some ResolverStrategy.Max)
                    |> List.map (fun x -> x.ResolverStrategy)
                    |> List.fold (++) None)
                    ++ strategy
                    |> function | Some s -> s | None -> ResolverStrategy.Max

                match updateMode with
                | Install
                | UpdateAll
                | UpdateGroup _ ->
                    match currentRequirement.Parent.IsRootRequirement(), Set.count currentRequirements with
                    | true, 1 -> ResolverStrategy.Max
                    | _ -> combined
                | UpdateFiltered (g, f) ->
                    match groupName = g && f.Match currentRequirement.Name with
                    | true -> ResolverStrategy.Max
                    | false -> combined

            // we didn't select a version yet so all versions are possible
            let isInRange mapF (ver,_,_) =
                currentRequirements
                |> Seq.forall (fun r -> (mapF r).VersionRequirement.IsInRange ver)

            let getSingleVersion v =
                match currentRequirement.Parent with
                | PackageRequirementSource.Package(_,_,parentSource) -> 
                    Seq.singleton (v,Some parentSource, sources)
                | _ -> Seq.singleton (v,Seq.tryHead sources,sources)

            availableVersions :=
                match currentRequirement.VersionRequirement.Range with
                | OverrideAll v -> getSingleVersion v
                | Specific v -> getSingleVersion v
                | _ -> 
                    getVersionsF sources resolverStrategy groupName currentRequirement.Name
                    |> Seq.map (fun (v,s) -> v,None,s)
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
                    let allPrereleases = prereleases |> List.filter (fun (v,_,_) -> v.PreRelease <> None) = prereleases
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
                    Seq.filter (fun (v,_,_) -> currentRequirement.VersionRequirement.IsInRange(v,currentRequirement.Parent.IsRootRequirement() |> not)) versions

                if Seq.isEmpty !compatibleVersions then
                    compatibleVersions := 
                        Seq.filter (fun (v,_,_) -> currentRequirement.IncludingPrereleases().VersionRequirement.IsInRange(v,currentRequirement.Parent.IsRootRequirement() |> not)) versions

        if Seq.isEmpty !compatibleVersions then
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

        let tryToImprove useUnlisted =
            let allUnlisted = ref true
            let state = ref conflictStatus
            let trial = ref 0
            let forceBreak = ref false
            
            let isOk() = 
                match !state with
                | Resolution.Ok _ -> true
                | _ -> false
            let versionsToExplore = ref !compatibleVersions

            let shouldTryHarder trial =
                if !forceBreak then false else
                if isOk() || Seq.isEmpty !versionsToExplore then false else
                if trial < 1 then true else
                getConflicts() |> Set.isEmpty

            while shouldTryHarder !trial do
                trial := !trial + 1
                let versionToExplore = Seq.head !versionsToExplore
                versionsToExplore := Seq.tail !versionsToExplore
                let exploredPackage = getExploredPackage(currentRequirement,versionToExplore)

                if exploredPackage.Unlisted && not useUnlisted then 
                    () 
                else
                    let newFilteredVersions = Map.add currentRequirement.Name ([versionToExplore],!globalOverride) filteredVersions
                        
                    let newOpen = calcOpenRequirements(exploredPackage,globalFrameworkRestrictions,versionToExplore,currentRequirement,closedRequirements,openRequirements)
                    let newResolution = Map.add exploredPackage.Name exploredPackage currentResolution

                    state := step (newFilteredVersions,newResolution,Set.add currentRequirement closedRequirements,newOpen)
                    match !state with
                    | Resolution.Conflict (_,_,stillOpen,_,_)
                        when stillOpen |> Set.exists (fun r -> r = currentRequirement || r.Graph |> List.contains currentRequirement) |> not ->
                        forceBreak := true
                    | _ -> ()

                    allUnlisted := exploredPackage.Unlisted && !allUnlisted

            !allUnlisted,!state

        match tryToImprove false with
        | true,Resolution.Conflict(_) -> tryToImprove true |> snd
        | _,x-> x

    step (Map.empty, Map.empty, Set.empty, rootDependencies)
