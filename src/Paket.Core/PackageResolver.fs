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

    override this.ToString() =
        let (PackageName name) = this.Name
        sprintf "%s %s" name (this.Version.ToString())

type PackageResolution = Map<NormalizedPackageName, ResolvedPackage>

let allPrereleases versions = versions |> List.filter (fun v -> v.PreRelease <> None) = versions

let cleanupNames (model : PackageResolution) : PackageResolution = 
    model |> Seq.fold (fun map x -> 
                 let package = x.Value
                 let cleanup = 
                     { package with Dependencies = 
                                        package.Dependencies 
                                        |> Set.map (fun ((NormalizedPackageName name), v, d) -> model.[name].Name, v, d) }
                 Map.add (NormalizedPackageName package.Name) cleanup map) Map.empty

[<RequireQualifiedAccess>]
type Resolution =
| Ok of PackageResolution
| Conflict of Set<PackageRequirement> * Set<PackageRequirement>
    with
    member this.GetModelOrFail() =
        match this with
        | Resolution.Ok model -> model
        | Resolution.Conflict(closed,stillOpen) ->

            let errorText = ref ""

            let addToError text = errorText := !errorText + Environment.NewLine + text

            let traceUnresolvedPackage (x : PackageRequirement) =
                let (PackageName name) = x.Name
                match x.Parent with
                | DependenciesFile _ ->
                    sprintf "    - %s %s" name (x.VersionRequirement.ToString())
                | Package(PackageName parentName,version) ->
                    sprintf "    - %s %s%s       - from %s %s" name (x.VersionRequirement.ToString()) Environment.NewLine 
                        parentName (version.ToString())
                |> addToError

            addToError "Error in resolution."

            if not closed.IsEmpty then
                addToError "  Resolved:"
                for x in closed do
                   traceUnresolvedPackage x

            addToError "  Could not resolve:"
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
    |> Set.map (fun (n,v,r) -> {dependency with Name = n; VersionRequirement = v; Parent = Package(dependency.Name,versionToExplore); Settings = { dependency.Settings with FrameworkRestrictions = r @ globalFrameworkRestrictions }})
    |> Set.filter (fun d -> Set.contains d closed |> not)
    |> Set.filter (fun d -> Set.contains d stillOpen |> not)
    |> Set.filter (fun d ->
        closed 
        |> Seq.filter (fun x -> x.Name = d.Name)
        |> Seq.exists (fun otherDep -> otherDep.VersionRequirement.Range.IsIncludedIn(d.VersionRequirement.Range))
        |> not)
    |> Set.filter (fun d ->
        rest 
        |> Seq.filter (fun x -> x.Name = d.Name)
        |> Seq.exists (fun otherDep -> otherDep.VersionRequirement.Range.IsIncludedIn(d.VersionRequirement.Range))
        |> not)
    |> Set.union rest

type Resolved = {
    ResolvedPackages : Resolution
    ResolvedSourceFiles : ModuleResolver.ResolvedSourceFile list }

