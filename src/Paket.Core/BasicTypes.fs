namespace Paket

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
        | Minimum v -> v <= version
        | Specific v -> v = version
        | Range(min, max) -> version >= min && version < max

    static member AtLeast version = Minimum(SemVer.parse version)

    static member Exactly version = Specific(SemVer.parse version)

    static member Between(version1,version2) = Range(SemVer.parse version1, SemVer.parse version2)


type Package = 
    { Name : string
      VersionRange : VersionRange
      SourceType : string
      Source : string }

type PackageDependency = 
    { Defining : Package
      Referenced : Package }

type Dependency = 
    | FromRoot of Package
    | FromPackage of PackageDependency
    member this.Referenced = 
        match this with
        | FromRoot p -> p
        | FromPackage d -> d.Referenced

type IDiscovery = 
    abstract GetDirectDependencies : string * string * string * string -> Async<Package list>
    abstract GetVersions : string * string * string -> Async<string seq>

type ResolvedVersion = 
    | Resolved of Dependency
    | Conflict of Dependency * Dependency

type PackageResolution =  Map<string,ResolvedVersion>