namespace Paket

/// Defines if the range bound is including or excluding.
[<RequireQualifiedAccess>]
type VersionRangeBound = 
    | Excluding
    | Including

/// Defines if the range accepts prereleases
type PreReleaseStatus = 
    | No
    | All
    | Concrete of string list

/// Represents version information.
type VersionRange =     
    | Minimum of SemVerInfo
    | GreaterThan of SemVerInfo
    | Maximum of SemVerInfo
    | LessThan of SemVerInfo
    | Specific of SemVerInfo    
    | OverrideAll of SemVerInfo
    | Range of fromB : VersionRangeBound * from : SemVerInfo * _to : SemVerInfo * _toB : VersionRangeBound 

    static member AtLeast version = Minimum(SemVer.Parse version)

    static member Exactly version = Specific(SemVer.Parse version)

    static member Between(version1,version2) = Range(VersionRangeBound.Including, SemVer.Parse version1, SemVer.Parse version2, VersionRangeBound.Excluding)

    member x.IsGlobalOverride = match x with | OverrideAll _ -> true | _ -> false

type VersionRequirement =
| VersionRequirement of VersionRange * PreReleaseStatus
    /// Checks wether the given version is in the version range
    member this.IsInRange(version : SemVerInfo) =         
        let (VersionRequirement (range,prerelease)) = this
        let checkPrerelease prerelease version = 
            match prerelease with
            | PreReleaseStatus.All -> true
            | PreReleaseStatus.No -> version.PreRelease = None
            | PreReleaseStatus.Concrete list ->
                 match version.PreRelease with
                 | None -> true
                 | Some pre -> List.exists ((=) pre.Name) list

        match range with
        | Specific v -> v = version
        | OverrideAll v -> v = version
        | Minimum v -> v = version || (v <= version && checkPrerelease prerelease version)
        | GreaterThan v -> v < version && checkPrerelease prerelease version
        | Maximum v -> v = version || (v >= version && checkPrerelease prerelease version)
        | LessThan v -> v > version && checkPrerelease prerelease version
        | Range(fromB, from, _to, _toB) ->
            let isInUpperBound = 
                match _toB with
                | VersionRangeBound.Including -> version <= _to
                | VersionRangeBound.Excluding -> version < _to

            let isInLowerBound =
                match fromB with
                | VersionRangeBound.Including -> version >= from
                | VersionRangeBound.Excluding -> version > from

            isInLowerBound && isInUpperBound  && checkPrerelease prerelease version

    member this.Range =
        match this with
        | VersionRequirement(range,_) -> range

    member this.PreReleases =
        match this with
        | VersionRequirement(_,prereleases) -> prereleases

    static member AllReleases = VersionRequirement(Minimum(SemVer.Parse "0"),PreReleaseStatus.No)
    static member NoRestriction = VersionRequirement(Minimum(SemVer.Parse "0"),PreReleaseStatus.All)

    override this.ToString() =
        match this.Range with
        | Specific v -> v.ToString()
        | OverrideAll v -> "== " + v.ToString()
        | Minimum v -> ">= " + v.ToString()
        | GreaterThan v -> "> " + v.ToString()
        | Maximum v -> "<= " + v.ToString()
        | LessThan v -> "< " + v.ToString()
        | Range(fromB, from, _to, _toB) ->
            let from = 
                match fromB with
                 | VersionRangeBound.Excluding -> "> " + from.ToString()
                 | VersionRangeBound.Including -> ">= " + from.ToString()

            let _to = 
                match _toB with
                 | VersionRangeBound.Excluding -> "< " + _to.ToString()
                 | VersionRangeBound.Including -> "<= " + _to.ToString()

            from + " " + _to

/// Represents a resolver strategy.
type ResolverStrategy =
| Max
| Min
