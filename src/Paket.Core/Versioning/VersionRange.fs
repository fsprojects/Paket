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
    static member AtMost version = Maximum(SemVer.Parse version)

    static member BasicOperators = ["~>";"==";"<=";">=";"=";">";"<"]
    static member StrategyOperators = ['!';'@']
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

    member this.IsConflicting (other : VersionRange) =
        let checkPre (v:SemVerInfo) = v.PreRelease.IsNone
        let (>) v1 v2 = v1 > v2 && checkPre v2
        let (<) v1 v2 = v1 < v2 && checkPre v2

        let isConflict this other =
            match this, other with
            | Minimum v1, Specific v2 when v1 > v2 -> true
            | Minimum v1, Maximum v2 when v1 > v2 -> true
            | Minimum v1, LessThan v2 when v1 > v2 -> true
            | Specific v1, Specific v2 when v1 <> v2 -> true
            | Range(_, min1, max1, _), Specific v2 when min1 > v2 || max1 < v2 -> true
            | Range(_, min1, max1, _), Range(_, min2, max2, _) when max1 < min2 || max2 < min1 -> true
            | Range(_, _, max1, _), Minimum min2 when max1 < min2  -> true
            | Range(_, _, max1, _), GreaterThan min2 when max1 < min2 -> true
            | Range(_, min1, _, _), Maximum max2 when max2 < min1 -> true
            | Range(_, min1, _, _), LessThan max2 when max2 < min1 -> true
            | GreaterThan v1, Specific v2 when v1 > v2 -> true
            | LessThan v1, Specific v2 when v1 < v2 -> true
            | _ -> false

        isConflict this other || isConflict other this

    override this.ToString() =
        match this with
        | Specific v -> v.NormalizeToShorter()
        | OverrideAll v -> "== " + v.ToString()
        | Minimum v ->
            match v.NormalizeToShorter() with
            | "0" -> ""
            | "0.0" -> ""
            |  x  -> ">= " + x
        | GreaterThan v -> "> " + v.NormalizeToShorter()
        | Maximum v -> "<= " + v.NormalizeToShorter()
        | LessThan v -> "< " + v.NormalizeToShorter()
        | Range(fromB, from, _to, _toB) ->
            let from =
                match fromB with
                | VersionRangeBound.Excluding -> "> " + from.NormalizeToShorter()
                | VersionRangeBound.Including -> ">= " + from.NormalizeToShorter()

            let _to =
                match _toB with
                | VersionRangeBound.Excluding -> "< " + _to.NormalizeToShorter()
                | VersionRangeBound.Including -> "<= " + _to.NormalizeToShorter()

            from + " " + _to


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

        let sameVersionWithoutPreRelease v =
            let sameVersion =
                match v.PreRelease with
                | None -> v.Major = version.Major && v.Minor = version.Minor && v.Patch = version.Patch && v.Build = version.Build
                | _ -> false

            prerelease <> PreReleaseStatus.No && sameVersion && checkPrerelease prerelease version

        match range with
        | Specific v -> v = version || sameVersionWithoutPreRelease v
        | OverrideAll v -> v = version
        | Minimum v -> v = version || (v <= version && checkPrerelease prerelease version) || sameVersionWithoutPreRelease v
        | GreaterThan v -> v < version && checkPrerelease prerelease version
        | Maximum v -> v = version || (v >= version && checkPrerelease prerelease version)
        | LessThan v -> v > version && checkPrerelease prerelease version && not (sameVersionWithoutPreRelease v)
        | Range(fromB, from, _to, _toB) ->
            let isInUpperBound =
                match _toB with
                | VersionRangeBound.Including -> version <= _to
                | VersionRangeBound.Excluding -> version < _to && not (sameVersionWithoutPreRelease _to)

            let isInLowerBound =
                match fromB with
                | VersionRangeBound.Including -> version >= from
                | VersionRangeBound.Excluding -> version > from

            (isInLowerBound && isInUpperBound && checkPrerelease prerelease version) || sameVersionWithoutPreRelease from

    member this.Range =
        match this with
        | VersionRequirement(range,_) -> range

    member this.IsConflicting (other : VersionRequirement) =
        match other, this with
        | VersionRequirement(v1,_), VersionRequirement(v2,_) ->
            v1.IsConflicting(v2)

    member this.PreReleases =
        match this with
        | VersionRequirement(_,prereleases) -> prereleases

    static member AllReleases = VersionRequirement(Minimum(SemVer.Zero),PreReleaseStatus.No)
    static member NoRestriction = VersionRequirement(Minimum(SemVer.Zero),PreReleaseStatus.All)

    override this.ToString() = this.Range.ToString()

    /// Parses NuGet V2 version range
    static member Parse text =
        if String.IsNullOrWhiteSpace text || text = "null" then VersionRequirement.AllReleases else

        let prereleases = ref PreReleaseStatus.No
        let analyzeVersion operator (text:string) =
            try
              if text.Contains "*" then
                  let v = SemVer.Parse (text.Replace("*","0"))
                  match v.PreRelease with
                  | Some _ -> prereleases := PreReleaseStatus.All
                  | _      -> prereleases := PreReleaseStatus.No
                  VersionRange.Minimum v
              else
                  let v = SemVer.Parse text
                  match v.PreRelease with
                  | Some _ -> prereleases := PreReleaseStatus.All
                  | _      -> prereleases := PreReleaseStatus.No
                  operator v
            with
            | exn -> failwithf "Error while parsing %s%sMessage: %s" text Environment.NewLine exn.Message

        let analyzeVersionSimple (text:string) =
            try
                let v = SemVer.Parse (text.Replace("*","0"))
                match v.PreRelease with
                | Some _ -> prereleases := PreReleaseStatus.All
                | _      -> prereleases := PreReleaseStatus.No
                v
            with
            | exn -> failwithf "Error while parsing %s%sMessage: %s" text Environment.NewLine exn.Message

        let parseRange (text:string) =

            let parseBound s = 
                match s with
                | '[' | ']' -> VersionRangeBound.Including
                | '(' | ')' -> VersionRangeBound.Excluding
                | _         -> failwithf "unable to parse bound %O in %s" s text

            let parsed =
                if not (text.Contains ",") then
                    if text.StartsWith "[" then 
                        text.Trim([|'['; ']'|]) 
                        |> analyzeVersion Specific
                    else analyzeVersion Minimum text
                else
                    let fromB = parseBound text.[0]
                    let toB   = parseBound (Seq.last text)
                    let versions =
                        text
                            .Trim([|'['; ']';'(';')'|])
                            .Split([|','|], StringSplitOptions.RemoveEmptyEntries)
                            |> Array.filter (String.IsNullOrWhiteSpace >> not)
                            |> Array.map analyzeVersionSimple

                    match versions.Length with
                    | 2 ->
                        Range(fromB, versions.[0], versions.[1], toB)
                    | 1 ->
                        if text.[1] = ',' then
                            match fromB, toB with
                            | VersionRangeBound.Excluding, VersionRangeBound.Including -> Maximum(versions.[0])
                            | VersionRangeBound.Excluding, VersionRangeBound.Excluding -> LessThan(versions.[0])
                            | VersionRangeBound.Including, VersionRangeBound.Including -> Maximum(versions.[0])
                            | _ -> failwithf "unable to parse %s" text
                        else
                            match fromB, toB with
                            | VersionRangeBound.Excluding, VersionRangeBound.Excluding -> GreaterThan(versions.[0])
                            | VersionRangeBound.Including, VersionRangeBound.Including -> Minimum(versions.[0])
                            | VersionRangeBound.Including, VersionRangeBound.Excluding -> Minimum(versions.[0])
                            | _ -> failwithf "unable to parse %s" text
                    | 0 -> Minimum(SemVer.Zero)
                    | _ -> failwithf "unable to parse %s" text
            match parsed with
            | Range(fromB, from, _to, _toB) -> 
                if (fromB = VersionRangeBound.Including) && (_toB = VersionRangeBound.Including) && (from = _to) then
                    Specific from
                else
                    parsed
            | x -> x
        let range = parseRange text

        VersionRequirement(range,!prereleases)


    static member TryParse text =
        try VersionRequirement.Parse text |> Some
        with _ -> None


    /// Formats a VersionRequirement in NuGet syntax
    member this.FormatInNuGetSyntax() =
        match this with
        | VersionRequirement(range,prerelease) ->
            let pre =
                match prerelease with
                | No -> ""
                | Concrete [x] -> "-" + x
                | _ -> "-prerelease"

            let normalize (v:SemVerInfo) =
                let s = 
                    let u = v.ToString()
                    let n = v.Normalize()
                    if u.Length > n.Length then u else n // Do not short version since Klondike doesn't understand

                if s.Contains("-") then s else s + pre

            let str =
                match range with
                | Minimum(version) ->
                    match normalize version with
                    | "0.0.0" -> ""
                    | x  -> x
                | GreaterThan(version) -> sprintf "(%s,)" (normalize version)
                | Maximum(version) -> sprintf "(,%s]" (normalize version)
                | LessThan(version) -> sprintf "(,%s)" (normalize version)
                | Specific(version) -> 
                    let v = normalize version
                    if v.EndsWith "-prerelease" then
                        let getMinDelimiter (v:VersionRangeBound) =
                            match v with
                            | VersionRangeBound.Including -> "["
                            | VersionRangeBound.Excluding -> "("

                        let getMaxDelimiter (v:VersionRangeBound) =
                            match v with
                            | VersionRangeBound.Including -> "]"
                            | VersionRangeBound.Excluding -> ")"

                        sprintf "[%s,%s]" v (v.Replace("-prerelease",""))
                    else
                        sprintf "[%s]" v
                | OverrideAll(version) -> sprintf "[%s]" (normalize version)
                | Range(fromB, from,_to,_toB) ->
                    let getMinDelimiter (v:VersionRangeBound) =
                        match v with
                        | VersionRangeBound.Including -> "["
                        | VersionRangeBound.Excluding -> "("

                    let getMaxDelimiter (v:VersionRangeBound) =
                        match v with
                        | VersionRangeBound.Including -> "]"
                        | VersionRangeBound.Excluding -> ")"

                    sprintf "%s%s,%s%s" (getMinDelimiter fromB) (normalize from) (normalize _to) (getMaxDelimiter _toB)


            match str with
            | "0" -> ""
            | versionStr -> versionStr

/// Represents a resolver strategy.
[<RequireQualifiedAccess>]
type ResolverStrategy =
| Max
| Min
