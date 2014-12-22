module Paket.Requirements

open Paket
open Paket.Domain
open Paket.PackageSources

[<RequireQualifiedAccess>]
type FrameworkRestriction = 
| Exactly of FrameworkIdentifier
| AtLeast of FrameworkIdentifier
| Between of FrameworkIdentifier * FrameworkIdentifier

type FrameworkRestrictions = FrameworkRestriction list

let optimizeRestrictions packages =
    let grouped = packages |> Seq.groupBy (fun (n,v,_) -> n,v)

    [for (name,versionRequirement),group in grouped do
        let plain = Seq.map (fun (_,_,res) -> res) group
        let restrictions =
            plain
            |> Seq.concat
            |> Seq.toList

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
