module Paket.TestHelpers

open Paket
open System
open Paket.Requirements
open Paket.PackageSources
open PackageResolver
open System.Xml
open System.IO
open Pri.LongPath
open Paket.Domain

let makeOrList (l:_ list) =
    if l.IsEmpty then FrameworkRestriction.NoRestriction
    else Seq.fold FrameworkRestriction.combineRestrictionsWithOr FrameworkRestriction.EmptySet l
    |> ExplicitRestriction

let getPortableRestriction s =
    let pf = PlatformMatching.extractPlatforms s
    FrameworkRestriction.AtLeastPortable(s, pf.Value.Platforms)

type GraphDependency = string * VersionRequirement * FrameworkRestrictions

type DependencyGraph = list<string * string * (GraphDependency) list * RuntimeGraph>

let OfSimpleGraph (g:seq<string * string * (string * VersionRequirement) list>) : DependencyGraph =
  g
  |> Seq.map (fun (x, y, (rqs)) ->
    x, y, rqs |> List.map (fun (a,b) -> (a, b, ExplicitRestriction FrameworkRestriction.NoRestriction)), RuntimeGraph.Empty)
  |> Seq.toList

let OfGraphWithRestriction (g:seq<string * string * (string * VersionRequirement * FrameworkRestrictions) list>) : DependencyGraph =
  g
  |> Seq.map (fun (x, y, (rqs)) ->
    x, y, rqs |> List.map (fun (a,b,c) -> (a, b, c)), RuntimeGraph.Empty)
  |> Seq.toList

let GraphOfNuspecs (g:seq<string>) : DependencyGraph =
  g
  |> Seq.map (fun nuspecText ->
    let nspec = Nuspec.Load("in-memory", nuspecText)
    nspec.OfficialName, nspec.Version, nspec.Dependencies |> List.map (fun (a,b,c) -> a.CompareString, b, c), RuntimeGraph.Empty)
  |> Seq.toList

let OfGraphWithRuntimeDeps (g:seq<string * string * (string * VersionRequirement) list * RuntimeGraph>) : DependencyGraph =
  g
  |> Seq.map (fun (x, y, rqs, run) ->
    x, y, rqs |> List.map (fun (a,b) -> (a, b, ExplicitRestriction FrameworkRestriction.NoRestriction)), run)
  |> Seq.toList


let PackageDetailsFromGraph (graph : DependencyGraph) sources groupName (package:PackageName) (version:SemVerInfo) = 
    let name,dependencies = 
        graph
        |> Seq.filter (fun (p, v, _, _) -> (PackageName p) = package && SemVer.Parse v = version)
        |> Seq.map (fun (n, _, d, _) -> PackageName n,d |> List.map (fun (x,y,z) -> PackageName x,y,z))
        |> Seq.head

    { Name = name
      Source = Seq.head sources
      DownloadLink = ""
      LicenseUrl = ""
      Unlisted = false
      DirectDependencies = Set.ofList dependencies }
    |> async.Return

let VersionsFromGraph (graph : DependencyGraph) sources groupName packageName = 
    let versions =
        graph
        |> Seq.filter (fun (p, _, _, _) -> (PackageName p) = packageName)
        |> Seq.map (fun (_, v, _, _) -> SemVer.Parse v)
        |> Seq.map (fun v -> v,sources)

    versions
    |> async.Return

let GetRuntimeGraphFromGraph (graph : DependencyGraph) groupName (package:ResolvedPackage) =
    graph
    |> Seq.filter (fun (p, v, _, r) -> (PackageName p) = package.Name && SemVer.Parse v = package.Version)
    |> Seq.map (fun (_, _, _, r) -> r)
    |> RuntimeGraph.mergeSeq
    // Properly returning None here makes the tests datastructures unneccessary complex.
    // It doesn't really matter because Empty is used anyway if all return "None", which is the same as merging a lot of Emtpy graphs...
    |> Some


let VersionsFromGraphAsSeq (graph : DependencyGraph) sources groupName packageName = 
   VersionsFromGraph graph sources groupName packageName

let safeResolve graph (dependencies : (string * VersionRange) list)  = 
    let sources = [ PackageSource.NuGetV2Source "" ]
    let packages = 
        dependencies
        |> List.map (fun (n, v) -> 
               { Name = PackageName n
                 VersionRequirement = VersionRequirement(v, PreReleaseStatus.No)
                 Parent = PackageRequirementSource.DependenciesFile ""
                 Graph = Set.empty
                 Sources = sources
                 IsCliTool = false
                 TransitivePrereleases = false
                 Settings = InstallSettings.Default
                 ResolverStrategyForDirectDependencies = Some ResolverStrategy.Max 
                 ResolverStrategyForTransitives = Some ResolverStrategy.Max })
        |> Set.ofList

    PackageResolver.Resolve(VersionsFromGraphAsSeq graph, (fun _ _ _ _ -> []), PackageDetailsFromGraph graph, Constants.MainDependencyGroup, None, None, ExplicitRestriction FrameworkRestriction.NoRestriction, packages, UpdateMode.UpdateAll)

let resolve graph dependencies = (safeResolve graph dependencies).GetModelOrFail()

let ResolveWithGraphR(dependenciesFile:DependenciesFile,getSha1,getVersionsF, getPackageDetailsF, getRuntimeGraph) =
    let groups = [Constants.MainDependencyGroup, None ] |> Map.ofSeq
    dependenciesFile.Resolve(true,getSha1,getVersionsF,(fun _ _ _ _ -> []),getPackageDetailsF,getRuntimeGraph,groups,UpdateMode.UpdateAll)

let ResolveWithGraph(dependenciesFile:DependenciesFile,getSha1,getVersionsF, getPackageDetailsF) =
    ResolveWithGraphR(dependenciesFile,getSha1,getVersionsF, getPackageDetailsF, (fun _ _ -> None))

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

let toPath elems = Path.Combine(elems |> Seq.toArray)

let ensureDir () = System.Environment.CurrentDirectory <-  NUnit.Framework.TestContext.CurrentContext.TestDirectory

let printSqs sqs = sqs |> Seq.iter (printfn "%A")