namespace Paket

/// Defines if the range is open or closed.
type Bound = 
    | Open
    | Closed

/// Represents version information.
type VersionRange = 
    | Latest
    | Specific of SemVerInfo
    | Minimum of SemVerInfo
    | GreaterThan of SemVerInfo
    | Maximum of SemVerInfo
    | LessThan of SemVerInfo
    | Range of fromB : Bound * from : SemVerInfo * _to : SemVerInfo * _toB : Bound
    
    /// Checks wether the given version is in the version range
    member this.IsInRange(version : SemVerInfo) = 
        match this with
        | Latest -> true
        | Specific v -> v = version
        | Minimum v -> v <= version
        | GreaterThan v -> v < version
        | Maximum v -> v >= version
        | LessThan v -> v > version
        | Range(fromB, from, _to, _toB) ->             
            let isInUpperBound = 
                match _toB with
                | Closed -> version <= _to
                | Open -> version < _to

            let isInLowerBound =
                match fromB with
                | Closed -> version >= from
                | Open -> version > from

            isInLowerBound && isInUpperBound
   
    override this.ToString() =
        match this with
        | Latest -> "Latest"
        | Specific v -> v.ToString()
        | Minimum v -> ">= " + v.ToString()
        | GreaterThan v -> "> " + v.ToString()
        | Maximum v -> "<= " + v.ToString()
        | LessThan v -> "< " + v.ToString()
        | Range(fromB, from, _to, _toB) ->
            let from = 
                match fromB with
                 | Open -> "> " + from.ToString()
                 | Closed -> ">= " + from.ToString()

            let _to = 
                match _toB with
                 | Open -> "< " + _to.ToString()
                 | Closed -> "<= " + _to.ToString()

            from + " " + _to


    static member AtLeast version = Minimum(SemVer.parse version)

    static member Exactly version = Specific(SemVer.parse version)

    static member Between(version1,version2) = Range(Closed, SemVer.parse version1, SemVer.parse version2, Open)

/// Represents a resolver strategy.
type ResolverStrategy =
| Max
| Min

/// Represents the package source type.
type PackageSource =
| Nuget of string
| LocalNuget of string
    override this.ToString() =
        match this with
        | Nuget url -> url
        | LocalNuget path -> path

    static member Parse source = 
        match System.Uri.TryCreate(source, System.UriKind.Absolute) with
        | true, uri -> if uri.Scheme = System.Uri.UriSchemeFile then LocalNuget(source) else Nuget(source)
        | _ -> failwith "unable to parse package source: %s" source

/// Represents a package.
type Package = 
    { Name : string
      DirectDependencies : string list
      VersionRange : VersionRange
      ResolverStrategy : ResolverStrategy
      Sources : PackageSource list }

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
type PackageDetails = {
    Source: PackageSource 
    DownloadLink: string
    DirectDependencies : Package list }

/// Interface for discovery APIs.
type IDiscovery = 
    abstract GetPackageDetails : bool * PackageSource list * string * ResolverStrategy * string -> Async<PackageDetails>
    abstract GetVersions : PackageSource list * string -> Async<(string seq) []>

/// Represents a resolved dependency.
type ResolvedDependency = 
    | Resolved of Dependency
    | Conflict of Dependency * Dependency

/// Represents a complete dependency resolution.
type PackageResolution = {
    DirectDependencies : Map<string*string,Package list>
    ResolvedVersionMap : Map<string,ResolvedDependency>
}