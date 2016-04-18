#load "Scripts/load-project-debug.fsx"
open System
open System.Collections.Generic
open System.IO
open System.Linq
open Mono.Cecil
open QuickGraph

let dependenciesFile, lockFile =
  let rootFolder = (@"C:\dev\src\g\fiddle3d") |> DirectoryInfo
  //let rootFolder = (__SOURCE_DIRECTORY__) |> DirectoryInfo
  let deps = Paket.Dependencies.Locate(rootFolder.FullName)
  let lock =
    deps.DependenciesFile
    |> Paket.DependenciesFile.ReadFromFile
    |> fun f -> f.FindLockfile().FullName
    |> Paket.LockFile.LoadFrom
  deps, lock

Paket.LoadingScripts.ScriptGeneratingModule.generateFSharpScriptsForRootFolder ((@"C:\dev\src\g\fiddle3d") |> DirectoryInfo)

(*
let tryFind = Paket.LoadingScripts.ScriptGeneratingModule.tryFind
let weightTargetProfiles = Paket.LoadingScripts.ScriptGeneratingModule.weightTargetProfiles
let makeTargetPredicate = Paket.LoadingScripts.ScriptGeneratingModule.makeTargetPredicate
let getDllFilesWithinPackage = Paket.LoadingScripts.ScriptGeneratingModule.getDllFilesWithinPackage

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

getDllFilesWithinPackage dependenciesFile targetPredicate None ("fspickler")

let packagesGraph = computePackageTopologyGraph lockFile

for (group, package) in packagesGraph.Keys do
  
  printfn "= %s %s =" (group.ToString()) (package.GetCompareString())
  try
    let dlls = getDllFilesWithinPackage dependenciesFile targetPredicate (None) (package.ToString())
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
*)