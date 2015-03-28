namespace Paket

open System

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

    member this.IsIncludedIn (other : VersionRange) =
        match other, this with
        | Minimum v1, Minimum v2 when v1 <= v2 -> true
        | Minimum v1, Specific v2 when v1 <= v2 -> true
        | Specific v1, Specific v2 when v1 = v2 -> true
        | Range(_, min1, max1, _), Specific v2 when min1 <= v2 && max1 >= v2 -> true
        | GreaterThan v1, GreaterThan v2 when v1 < v2 -> true
        | GreaterThan v1, Specific v2 when v1 < v2 -> true
        | _ -> false


    override this.ToString() =
        match this with
        | Specific v -> v.ToString()
        | OverrideAll v -> "== " + v.ToString()
        | Minimum v ->
            match v.ToString() with
            | "0" -> ""
            |  x  -> ">= " + x
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

    /// formats a VersionRange in NuGet syntax
    member this.FormatInNuGetSyntax() =
        match this with
        | Minimum(version) -> 
            match version.ToString() with
            | "0" -> ""
            | x  -> x
        | GreaterThan(version) -> sprintf "(%s,)" (version.ToString())
        | Maximum(version) -> sprintf "(,%s]" (version.ToString())
        | LessThan(version) -> sprintf "(,%s)" (version.ToString())
        | Specific(version) -> sprintf "[%s]" (version.ToString())
        | OverrideAll(version) -> sprintf "[%s]" (version.ToString()) 
        | Range(fromB, from,_to,_toB) -> 
            let getMinDelimiter (v:VersionRangeBound) =
                match v with
                | VersionRangeBound.Including -> "["
                | VersionRangeBound.Excluding -> "("

            let getMaxDelimiter (v:VersionRangeBound) =
                match v with
                | VersionRangeBound.Including -> "]"
                | VersionRangeBound.Excluding -> ")"
        
            sprintf "%s%s,%s%s" (getMinDelimiter fromB) (from.ToString()) (_to.ToString()) (getMaxDelimiter _toB) 

type VersionRequirement =
| VersionRequirement of VersionRange * PreReleaseStatus
    /// Checks wether the given version is in the version range
    member this.IsInRange(version : SemVerInfo,?ignorePrerelease) =         
        let ignorePrerelease = defaultArg ignorePrerelease false
        let (VersionRequirement (range,prerelease)) = this
        let checkPrerelease prerelease version =
            if ignorePrerelease then true else
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

    override this.ToString() = this.Range.ToString()

    /// Parses NuGet version range
    static member Parse (text:string) = 
        if  text = null || text = "" || text = "null" then VersionRequirement.AllReleases else

        let parseRange (text:string) = 
            let failParse() = failwithf "unable to parse %s" text

            let parseBound  = function
                | '[' | ']' -> VersionRangeBound.Including
                | '(' | ')' -> VersionRangeBound.Excluding
                | _         -> failParse()
        
            if not <| text.Contains "," then
                if text.StartsWith "[" then Specific(text.Trim([|'['; ']'|]) |> SemVer.Parse)
                else Minimum(SemVer.Parse text)
            else
                let fromB = parseBound text.[0]
                let toB   = parseBound (Seq.last text)
                let versions = 
                    text
                        .Trim([|'['; ']';'(';')'|])
                        .Split([|','|], StringSplitOptions.RemoveEmptyEntries)
                        |> Array.filter (fun s -> String.IsNullOrWhiteSpace s |> not)
                        |> Array.map SemVer.Parse

                match versions.Length with
                | 2 ->
                    Range(fromB, versions.[0], versions.[1], toB)
                | 1 ->
                    if text.[1] = ',' then
                        match fromB, toB with
                        | VersionRangeBound.Excluding, VersionRangeBound.Including -> Maximum(versions.[0])
                        | VersionRangeBound.Excluding, VersionRangeBound.Excluding -> LessThan(versions.[0])
                        | VersionRangeBound.Including, VersionRangeBound.Including -> Maximum(versions.[0])
                        | _ -> failParse()
                    else 
                        match fromB, toB with
                        | VersionRangeBound.Excluding, VersionRangeBound.Excluding -> GreaterThan(versions.[0])
                        | VersionRangeBound.Including, VersionRangeBound.Including -> Minimum(versions.[0])
                        | _ -> failParse()
                | _ -> failParse()
        VersionRequirement(parseRange text,PreReleaseStatus.No)

/// Represents a resolver strategy.
[<RequireQualifiedAccess>]
type ResolverStrategy =
| Max
| Min
