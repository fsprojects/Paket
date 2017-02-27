namespace Paket.LoadingScripts

open System
open System.IO
open Paket
open Paket.Domain
open Paket.Queries
open Mono.Cecil

module PackageAndAssemblyResolution =
    let getLeafPackagesGeneric getPackageName getDependencies (knownPackages:Set<_>) openList =
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

    let getDllOrder (dllFiles : AssemblyDefinition list) =
      // this check saves looking at assembly metadata when we know this is not needed
      if List.length dllFiles = 1 then
          dllFiles 
      else
          // we ignore all unknown references as they are most likely resolved on package level
          let known = dllFiles |> Seq.map (fun a -> a.FullName) |> Set.ofSeq
          getPackageOrderGeneric
            (fun (p:AssemblyDefinition) -> p.FullName)
            (fun p -> p.MainModule.AssemblyReferences |> Seq.map (fun r -> r.FullName) |> Seq.filter (known.Contains))
            dllFiles

    let getDllsWithinPackage (framework: FrameworkIdentifier) (installModel :InstallModel) =
      let dllFiles =
        installModel
        |> InstallModel.getLibReferences (SinglePlatform framework)
        |> Seq.map (fun path -> AssemblyDefinition.ReadAssembly path, FileInfo(path))
        |> dict

      getDllOrder (dllFiles.Keys |> Seq.toList)
      |> List.map (fun a -> dllFiles.[a])

    let getFrameworkReferencesWithinPackage (installModel :InstallModel) =
        installModel
        |> InstallModel.getFrameworkAssembliesLazy
        |> force
        |> Set.toList

