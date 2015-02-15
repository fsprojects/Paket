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
      Unlisted : bool
      DirectDependencies : DependencySet }

/// Represents data about resolved packages
type ResolvedPackage =
    { Name : PackageName
      Version : SemVerInfo
      Dependencies : DependencySet
      Unlisted : bool      
      FrameworkRestrictions: FrameworkRestrictions
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

            addToError "  Can't resolve:"
            stillOpen
            |> Seq.head
            |> traceUnresolvedPackage

            addToError " Please try to relax some conditions."
            failwith !errorText


let calcOpenRequirements (exploredPackage:ResolvedPackage,versionToExplore,dependency,closed:Set<PackageRequirement>,stillOpen:Set<PackageRequirement>) =
    let dependenciesByName =
        // there are packages which define multiple dependencies to the same package
        // we just take the latest one - see #567
        let hashSet = new HashSet<_>()
        exploredPackage.Dependencies
        |> Set.filter (fun (name,_,_) -> hashSet.Add name)

    let rest = Set.remove dependency stillOpen
                        
    dependenciesByName
    |> Set.map (fun (n,v,r) -> {dependency with Name = n; VersionRequirement = v; Parent = Package(dependency.Name,versionToExplore); FrameworkRestrictions = r })
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
    ResolvedPackages : ResolvedPackages
    ResolvedSourceFiles : ModuleResolver.ResolvedSourceFile list }

/// Resolves all direct and transitive dependencies
let Resolve(getVersionsF, getPackageDetailsF, rootDependencies:PackageRequirement list) =
    tracefn "Resolving packages:"
    let exploredPackages = Dictionary<NormalizedPackageName*SemVerInfo,ResolvedPackage>()
    let allVersions = Dictionary<NormalizedPackageName,SemVerInfo list>()

    let getExploredPackage(sources,packageName:PackageName,version,frameworkRestrictions) =
        let normalizedPackageName = NormalizedPackageName packageName
        match exploredPackages.TryGetValue <| (normalizedPackageName,version) with
        | true,package -> package
        | false,_ ->
            let (PackageName name) = packageName
            tracefn "    - exploring %s %A" name version
            let packageDetails : PackageDetails = getPackageDetailsF sources packageName version
            let restrictedDependencies = DependencySetFilter.filterByRestrictions frameworkRestrictions packageDetails.DirectDependencies
            let explored =
                { Name = packageDetails.Name
                  Version = version
                  Dependencies = restrictedDependencies
                  Unlisted = packageDetails.Unlisted
                  FrameworkRestrictions = frameworkRestrictions
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
                    tracefn "  - fetching versions for %s" name
                    getVersionsF(sources,packageName)

            if Seq.isEmpty versions then
                failwithf "Couldn't retrieve versions for %s." name
            allVersions.Add(normalizedPackageName,versions)
            versions
        | true,versions -> versions        

    let rec improveModel (filteredVersions:Map<PackageName, (SemVerInfo list * bool)>,packages:ResolvedPackage list,closed:Set<PackageRequirement>,stillOpen:Set<PackageRequirement>) =
        if Set.isEmpty stillOpen then
            // we're done. check if we have a valid resolution and return it
            let isOk =
                filteredVersions
                |> Map.forall (fun _ v ->
                    match v with
                    | [_],_ -> true
                    | _ -> false)

            if isOk then
                ResolvedPackages.Ok(packages |> Seq.fold (fun map p -> Map.add (NormalizedPackageName p.Name) p map) Map.empty)
            else
                ResolvedPackages.Conflict(closed,stillOpen)
        else
            let dependency = Seq.head stillOpen
     
            let allVersions,compatibleVersions,globalOverride = 
                match Map.tryFind dependency.Name filteredVersions with
                | None ->
                    let versions = getAllVersions(dependency.Sources,dependency.Name,dependency.VersionRequirement.Range)
                    if dependency.VersionRequirement.Range.IsGlobalOverride then
                        versions,List.filter dependency.VersionRequirement.IsInRange versions,true
                    else
                        let compatible = List.filter dependency.VersionRequirement.IsInRange versions
                        if compatible = [] then
                            let prereleases = List.filter (dependency.IncludingPrereleases().VersionRequirement.IsInRange) versions
                            if allPrereleases prereleases then
                                prereleases,prereleases,false
                            else
                                versions,[],false
                        else
                            versions,compatible,false
                | Some(versions,globalOverride) -> 
                    if globalOverride then 
                        versions,versions,true 
                    else
                        let filtered = 
                            List.filter (fun v -> dependency.VersionRequirement.IsInRange(v,dependency.Parent.IsRootRequirement() |> not)) versions
                        versions,filtered,false

            if compatibleVersions = [] && dependency.Parent.IsRootRequirement() then    
                let versionText = String.Join(Environment.NewLine + "     - ",List.sort allVersions)
                failwithf "Could not find compatible versions for top level dependency:%s     %A%s   Available versions:%s     - %s%s   Try to relax the dependency or allow prereleases." 
                    Environment.NewLine (dependency.ToString()) Environment.NewLine Environment.NewLine versionText Environment.NewLine
                
            let sortedVersions =                
                if dependency.Parent.IsRootRequirement() then
                    List.sort compatibleVersions |> List.rev
                else
                    match dependency.ResolverStrategy with
                    | ResolverStrategy.Max -> List.sort compatibleVersions |> List.rev
                    | ResolverStrategy.Min -> List.sort compatibleVersions

            let tryToImprove useUnlisted =
                sortedVersions
                |> List.fold (fun (allUnlisted,state) versionToExplore ->
                    match state with
                    | ResolvedPackages.Conflict _ ->
                        let exploredPackage = getExploredPackage(dependency.Sources,dependency.Name,versionToExplore,dependency.FrameworkRestrictions)    
                        if exploredPackage.Unlisted && not useUnlisted then 
                            allUnlisted,state 
                        else                
                            let newFilteredVersions = Map.add dependency.Name ([versionToExplore],globalOverride) filteredVersions
                        
                            let newOpen = calcOpenRequirements(exploredPackage,versionToExplore,dependency,closed,stillOpen)
                            
                            (exploredPackage.Unlisted && allUnlisted),improveModel (newFilteredVersions,exploredPackage::packages,Set.add dependency closed,newOpen)
                    | ResolvedPackages.Ok _ -> allUnlisted,state)
                        (true,ResolvedPackages.Conflict(closed,stillOpen))
            
            match tryToImprove false with
            | true,ResolvedPackages.Conflict(x,y) -> tryToImprove true |> snd         
            | _,x-> x

    match improveModel (Map.empty, [], Set.empty, Set.ofList rootDependencies) with
    | ResolvedPackages.Conflict(_) as c -> c
    | ResolvedPackages.Ok model -> ResolvedPackages.Ok(cleanupNames model)
