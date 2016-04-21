namespace Paket.LoadingScripts

open System
open System.IO
open Paket
open Paket.Domain
open Mono.Cecil

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

    let getDllOrder (dllFiles : AssemblyDefinition list) =
      // we ignore all unknown references as they are most likely resolved on package level
      let known = dllFiles |> Seq.map (fun a -> a.FullName) |> Set.ofSeq
      getPackageOrderGeneric
        (fun (p:AssemblyDefinition) -> p.FullName)
        (fun p -> p.MainModule.AssemblyReferences |> Seq.map (fun r -> r.FullName) |> Seq.filter (known.Contains))
        dllFiles

module ScriptGeneratingModule =
  type ScriptType = 
  | CSharp 
  | FSharp
    with
      member x.Extension =
        match x with
        | CSharp -> "csx"
        | FSharp -> "fsx"
  
  let getDllFilesWithinPackage (framework: FrameworkIdentifier) (installModel :InstallModel) =
    let dllFiles =
      installModel
      |> InstallModel.getLibReferences (SinglePlatform framework)
      |> Seq.map (fun path -> AssemblyDefinition.ReadAssembly path, FileInfo(path))
      |> dict

    LoadingScriptsGenerator.getDllOrder (dllFiles.Keys |> Seq.toList)
      |> List.map (fun a -> dllFiles.[a])

  type ScriptGenInput = { 
      Framework                : FrameworkIdentifier
      PackagesOrGroupFolder    : DirectoryInfo
      IncludeScriptsRootFolder : DirectoryInfo
      PackageInstallModel      : InstallModel
      DependentScripts         : FileInfo seq
  }

  let getIncludeScriptRootFolder (includeScriptsRootFolder: DirectoryInfo) (framework: FrameworkIdentifier) = 
      Path.Combine(includeScriptsRootFolder.FullName, string framework)
      |> DirectoryInfo

  let getScriptFolder (includeScriptsRootFolder: DirectoryInfo) (framework: FrameworkIdentifier) (groupName: GroupName) =
      if groupName = Constants.MainDependencyGroup then
          getIncludeScriptRootFolder includeScriptsRootFolder framework
      else
          Path.Combine((getIncludeScriptRootFolder includeScriptsRootFolder framework).FullName, groupName.ToString())
          |> DirectoryInfo

  let getScriptFile (includeScriptsRootFolder: DirectoryInfo) (framework: FrameworkIdentifier) (groupName: GroupName) (package: PackageName) (scriptType: ScriptType) = 
      let folder = getScriptFolder includeScriptsRootFolder framework groupName

      Path.Combine(folder.FullName, sprintf "include.%s.%s" (package.GetCompareString()) scriptType.Extension)
      |> FileInfo

  let makeRelativePath (scriptFile: FileInfo) (libFile: FileInfo) =
    (scriptFile.FullName |> Uri).MakeRelativeUri(libFile.FullName |> Uri).ToString()

  let generateFSharpScript scriptFile (input: ScriptGenInput) =
    let packageName = input.PackageInstallModel.PackageName

    let depLines =
      input.DependentScripts
      |> Seq.map (fun script -> sprintf """#load @"%s" """ script.Name)

    let dllFiles =
      if packageName.GetCompareString().ToLowerInvariant() = "fsharp.core" then
        List.empty
      else
        getDllFilesWithinPackage input.Framework input.PackageInstallModel

    let dllLines =
      dllFiles
      |> Seq.map (makeRelativePath scriptFile >> sprintf """#r "%s" """)

    depLines
    |> fun lines -> Seq.append lines dllLines
    |> fun lines -> Seq.append lines [ sprintf "printfn \"%%s\" \"Loaded %s\"" (packageName.GetCompareString()) ]

  let generateCSharpScript scriptFile (input: ScriptGenInput) =
    let packageName = input.PackageInstallModel.PackageName

    let depLines =
      input.DependentScripts
      |> Seq.map (fun script -> sprintf """#load "%s" """ script.Name)

    let dllFiles = getDllFilesWithinPackage input.Framework input.PackageInstallModel

    let dllLines =
      dllFiles
      |> Seq.map (makeRelativePath scriptFile >> sprintf """#r "%s" """)

    depLines
    |> fun lines -> Seq.append lines dllLines
    |> fun lines -> Seq.append lines [ sprintf "System.Console.WriteLine(\"Loaded {0}\", \"%s\");" (packageName.GetCompareString()) ]
  
  let getGroupNameAsOption groupName =
      if groupName = Constants.MainDependencyGroup then
          None
      else
          Some (groupName.ToString())

  // Generate a fsharp script from the given order of packages, if a package is ordered before its dependencies this function will throw.
  let generateScripts
      (scriptGenerator          : FileInfo -> ScriptGenInput -> seq<string>)
      (getScriptFile            : GroupName -> PackageName -> FileInfo)
      (includeScriptsRootFolder : DirectoryInfo)
      (framework                : FrameworkIdentifier)
      (dependenciesFile         : Dependencies)
      (packagesOrGroupFolder    : DirectoryInfo)
      (groupName                : GroupName)
      (orderedPackages          : PackageResolver.ResolvedPackage list)
      =
      orderedPackages
      |> Seq.fold (fun (knownIncludeScripts: Map<_,_>) (package: PackageResolver.ResolvedPackage) ->
        
          let scriptFile = getScriptFile groupName package.Name
        
          let groupName = getGroupNameAsOption groupName
          let dependencies = package.Dependencies |> Seq.map (fun (depName,_,_) -> knownIncludeScripts.[depName])
        
          let installModel = dependenciesFile.GetInstalledPackageModel(groupName, package.Name.GetCompareString())

          let lines =
              scriptGenerator scriptFile {
                  Framework                = framework
                  PackagesOrGroupFolder    = packagesOrGroupFolder
                  IncludeScriptsRootFolder = includeScriptsRootFolder
                  PackageInstallModel      = installModel
                  DependentScripts         = dependencies
              }
        
          scriptFile.Directory.Create()
        
          File.WriteAllLines (scriptFile.FullName, lines)
        
          knownIncludeScripts |> Map.add package.Name scriptFile

      ) Map.empty

      |> ignore

  // Generate a fsharp script from the given order of packages, if a package is ordered before its dependencies this function will throw.
  let generateScriptsForRootFolder scriptType (framework: FrameworkIdentifier) (rootFolder: DirectoryInfo)  =
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
          Path.Combine(rootFolder.FullName, Constants.PackagesFolderName)
          |> DirectoryInfo
        
      let includeScriptsRootFolder = 
          Path.Combine(((dependenciesFile.DependenciesFile) |> FileInfo).Directory.FullName, Constants.PaketFilesFolderName, "include-scripts")
          |> DirectoryInfo

      let scriptGenerator =
          match scriptType with
          | CSharp -> generateCSharpScript
          | FSharp -> generateFSharpScript

      let getScriptFile groupName packageName =
        getScriptFile includeScriptsRootFolder framework groupName packageName scriptType

      dependencies
      |> Map.map (fun groupName packages ->
          
          let packagesOrGroupFolder =
              match getGroupNameAsOption groupName with
              | None           -> packagesFolder
              | Some groupName -> Path.Combine(packagesFolder.FullName, groupName) |> DirectoryInfo

          generateScripts scriptGenerator getScriptFile includeScriptsRootFolder framework dependenciesFile packagesOrGroupFolder groupName packages
      )
      |> ignore