module ScriptGeneration =
  open PackageAndAssemblyResolution
  
  type ScriptPiece =
  | ReferenceAssemblyFile      of FileInfo
  | ReferenceFrameworkAssembly of string
  | LoadScript                 of FileInfo

  type ScriptGenInput = {
      PackageName                  : PackageName
      PackagesOrGroupFolder        : DirectoryInfo
      IncludeScriptsRootFolder     : DirectoryInfo
      DependentScripts             : FileInfo list
      FrameworkReferences          : string list
      OrderedDllReferences : FileInfo list
  }

  type ScriptGenResult = 
  | DoNotGenerate
  | Generate of lines : ScriptPiece list

  let private makeRelativePath (scriptFile: FileInfo) (libFile: FileInfo) =
    (Uri scriptFile.FullName).MakeRelativeUri(Uri libFile.FullName).ToString()
  
  let shouldExcludeNugetForFSharpScript nuget =
    match nuget with
    | "FSharp.Core" -> true
    | _ -> false

  let filterFSharpFrameworkReferences assemblies =
    assemblies
    |> Seq.filter (
          function 
          | "mscorlib" ->
              // we never want to reference mscorlib directly (some nuget package state it as a dependency)
              // reason is that having it referenced more than once fails in FSI
              false 
          | _ -> true
    )

  /// default implementation of F# include script generator
  let generateFSharpScript (input: ScriptGenInput) =
    let packageName = input.PackageName.GetCompareString()

    let depLines =
      input.DependentScripts
      |> List.map LoadScript

    let frameworkRefLines =
      input.FrameworkReferences
      |> filterFSharpFrameworkReferences
      |> Seq.map ReferenceFrameworkAssembly
      |> Seq.toList

    let dllLines =
      match packageName.ToLowerInvariant() with
      | "fsharp.core" -> []
      | _ -> 
        input.OrderedDllReferences 
        |> List.map ReferenceAssemblyFile

    let lines = List.concat [depLines; frameworkRefLines; dllLines]
    match lines with
    | [] -> DoNotGenerate
    | xs -> Generate xs

  /// default implementation of C# include script generator
  let generateCSharpScript (input: ScriptGenInput) =
    let packageName = input.PackageName.GetCompareString()

    let depLines =
      input.DependentScripts
      |> List.map LoadScript

    let frameworkRefLines =
      input.FrameworkReferences
      |> List.map ReferenceFrameworkAssembly

    let dllLines =
      input.OrderedDllReferences
      |> List.map ReferenceAssemblyFile

    let lines = List.concat [depLines; frameworkRefLines; dllLines]

    match lines with
    | [] -> DoNotGenerate
    | xs -> Generate xs

  let writeFSharpScript scriptFile input =
    let pieces = [
      for piece in input do
        yield
          match piece with
          | ReferenceAssemblyFile file ->
            makeRelativePath scriptFile file
            |> sprintf """#r "%s" """
          | LoadScript script ->
            makeRelativePath scriptFile script
            |> sprintf """#load @"%s" """
          | ReferenceFrameworkAssembly name ->
            sprintf """#r "%s" """ name
    ]

    let text =
      pieces
      |> String.concat ("\n")
    

    scriptFile.Directory.Create()
    File.WriteAllText(scriptFile.FullName, text)
    
  let writeCSharpScript scriptFile input =
    let pieces = [
      for piece in input do
        yield
          match piece with
          | ReferenceAssemblyFile file ->
            makeRelativePath scriptFile file
            |> sprintf """#r "%s" """
          | LoadScript script ->
            makeRelativePath scriptFile script
            |> sprintf """#load "%s" """
          | ReferenceFrameworkAssembly name ->
            sprintf """#r "%s" """ name
    ]
    
    let text =
      pieces
      |> String.concat ("\n")
    
    scriptFile.Directory.Create()
    File.WriteAllText(scriptFile.FullName, text)

  let getIncludeScriptRootFolder (includeScriptsRootFolder: DirectoryInfo) (framework: FrameworkIdentifier) (folderForDefaultFramework: bool) = 
      if folderForDefaultFramework then
        DirectoryInfo(includeScriptsRootFolder.FullName)
      else
        DirectoryInfo(Path.Combine(includeScriptsRootFolder.FullName, string framework))

  let getScriptFolder (includeScriptsRootFolder: DirectoryInfo) (framework: FrameworkIdentifier) (groupName: GroupName) (folderForDefaultFramework: bool) =
      if groupName = Constants.MainDependencyGroup then
          getIncludeScriptRootFolder includeScriptsRootFolder framework folderForDefaultFramework
      else
          DirectoryInfo(Path.Combine((getIncludeScriptRootFolder includeScriptsRootFolder framework folderForDefaultFramework).FullName, groupName.GetCompareString()))

  let getScriptFile (includeScriptsRootFolder: DirectoryInfo) (framework: FrameworkIdentifier) (groupName: GroupName)  (folderForDefaultFramework: bool) (package: PackageName) (extension: string) =
      let folder = getScriptFolder includeScriptsRootFolder framework groupName folderForDefaultFramework

      FileInfo(Path.Combine(folder.FullName, sprintf "%s.%s" (package.GetCompareString()) extension))

  let getGroupNameAsOption groupName =
      if groupName = Constants.MainDependencyGroup then
          None
      else
          Some (groupName.ToString())

  let generateGroupScript
    (lockFile                  : LockFile)
    (getScriptFile             : GroupName -> FileInfo)
    (writeScript               : FileInfo -> ScriptPiece seq -> unit)
    (filterFrameworkAssemblies : string seq -> string seq)
    (filterNuget               : string -> bool)
    (framework                 : FrameworkIdentifier)
    =
      let all =
        seq {
          let mainGroupSeen = ref false
          let mainGroupKey = Constants.MainDependencyGroup.GetCompareString()
          for group, nuget, _ in Queries.getAllInstalledPackagesFromLockFile lockFile do
            if not (filterNuget nuget) then
              if group = mainGroupKey then
                mainGroupSeen := true
              let model = Queries.getInstalledPackageModel lockFile (QualifiedPackageName.FromStrings(Some group, nuget))
              let libs = model.GetLibReferences(framework) |> Seq.map (fun f -> FileInfo f)
              let syslibs = model.GetFrameworkAssembliesLazy.Value
              yield group, (libs, syslibs |> Set.toSeq)

//          if not !mainGroupSeen then
  //          yield mainGroupKey, (Seq.empty, Seq.empty) // Always generate Main group
        }
        |> Seq.groupBy fst
        |> Seq.map (fun (group, items) -> group, items |> Seq.map snd)
      
      for group, libs in all do
        let assemblies, frameworkLibs =
          Seq.foldBack (fun (l,r) (pl, pr) -> Seq.concat [pl ; l], Seq.concat [pr ; r]) libs (Seq.empty, Seq.empty)
          |> fun (l,r) -> Seq.distinct l, Seq.distinct r |> filterFrameworkAssemblies
        
        let assemblies = 
          let assemblyFilePerAssemblyDef = 
            assemblies
            |> Seq.map (fun (f:FileInfo) -> AssemblyDefinition.ReadAssembly(f.FullName:string), f)
            |> dict

          assemblyFilePerAssemblyDef.Keys
          |> Seq.toList
          |> PackageAndAssemblyResolution.getDllOrder
          |> Seq.map (assemblyFilePerAssemblyDef.TryGetValue >> snd)

        let scriptFile = getScriptFile (GroupName group)
        
        [
          for a in frameworkLibs do
            yield ScriptPiece.ReferenceFrameworkAssembly a
          for a in assemblies do
            yield ScriptPiece.ReferenceAssemblyFile a
        ]
        |> writeScript scriptFile
  
  /// Generate a include script from given order of packages,
  /// if a package is ordered before its dependencies this function 
  /// will throw.
  let generateScripts
      (scriptGenerator          : ScriptGenInput -> ScriptGenResult)
      (writeScript              : FileInfo -> ScriptPiece seq -> unit)
      (getScriptFile            : GroupName -> PackageName -> FileInfo)
      (includeScriptsRootFolder : DirectoryInfo)
      (framework                : FrameworkIdentifier)
      (lockFile                 : LockFile)
      (packagesOrGroupFolder    : DirectoryInfo)
      (groupName                : GroupName)
      (orderedPackages          : PackageResolver.ResolvedPackage list)
      =
      let fst' (a,_,_) = a

      orderedPackages
      |> Seq.fold (fun (knownIncludeScripts: Map<_,_>) (package: PackageResolver.ResolvedPackage) ->
          let scriptFile = getScriptFile groupName package.Name
          let groupName = getGroupNameAsOption groupName
          let dependencies = package.Dependencies |> Seq.map fst' |> Seq.choose knownIncludeScripts.TryFind |> List.ofSeq
          let installModel = Queries.getInstalledPackageModel lockFile (QualifiedPackageName.FromStrings(groupName, package.Name.ToString()))
          let dllFiles = getDllsWithinPackage framework installModel

          let scriptInfo = {
            PackageName                  = installModel.PackageName
            PackagesOrGroupFolder        = packagesOrGroupFolder
            IncludeScriptsRootFolder     = includeScriptsRootFolder
            FrameworkReferences          = getFrameworkReferencesWithinPackage installModel
            OrderedDllReferences = dllFiles
            DependentScripts             = dependencies
          }

          match scriptGenerator scriptInfo with
          | DoNotGenerate -> knownIncludeScripts
          | Generate pieces -> 
            writeScript scriptFile pieces
            knownIncludeScripts |> Map.add package.Name scriptFile

      ) Map.empty

      |> ignore

  /// Generate a include scripts for all packages defined in paket.dependencies,
  /// if a package is ordered before its dependencies this function will throw.
  let generateScriptsForRootFolderGeneric extension scriptGenerator scriptWriter filterFrameworkLibs filterNuget (framework: FrameworkIdentifier) isDefaultFramework (rootFolder: DirectoryInfo) =
      match Queries.PaketFiles.LocateFromDirectory rootFolder with
      | Queries.PaketFiles.JustDependencies _ -> failwith "paket.lock file not found"
      | Queries.PaketFiles.DependenciesAndLock(dependenciesFile, lockFile) ->
      
          let dependencies = getPackageOrderFromDependenciesFile (FileInfo(lockFile.FileName))
      
          let packagesFolder = DirectoryInfo(Path.Combine(rootFolder.FullName, Constants.PackagesFolderName))
        
          let includeScriptsRootFolder = 
              DirectoryInfo(Path.Combine((FileInfo dependenciesFile.FileName).Directory.FullName, Constants.PaketFolderName, "load"))

          let getScriptFile groupName packageName =
            getScriptFile includeScriptsRootFolder framework groupName isDefaultFramework packageName extension
      

          dependencies
          |> Map.map (fun groupName packages ->
              let packagesOrGroupFolder =
                  match getGroupNameAsOption groupName with
                  | None           -> packagesFolder
                  | Some groupName -> DirectoryInfo(Path.Combine(packagesFolder.FullName, groupName))

              generateScripts scriptGenerator scriptWriter getScriptFile includeScriptsRootFolder framework lockFile packagesOrGroupFolder groupName packages
          )
          |> ignore

          let getGroupFile group = 
              let folder = getScriptFolder includeScriptsRootFolder framework group isDefaultFramework
              let fileName = (sprintf "%s.group.%s" (group.GetCompareString()) extension).ToLowerInvariant()
              FileInfo(Path.Combine(folder.FullName, fileName))
        
          generateGroupScript lockFile getGroupFile scriptWriter filterFrameworkLibs filterNuget framework

  type ScriptType =
  | CSharp
  | FSharp
    with
      member x.Extension =
        match x with
        | CSharp -> "csx"
        | FSharp -> "fsx"
      static member TryCreate s = 
        match s with
        | "csx" -> Some CSharp
        | "fsx" -> Some FSharp
        | _ -> None

  let generateScriptsForRootFolder scriptType =
      let scriptGenerator, scriptWriter, filterFrameworkLibs, shouldExcludeNuget =
          match scriptType with
          | CSharp -> generateCSharpScript, writeCSharpScript, id, (fun _ -> false)
          | FSharp -> generateFSharpScript, writeFSharpScript, filterFSharpFrameworkReferences, shouldExcludeNugetForFSharpScript

      generateScriptsForRootFolderGeneric scriptType.Extension scriptGenerator scriptWriter filterFrameworkLibs shouldExcludeNuget

  let executeCommand directory providedFrameworks providedScriptTypes =
      match PaketFiles.LocateFromDirectory directory with
      | PaketFiles.JustDependencies _ -> failwith "paket.lock not found."
      | PaketFiles.DependenciesAndLock(dependenciesFile, lockFile) ->
          let rootFolder = DirectoryInfo(dependenciesFile.RootPath)
          let frameworksForDependencyGroups = Queries.resolveFrameworkForScriptGeneration dependenciesFile
          let environmentFramework = Queries.resolveEnvironmentFrameworkForScriptGeneration

          let tupleMap f v = (v, f v)
          let failOnMismatch toParse parsed f message =
              if List.length toParse <> List.length parsed then
                  toParse
                  |> Seq.map (tupleMap f)
                  |> Seq.filter (snd >> Option.isNone)
                  |> Seq.map fst
                  |> String.concat ", "
                  |> sprintf "%s: %s. Cannot generate include scripts." message
                  |> failwith

          // prepare list of frameworks to generate, paired with "is default framework"
          // default framework will get generated under root folder rather than framework specific subfolder
          let frameworksToGenerate =
              // specified frameworks are never considered default
              let targetFrameworkList = providedFrameworks |> List.choose FrameworkDetection.Extract |> List.map (fun f -> f, false)

              failOnMismatch providedFrameworks targetFrameworkList FrameworkDetection.Extract "Unrecognized Framework(s)"

              if not (Seq.isEmpty targetFrameworkList) then targetFrameworkList |> Seq.ofList
              else if not (Seq.isEmpty frameworksForDependencyGroups.Value) then 
                  // if paket.dependencies evaluate to single framework, consider it as default
                  let isDefaultFramework = Seq.length frameworksForDependencyGroups.Value = 1
                  frameworksForDependencyGroups.Value |> Seq.map (fun f -> f, isDefaultFramework)
                  
              else
                  // environment framework is default
                  Seq.singleton (environmentFramework.Value, true)

          let scriptTypesToGenerate =
              let parsedScriptTypes = providedScriptTypes |> List.choose ScriptType.TryCreate

              failOnMismatch providedScriptTypes parsedScriptTypes ScriptType.TryCreate "Unrecognized Script Type(s)"

              match parsedScriptTypes with
              | [] -> [CSharp; FSharp]
              | xs -> xs

          let workaround() = null |> ignore
          for framework, isDefaultFramework in frameworksToGenerate do
              Paket.Logging.tracefn "generating scripts for framework %s" (framework.ToString())
              workaround() // https://github.com/Microsoft/visualfsharp/issues/759#issuecomment-162243299
              for scriptType in scriptTypesToGenerate do
                  generateScriptsForRootFolder scriptType framework isDefaultFramework rootFolder

      ()