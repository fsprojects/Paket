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
type PackageDetails = {
    Name               : PackageName
    Source             : PackageSource
    DownloadLink       : string
    LicenseUrl         : string
    Unlisted           : bool
    DirectDependencies : DependencySet
}

/// Represents data about resolved packages
type ResolvedPackage = {
    Name         : PackageName
    Version      : SemVerInfo
    Dependencies : DependencySet
    Unlisted     : bool
    Settings     : InstallSettings
    Source       : PackageSource
} with
    override this.ToString () = sprintf "%O %O" this.Name this.Version

    member self.HasFrameworkRestrictions =
        not (getRestrictionList self.Settings.FrameworkRestrictions = [])

type PackageResolution = Map<PackageName, ResolvedPackage>

let cleanupNames (model : PackageResolution) : PackageResolution =
    model
    |> Map.map (fun _ package ->
        { package with
            Dependencies =
                package.Dependencies
                |> Set.map (fun (name, v, d) -> model.[name].Name, v, d) })


type ResolverStep = {
    Relax: bool
    FilteredVersions : Map<PackageName, ((SemVerInfo * PackageSource list) list * bool)>
    CurrentResolution : Map<PackageName,ResolvedPackage>;
    ClosedRequirements : Set<PackageRequirement>
    OpenRequirements : Set<PackageRequirement> }

[<RequireQualifiedAccess>]
type Resolution =
| Ok of PackageResolution
| Conflict of resolveStep    : ResolverStep
            * requirementSet : PackageRequirement Set
            * requirement    : PackageRequirement
            * getPackageVersions : (PackageName -> (SemVerInfo * PackageSource list) seq)


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Resolution =

    open System.Text

    let getConflicts (res:Resolution) =
        match res with
        | Resolution.Ok _ -> []
        | Resolution.Conflict (currentStep,_,lastPackageRequirement,_) ->
            currentStep.ClosedRequirements
            |> Set.union currentStep.OpenRequirements
            |> Set.add lastPackageRequirement
            |> Seq.filter (fun x -> x.Name = lastPackageRequirement.Name)
            |> Seq.sortBy (fun x -> x.Parent)
            |> Seq.toList

    let buildConflictReport (errorReport:StringBuilder)  (conflicts:PackageRequirement list) =
        match conflicts with
        | [] -> errorReport
        | req::conflicts ->
            errorReport.AddLine (sprintf "  Could not resolve package %O:" req.Name)
            let hasPrereleases =
                conflicts |> List.exists (fun r -> r.VersionRequirement.PreReleases <> PreReleaseStatus.No)

            let rec loop conflicts (errorReport:StringBuilder) =
                match conflicts with
                | [] -> errorReport
                | hd::tl ->
                    let vr =
                        hd.VersionRequirement.ToString ()
                        |> fun s -> if String.IsNullOrWhiteSpace s then ">= 0" else s
                    let pr = if hasPrereleases && hd.VersionRequirement.PreReleases = PreReleaseStatus.No then " (no prereleases)" else ""
                    match hd.Parent with
                    | DependenciesFile _ ->
                        loop tl (errorReport.AppendLinef "   - Dependencies file requested package %O: %s%s" req.Name vr pr)
                    | Package (parentName,version,_) ->
                        loop tl (errorReport.AppendLinef  "   - %O %O requested package %O: %s%s" parentName version req.Name vr pr)
            loop conflicts errorReport


    let getErrorText showResolvedPackages = function
    | Resolution.Ok _ -> ""
    | Resolution.Conflict (currentStep,_,_,getVersionF) as res ->
        let errorText =
            if showResolvedPackages && not currentStep.CurrentResolution.IsEmpty then
                ( StringBuilder().AppendLine  "  Resolved packages:"
                , currentStep.CurrentResolution)
                ||> Map.fold (fun sb _ resolvedPackage ->
                    sb.AppendLinef "   - %O %O" resolvedPackage.Name resolvedPackage.Version)
            else StringBuilder()

        match getConflicts res with
        | []  ->
            errorText.AppendLinef
                "  Could not resolve package %O. Unknown resolution error."
                    (Seq.head currentStep.OpenRequirements)
        | [c] ->
            let errorText = buildConflictReport errorText  [c]
            match getVersionF c.Name |> Seq.toList with
            | [] -> errorText.AppendLinef  "   - No versions available."
            | avalaibleVersions ->
                ( errorText.AppendLinef  "   - Available versions:"
                , avalaibleVersions )
                ||> List.fold (fun sb elem -> sb.AppendLinef "     - %O" elem)
        | conflicts -> buildConflictReport errorText conflicts
        |> string


    let getModelOrFail = function
    | Resolution.Ok model -> model
    | Resolution.Conflict _ as res ->
        failwithf  "There was a version conflict during package resolution.\n\
                    %s\n  Please try to relax some conditions." (getErrorText true res)


    let isDone = function
    | Resolution.Ok _ -> true
    | _ -> false

