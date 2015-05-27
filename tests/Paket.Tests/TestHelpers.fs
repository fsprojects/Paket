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
        |> Seq.filter (fun (p, v, _) -> NormalizedPackageName (PackageName p) = NormalizedPackageName package && SemVer.Parse v = version)
        |> Seq.map (fun (n, _, d) -> PackageName n,d |> List.map (fun (x,y) -> PackageName x,y,[]))
        |> Seq.head

    { Name = name
      Source = Seq.head sources
      DownloadLink = ""
      Unlisted = false
      DirectDependencies = Set.ofList dependencies }

let VersionsFromGraph (graph : seq<string * string * (string * VersionRequirement) list>) (sources, package : PackageName) = 
    graph
    |> Seq.filter (fun (p, _, _) -> NormalizedPackageName (PackageName p) = NormalizedPackageName package)
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
    PackageResolver.Resolve(VersionsFromGraph graph, PackageDetailsFromGraph graph, [], packages)

let resolve graph dependencies = (safeResolve graph dependencies).GetModelOrFail()

let getVersion (resolved:ResolvedPackage) = resolved.Version.ToString()

let getSource (resolved:ResolvedPackage) = resolved.Source

let normalizeLineEndings (text : string) = 
    text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine)

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