namespace Paket

/// Represents version information.
type VersionRange = 
    | Latest
    | Minimum of SemVerInfo
    | Specific of SemVerInfo
    | Range of SemVerInfo * SemVerInfo
    /// Checks wether the given version is in the version range
    member this.IsInRange(version:string) =
        this.IsInRange(SemVer.parse version)

    /// Checks wether the given version is in the version range
    member this.IsInRange(version:SemVerInfo) =
        match this with
        | Latest -> true
        | Minimum v -> v <= version
        | Specific v -> v = version
        | Range(min, max) -> version >= min && version < max

    static member AtLeast version = Minimum(SemVer.parse version)

    static member Exactly version = Specific(SemVer.parse version)

    static member Between(version1,version2) = Range(SemVer.parse version1, SemVer.parse version2)

/// Represents a package.
type Package = 
    { Name : string
      VersionRange : VersionRange
      SourceType : string
      Source : string }

/// Represents a package dependency.
type PackageDependency = 
    { Defining : Package
      Referenced : Package }

/// Represents a dependency.
type Dependency = 
    | FromRoot of Package
    | FromPackage of PackageDependency
    member this.Referenced = 
        match this with
        | FromRoot p -> p
        | FromPackage d -> d.Referenced

/// Represents package details
type PackageDetails =  string * Package list

/// Interface for discovery APIs.
type IDiscovery = 
    abstract GetPackageDetails : bool * string * string * string * string -> Async<PackageDetails>
    abstract GetVersions : string * string * string -> Async<string seq>

/// Represents a resolved dependency.
type ResolvedDependency = 
    | Resolved of Dependency
    | Conflict of Dependency * Dependency

/// Represents a complete dependency resolution.
type PackageResolution = {
    DirectDependencies : Map<string*string,Package list>
    ResolvedVersionMap : Map<string,ResolvedDependency>
}
