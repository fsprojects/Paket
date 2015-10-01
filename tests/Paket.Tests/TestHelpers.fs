module Paket.TestHelpers

open Paket
open System
open Paket.Requirements
open Paket.PackageSources
open PackageResolver
open System.Xml
open System.IO
open Paket.Domain

let PackageDetailsFromGraph (graph : seq<string * string * (string * VersionRequirement) list>) sources (package:PackageName) (version:SemVerInfo) = 
    let name,dependencies = 
        graph
        |> Seq.filter (fun (p, v, _) -> (PackageName p) = package && SemVer.Parse v = version)
        |> Seq.map (fun (n, _, d) -> PackageName n,d |> List.map (fun (x,y) -> PackageName x,y,[]))
        |> Seq.head

    { Name = name
      Source = Seq.head sources
      DownloadLink = ""
      LicenseUrl = ""
      Unlisted = false
      DirectDependencies = Set.ofList dependencies }

let VersionsFromGraph (graph : seq<string * string * (string * VersionRequirement) list>) (sources, package : PackageName,_) = 
    graph
    |> Seq.filter (fun (p, _, _) -> (PackageName p) = package)
    |> Seq.map (fun (_, v, _) -> SemVer.Parse v)
    |> Seq.toList

let safeResolve graph (dependencies : (string * VersionRange) list)  = 
    let packages = 
        dependencies |> List.map (fun (n, v) -> 
                            { Name = PackageName n
                              VersionRequirement = VersionRequirement(v,PreReleaseStatus.No)
                              Sources = [ PackageSource.NugetSource "" ]
                              Parent = PackageRequirementSource.DependenciesFile ""
                              Settings = InstallSettings.Default
                              ResolverStrategy = ResolverStrategy.Max })
    PackageResolver.Resolve(Constants.MainDependencyGroup,VersionsFromGraph graph, PackageDetailsFromGraph graph, [], packages, Set.empty)

let resolve graph dependencies = (safeResolve graph dependencies).GetModelOrFail()

let ResolveWithGraph(dependenciesFile:DependenciesFile,getSha1,getVersionF, getPackageDetailsF) =
    let mainGroup = 
        { Name = Constants.MainDependencyGroup
          RemoteFiles = dependenciesFile.Groups.[Constants.MainDependencyGroup].RemoteFiles
          RootDependencies = Some dependenciesFile.Groups.[Constants.MainDependencyGroup].Packages
          FrameworkRestrictions = dependenciesFile.Groups.[Constants.MainDependencyGroup].Options.Settings.FrameworkRestrictions
          PackageRequirements = [] }
        
    let groups = [Constants.MainDependencyGroup, mainGroup ] |> Map.ofSeq

    dependenciesFile.Resolve(true,getSha1,getVersionF,getPackageDetailsF,groups)

let getVersion (resolved:ResolvedPackage) = resolved.Version.ToString()

let getSource (resolved:ResolvedPackage) = resolved.Source

let removeLineEndings (text : string) = 
    text.Replace("\r\n", "").Replace("\r", "").Replace("\n", "")

let toLines (text : string) = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')

let noSha1 owner repo branch = failwith "no github configured"

let fakeSha1 owner repo branch = "12345"

let normalizeXml(text:string) =
    let doc = new XmlDocument()
    doc.LoadXml(text)
    use stringWriter = new StringWriter()
    let settings = XmlWriterSettings()
    settings.Indent <- true
        
    use xmlTextWriter = XmlWriter.Create(stringWriter, settings)
    doc.WriteTo(xmlTextWriter)
    xmlTextWriter.Flush()
    stringWriter.GetStringBuilder().ToString()

let toPath elems = System.IO.Path.Combine(elems |> Seq.toArray)