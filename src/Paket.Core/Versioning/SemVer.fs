namespace Paket

open System
open System.Text.RegularExpressions

module utils =  
    let zipOpt l1 l2 =
        let llength = if l1 = Unchecked.defaultof<_> then 0 else List.length l1
        let rlength = if l2 = Unchecked.defaultof<_> then 0 else List.length l2
        seq {
            for i in 0..(max llength rlength) do
                let l = if llength > i then Some (List.item i l1) else None
                let r = if rlength > i then Some (List.item i l2) else None
                yield l, r
        }

[<CustomEquality; CustomComparison>]
type PreReleaseSegment = 
    | AlphaNumeric of string
    | Numeric of bigint

    interface System.IComparable with
        member x.CompareTo yobj =
            match yobj with
            | :? PreReleaseSegment as y ->
                match x, y with
                | AlphaNumeric a, AlphaNumeric b -> compare a b
                | Numeric a, Numeric b -> compare a b
                | AlphaNumeric a, Numeric b -> 1
                | Numeric a , AlphaNumeric b -> -1
            | _ -> invalidArg "yobj" "can't compare to other types of objects."

    override x.GetHashCode() = hash x
    override x.Equals yobj = 
        match yobj with 
        | :? PreReleaseSegment as y ->
            match x, y with
            | AlphaNumeric a, AlphaNumeric b -> a = b
            | Numeric a, Numeric b -> a = b
            | AlphaNumeric a, Numeric b -> false
            | Numeric a , AlphaNumeric b -> false
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
            let name = Regex("^(?<name>[a-zA-Z]+)").Match(str).Value

            let parse segment =
                match bigint.TryParse segment with
                | true, bint -> Numeric bint
                | false, _ -> AlphaNumeric segment
                    
            let values = str.Split([|'.'|]) |> Array.map parse |> List.ofArray
            Some { Origin = str; Name = name; Values = values}

    override x.Equals(yobj) = 
        match yobj with
        | :? PreRelease as y -> x.Origin = y.Origin
        | _ -> false

    override x.ToString() = x.Origin
    
    override x.GetHashCode() = hash x.Origin
    interface System.IComparable with
        member x.CompareTo yobj = 
            match yobj with
            | :? PreRelease as y -> 
                utils.zipOpt x.Values y.Values
                |> Seq.fold (fun cmp (a, b) -> 
                    if cmp <> 0 then cmp
                    else 
                        match a, b with
                        | None, Some _ -> -1
                        | Some _, None -> 1
                        | _ , _ -> compare a b) 0

            | _ -> invalidArg "yobj" "cannot compare values of different types"

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
      Build : string
      BuildMetaData : string
      // The original version text
      Original : string option }
    
    member x.Normalize() = 
        let build = 
            if String.IsNullOrEmpty x.Build |> not && x.Build <> "0" then "." + x.Build
            else ""
                        
        let pre = 
            match x.PreRelease with
            | Some preRelease -> sprintf "-%s" preRelease.Origin
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

    override x.Equals(yobj) = 
        match yobj with
        | :? SemVerInfo as y -> 
            x.Major = y.Major && x.Minor = y.Minor && x.Patch = y.Patch && x.PreRelease = y.PreRelease && x.Build = y.Build && x.BuildMetaData = y.BuildMetaData 
        | _ -> false
    
    override x.GetHashCode() = hash (x.Minor, x.Minor, x.Patch, x.PreRelease, x.Build)
    interface System.IComparable with
        member x.CompareTo yobj = 
            match yobj with
            | :? SemVerInfo as y -> 
                if x.Major <> y.Major then compare x.Major y.Major
                else if x.Minor <> y.Minor then compare x.Minor y.Minor
                else if x.Patch <> y.Patch then compare x.Patch y.Patch
                else if x.Build <> y.Build then
                    match Int32.TryParse x.Build, Int32.TryParse y.Build with
                    | (true, b1), (true, b2) -> compare b1 b2
                    | _ -> compare x.Build y.Build
                else 
                    match x.PreRelease, y.PreRelease with
                    | None, None -> 0
                    | Some p, None -> -1
                    | None, Some p -> 1
                    | Some p, Some p2 when p.ToString() = "prerelease" && p2.ToString() = "prerelease" -> 0
                    | Some p, _ when p.ToString() = "prerelease" -> -1
                    | _, Some p when p.ToString() = "prerelease" -> 1
                    | Some left, Some right -> compare left right

            | _ -> invalidArg "yobj" "cannot compare values of different types"

///  Parser which allows to deal with [Semantic Versioning](http://semver.org/) (SemVer).
module SemVer = 
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
            try

                /// sanity check to make sure that all of the integers in the string are positive, and that no parts lead zeroes
                /// because we use raw substrings with dashes this is very complex :(
                version.Split([|'.'|]) |> Array.iter (fun s ->
                    match Int32.TryParse s, s.StartsWith("0") with
                    | (true, s), _ when s < 0 -> failwith "no negatives!"
                    | _, true -> failwith "no leading zeroes"
                    | _, _-> ignore ())

                if version.Contains("!") then 
                    failwithf "Invalid character found in %s" version
                if version.Contains("..") then 
                    failwithf "Empty version part found in %s" version
        
                let firstDash = version.IndexOf("-")
                let plusIndex = version.IndexOf("+")

                let majorMinorPatch =
                    let firstSigil = if firstDash > 0 then firstDash else plusIndex
                    match firstSigil with
                    | -1 -> version
                    | n -> version.Substring(0, n)

                let prerelease = 
                    match firstDash, plusIndex with
                    | -1, _ -> ""
                    | d, p when p = -1 -> version.Substring(d+1)
                    | d, p -> version.Substring(d+1, (p - 1 - d) )
            
                /// there can only be one piece of build metadata, and it is signified by a + and then any number of dot-separated alpha-numeric groups.
                /// this just greedily takes the whole remaining string :(
                let buildmeta =
                    match plusIndex with
                    | -1 -> ""
                    | n when plusIndex = version.Length - 1 -> ""
                    | n -> version.Substring(plusIndex + 1)
        
                let major, minor, patch, build =
                    match majorMinorPatch.Split([|'.'|]) with
                    | [|M; m; p; b|] -> uint32 M, uint32 m, uint32 p, b
                    | [|M; m; p; |] -> uint32 M, uint32 m, uint32 p, "0"
                    | [|M; m;|] -> uint32 M, uint32 m, 0u, "0"
                    | [|M;|] -> uint32 M, 0u, 0u, "0"
                    | _ -> 0u, 0u, 0u, "0"

                { Major = major
                  Minor = minor
                  Patch = patch
                  Build = build
                  PreRelease = PreRelease.TryParse prerelease
                  BuildMetaData = buildmeta
                  Original = Some version }

            with
            | exn ->
                failwithf "Can't parse \"%s\".%s%s" version Environment.NewLine exn.Message

    let Zero = Parse "0"

    let SortVersions =
        Array.choose (fun v -> try Some(v,Parse v) with | _ -> None)
        >> Array.sortBy snd
        >> Array.map fst
        >> Array.rev