#load "Scripts/load-project-debug.fsx"
open System.Collections.Generic
open System.IO
open System.Linq
open Mono.Cecil
open QuickGraph
let rootFolder        = (__SOURCE_DIRECTORY__) |> DirectoryInfo
let dependenciesFile, lockFile =
  let deps = Paket.Dependencies.Locate(rootFolder.FullName)
  let lock =
    deps.DependenciesFile
    |> Paket.DependenciesFile.ReadFromFile
    |> fun f -> f.FindLockfile().FullName
    |> Paket.LockFile.LoadFrom
  deps, lock

let getDllFilesWithinPackage (paketDependencies: Paket.Dependencies) packageName =
  
  let installModel =
    let groupName = None
    paketDependencies.GetInstalledPackageModel(groupName, packageName)

  for p in installModel.ReferenceFileFolders do
    printfn "reference file folders: %A" p
  
  // HACK: take first one for now
  let references = installModel.ReferenceFileFolders.[0].Files.References

  let referenceByAssembly =
    references
    |> Seq.map (fun r -> (r.Path |> AssemblyDefinition.ReadAssembly), r)
    |> dict

  let assemblyByName =
    referenceByAssembly.Keys
    |> Seq.map (fun a -> a.Name.ToString(), a)
    |> dict

  let tryFind key (dict: IDictionary<_,_>) =
    match dict.TryGetValue(key) with
    | true, v -> Some v
    | _       -> None

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

let packagesGraph = computePackageTopologyGraph lockFile

for (group, package) in packagesGraph.Keys do
  
  printfn "= %s %s =" (group.ToString()) (package.ToString())
  try
    let dlls = getDllFilesWithinPackage dependenciesFile (package.ToString())
    for d in dlls do
      printfn "\t-> %s" d.Path
  with
  | e -> printfn "\t ERROR: %s" (e.ToString())