/// Resolves all direct and transitive dependencies
let Resolve(getVersionsF, getPackageDetailsF, globalFrameworkRestrictions, rootDependencies:PackageRequirement list) =
    tracefn "Resolving packages:"
    let exploredPackages = Dictionary<NormalizedPackageName*SemVerInfo,ResolvedPackage>()
    let allVersions = Dictionary<NormalizedPackageName,SemVerInfo list>()
    let conflictHistory = Dictionary<NormalizedPackageName,int>()

    let getExploredPackage(dependency:PackageRequirement,version) =
        let normalizedPackageName = NormalizedPackageName dependency.Name
        let (PackageName name) = dependency.Name
        match exploredPackages.TryGetValue <| (normalizedPackageName,version) with
        | true,package -> 
            match dependency.Parent with
            | PackageRequirementSource.DependenciesFile(_) -> 
                let package = { package with Settings = { package.Settings with FrameworkRestrictions = dependency.Settings.FrameworkRestrictions } }
                exploredPackages.[(normalizedPackageName,version)] <- package
                package
            | _ -> package
        | false,_ ->         
            tracefn  " - %s %A" name version
            let packageDetails : PackageDetails = getPackageDetailsF dependency.Sources dependency.Name version
            let restrictedDependencies = DependencySetFilter.filterByRestrictions (dependency.Settings.FrameworkRestrictions @ globalFrameworkRestrictions) packageDetails.DirectDependencies
            let explored =
                { Name = packageDetails.Name
                  Version = version
                  Dependencies = restrictedDependencies
                  Unlisted = packageDetails.Unlisted
                  Settings = dependency.Settings
                  Source = packageDetails.Source }
            exploredPackages.Add((normalizedPackageName,version),explored)
            explored

    let getAllVersions(sources,packageName:PackageName,vr : VersionRange)  =
        let normalizedPackageName = NormalizedPackageName packageName
        let (PackageName name) = packageName
        match allVersions.TryGetValue(normalizedPackageName) with
        | false,_ ->            
            let versions = 
                match vr with
                | OverrideAll v -> [v]
                | Specific v -> [v]
                | _ -> 
                    verbosefn "  - fetching versions for %s" name
                    getVersionsF(sources,packageName)

            if Seq.isEmpty versions then
                failwithf "Couldn't retrieve versions for %s." name
            allVersions.Add(normalizedPackageName,versions)
            versions
        | true,versions -> versions        

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
                    |> Seq.fold (fun map p -> Map.add (NormalizedPackageName p.Name) p map) Map.empty

                Resolution.Ok(resolution)
            else
                Resolution.Conflict(closedRequirements,openRequirements)
        else
            let packageCount = selectedPackageVersions |> List.length
            verbosefn "  %d packages in resolution. %d requirements left" packageCount openRequirements.Count
            
            let currentRequirement =
                let currentMin = ref (Seq.head openRequirements)
                let currentBoost = ref 0
                for d in openRequirements do
                    let boost = 
                        match conflictHistory.TryGetValue(NormalizedPackageName d.Name) with
                        | true,c -> -c
                        | _ -> 0
                    if PackageRequirement.Compare(d,!currentMin,boost,!currentBoost) = -1 then
                        currentMin := d
                        currentBoost := boost
                !currentMin
            verbosefn "  Trying to resolve %O" currentRequirement

            let availableVersions = ref []
            let compatibleVersions = ref []
            let globalOverride = ref false
     
            match Map.tryFind currentRequirement.Name filteredVersions with
            | None ->
                // we didn't select a version yet so all versions are possible
                availableVersions := getAllVersions(currentRequirement.Sources,currentRequirement.Name,currentRequirement.VersionRequirement.Range)
                compatibleVersions := List.filter currentRequirement.VersionRequirement.IsInRange (!availableVersions)
                if currentRequirement.VersionRequirement.Range.IsGlobalOverride then
                    globalOverride := true
                else
                    if !compatibleVersions = [] then
                        let prereleases = List.filter (currentRequirement.IncludingPrereleases().VersionRequirement.IsInRange) (!availableVersions)
                        if allPrereleases prereleases then
                            availableVersions := prereleases
                            compatibleVersions := prereleases
            | Some(versions,globalOverride') -> 
                // we already selected a version so we can't pick a different
                globalOverride := globalOverride'
                availableVersions := versions
                if globalOverride' then
                    compatibleVersions := versions
                else
                    compatibleVersions := 
                        List.filter (fun v -> currentRequirement.VersionRequirement.IsInRange(v,currentRequirement.Parent.IsRootRequirement() |> not)) versions

            if !compatibleVersions = [] then
                if currentRequirement.Parent.IsRootRequirement() then    
                    let versionText = String.Join(Environment.NewLine + "     - ",List.sort !availableVersions)
                    failwithf "Could not find compatible versions for top level dependency:%s     %A%s   Available versions:%s     - %s%s   Try to relax the dependency or allow prereleases." 
                        Environment.NewLine (currentRequirement.ToString()) Environment.NewLine Environment.NewLine versionText Environment.NewLine
                else
                    // boost the conflicting package, in order to solve conflicts faster
                    match conflictHistory.TryGetValue(NormalizedPackageName currentRequirement.Name) with
                    | true,count -> conflictHistory.[NormalizedPackageName currentRequirement.Name] <- count + 1
                    | _ -> conflictHistory.Add(NormalizedPackageName currentRequirement.Name, 1)
                    
                    if verbose then
                        tracefn "  Conflicts with:"
                    
                        closedRequirements
                        |> Seq.filter (fun d -> d.Name = currentRequirement.Name)
                        |> fun xs -> String.Join(Environment.NewLine + "    ",xs)
                        |> tracefn "    %s"

                        match filteredVersions |> Map.tryFind currentRequirement.Name with
                        | Some (v,_) -> tracefn "    Package %O was already pinned to %O" currentRequirement.Name v
                        | None -> ()

                        tracefn "    ==> Trying different resolution."


            let sortedVersions =
                if currentRequirement.Parent.IsRootRequirement() then
                    List.sort !compatibleVersions |> List.rev
                else
                    match currentRequirement.ResolverStrategy with
                    | ResolverStrategy.Max -> List.sort !compatibleVersions |> List.rev
                    | ResolverStrategy.Min -> List.sort !compatibleVersions

            let tryToImprove useUnlisted =
                sortedVersions
                |> List.fold (fun (allUnlisted,state) versionToExplore ->
                    match state with
                    | Resolution.Conflict _ ->
                        let exploredPackage = getExploredPackage(currentRequirement,versionToExplore)
                        if exploredPackage.Unlisted && not useUnlisted then 
                            allUnlisted,state 
                        else                
                            let newFilteredVersions = Map.add currentRequirement.Name ([versionToExplore],!globalOverride) filteredVersions
                        
                            let newOpen = calcOpenRequirements(exploredPackage,globalFrameworkRestrictions,versionToExplore,currentRequirement,closedRequirements,openRequirements)
                            let newPackages =
                                exploredPackage::(selectedPackageVersions |> List.filter (fun p -> NormalizedPackageName p.Name <> NormalizedPackageName exploredPackage.Name || p.Version <> exploredPackage.Version))

                            let improved = step (newFilteredVersions,newPackages,Set.add currentRequirement closedRequirements,newOpen)                            
                            (exploredPackage.Unlisted && allUnlisted),improved
                            
                    | Resolution.Ok _ -> allUnlisted,state)
                        (true,Resolution.Conflict(closedRequirements,openRequirements))
            
            match tryToImprove false with
            | true,Resolution.Conflict(_) -> tryToImprove true |> snd       
            | _,x-> x

    match step (Map.empty, [], Set.empty, Set.ofList rootDependencies) with
    | Resolution.Conflict(_) as c -> c
    | Resolution.Ok model -> Resolution.Ok(cleanupNames model)
