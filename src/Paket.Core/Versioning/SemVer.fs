namespace Paket

open System
open System.Globalization
open System.Text.RegularExpressions

[<CustomEquality; CustomComparison>]
type PreReleaseSegment = 
    | AlphaNumeric of string
    | Numeric of bigint

    member x.CompareTo(y) =
        match x, y with
        | AlphaNumeric a, AlphaNumeric b -> compare a b
        | Numeric a, Numeric b -> compare a b
        | AlphaNumeric a, Numeric b -> 1
        | Numeric a , AlphaNumeric b -> -1

    interface System.IComparable with
        member x.CompareTo yobj =
            match yobj with
            | :? PreReleaseSegment as y -> x.CompareTo(y)                
            | _ -> invalidArg "yobj" "can't compare to other types of objects."
            
    override x.GetHashCode() = hash x
    
    member x.Equals(y) =
        match x, y with
        | AlphaNumeric a, AlphaNumeric b -> a = b
        | Numeric a, Numeric b -> a = b
        | AlphaNumeric a, Numeric b -> false
        | Numeric a , AlphaNumeric b -> false

    override x.Equals yobj = 
        match yobj with 
        | :? PreReleaseSegment as y -> x.Equals(y)
        | _ -> false

/// Information about PreRelease packages.
[<CustomEquality; CustomComparison>]
type PreRelease = 
    { Origin : string
      Name : string
      Values : PreReleaseSegment list }
      
    static member TryParse (str : string) = 
        if String.IsNullOrEmpty str then None
        else
            let getName fromList =
                match fromList with
                | AlphaNumeric(a)::_ -> a
                | _::AlphaNumeric(a)::_ -> a // fallback to 2nd
                | _ -> ""
                
            let parse (segment:string) =
                match bigint.TryParse segment with
                | true, number when number >= 0I -> Numeric number
                | _ -> AlphaNumeric segment
                
            let notEmpty = StringSplitOptions.RemoveEmptyEntries
            let name, values = 
                match str.Split([|'.'|],notEmpty) with
                | [|one|] -> 
                    // without semver1 embedded numbers but allow hyphens
                    let prefix = 
                        Regex(@"(?in)^(?<name>[a-z]+(-[a-z]+)*)")
                    let preName =
                        match prefix.Match(one) with 
                        | ex when ex.Success -> ex.Value
                        | _ -> // "1.2.3.4.5-alpha-45" ==> "alpha"
                            let list = one.Split([|'-'|],notEmpty) 
                                        |> Array.map parse |> List.ofArray 
                            getName list
                        
                    preName, [parse one] // both semver 1 and 2 compliant
                                    
                | multiple -> //semver2: dashes are ok, inline numbers not
                
                    let list = multiple |> Array.map parse |> List.ofArray
                    getName list, list
                    
            Some { Origin = str; Name = name; Values = values }

    member x.Equals(y) = x.Origin = y.Origin

    override x.Equals(yobj) = 
        match yobj with
        | :? PreRelease as y -> x.Equals(y)
        | _ -> false
        
    override x.ToString() = x.Origin
    
    override x.GetHashCode() = hash x.Origin
    
    member x.CompareTo(yobj) = 
        let rec cmp item count xlist ylist = 
            if item < count then
                let res = compare (List.head xlist) (List.head ylist)
                if res = 0 then 
                    cmp (item + 1) count (List.tail xlist) (List.tail ylist)
                else
                    res // result given by first difference
            else
                sign xlist.Length - ylist.Length // https://semver.org/#spec-item-11
        let len = min x.Values.Length yobj.Values.Length // compare up to common len
        cmp 0 len x.Values yobj.Values
        
    interface System.IComparable with
        member x.CompareTo yobj =
            match yobj with
            | :? PreRelease as y -> x.CompareTo(y)
            | _ -> invalidArg "yobj" "PreRelease: cannot compare to values of different types"


/// Contains the version information.
[<CustomEquality; CustomComparison; StructuredFormatDisplay("{AsString}")>]
type SemVerInfo = 
    { /// MAJOR version when you make incompatible API changes.
      Major : uint32
      /// MINOR version when you add functionality in a backwards-compatible manner.
      Minor : uint32
      /// PATCH version when you make backwards-compatible bug fixes.
      Patch : uint32
      /// The optional PreRelease version
      PreRelease : PreRelease option
      /// The optional build no.
      Build : bigint
      BuildMetaData : string
      // The original version text
      Original : string option }
    
    member x.Normalize() = 
        let build = 
            if x.Build > 0I then ("." + x.Build.ToString("D")) else ""
                        
        let pre = 
            match x.PreRelease with
            | Some preRelease -> ("-" + preRelease.Origin)
            | None -> ""

        sprintf "%d.%d.%d%s%s" x.Major x.Minor x.Patch build pre

    member x.NormalizeToShorter() = 
        let s = x.Normalize()
        let s2 = sprintf "%d.%d" x.Major x.Minor
        if s = s2 + ".0" then s2 else s

    override x.ToString() = 
        match x.Original with
        | Some version -> version.Trim()
        | None -> x.Normalize()
    
    member x.AsString
        with get() = x.ToString()
        
    member x.Equals(y) =
        x.Major = y.Major && x.Minor = y.Minor && x.Patch = y.Patch && x.Build = y.Build && x.PreRelease = y.PreRelease

    override x.Equals(yobj) = 
        match yobj with
        | :? SemVerInfo as y -> x.Equals(y)
        | _ -> false
    
    override x.GetHashCode() = hash (x.Major, x.Minor, x.Patch, x.Build, x.PreRelease)
    
    member x.CompareTo(y) =
        let comparison =  
            match compare x.Major y.Major with 
            | 0 ->
                match compare x.Minor y.Minor with
                | 0 ->
                    match compare x.Patch y.Patch with
                    | 0 ->  
                        match compare x.Build y.Build with 
                        | 0 -> 
                            match x.PreRelease, y.PreRelease with
                            | None, None -> 0
                            | Some p, None -> -1
                            | None, Some p -> 1
                            | Some p, Some p2 when p.Origin = "prerelease" && p2.Origin = "prerelease" -> 0
                            | Some p, _ when p.Origin = "prerelease" -> -1
                            | _, Some p when p.Origin = "prerelease" -> 1
                            | Some left, Some right -> compare left right
                        | c -> c
                    | c -> c
                | c -> c
            | c -> c
        comparison
    
    interface System.IComparable with
        member x.CompareTo yobj = 
            match yobj with
            | :? SemVerInfo as y -> x.CompareTo(y)
            | _ -> invalidArg "yobj" "SemVerInfo: cannot compare to values of different types"

