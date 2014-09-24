namespace Paket

open System.IO

type Auth = 
    { Username : string
      Password : string }

type NugetSource = 
    { Url : string
      Auth : Auth option }

/// Represents the package source type.
type PackageSource =
| Nuget of NugetSource
| LocalNuget of string
    override this.ToString() =
        match this with
        | Nuget source -> source.Url
        | LocalNuget path -> path

    static member Parse(source,auth) = 
        match System.Uri.TryCreate(source, System.UriKind.Absolute) with
        | true, uri -> if uri.Scheme = System.Uri.UriSchemeFile then LocalNuget(source) else Nuget({ Url = source; Auth = auth })
        | _ -> failwith "unable to parse package source: %s" source

    static member NugetSource url = Nuget { Url = url; Auth = None }

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