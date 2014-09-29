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


       
/// Represents type of NuGet packages.config file
type NugetPackagesConfigType = ProjectLevel | SolutionLevel

/// Represents NuGet packages.config file
type NugetPackagesConfig = {
    File: FileInfo;
    Packages: (string*SemVerInfo) list
    Type: NugetPackagesConfigType
}

type PackageRequirementSource =
| DependenciesFile of string
| Package of string * SemVerInfo
            
/// Represents an unresolved package.
[<CustomEquality;CustomComparison>]
type PackageRequirement =
    { Name : string
      VersionRequirement : VersionRequirement
      ResolverStrategy : ResolverStrategy
      Parent: PackageRequirementSource
      Sources : PackageSource list }
    override this.Equals(that) = 
        match that with
        | :? PackageRequirement as that -> this.Name = that.Name && this.VersionRequirement = that.VersionRequirement
        | _ -> false

    override this.GetHashCode() = hash (this.Name,this.VersionRequirement)

    interface System.IComparable with
       member this.CompareTo that = 
          match that with 
          | :? PackageRequirement as that -> compare (this.Parent,this.Name,this.VersionRequirement) (that.Parent,that.Name,that.VersionRequirement)
          | _ -> invalidArg "that" "cannot compare value of different types" 

