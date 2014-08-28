namespace Paket

type VersionRange = 
    | Latest
    | AtLeast of string
    | Exactly of string
    | Between of string * string
    /// Checks wether the given version is in the version range
    member this.IsInRange version = 
        match this with
        | AtLeast v -> v <= version
        | Exactly v -> v = version
        | Between(min, max) -> version >= min && version < max

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