///  Parser which allows to deal with [Semantic Versioning](http://semver.org/) (SemVer).
module SemVer =
    open System.Numerics
  
    /// Matches if str is convertible to Int and not less than zero, and returns the value as UInt.
    let inline private (|Int|_|) (str:string) =
        match Int32.TryParse (str, NumberStyles.Integer, null) with
        | true, num -> Some num // ALLOW negative as we need to fail
        | _ -> None
        
    /// Matches if str is convertible to big int and not less than zero, and returns the bigint value.
    let inline private (|Big|_|) (str:string) =
        match BigInteger.TryParse (str, NumberStyles.Integer, null) with
        | true, big when big > -1I -> Some big // positive, or fallback as prerelease
        | _ -> None

    /// Splits the given version string by possible delimiters but keeps them as parts of resulting list.
    let private expand delimiter (text : string) =
        let sb = Text.StringBuilder()
        let res = seq {
            for ch in text do // no subsequent delims
                match List.contains ch delimiter with
                | true when sb.Length > 0 ->
                    yield sb.ToString()
                    sb.Clear() |> ignore
                    yield ch.ToString()
                | _ ->
                    sb.Append(ch) |> ignore
            if sb.Length > 0 then
                yield sb.ToString()
                sb.Clear() |> ignore
            }
        res |> Seq.toList
        
    let private validContent = Regex(@"(?in)^[a-z0-9-]+(\.[a-z0-9-]+)*")

    /// Parses the given version string into a SemVerInfo which can be printed using ToString() or compared
    /// according to the rules described in the [SemVer docs](http://semver.org/).
    /// ## Sample
    ///
    ///     parse "1.0.0-rc.1"     < parse "1.0.0"          // true
    ///     parse "1.2.3-alpha"    > parse "1.2.2"          // true
    ///     parse "1.2.3-alpha2"   > parse "1.2.3-alpha"    // true
    ///     parse "1.2.3-alpha002" > parse "1.2.3-alpha1"   // true
    ///     parse "1.5.0-beta.2"   > parse "1.5.0-rc.1"     // false
    let Parse = 
        memoize <| fun (version : string) ->
            try // negative numbers are handled diffently for mandatory and optional segments
                if version.Contains("!") then 
                    failwithf "Invalid character found in %s" version
                if version.Contains("..") then 
                    failwithf "Empty version part found in %s" version

                let plusIndex = version.IndexOf("+")

                let versionStr = 
                    match plusIndex with
                    | n when n < 0 -> version
                    | n -> version.Substring(0, n)

                /// there can only be one piece of build metadata, and it is signified by + sign
                /// and then any number of dot-separated alpha-numeric groups.
                let buildmeta =
                    match plusIndex with
                    | -1 -> ""
                    | n when n = version.Length - 1 -> ""
                    | n -> 
                        let content = validContent.Match(version.Substring(n + 1))
                        if content.Success then content.Value else ""

                let fragments = expand [ '.'; '-' ] versionStr
                /// matches over list of the version fragments *and* delimiters
                let major, minor, patch, revision, suffix =
                    match fragments with
                    | Int M::"."::Int m::"."::Int p::"."::Big b::tail -> M, m, p, b, tail
                    | Int M::"."::Int m::"."::Int p::tail -> M, m, p, 0I, tail
                    | Int M::"."::Int m::tail -> M, m, 0, 0I, tail
                    | Int M::tail -> M, 0, 0, 0I, tail
                    | _ -> raise(ArgumentException("SemVer.Parse", "version"))
                    //this is expected to fail, for now :/
                    //| [text] -> 0, 0, 0, 0I, [text] 
                    //| [] | _ -> 0, 0, 0, 0I, []
                
                /// recreate the remaining string to parse as prerelease segments
                let prerelease() =
                    if suffix.IsEmpty || suffix.Tail.IsEmpty then ""
                    else String.Concat(suffix.Tail).TrimEnd([|'.'|])
                
                { Major = Checked.uint32 major
                  Minor = Checked.uint32 minor
                  Patch = Checked.uint32 patch
                  Build = revision // unchecked: positive or pre
                  PreRelease = PreRelease.TryParse (prerelease())
                  BuildMetaData = buildmeta
                  Original = Some version }

            with
            | exn ->
                failwithf "Can't parse \"%s\". %s" version (exn.ToString())

    let Zero = Parse "0"

    let SortVersions =
        Array.choose (fun v -> try Some(v,Parse v) with | _ -> None)
        >> Array.sortBy snd
        >> Array.map fst
        >> Array.rev