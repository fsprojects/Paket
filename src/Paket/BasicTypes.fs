namespace Paket

open System.IO

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

/// Represents the package source type.
type PackageSource =
| Nuget of string
| LocalNuget of string
    override this.ToString() =
        match this with
        | Nuget url -> url
        | LocalNuget path -> path

    static member Parse source = 
        match System.Uri.TryCreate(source, System.UriKind.Absolute) with
        | true, uri -> if uri.Scheme = System.Uri.UriSchemeFile then LocalNuget(source) else Nuget(source)
        | _ -> failwith "unable to parse package source: %s" source

// Represents details on a dependent source file.
//TODO: As new sources e.g. fssnip etc. are added, this should probably become a DU or perhaps have an enum marker.
type SourceFile =
    { Owner : string
      Project : string
      Name : string
      CommitSpecified: bool
      Commit : string }
    member this.FilePath =
        let path = this.Name
                    .TrimStart('/')
                    .Replace("/", "\\")
        let di = DirectoryInfo(Path.Combine("paket-files", this.Owner, this.Project, this.Commit, path))
        di.FullName

    override this.ToString() = sprintf "(%s:%s:%s) %s" this.Owner this.Project this.Commit this.Name
       
/// Represents type of NuGet packages.config file
type NugetPackagesConfigType = ProjectLevel | SolutionLevel

/// Represents NuGet packages.config file
type NugetPackagesConfig = {
    File: FileInfo;
    Packages: (string*SemVerInfo) list
    Type: NugetPackagesConfigType
}
            
/// Represents an unresolved package.
type UnresolvedPackage =
    { Name : string
      VersionRange : VersionRange
      ResolverStrategy : ResolverStrategy
      Sources : PackageSource list }

/// Represents data about resolved packages
type ResolvedPackage = 
    { Name : string
      Version : SemVerInfo
      Dependencies : (string * VersionRange) list
      Source : PackageSource }

/// Represents package details
type PackageDetails = 
    { Name : string
      Source : PackageSource
      DownloadLink : string
      DirectDependencies :  (string * VersionRange) list }
      
type FilteredVersions = Map<string,SemVerInfo list>

type PackageResolution = Map<string , ResolvedPackage>

type Resolved =
| Ok of PackageResolution
| Conflict of Set<UnresolvedPackage> * UnresolvedPackage list