namespace Paket.LoadingScripts

open System
open System.IO
open Paket
open Paket.Domain
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
  | PrintStatement             of string

  type ScriptGenInput = {
      PackageName                  : PackageName
      Framework                    : FrameworkIdentifier
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

  /// default implementation of F# include script generator
  let generateFSharpScript (input: ScriptGenInput) =
    let packageName = input.PackageName.GetCompareString()

    let depLines =
      input.DependentScripts
      |> List.map LoadScript

    let frameworkRefLines =
      input.FrameworkReferences
      |> List.filter (
          function 
          | "mscorlib" ->
              // we never want to reference mscorlib directly (some nuget package state it as a dependency)
              // reason is that having it referenced more than once fails in FSI
              false 
          | _ -> true
      )
      |> List.map ReferenceFrameworkAssembly

    let dllLines =
      match packageName.ToLowerInvariant() with
      | "fsharp.core" -> []
      | _ -> 
        input.OrderedDllReferences 
        |> List.map ReferenceAssemblyFile

    let lines = List.concat [depLines; frameworkRefLines; dllLines]
    match lines with
    | [] -> DoNotGenerate
    | xs -> List.append xs [ PrintStatement (sprintf "%s Loaded" packageName) ] |> Generate

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
    | xs -> List.append xs [ PrintStatement (sprintf "%s Loaded" packageName) ] |> Generate

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
          | PrintStatement text -> 
            let escape = 
              // /!\ /!\ /!\ TODO escape text /!\ /!\ /!\
              id
            sprintf @"printfn ""%s"" " (escape text)
    ]

    let text =
      pieces
      |> String.concat ("\n")
    
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
          | PrintStatement text -> 
            let escape = 
              // /!\ /!\ /!\ TODO escape text /!\ /!\ /!\
              id
            sprintf @"System.Console.WriteLine(""%s""); " (escape text)
    ]

    let text =
      pieces
      |> String.concat ("\n")
    
    File.WriteAllText(scriptFile.FullName, text)

  let getIncludeScriptRootFolder (includeScriptsRootFolder: DirectoryInfo) (framework: FrameworkIdentifier) = 
      DirectoryInfo(Path.Combine(includeScriptsRootFolder.FullName, string framework))

  let getScriptFolder (includeScriptsRootFolder: DirectoryInfo) (framework: FrameworkIdentifier) (groupName: GroupName) =
      if groupName = Constants.MainDependencyGroup then
          getIncludeScriptRootFolder includeScriptsRootFolder framework
      else
          DirectoryInfo(Path.Combine((getIncludeScriptRootFolder includeScriptsRootFolder framework).FullName, groupName.GetCompareString()))

  let getScriptFile (includeScriptsRootFolder: DirectoryInfo) (framework: FrameworkIdentifier) (groupName: GroupName) (package: PackageName) (extension: string) =
      let folder = getScriptFolder includeScriptsRootFolder framework groupName

      FileInfo(Path.Combine(folder.FullName, sprintf "include.%s.%s" (package.GetCompareString()) extension))

  let getGroupNameAsOption groupName =
      if groupName = Constants.MainDependencyGroup then
          None
      else
          Some (groupName.ToString())
  
  /// Generate a include script from given order of packages,
  /// if a package is ordered before its dependencies this function 
  /// will throw.
  let generateScripts
      (scriptGenerator          : ScriptGenInput -> ScriptGenResult)
      (writeScript              : FileInfo -> ScriptPiece list -> unit)
      (getScriptFile            : GroupName -> PackageName -> FileInfo)
      (includeScriptsRootFolder : DirectoryInfo)
      (framework                : FrameworkIdentifier)
      (dependenciesFile         : Dependencies)
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
          let installModel = dependenciesFile.GetInstalledPackageModel(groupName, package.Name.ToString())
          let dllFiles = getDllsWithinPackage framework installModel

          let scriptInfo = {
            PackageName                  = installModel.PackageName
            Framework                    = framework
            PackagesOrGroupFolder        = packagesOrGroupFolder
            IncludeScriptsRootFolder     = includeScriptsRootFolder
            FrameworkReferences          = getFrameworkReferencesWithinPackage installModel
            OrderedDllReferences = dllFiles
            DependentScripts             = dependencies
          }

          match scriptGenerator scriptInfo with
          | DoNotGenerate -> knownIncludeScripts
          | Generate pieces -> 
            scriptFile.Directory.Create()
            writeScript scriptFile pieces
            knownIncludeScripts |> Map.add package.Name scriptFile

      ) Map.empty

      |> ignore

  /// Generate a include scripts for all packages defined in paket.dependencies,
  /// if a package is ordered before its dependencies this function will throw.
  let generateScriptsForRootFolderGeneric extension scriptGenerator scriptWriter (framework: FrameworkIdentifier) (rootFolder: DirectoryInfo) =
      let dependenciesFile, lockFile =
          let deps = Paket.Dependencies.Locate(rootFolder.FullName)
          let lock =
            deps.DependenciesFile
            |> Paket.DependenciesFile.ReadFromFile
            |> fun f -> f.FindLockfile().FullName
            |> Paket.LockFile.LoadFrom
          deps, lock
      
      let dependencies = getPackageOrderFromDependenciesFile (FileInfo(lockFile.FileName))
      
      let packagesFolder = DirectoryInfo(Path.Combine(rootFolder.FullName, Constants.PackagesFolderName))
        
      let includeScriptsRootFolder = 
          DirectoryInfo(Path.Combine((FileInfo dependenciesFile.DependenciesFile).Directory.FullName, Constants.PaketFilesFolderName, "include-scripts"))

      let getScriptFile groupName packageName =
        getScriptFile includeScriptsRootFolder framework groupName packageName extension

      dependencies
      |> Map.map (fun groupName packages ->
          
          let packagesOrGroupFolder =
              match getGroupNameAsOption groupName with
              | None           -> packagesFolder
              | Some groupName -> DirectoryInfo(Path.Combine(packagesFolder.FullName, groupName))

          generateScripts scriptGenerator scriptWriter getScriptFile includeScriptsRootFolder framework dependenciesFile packagesOrGroupFolder groupName packages
      )
      |> ignore

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
      let scriptGenerator, scriptWriter =
          match scriptType with
          | CSharp -> generateCSharpScript, writeCSharpScript
          | FSharp -> generateFSharpScript, writeFSharpScript

      generateScriptsForRootFolderGeneric scriptType.Extension scriptGenerator scriptWriter