type Resolution with

    member self.GetConflicts () = Resolution.getConflicts self
    member self.GetErrorText showResolvedPackages = Resolution.getErrorText showResolvedPackages self
    member self.GetModelOrFail () = Resolution.getModelOrFail self
    member self.IsDone = Resolution.isDone self


let calcOpenRequirements (exploredPackage:ResolvedPackage,globalFrameworkRestrictions,(versionToExplore,_),dependency,resolverStep:ResolverStep) =
    let dependenciesByName =
        // there are packages which define multiple dependencies to the same package
        // we compress these here - see #567
        let dict = Dictionary<_,_>()
        exploredPackage.Dependencies
        |> Set.iter (fun ((name,v,r) as dep) ->
            match dict.TryGetValue name with
            | true,(_,v2,r2) ->
                match v,v2 with
                | VersionRequirement(ra1,p1),VersionRequirement(ra2,p2) when p1 = p2 ->
                    let newRestrictions =
                        match r with
                        | FrameworkRestrictionList r ->
                            match r2 with
                            | FrameworkRestrictionList r2 ->
                                FrameworkRestrictionList (r @ r2)
                            | AutoDetectFramework -> FrameworkRestrictionList r
                        | AutoDetectFramework -> r

                    if ra1.IsIncludedIn ra2 then
                        dict.[name] <- (name,v,newRestrictions)
                    elif ra2.IsIncludedIn ra1 then
                        dict.[name] <- (name,v2,newRestrictions)
                    else dict.[name] <- dep
                | _ ->  dict.[name] <- dep
            | _ -> dict.Add(name,dep))
        dict
        |> Seq.map (fun kv -> kv.Value)
        |> Set.ofSeq

    let rest =
        resolverStep.OpenRequirements
        |> Set.remove dependency

    dependenciesByName
    |> Set.map (fun (n, v, restriction) ->
        let newRestrictions =
            filterRestrictions restriction exploredPackage.Settings.FrameworkRestrictions
            |> filterRestrictions globalFrameworkRestrictions
            |> fun xs -> if xs = FrameworkRestrictionList [] then exploredPackage.Settings.FrameworkRestrictions else xs

        { dependency with
            Name = n
            VersionRequirement = v
            Parent = Package(dependency.Name, versionToExplore, exploredPackage.Source)
            Graph = [dependency] @ dependency.Graph
            Settings = { dependency.Settings with FrameworkRestrictions = newRestrictions } })
    |> Set.filter (fun d ->
        resolverStep.ClosedRequirements
        |> Seq.exists (fun x ->
            x.Name = d.Name &&
               x.Settings.FrameworkRestrictions = d.Settings.FrameworkRestrictions &&
                (x = d ||
                 x.VersionRequirement.Range.IsIncludedIn d.VersionRequirement.Range ||
                 x.VersionRequirement.Range.IsGlobalOverride))
        |> not)
    |> Set.filter (fun d ->
        resolverStep.OpenRequirements
        |> Seq.exists (fun x -> x.Name = d.Name && (x = d || x.VersionRequirement.Range.IsGlobalOverride) && x.Settings.FrameworkRestrictions = d.Settings.FrameworkRestrictions)
        |> not)
    |> Set.union rest


type Resolved = {
    ResolvedPackages : Resolution
    ResolvedSourceFiles : ModuleResolver.ResolvedSourceFile list
}

