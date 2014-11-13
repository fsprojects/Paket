namespace Paket

open System
open System.Text.RegularExpressions

/// Information about PreRelease packages.
[<CustomEquality; CustomComparison>]
type PreRelease = 
    { Origin : string
      Name : string
      Number : int option }
    
    static member TryParse str = 
        let m = Regex("^(?<name>[a-zA-Z]+)(?<number>\d*)$").Match(str)
        match m.Success, m.Groups.["name"].Value, m.Groups.["number"].Value with
        | true, name, "" -> 
            Some { Origin = str
                   Name = name
                   Number = None }
        | true, name, number -> 
            Some { Origin = str
                   Name = name
                   Number = Some(int number) }
        | _ -> None
    
    override x.Equals(yobj) = 
        match yobj with
        | :? PreRelease as y -> x.Origin = y.Origin
        | _ -> false
    
    override x.GetHashCode() = hash x.Origin
    interface System.IComparable with
        member x.CompareTo yobj = 
            match yobj with
            | :? PreRelease as y -> 
                if x.Name <> y.Name then compare x.Name y.Name
                else compare x.Number y.Number
            | _ -> invalidArg "yobj" "cannot compare values of different types"

/// Contains the version information.
[<CustomEquality; CustomComparison; StructuredFormatDisplay("{AsString}")>]
type SemVerInfo = 
    { /// MAJOR version when you make incompatible API changes.
      Major : int
      /// MINOR version when you add functionality in a backwards-compatible manner.
      Minor : int
      /// PATCH version when you make backwards-compatible bug fixes.
      Patch : int
      /// The optional PreRelease version
      PreRelease : PreRelease option
      /// The optional build no.
      Build : string
      // The original version text
      Original : string option }
    
    member x.Normalize() = 
        let build = 
            if String.IsNullOrEmpty x.Build |> not then "." + x.Build
            else ""
            
        let pre = 
            match x.PreRelease, String.IsNullOrEmpty x.Build |> not with
            | Some preRelease, _ -> "-" + preRelease.Name + build
            | None, true -> build
            | _ -> build

        sprintf "%d.%d.%d" x.Major x.Minor x.Patch + pre

    member x.AsString = x.ToString()

    override x.ToString() = 
        match x.Original with
        | Some version -> version
        | None -> x.Normalize()
    
    override x.Equals(yobj) = 
        match yobj with
        | :? SemVerInfo as y -> 
            x.Major = y.Major && x.Minor = y.Minor && x.Patch = y.Patch && x.PreRelease = y.PreRelease 
            && (x.Build = y.Build || (x.Build = "0" && y.Build = "") || (y.Build = "0" && x.Build = ""))
        | _ -> false
    
    override x.GetHashCode() = hash (x.Minor, x.Minor, x.Patch, x.PreRelease, x.Build)
    interface System.IComparable with
        member x.CompareTo yobj = 
            match yobj with
            | :? SemVerInfo as y -> 
                if x.Major <> y.Major then compare x.Major y.Major
                else if x.Minor <> y.Minor then compare x.Minor y.Minor
                else if x.Patch <> y.Patch then compare x.Patch y.Patch
                else if x.PreRelease = y.PreRelease && x.Build = y.Build then 0
                else if x.PreRelease.IsNone && not y.PreRelease.IsNone && x.Build = "" then 1
                else if y.PreRelease.IsNone && not x.PreRelease.IsNone && y.Build = "" then -1
                else if x.PreRelease <> y.PreRelease then compare x.PreRelease y.PreRelease
                else if x.Build <> y.Build then 
                    match Int32.TryParse x.Build, Int32.TryParse y.Build with
                    | (true, b1), (true, b2) -> compare b1 b2
                    | _ -> compare x.Build y.Build
                else 0
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
    let Parse(version : string) = 
        let splitted = version.Split '.'
        let l = splitted.Length
        
        let patch, preRelease = 
            match l with
            | 0 -> 0, ""
            | 1 ->
                let splitted' = splitted.[0].Split '-'
                0, 
                if splitted'.Length > 1 then splitted'.[1]
                else ""
            | 2 ->
                let splitted' = splitted.[1].Split '-'
                0, 
                if splitted'.Length > 1 then splitted'.[1]
                else ""
            | _ ->
                let splitted' = splitted.[2].Split '-'
                Int32.Parse splitted'.[0], 
                if splitted'.Length > 1 then splitted'.[1]
                else ""
        { Major = 
              if l > 0 then Int32.Parse (splitted.[0].Split('-').[0])
              else 0
          Minor = 
              if l > 1 then Int32.Parse (splitted.[1].Split('-').[0])
              else 0
          Patch = patch
          PreRelease = PreRelease.TryParse preRelease
          Build = 
              if l > 3 then splitted.[3]
              else String.Empty
          Original = Some version }