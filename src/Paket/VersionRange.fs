namespace Paket

/// Defines if the range is open or closed.
type Bound = 
    | Excluding
    | Including

/// Defines if the range accepts prereleases
type PreReleaseStatus = 
    | No
    | AllPrereleases
    | Beta
    | ReleaseCandidate

/// Represents version information.
type VersionRange = 
    | Specific of SemVerInfo
    | Minimum of SemVerInfo
    | GreaterThan of SemVerInfo
    | Maximum of SemVerInfo
    | LessThan of SemVerInfo
    | Range of fromB : Bound * from : SemVerInfo * _to : SemVerInfo * _toB : Bound
      

    static member AtLeast version = Minimum(SemVer.parse version)

    static member Exactly version = Specific(SemVer.parse version)

    static member Between(version1,version2) = Range(Including, SemVer.parse version1, SemVer.parse version2, Excluding)

type VersionRequirement =
| VersionRequirement of VersionRange * PreReleaseStatus
    /// Checks wether the given version is in the version range
    member this.IsInRange(version : SemVerInfo) =         
        match this with
        | VersionRequirement(range,prerelease) ->
            let checkPrerelease prerelease version = version.PreRelease = None
            match range with
            | Specific v -> v = version
            | Minimum v -> v <= version && checkPrerelease prerelease version
            | GreaterThan v -> v < version && checkPrerelease prerelease version
            | Maximum v -> v >= version && checkPrerelease prerelease version
            | LessThan v -> v > version && checkPrerelease prerelease version
            | Range(fromB, from, _to, _toB) ->
                let isInUpperBound = 
                    match _toB with
                    | Including -> version <= _to
                    | Excluding -> version < _to

                let isInLowerBound =
                    match fromB with
                    | Including -> version >= from
                    | Excluding -> version > from

                isInLowerBound && isInUpperBound  && checkPrerelease prerelease version

    member this.Range =
        match this with
        | VersionRequirement(range,_) -> range

    static member NoRestriction = VersionRequirement(Minimum(SemVer.parse "0"),PreReleaseStatus.No)

    override this.ToString() =
        match this.Range with
        | Specific v -> v.ToString()
        | Minimum v -> ">= " + v.ToString()
        | GreaterThan v -> "> " + v.ToString()
        | Maximum v -> "<= " + v.ToString()
        | LessThan v -> "< " + v.ToString()
        | Range(fromB, from, _to, _toB) ->
            let from = 
                match fromB with
                 | Excluding -> "> " + from.ToString()
                 | Including -> ">= " + from.ToString()

            let _to = 
                match _toB with
                 | Excluding -> "< " + _to.ToString()
                 | Including -> "<= " + _to.ToString()

            from + " " + _to

/// Represents a resolver strategy.
type ResolverStrategy =
| Max
| Min