let getResolverStrategy globalStrategyForDirectDependencies globalStrategyForTransitives (allRequirementsOfCurrentPackage:Set<PackageRequirement>) (currentRequirement:PackageRequirement) =
    if currentRequirement.Parent.IsRootRequirement() && Set.count allRequirementsOfCurrentPackage = 1 then
        let combined = currentRequirement.ResolverStrategyForDirectDependencies ++ globalStrategyForDirectDependencies

        defaultArg combined ResolverStrategy.Max
    else
        let combined =
            (allRequirementsOfCurrentPackage
                |> Seq.filter (fun x -> x.Depth > 0)
                |> Seq.sortBy (fun x -> x.Depth, x.ResolverStrategyForTransitives <> globalStrategyForTransitives, x.ResolverStrategyForTransitives <> Some ResolverStrategy.Max)
                |> Seq.map (fun x -> x.ResolverStrategyForTransitives)
                |> Seq.fold (++) None)
                ++ globalStrategyForTransitives

        defaultArg combined ResolverStrategy.Max

type UpdateMode =
    | UpdateGroup of GroupName
    | UpdateFiltered of GroupName * PackageFilter
    | Install
    | UpdateAll

type private PackageConfig = {
    Dependency         : PackageRequirement
    GroupName          : GroupName
    GlobalRestrictions : FrameworkRestrictions
    RootSettings       : IDictionary<PackageName,InstallSettings>
    Version            : SemVerInfo
    Sources            : PackageSource list
    UpdateMode         : UpdateMode
} with
    member self.HasGlobalRestrictions =
        not(getRestrictionList self.GlobalRestrictions = [])

    member self.HasDependencyRestrictions =
        not(getRestrictionList self.Dependency.Settings.FrameworkRestrictions = [])


let private updateRestrictions (pkgConfig:PackageConfig) (package:ResolvedPackage) =
    let newRestrictions =
        if  not pkgConfig.HasGlobalRestrictions
            && (List.isEmpty (package.Settings.FrameworkRestrictions |> getRestrictionList)
            ||  not pkgConfig.HasDependencyRestrictions )
        then
            []
        else
            let packageSettings = package.Settings.FrameworkRestrictions |> getRestrictionList
            let dependencySettings = pkgConfig.Dependency.Settings.FrameworkRestrictions |> getRestrictionList
            let globalSettings = pkgConfig.GlobalRestrictions |> getRestrictionList
            optimizeRestrictions (List.concat[packageSettings;dependencySettings;globalSettings])

    { package with
        Settings = { package.Settings with FrameworkRestrictions = FrameworkRestrictionList newRestrictions }
    }


let private explorePackageConfig getPackageDetailsF  (pkgConfig:PackageConfig) =
    let dependency, version = pkgConfig.Dependency, pkgConfig.Version
    let packageSources      = pkgConfig.Sources

    match pkgConfig.UpdateMode with
    | Install -> tracefn  " - %O %A" dependency.Name version
    | _ ->
        match dependency.VersionRequirement.Range with
        | Specific _ when dependency.Parent.IsRootRequirement() -> traceWarnfn " - %O is pinned to %O" dependency.Name version
        | _ -> tracefn  " - %O %A" dependency.Name version

    let newRestrictions =
        filterRestrictions dependency.Settings.FrameworkRestrictions pkgConfig.GlobalRestrictions
    try
        let packageDetails : PackageDetails =
            getPackageDetailsF packageSources pkgConfig.GroupName dependency.Name version
        let filteredDependencies =
            DependencySetFilter.filterByRestrictions newRestrictions packageDetails.DirectDependencies
        let settings =
            match dependency.Parent with
            | DependenciesFile _ -> dependency.Settings
            | Package _ ->
                match pkgConfig.RootSettings.TryGetValue packageDetails.Name with
                | true, s -> s + dependency.Settings
                | _ -> dependency.Settings
            |> fun x -> x.AdjustWithSpecialCases packageDetails.Name
        Some
            {   Name         = packageDetails.Name
                Version      = version
                Dependencies = filteredDependencies
                Unlisted     = packageDetails.Unlisted
                Settings     = { settings with FrameworkRestrictions = newRestrictions }
                Source       = packageDetails.Source
            }
    with
    | exn ->
        traceWarnfn "    Package not available.%s      Message: %s" Environment.NewLine exn.Message
        None



