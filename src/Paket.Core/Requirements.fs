module Paket.Requirements

open Paket
open Paket.Domain
open Paket.PackageSources

[<RequireQualifiedAccess>]
type FrameworkRestriction = 
| Exactly of FrameworkIdentifier
| AtLeast of FrameworkIdentifier
| Between of FrameworkIdentifier * FrameworkIdentifier
    
    override this.ToString() =
        match this with    
        | FrameworkRestriction.Exactly r -> r.ToString()
        | FrameworkRestriction.AtLeast r -> ">= " + r.ToString()
        | FrameworkRestriction.Between(min,max) -> sprintf ">= %s < %s" (min.ToString()) (max.ToString())

type FrameworkRestrictions = FrameworkRestriction list

let private minRestriction = FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V1))

let findMaxDotNetRestriction restrictions =
    minRestriction :: restrictions
    |> List.filter (fun (r:FrameworkRestriction) ->
        match r with
        | FrameworkRestriction.Exactly r -> r.ToString().StartsWith("net")
        | _ -> false)
    |> List.max
    |> fun r ->
        match r with
        | FrameworkRestriction.Exactly r -> r
        | _ -> failwith "error"

let optimizeRestrictions packages =
    let grouped = packages |> Seq.groupBy (fun (n,v,_) -> n,v) |> Seq.toList    

    let invertedRestrictions =
        let expanded =
            [for (n,vr,r:FrameworkRestrictions) in packages do
                for r' in r do
                    yield n,vr,r']
            |> Seq.groupBy (fun (_,_,r) -> r)

        [for restriction,packages in expanded do
            match restriction with
            | FrameworkRestriction.Exactly r -> 
                if r.ToString().StartsWith("net") then
                    yield r,packages |> Seq.map (fun (n,v,_) -> n,v) |> Seq.toList
            | _ -> () ]
        |> List.sortBy fst

    let emptyRestrictions =
        [for (n,vr,r:FrameworkRestrictions) in packages do
            if r = [] then
                yield n,vr]
        |> Set.ofList

    [for (name,versionRequirement:VersionRequirement),group in grouped do
        if name <> PackageName "" then
            if not (Set.isEmpty emptyRestrictions) && Set.contains (name,versionRequirement) emptyRestrictions then
                yield name,versionRequirement,[]
            else
                let plain = 
                    group 
                    |> Seq.map (fun (_,_,res) -> res) 
                    |> Seq.concat 
                    |> Seq.toList

                let localMaxDotNetRestriction = findMaxDotNetRestriction plain        

                let restrictions =
                    plain
                    |> List.map (fun restriction ->
                        match restriction with
                        | FrameworkRestriction.Exactly r ->                     
                            if r = localMaxDotNetRestriction then
                                let globalMax = 
                                    invertedRestrictions
                                    |> Seq.skipWhile (fun (r,l) -> r <= localMaxDotNetRestriction && l |> List.exists (fun (n,vr) -> n = name && vr = versionRequirement))
                                    |> Seq.map fst
                                    |> Seq.toList

                                if globalMax = [] || r >= globalMax.Head then
                                    FrameworkRestriction.AtLeast r
                                else
                                    FrameworkRestriction.Between(r,globalMax.Head)
                            else
                                restriction
                        | _ -> restriction)
                    |> Seq.toList
                    |> List.sort

                yield name,versionRequirement,restrictions]

type PackageRequirementSource =
| DependenciesFile of string
| Package of PackageName * SemVerInfo   

/// Represents an unresolved package.
[<CustomEquality;CustomComparison>]
type PackageRequirement =
    { Name : PackageName
      VersionRequirement : VersionRequirement
      ResolverStrategy : ResolverStrategy
      Parent: PackageRequirementSource
      FrameworkRestrictions: FrameworkRestrictions
      Sources : PackageSource list }

    override this.Equals(that) = 
        match that with
        | :? PackageRequirement as that -> this.Name = that.Name && this.VersionRequirement = that.VersionRequirement
        | _ -> false

    override this.ToString() =
        let (PackageName name) = this.Name
        sprintf "%s %s" name (this.VersionRequirement.ToString())

    override this.GetHashCode() = hash (this.Name,this.VersionRequirement)

    member this.IncludingPrereleases() = 
        { this with VersionRequirement = VersionRequirement(this.VersionRequirement.Range,PreReleaseStatus.All) }

    interface System.IComparable with
       member this.CompareTo that = 
          match that with 
          | :? PackageRequirement as that -> 
                if this = that then 0 else
                let c1 =
                    compare 
                       (not this.VersionRequirement.Range.IsGlobalOverride,this.Parent)
                       (not that.VersionRequirement.Range.IsGlobalOverride,this.Parent)
                if c1 <> 0 then c1 else
                let c2 = -1 * compare this.ResolverStrategy that.ResolverStrategy
                if c2 <> 0 then c2 else
                let c3 = -1 * compare this.VersionRequirement that.VersionRequirement
                if c3 <> 0 then c3 else
                compare this.Name that.Name
                
          | _ -> invalidArg "that" "cannot compare value of different types" 
