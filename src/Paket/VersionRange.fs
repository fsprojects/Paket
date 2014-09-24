namespace Paket

/// Defines if the range is open or closed.
type Bound = 
    | Excluding
    | Including

/// Represents version information.
type VersionRange = 
    | Specific of SemVerInfo
    | Minimum of SemVerInfo
    | GreaterThan of SemVerInfo
    | Maximum of SemVerInfo
    | LessThan of SemVerInfo
    | Range of fromB : Bound * from : SemVerInfo * _to : SemVerInfo * _toB : Bound
    
    /// Checks wether the given version is in the version range
    member this.IsInRange(version : SemVerInfo) =                      
        match this with
        | Specific v -> v = version
        | Minimum v -> v <= version && version.PreRelease = None
        | GreaterThan v -> v < version && version.PreRelease = None
        | Maximum v -> v >= version && version.PreRelease = None
        | LessThan v -> v > version && version.PreRelease = None
        | Range(fromB, from, _to, _toB) ->
            let isInUpperBound = 
                match _toB with
                | Including -> version <= _to
                | Excluding -> version < _to

            let isInLowerBound =
                match fromB with
                | Including -> version >= from
                | Excluding -> version > from

            isInLowerBound && isInUpperBound  && version.PreRelease = None
   
    override this.ToString() =
        match this with
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


    static member AtLeast version = Minimum(SemVer.parse version)

    static member NoRestriction = Minimum(SemVer.parse "0")

    static member Exactly version = Specific(SemVer.parse version)

    static member Between(version1,version2) = Range(Including, SemVer.parse version1, SemVer.parse version2, Excluding)

/// Represents a resolver strategy.
type ResolverStrategy =
| Max
| Min