let private getExploredPackage (pkgConfig:PackageConfig) getPackageDetailsF (exploredPackages:Dictionary<_,_>) =
    let key = (pkgConfig.Dependency.Name, pkgConfig.Version)

    match exploredPackages.TryGetValue key with
    | true, package ->
        let package = updateRestrictions pkgConfig package
        exploredPackages.[key] <- package
        Some package
    | false,_ ->
        match explorePackageConfig getPackageDetailsF pkgConfig with
        | Some explored ->
            exploredPackages.Add(key,explored)
            Some explored
        | None ->
            None



let private getCompatibleVersions
               (currentStep:ResolverStep)
                groupName
               (currentRequirement:PackageRequirement)
               (getVersionsF: PackageSource list -> ResolverStrategy -> GroupName -> PackageName -> seq<SemVerInfo * PackageSource list>)
                globalOverride
                globalStrategyForDirectDependencies
                globalStrategyForTransitives        =
    verbosefn "  Trying to resolve %O" currentRequirement

    match Map.tryFind currentRequirement.Name currentStep.FilteredVersions with
    | None ->
        let allRequirementsOfCurrentPackage =
            currentStep.OpenRequirements
            |> Set.filter (fun r -> currentRequirement.Name = r.Name)

        // we didn't select a version yet so all versions are possible
        let isInRange mapF (ver,_) =
            allRequirementsOfCurrentPackage
            |> Set.forall (fun r -> (mapF r).VersionRequirement.IsInRange ver)

        let getSingleVersion v =
            match currentRequirement.Parent with
            | PackageRequirementSource.Package(_,_,parentSource) ->
                let sources = parentSource :: currentRequirement.Sources |> List.distinct
                Seq.singleton (v,sources)
            | _ ->
                let sources : PackageSource list = currentRequirement.Sources |> List.sortBy (fun x -> not x.IsLocalFeed, String.containsIgnoreCase "nuget.org" x.Url |> not)
                Seq.singleton (v,sources)

        let availableVersions =
            match currentRequirement.VersionRequirement.Range with
            | OverrideAll v -> getSingleVersion v
            | Specific v -> getSingleVersion v
            | _ ->
                let resolverStrategy = getResolverStrategy globalStrategyForDirectDependencies globalStrategyForTransitives allRequirementsOfCurrentPackage currentRequirement
                getVersionsF currentRequirement.Sources resolverStrategy groupName currentRequirement.Name

        let compatibleVersions = Seq.filter (isInRange id) (availableVersions)
        let compatibleVersions, globalOverride =
            if currentRequirement.VersionRequirement.Range.IsGlobalOverride then
                compatibleVersions |> Seq.cache, true
            elif Seq.isEmpty compatibleVersions then
                let prereleaseStatus (r:PackageRequirement) =
                    if r.Parent.IsRootRequirement() && r.VersionRequirement <> VersionRequirement.AllReleases then
                        r.VersionRequirement.PreReleases
                    else
                        PreReleaseStatus.All

                let available = availableVersions |> Seq.toList
                let prereleases = List.filter (isInRange (fun r -> r.IncludingPrereleases(prereleaseStatus r))) available
                let allPrereleases = prereleases |> List.filter (fun (v,_) -> v.PreRelease <> None) = prereleases
                if allPrereleases then
                    Seq.ofList prereleases, globalOverride
                else
                    compatibleVersions|> Seq.cache, globalOverride
            else
                compatibleVersions|> Seq.cache, globalOverride

        compatibleVersions, globalOverride, false

    | Some(versions,globalOverride) ->
        // we already selected a version so we can't pick a different
        let compatibleVersions, tryRelaxed =
            if globalOverride then List.toSeq versions, false else
            let compat =
                Seq.filter (fun (v,_) -> currentRequirement.VersionRequirement.IsInRange(v,currentRequirement.Parent.IsRootRequirement() |> not)) versions

            if Seq.isEmpty compat then
                let withPrereleases = Seq.filter (fun (v,_) -> currentRequirement.IncludingPrereleases().VersionRequirement.IsInRange(v,currentRequirement.Parent.IsRootRequirement() |> not)) versions
                if currentStep.Relax || Seq.isEmpty withPrereleases then
                    withPrereleases, false
                else
                    withPrereleases, true
            else
                compat, false
        compatibleVersions, false, tryRelaxed


