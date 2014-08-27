namespace Paket

open System

type VersionRange = 
    | AtLeast of string
    | Exactly of string
    | Between of string * string
    
    static member Parse(text : string) : VersionRange = 
        // TODO: Make this pretty
        if text.StartsWith "~> " then 
            let min = text.Replace("~> ", "")
            let parts = min.Split('.')
            let major = Int32.Parse parts.[0]
            
            let newParts = 
                (major + 1).ToString() :: Seq.toList (parts
                                                      |> Seq.skip 1
                                                      |> Seq.map (fun _ -> "0"))
            VersionRange.Between(min, String.Join(".", newParts))
        else if text.StartsWith "= " then VersionRange.Exactly(text.Replace("= ", ""))
        else VersionRange.AtLeast text
    
    /// Checks wether the given version is in the version range
    member this.IsInRange version = 
        match this with
        | AtLeast v -> v <= version
        | Exactly v -> v = version
        | Between(min, max) -> version >= min && version < max

type Package = 
    { Name : string
      VersionRange : VersionRange
      Source : string }

type DefindedDependency = 
    { DefiningPackage : string
      DefiningVersion : string
      ReferencedPackage : string
      ReferencedVersion : VersionRange
      Source : string }

type IDiscovery = 
    abstract GetDirectDependencies : string * string * string -> Package list
    abstract GetVersions : string -> string seq

type ResolvedVersion = 
    | Resolved of DefindedDependency
    | Conflict of DefindedDependency * DefindedDependency
