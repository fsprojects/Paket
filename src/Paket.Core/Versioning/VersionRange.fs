namespace Paket

open System
open System.Collections.Generic

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
    
    static member Between(lower,minimum,maximum,upper) =
        Range(lower,minimum,maximum,upper)      

    static member Between(lower,version1,version2,upper) =
        let minimum = SemVer.Parse version1
        let maximum = SemVer.Parse version2
        VersionRange.Between(lower,minimum,maximum,upper)
        
    static member Between(minimum:string,maximum:string) =
        VersionRange.Between(VersionRangeBound.Including,minimum,maximum,VersionRangeBound.Excluding)
              
    static member Between(minimum:SemVerInfo,maximum:SemVerInfo) =
        VersionRange.Between(VersionRangeBound.Including,minimum,maximum,VersionRangeBound.Excluding)
        
    member x.IsGlobalOverride = match x with | OverrideAll _ -> true | _ -> false

    member this.IsIncludedIn (other : VersionRange) =
        match other, this with
        | Minimum v1, Minimum v2 when v1 <= v2 -> true
        | Minimum v1, Specific v2 when v1 <= v2 -> true
        | Minimum v1, Range(_, min2, max2, _) when v1 <= min2 && v1 <= max2 -> true
        | Specific v1, Specific v2 when v1 = v2 -> true
        | GreaterThan v1, GreaterThan v2 when v1 < v2 -> true
        | GreaterThan v1, Specific v2 when v1 < v2 -> true
        | GreaterThan v1, Range(_, min2, max2, _) when v1 < min2 && v1 < max2 -> true
        | Range(lower, min1, max1, upper), Specific v2 -> 
            let left, right = 
                match lower, upper with
                | VersionRangeBound.Excluding, VersionRangeBound.Excluding -> (<), (>)
                | VersionRangeBound.Including, VersionRangeBound.Excluding -> (<=), (>)
                | VersionRangeBound.Excluding, VersionRangeBound.Including -> (<), (>=)
                | VersionRangeBound.Including, VersionRangeBound.Including -> (<=), (>=)
            left min1 v2 && right max1 v2 
        | Range(from1, min1, max1, upto1), Range(from2, min2, max2, upto2) ->
            let lowerMatch = 
                match from1, from2 with
                | VersionRangeBound.Excluding, VersionRangeBound.Including -> min1 < min2
                | _ -> min1 <= min2
            let upperMatch = 
                match upto1, upto2 with
                | VersionRangeBound.Including, VersionRangeBound.Excluding -> max2 < max1
                | _ -> max2 <= max1
            lowerMatch && upperMatch
        | _ -> false

    member this.GetPreReleaseStatus =
        let prerelease =
            match this with 
            | Minimum v1 -> v1.PreRelease
            | GreaterThan v1 -> v1.PreRelease 
            | Maximum v1 -> v1.PreRelease
            | LessThan v1 -> v1.PreRelease
            | Specific v1 -> v1.PreRelease
            | OverrideAll v1 -> v1.PreRelease
            | Range(_,l1,r1,_) -> 
                match l1.PreRelease with
                | Some(p) -> Some(p)
                | None -> r1.PreRelease
        match prerelease with 
        | Some(prerelease) -> 
            match prerelease.Name with
            | null | "" -> PreReleaseStatus.No
            | "prerelease" -> PreReleaseStatus.All
            | name -> PreReleaseStatus.Concrete [name]
        | None -> PreReleaseStatus.No
    
    member this.IsConflicting (other : VersionRange) =
        (other, this.GetPreReleaseStatus, other.GetPreReleaseStatus) |> this.IsConflicting 

    member this.IsConflicting (tuple : VersionRange * PreReleaseStatus * PreReleaseStatus) =
        let checkPre pre1 pre2 =
            match pre1 with 
            | PreReleaseStatus.No -> true
            | PreReleaseStatus.All -> pre2 = PreReleaseStatus.No
            | PreReleaseStatus.Concrete list1 ->
                match pre2 with 
                | PreReleaseStatus.No -> true
                | PreReleaseStatus.All -> false 
                | PreReleaseStatus.Concrete list2 -> list1.Head <> list2.Head
        
        let other, pre1, pre2 = tuple   
             
        let (>) v1 v2 = v1 > v2 && checkPre pre1 pre2
        let (<) v1 v2 = v1 < v2 && checkPre pre1 pre2
        
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
        | VersionRequirement(v1,p1), VersionRequirement(v2,p2) ->
            (v2,p1,p2) |> v1.IsConflicting

    member this.PreReleases =
        match this with
        | VersionRequirement(_,prereleases) -> prereleases

    static member AllReleases = VersionRequirement(Minimum(SemVer.Zero),PreReleaseStatus.No)
    static member NoRestriction = VersionRequirement(Minimum(SemVer.Zero),PreReleaseStatus.All)

    override this.ToString() = this.Range.ToString()

    /// Parses NuGet V2 version range
    static member Parse text =
        if String.IsNullOrWhiteSpace text || text = "null" then VersionRequirement.AllReleases else

        let prereleases = List<string>()
        let analyzeVersion operator (text:string) =
            try
              if text.Contains "*" then
                  let v = SemVer.Parse (text.Replace("*","0"))
                  match v.PreRelease with
                  | Some p -> prereleases.Add(p.Name)
                  | _      -> ignore()
                  VersionRange.Minimum v
              else
                  let v = SemVer.Parse text
                  match v.PreRelease with
                  | Some p -> prereleases.Add(p.Name)
                  | _      -> ignore()
                  operator v
            with
            | exn -> failwithf "Error while parsing %s%sMessage: %s" text Environment.NewLine exn.Message

        let analyzeVersionSimple (text:string) =
            try
                let v = SemVer.Parse (text.Replace("*","0"))
                match v.PreRelease with
                | Some p -> prereleases.Add(p.Name)
                | _      -> ignore()
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
                    VersionRange.Between(fromB,from,_to,_toB)
            | x -> x
            
        let range = parseRange text
        
        let prerelease = 
            match prereleases |> Seq.where (fun s -> not(String.IsNullOrEmpty s)) |> Seq.distinct |> List.ofSeq with
            | [] -> PreReleaseStatus.No
            | list when list |> List.contains "prerelease" -> PreReleaseStatus.All 
            | list -> PreReleaseStatus.Concrete list
            
        VersionRequirement(range,prerelease)


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
                | Concrete name -> "-" + List.head name
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