let private getConflicts (currentStep:ResolverStep) (currentRequirement:PackageRequirement) (knownConflicts:HashSet<_>) =
    let allRequirements =
        currentStep.OpenRequirements
        |> Set.filter (fun r -> r.Graph |> List.contains currentRequirement |> not)
        |> Set.union currentStep.ClosedRequirements

    knownConflicts
    |> Seq.map (fun (conflicts,selectedVersion) ->
        match selectedVersion with
        | None when Set.isSubset conflicts allRequirements -> conflicts
        | Some(selectedVersion,_) ->
            let n = (Seq.head conflicts).Name
            match currentStep.FilteredVersions |> Map.tryFind n with
            | Some(v,_) when v = selectedVersion && Set.isSubset conflicts allRequirements -> conflicts
            | _ -> Set.empty
        | _ -> Set.empty)
    |> Set.unionMany


let private getCurrentRequirement packageFilter (openRequirements:Set<PackageRequirement>) (conflictHistory:Dictionary<_,_>) =
    let initialMin = Seq.head openRequirements
    let initialBoost = 0

    let currentMin, _ =
        ((initialMin,initialBoost),openRequirements)
        ||> Seq.fold (fun (cmin,cboost) d ->
            let boost =
                match conflictHistory.TryGetValue d.Name with
                | true,c -> -c
                | _ -> 0
            if PackageRequirement.Compare(d,cmin,packageFilter,boost,cboost) = -1 then
                d, boost
            else
                cmin,cboost)
    currentMin


type ConflictState = {
    Status               : Resolution
    LastConflictReported : DateTime
    TryRelaxed           : bool
    ExploredPackages     : Dictionary<PackageName*SemVerInfo,ResolvedPackage>
    Conflicts            : Set<PackageRequirement>
    VersionsToExplore    : seq<SemVerInfo * PackageSource list>
    GlobalOverride       : bool
}

let private boostConflicts
                    (filteredVersions:Map<PackageName, ((SemVerInfo * PackageSource list) list * bool)>)
                    (currentRequirement:PackageRequirement)
                    (knownConflicts:HashSet<Set<PackageRequirement> * ((SemVerInfo * PackageSource list)list * bool) option>)
                    (conflictHistory:Dictionary<PackageName,int>)
                    (conflictState:ConflictState) =
    let conflictStatus = conflictState.Status
    let isNewConflict  =
        match conflictHistory.TryGetValue currentRequirement.Name with
        | true,count ->
            conflictHistory.[currentRequirement.Name] <- count + 1
            false
        | _ ->
            conflictHistory.Add(currentRequirement.Name, 1)
            true

    let conflicts = conflictStatus.GetConflicts()
    let lastConflictReported =
        match conflicts with
        | c::_  ->
            let selectedVersion = Map.tryFind c.Name filteredVersions
            let key = conflicts |> Set.ofList,selectedVersion
            knownConflicts.Add key |> ignore

            let reportThatResolverIsTakingLongerThanExpected =
                not isNewConflict && DateTime.Now - conflictState.LastConflictReported > TimeSpan.FromSeconds 10.

            if Logging.verbose then
                tracefn "%s" <| conflictStatus.GetErrorText(false)
                tracefn "    ==> Trying different resolution."
            if reportThatResolverIsTakingLongerThanExpected then
                traceWarnfn "%s" <| conflictStatus.GetErrorText(false)
                traceWarn "The process is taking longer than expected."
                traceWarn "Paket may still find a valid resolution, but this might take a while."
                DateTime.Now
            else
                conflictState.LastConflictReported
        | _ -> conflictState.LastConflictReported
    { conflictState with
        LastConflictReported = lastConflictReported
    }


[<Struct>]
type private StepFlags (ready:bool,useUnlisted:bool,hasUnlisted:bool,forceBreak:bool,firstTrial:bool) =
    member __.Ready       = ready
    member __.UseUnlisted = useUnlisted
    member __.HasUnlisted = hasUnlisted
    member __.ForceBreak  = forceBreak
    member __.FirstTrial  = firstTrial


type private Stage =
    | Outer of conflictState : ConflictState
    | Inner of conflictState : ConflictState

