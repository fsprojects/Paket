module Paket.Requirements

open Paket
open Paket.PackageSources

type PackageRequirementSource =
| DependenciesFile of string
| Package of string * SemVerInfo   

/// Represents an unresolved package.
[<CustomEquality;CustomComparison>]
type PackageRequirement =
    { Name : string
      VersionRequirement : VersionRequirement
      ResolverStrategy : ResolverStrategy
      Parent: PackageRequirementSource
      Sources : PackageSource list }
    override this.Equals(that) = 
        match that with
        | :? PackageRequirement as that -> this.Name = that.Name && this.VersionRequirement = that.VersionRequirement
        | _ -> false

    override this.GetHashCode() = hash (this.Name,this.VersionRequirement)

    interface System.IComparable with
       member this.CompareTo that = 
          match that with 
          | :? PackageRequirement as that -> compare (this.Parent,this.Name,this.VersionRequirement) (that.Parent,that.Name,that.VersionRequirement)
          | _ -> invalidArg "that" "cannot compare value of different types" 