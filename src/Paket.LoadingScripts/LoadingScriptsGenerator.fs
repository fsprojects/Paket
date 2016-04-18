namespace Paket.LoadingScripts

open Paket
open Paket.Domain
open System.IO
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
  let getDllFilesWithinPackage (framework: FrameworkIdentifier) (installModel :InstallModel) =
    let dllFiles =
      installModel
      |> InstallModel.getLibReferences (SinglePlatform framework)
      |> Seq.map (fun path -> AssemblyDefinition.ReadAssembly path, path)
      |> dict

    LoadingScriptsGenerator.getDllOrder (dllFiles.Keys |> Seq.toList)
      |> List.map (fun a -> dllFiles.[a])

  type ScriptGenInput =
    { Framework : FrameworkIdentifier
      PackagesOrGroupFolder : DirectoryInfo
      PackageInstallModel : InstallModel
      DependentScripts : string seq }

  // Generate a fsharp script from the given order of packages, if a package is ordered before its dependencies this function will throw.
  let generateScripts
    getScriptName
    (scriptGenerator: ScriptGenInput -> string seq)
    (framework: FrameworkIdentifier)
    (dependenciesFile:Dependencies)
    (packagesOrGroupFolder:DirectoryInfo)
    groupName
    (orderedPackages: PackageResolver.ResolvedPackage list) =
      orderedPackages
      |> Seq.fold (fun (knownIncludeScripts:Map<PackageName,string>) (p:PackageResolver.ResolvedPackage) ->
        let scriptFile = Path.Combine (packagesOrGroupFolder.FullName, p.Name.GetCompareString(), getScriptName framework p.Name)
        let relScriptFile = Path.Combine (p.Name.GetCompareString(), getScriptName framework p.Name)
        let dependencies = p.Dependencies |> Seq.map (fun (depName,_,_) -> knownIncludeScripts.[depName])
        let installModel = dependenciesFile.GetInstalledPackageModel(groupName, p.Name.GetCompareString())
        let lines =
          scriptGenerator
            { Framework = framework
              PackagesOrGroupFolder = packagesOrGroupFolder
              PackageInstallModel = installModel
              DependentScripts = dependencies }
        File.WriteAllLines (scriptFile, lines)
        knownIncludeScripts |> Map.add p.Name relScriptFile) Map.empty
      |> ignore

  // Generate a fsharp script from the given order of packages, if a package is ordered before its dependencies this function will throw.
  let generateScriptsForRootFolder getScriptName scriptGenerator (framework: FrameworkIdentifier) (rootFolder: DirectoryInfo)  =
      
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
        let packagesOrGroupFolder, groupName =
          match groupName.GetCompareString () with
          | "main"    -> packagesFolder, None
          | groupName -> Path.Combine(packagesFolder.FullName, groupName) |> DirectoryInfo, Some groupName
        generateScripts getScriptName scriptGenerator framework dependenciesFile packagesOrGroupFolder groupName packages
        )
      |> ignore

  let generateFSharpScript (input: ScriptGenInput) =
    let packageName = input.PackageInstallModel.PackageName
    let packageFolder =
      Path.Combine (input.PackagesOrGroupFolder.FullName, packageName.GetCompareString())
      |> DirectoryInfo

    let depLines =
      input.DependentScripts
      |> Seq.map (fun s -> sprintf "#load \"../%s\"" (s.Replace("\\", "/")))

    let toRelative = (Path.GetFullPath >> (fun f -> f.Substring(packageFolder.FullName.Length + 1)))

    let dllFiles =
      if packageName.GetCompareString().ToLowerInvariant() = "fsharp.core" then
        Seq.empty
      else
        getDllFilesWithinPackage input.Framework input.PackageInstallModel
        |> Seq.map toRelative

    let dllLines =
      dllFiles
      |> Seq.map (fun dll -> sprintf "#r \"%s\"" (dll.Replace("\\", "/")))

    depLines
    |> fun lines -> Seq.append lines dllLines
    |> fun lines -> Seq.append lines [ sprintf "printfn \"%%s\" \"Loaded %s\"" (packageName.GetCompareString()) ]

  let generateFSharpScriptsForRootFolder =
    let getScriptName (framework: FrameworkIdentifier) (package: PackageName) = sprintf "Include_%s_%s.fsx" (package.GetCompareString()) (framework.ToString())
    generateScriptsForRootFolder getScriptName generateFSharpScript

  let generateCSharpScript (input: ScriptGenInput) =
    let packageName = input.PackageInstallModel.PackageName
    let packageFolder =
      Path.Combine (input.PackagesOrGroupFolder.FullName, packageName.GetCompareString())
      |> DirectoryInfo

    let depLines =
      input.DependentScripts
      |> Seq.map (fun s -> sprintf "#load \"../%s\"" (s.Replace("\\", "/")))

    let toRelative = (Path.GetFullPath >> (fun f -> f.Substring(packageFolder.FullName.Length + 1)))

    let dllFiles =
      getDllFilesWithinPackage input.Framework input.PackageInstallModel
      |> Seq.map toRelative

    let dllLines =
      dllFiles
      |> Seq.map (fun dll -> sprintf "#r \"%s\"" (dll.Replace("\\", "/")))

    depLines
    |> fun lines -> Seq.append lines dllLines
    |> fun lines -> Seq.append lines [ sprintf "System.Console.WriteLine(\"Loaded {0}\", \"%s\");" (packageName.GetCompareString()) ]

  let generateCSharpScriptsForRootFolder =
    let getScriptName (framework: FrameworkIdentifier) (package: PackageName) = sprintf "Include_%s_%s.csx" (package.GetCompareString()) (framework.ToString())
    generateScriptsForRootFolder getScriptName generateCSharpScript