/// Resolves all direct and transitive dependencies
let Resolve (getVersionsF, getPackageDetailsF, groupName:GroupName, globalStrategyForDirectDependencies, globalStrategyForTransitives, globalFrameworkRestrictions, (rootDependencies:PackageRequirement Set), updateMode : UpdateMode) =
    tracefn "Resolving packages for group %O:" groupName

    let packageFilter =
        match updateMode with
        | UpdateFiltered (g, f) when g = groupName -> Some f
        | _ -> None

    let rootSettings =
        rootDependencies
        |> Seq.map (fun x -> x.Name,x.Settings)
        |> dict

    if Set.isEmpty rootDependencies then Resolution.Ok Map.empty else

    let startingStep = {
        Relax = false
        FilteredVersions   = Map.empty
        CurrentResolution  = Map.empty
        ClosedRequirements = Set.empty
        OpenRequirements   = rootDependencies
    }

    let currentRequirement = getCurrentRequirement packageFilter startingStep.OpenRequirements (Dictionary())

    let status =
        let getVersionsF = getVersionsF currentRequirement.Sources ResolverStrategy.Max groupName
        Resolution.Conflict(startingStep,Set.empty,currentRequirement,getVersionsF)

    let conflictState : ConflictState = {
        Status               = status
        LastConflictReported = DateTime.Now
        TryRelaxed           = false
        ExploredPackages     = Dictionary<PackageName*SemVerInfo,ResolvedPackage>()
        VersionsToExplore    = Seq.empty
        Conflicts            = Set.empty
        GlobalOverride       = false
    }
    // NOTE - the contents of these collections will be mutated throughout iterations of 'step'
    let knownConflicts  = HashSet()
    let conflictHistory = Dictionary()


    let flags =
        StepFlags
            (   ready       = false
            ,   useUnlisted = false
            ,   hasUnlisted = false
            ,   forceBreak  = false
            ,   firstTrial  = true
            )

    let stopLooping (flags:StepFlags) (conflictState:ConflictState) =
        if flags.ForceBreak then true else
        if conflictState.Status.IsDone || Seq.isEmpty conflictState.VersionsToExplore then true else
        if (flags.FirstTrial || Set.isEmpty conflictState.Conflicts) then false
        else true



    let rec step (currentStep:ResolverStep) (flags:StepFlags) =
        if Set.isEmpty currentStep.OpenRequirements then
            { conflictState with
               Status = Resolution.Ok (cleanupNames currentStep.CurrentResolution)
            }
        // ----------- TERMINATE --------------
        else
        verbosefn "  %d packages in resolution. %d requirements left" currentStep.CurrentResolution.Count currentStep.OpenRequirements.Count

        let currentRequirement = getCurrentRequirement packageFilter currentStep.OpenRequirements conflictHistory
        let conflicts = getConflicts currentStep currentRequirement knownConflicts


        let conflictState =
            if Set.isEmpty conflicts then
                let getVersionsF = getVersionsF currentRequirement.Sources ResolverStrategy.Max groupName
                { conflictState with
                    Status = Resolution.Conflict(currentStep,Set.empty,currentRequirement,getVersionsF)
                }
            else
                let getVersionsF = getVersionsF currentRequirement.Sources ResolverStrategy.Max groupName
                { conflictState with
                    Status = Resolution.Conflict(currentStep,conflicts,Seq.head conflicts,getVersionsF)
                }


        if not (Set.isEmpty conflicts) then
            conflictState
        // ----------- TERMINATE --------------

        else
        let compatibleVersions,globalOverride,tryRelaxed =
            getCompatibleVersions currentStep groupName currentRequirement getVersionsF
