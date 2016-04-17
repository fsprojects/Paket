#load "Scripts/load-project-debug.fsx"
open System
open System.Collections.Generic
open System.IO
open System.Linq
open Mono.Cecil
open QuickGraph

let dependenciesFile, lockFile =
  //let rootFolder = (@"C:\dev\src\g\fiddle3d") |> DirectoryInfo
  let rootFolder = (__SOURCE_DIRECTORY__) |> DirectoryInfo
  let deps = Paket.Dependencies.Locate(rootFolder.FullName)
  let lock =
    deps.DependenciesFile
    |> Paket.DependenciesFile.ReadFromFile
    |> fun f -> f.FindLockfile().FullName
    |> Paket.LockFile.LoadFrom
  deps, lock

let tryFind key (dict: IDictionary<_,_>) =
  match dict.TryGetValue(key) with
  | true, v -> Some v
  | _       -> None

let getDllFilesWithinPackage (paketDependencies: Paket.Dependencies) (targetPredicate: _ -> Paket.LibFolder option) packageName =
  
  let installModel =
    let groupName = None
    paketDependencies.GetInstalledPackageModel(groupName, packageName)
  let libFolder = 
    targetPredicate installModel.ReferenceFileFolders
    (**installModel.ReferenceFileFolders
    |> Seq.map (fun f -> f, targetPredicate f)
    |> Seq.choose (snd >> id)
    |> Seq.tryHead*)
  
  let references = 
    match libFolder with
    | None        -> Set.empty
    | Some folder -> folder.Files.References

  let referenceByAssembly =
    references
    |> Seq.filter (fun f -> f.LibName.IsSome)
    |> Seq.map (fun r -> (r.Path |> AssemblyDefinition.ReadAssembly), r)
    |> dict

  let assemblyByName =
    referenceByAssembly.Keys
    |> Seq.map (fun a -> a.Name.ToString(), a)
    |> dict

  let graph = AdjacencyGraph<_,_>()
  
  for r in referenceByAssembly do
    let assembly = r.Key
    let paketRef = referenceByAssembly.[assembly]
    graph.AddVertex(paketRef) |> ignore
    let references = assembly.MainModule.AssemblyReferences
    printfn "%A" paketRef
    references
    |> Seq.map (fun a -> tryFind a.FullName assemblyByName)
    |> Seq.choose id
    |> Seq.map (fun a -> paketRef, referenceByAssembly.[a])
    |> Seq.iter (fun (fromRef, toRef) ->
      graph.AddVertex(toRef) |> ignore
      graph.AddEdge(Edge(fromRef, toRef)) |> ignore
      )

  let result =
    let topologicalSort = Algorithms.TopologicalSort.TopologicalSortAlgorithm(graph)
    topologicalSort.Compute()
    topologicalSort.SortedVertices |> Seq.rev |> Seq.toArray

  result

let computePackageTopologyGraph (lockFile: Paket.LockFile) =
  let lookup = Dictionary<_,_>()
  for g in lockFile.Groups do
    let groupName = g.Value.Name
    let lookup =
      let dict = Dictionary<_,_>()
      lookup.[groupName] <- dict
      dict
    for r in g.Value.Resolution do
      let package = r.Key
      let deps =
        lockFile.GetAllNormalizedDependenciesOf(groupName, package)
        |> Seq.map snd
        |> Seq.filter ((<>) package)
        |> HashSet<_>
      lookup.[package] <- deps
  
  [
    for group in lookup.Keys do
      let depTree = lookup.[group]
      for package in depTree.Keys do
        let graph = new AdjacencyGraph<_,_>()
        graph.AddVertex(package) |> ignore
        depTree.[package] |> Seq.iter (graph.AddVertex >> ignore)
        depTree.[package] |> Seq.iter (fun d -> graph.AddEdge(Edge<_>(package, d)) |> ignore)
        let depsInOrder = 
          let sortAlgorithm = Algorithms.TopologicalSort.TopologicalSortAlgorithm<_,_>(graph)
          sortAlgorithm.Compute()
          sortAlgorithm.SortedVertices |> Seq.rev |> Seq.toArray
        yield ((group, package), depsInOrder)
  ] 
  |> dict

let weightTargetProfiles possibleFrameworksInOrderOfPreference (profiles: Paket.TargetProfile list) =
  
  let relevantFrameworksWithPreferenceIndex =
    possibleFrameworksInOrderOfPreference
    |> Seq.mapi (fun i f -> f, i)
    |> dict
  
  let profileWithAllFrameworks =
    profiles
    |> Seq.map (fun targetProfile ->
      targetProfile, 
      match targetProfile with
      | Paket.SinglePlatform platform -> [platform]
      | Paket.PortableProfile (_, platforms) -> platforms
    )
    |> Seq.map (fun (targetProfile, profiles) ->
      targetProfile, 
      profiles
      |> Seq.map (fun p -> p, tryFind p relevantFrameworksWithPreferenceIndex)
    )
  
  // we pick the first one for which the sum of prefered indexes is the lowest
  let choices = 
    profileWithAllFrameworks
    |> Seq.map (fun (targetProfile, selection) -> 
      let sum =
        let selectionWithMatches =
          selection
          |> Seq.filter (snd >> Option.isSome)
        if selectionWithMatches |> Seq.isEmpty then
          Int32.MaxValue
        else
          selectionWithMatches
          |> Seq.map (snd)
          |> Seq.choose id
          |> Seq.sum
      targetProfile,sum
    )
    |> Seq.filter (snd >> ((<>) Int32.MaxValue))
  choices

let makeTargetPredicate frameworks =
  fun (folders: Paket.LibFolder list) ->
    folders
    |> Seq.map (fun folder-> folder, weightTargetProfiles frameworks folder.Targets)
    |> Seq.map (fun (folder, weightedTargetProfiles) -> 
      folder, weightedTargetProfiles |> Seq.sumBy snd
    )
    |> Seq.sortBy snd
    |> Seq.map fst
    |> Seq.tryHead

let frameworkIdentifiersInPreferenceOrder =
  [
  Paket.FrameworkIdentifier.DotNetFramework Paket.FrameworkVersion.V4_5
  Paket.FrameworkIdentifier.DotNetFramework Paket.FrameworkVersion.V4_Client
  Paket.FrameworkIdentifier.DotNetFramework Paket.FrameworkVersion.V3_5
  Paket.FrameworkIdentifier.DotNetFramework Paket.FrameworkVersion.V2
  ]

let profiles = dependenciesFile.GetInstalledPackageModel(None, "Chessie")
for refFolder in profiles.ReferenceFileFolders do
  printfn "%A" (weightTargetProfiles frameworkIdentifiersInPreferenceOrder refFolder.Targets)

let targetPredicate = makeTargetPredicate frameworkIdentifiersInPreferenceOrder

getDllFilesWithinPackage dependenciesFile targetPredicate ("fspickler")

let packagesGraph = computePackageTopologyGraph lockFile

for (group, package) in packagesGraph.Keys do
  
  printfn "= %s %s =" (group.ToString()) (package.GetCompareString())
  try
    let dlls = getDllFilesWithinPackage dependenciesFile targetPredicate (package.ToString())
    for d in dlls do
      printfn "\t-> %s" d.Path
  with
  | e -> printfn "\t ERROR: %s" (e.ToString())

let installModel = dependenciesFile.GetInstalledPackageModel(Some "main", "newtonsoft.json")

for p in installModel.ReferenceFileFolders do
  let folderName = p.Name
  printfn "%s" folderName
  for t in p.Targets do
    printfn "\t-> %A" t
