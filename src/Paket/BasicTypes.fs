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
      SourceType : string
      Source : string }

type PackageDependency = 
    { Defining : Package
      Referenced : Package }

type Dependency = 
    | FromRoot of Package
    | FromPackage of PackageDependency
    member this.Referenced = 
        match this with
        | FromRoot p -> p
        | FromPackage d -> d.Referenced

type IDiscovery = 
    abstract GetDirectDependencies : string * string * string * string -> Package list
    abstract GetVersions : string * string * string -> string seq

type ResolvedVersion = 
    | Resolved of Dependency
    | Conflict of Dependency * Dependency