//                    false //globalOverride
                    conflictState.GlobalOverride
                    globalStrategyForDirectDependencies
                    globalStrategyForTransitives

        let conflictState = {
            conflictState with
                Conflicts         = conflicts
                VersionsToExplore = compatibleVersions
                TryRelaxed        = tryRelaxed
                GlobalOverride    = globalOverride
        }

        let conflictState =
            if Seq.isEmpty compatibleVersions then
                boostConflicts currentStep.FilteredVersions currentRequirement knownConflicts conflictHistory conflictState
            else
                conflictState

        let flags =
            StepFlags
                (   ready       = false
                ,   useUnlisted = false
                ,   hasUnlisted = false
                ,   forceBreak  = flags.ForceBreak
                ,   firstTrial  = flags.FirstTrial
                )

        let rec stepLoop (flags:StepFlags) (stage:Stage) =
            match stage with
            | Outer (conflictState) ->
                if flags.Ready then conflictState else
                let flags = StepFlags(flags.Ready,flags.UseUnlisted,flags.HasUnlisted,false,true)
                stepLoop flags (Inner(conflictState))

            | Inner (conflictState) ->
                if stopLooping flags conflictState then
                    let flags =
                        if not flags.UseUnlisted && flags.HasUnlisted && not conflictState.Status.IsDone then
                            StepFlags(flags.Ready,true,flags.HasUnlisted,flags.ForceBreak,flags.FirstTrial)
                        else
                            StepFlags(true,flags.UseUnlisted,flags.HasUnlisted,flags.ForceBreak,flags.FirstTrial)
                    stepLoop flags (Outer(conflictState))
                else
                let flags = StepFlags(flags.Ready,flags.UseUnlisted,flags.HasUnlisted,flags.ForceBreak,false)
                let (version,sources) & versionToExplore = Seq.head conflictState.VersionsToExplore

                let conflictState = {
                    conflictState with
                        VersionsToExplore = Seq.tail conflictState.VersionsToExplore
                }
                let packageDetails = {
                    GroupName          = groupName
                    Dependency         = currentRequirement
                    GlobalRestrictions = globalFrameworkRestrictions
                    RootSettings       = rootSettings
                    Version            = version
                    Sources            = sources
                    UpdateMode         = updateMode
                }

                let exploredPackages = conflictState.ExploredPackages

                match getExploredPackage packageDetails getPackageDetailsF exploredPackages with
                | None ->
                    stepLoop flags (Inner(conflictState))
                | Some exploredPackage ->
                    let hasUnlisted = exploredPackage.Unlisted || flags.HasUnlisted

                    let flags = StepFlags(flags.Ready,flags.UseUnlisted,hasUnlisted,flags.ForceBreak,flags.FirstTrial)
                    if exploredPackage.Unlisted && not flags.UseUnlisted then
                        tracefn "     unlisted"
                    
                    let nextStep =
                        {   Relax              = currentStep.Relax
                            FilteredVersions   = Map.add currentRequirement.Name ([versionToExplore],conflictState.GlobalOverride) currentStep.FilteredVersions
                            CurrentResolution  = Map.add exploredPackage.Name exploredPackage currentStep.CurrentResolution
                            ClosedRequirements = Set.add currentRequirement currentStep.ClosedRequirements
                            OpenRequirements   = calcOpenRequirements(exploredPackage,globalFrameworkRestrictions,versionToExplore,currentRequirement,currentStep)
                        }

                    if nextStep.OpenRequirements = currentStep.OpenRequirements then
                        failwithf "The resolver confused itself. The new open requirements are the same as the old ones. This will result in an endless loop.%sCurrent Requirement: %A%sRequirements: %A" Environment.NewLine currentRequirement Environment.NewLine nextStep.OpenRequirements

                    let versionsToExplore = conflictState.VersionsToExplore

                    let conflictState = {
                        step nextStep flags with
                            VersionsToExplore = versionsToExplore
                    }

                    match conflictState.Status with
                    | Resolution.Conflict(_,conflicts,_,_)
                        when
                            (Set.isEmpty conflicts |> not)
                            && nextStep.CurrentResolution.Count > 1
                            && not (conflicts |> Set.exists (fun r ->
                                r = currentRequirement
                                || r.Graph |> List.contains currentRequirement)) ->
                        let flags = StepFlags(flags.Ready,flags.UseUnlisted,flags.HasUnlisted,true,flags.FirstTrial)
                        stepLoop flags (Inner (conflictState))
                    | _ ->
                        stepLoop flags (Inner (conflictState))

        stepLoop flags (Outer conflictState)
        // ----------- TERMINATE --------------

    match step startingStep flags  with
    | { Status = Resolution.Conflict _ } as conflict ->
        if conflict.TryRelaxed then
            knownConflicts.Clear()
            conflictHistory.Clear()
            (step { startingStep with Relax = true } flags ).Status
        else
            conflict.Status
    | x -> x.Status

