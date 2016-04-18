namespace Paket.LoadingScripts

open Paket
open Paket.Domain
open System.IO

module LoadingScriptsGenerator =
    
    let getLeafPackagesGeneric getPackageName getDependencies (knownPackages:Set<_>) (openList) =
        let leafPackages =
          openList 
          |> List.filter (fun p ->
              not (knownPackages.Contains(getPackageName p)) &&
              getDependencies p |> Seq.forall (knownPackages.Contains))
        let newKnownPackages =
          leafPackages
          |> Seq.fold (fun state package -> state |> Set.add (getPackageName package)) knownPackages
        let newState =
          openList
          |> List.filter (fun p -> leafPackages |> Seq.forall (fun l -> getPackageName l <> getPackageName p))
        leafPackages, newKnownPackages, newState

    let getPackageOrderGeneric getPackageName getDependencies packages =
      let rec step finalList knownPackages currentPackages =
        match currentPackages |> getLeafPackagesGeneric getPackageName getDependencies knownPackages with
        | ([], _, _) -> finalList
        | (leafPackages, newKnownPackages, newState) ->
          step (leafPackages @ finalList) newKnownPackages newState
      step [] Set.empty packages
      |> List.rev  

    let getPackageOrderResolvedPackage =
      getPackageOrderGeneric 
        (fun (p:PackageResolver.ResolvedPackage) -> p.Name) 
        (fun p -> p.Dependencies |> Seq.map (fun (n,_,_) -> n))

    let getPackageOrderFromDependenciesFile (lockFile:FileInfo) =
        let lockFile = LockFileParser.Parse (System.IO.File.ReadAllLines lockFile.FullName)
        lockFile
        |> Seq.map (fun p -> p.GroupName, getPackageOrderResolvedPackage p.Packages)
        |> Map.ofSeq


module ScriptGeneratingModule =
  open System.IO
  open System.Collections.Generic
  open Mono.Cecil
  open QuickGraph
  open System

  let private listOfFrameworks = [
    Paket.FrameworkIdentifier.DotNetFramework Paket.FrameworkVersion.V4_5
    Paket.FrameworkIdentifier.DotNetFramework Paket.FrameworkVersion.V4_Client
    Paket.FrameworkIdentifier.DotNetFramework Paket.FrameworkVersion.V3_5
    Paket.FrameworkIdentifier.DotNetFramework Paket.FrameworkVersion.V2
  ]

  let tryFind key (dict: IDictionary<_,_>) =
    match dict.TryGetValue(key) with
    | true, v -> Some v
    | _       -> None
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

  let getDllFilesWithinPackage (paketDependencies: Paket.Dependencies) (targetPredicate: _ -> Paket.LibFolder option) groupName packageName =
  
    let installModel = paketDependencies.GetInstalledPackageModel(groupName, packageName)
    let libFolder = targetPredicate installModel.ReferenceFileFolders
  
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

  let getScriptName (package: PackageName) = sprintf "Include_%s.fsx" (package.GetCompareString())
  let generateFSharpScript dependenciesFile lockFile (packagesOrGroupFolder: DirectoryInfo) (knownIncludeScripts:Map<PackageName, string>) (package: PackageResolver.ResolvedPackage) =
    let packageFolder = 
      Path.Combine (packagesOrGroupFolder.FullName, package.Name.GetCompareString())
      |> DirectoryInfo

    let scriptFile = Path.Combine (packageFolder.FullName, getScriptName package.Name)
    let relScriptFile = Path.Combine (package.Name.GetCompareString(), getScriptName package.Name)
    let depLines =
      package.Dependencies
      |> Seq.map (fun (depName,_,_) -> sprintf "#load \"../%s\"" ((knownIncludeScripts |> Map.find depName).Replace("\\", "/")))
    
    let toRelative = (Path.GetFullPath >> (fun f -> f.Substring(packageFolder.FullName.Length + 1)))

    let dllFiles =
      if package.Name.GetCompareString().ToLowerInvariant() = "fsharp.core" then
        Seq.empty
      else
        let group = None
        let references = getDllFilesWithinPackage dependenciesFile (makeTargetPredicate listOfFrameworks) group (package.Name.GetCompareString())
        references
        |> Seq.map (fun r -> toRelative r.Path)

    let orderedDllFiles =
      // TODO: Order by the inter-dependencies
      // 1. Drop all unknown dependencies (they are either already resolved or we cannot do it anyway)
      // 2. Use the algorithm above to sort.
      dllFiles
      |> Seq.sortBy (fun l -> l.Length)
      
    let dllLines =
      orderedDllFiles
      |> Seq.map (fun dll -> sprintf "#r \"%s\"" (dll.Replace("\\", "/")))

    depLines
    |> fun lines -> Seq.append lines dllLines
    |> fun lines -> Seq.append lines [ sprintf "printfn \"%%s\" \"Loaded %s\"" (package.Name.GetCompareString()) ]
    |> fun lines -> File.WriteAllLines (scriptFile, lines)
    
    knownIncludeScripts |> Map.add package.Name relScriptFile
    
  // Generate a fsharp script from the given order of packages, if a package is ordered before its dependencies this function will throw.
  let generateFSharpScripts dependenciesFile lockFile packagesOrGroupFolder (orderedPackages: PackageResolver.ResolvedPackage list) =
      orderedPackages
      |> Seq.fold (fun (knownIncludeScripts) p ->
        generateFSharpScript dependenciesFile lockFile packagesOrGroupFolder knownIncludeScripts p) Map.empty
      |> ignore

        
  // Generate a fsharp script from the given order of packages, if a package is ordered before its dependencies this function will throw.
  let generateFSharpScriptsForRootFolder (rootFolder: DirectoryInfo) =
      
      let dependenciesFile, lockFile =
          let deps = Paket.Dependencies.Locate(rootFolder.FullName)
          let lock =
            deps.DependenciesFile
            |> Paket.DependenciesFile.ReadFromFile
            |> fun f -> f.FindLockfile().FullName
            |> Paket.LockFile.LoadFrom
          deps, lock
      
      let dependencies = LoadingScriptsGenerator.getPackageOrderFromDependenciesFile (FileInfo(lockFile.FileName))
      
      let packagesFolder =
        Path.Combine(rootFolder.FullName, "packages")
        |> DirectoryInfo

      dependencies
      |> Map.map (fun groupName packages ->
        let packagesOrGroupFolder =
          match groupName.GetCompareString () with
          | "main"    -> packagesFolder 
          | groupName -> Path.Combine(packagesFolder.FullName, groupName) |> DirectoryInfo
        generateFSharpScripts dependenciesFile lockFile packagesOrGroupFolder packages
        )
      |> ignore
      