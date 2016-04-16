namespace Paket.LoadingScripts

open Paket
open Paket.Domain

module LoadingScriptsGenerator =
    let getLeafPackages (knownPackages:Set<PackageName>) (state:PackageResolver.ResolvedPackage list) =
        let leafPackages =
          state 
          |> List.filter (fun p ->
              not (knownPackages.Contains(p.Name)) &&
              p.Dependencies |> Seq.map (fun (n, _, _)-> n) |> Seq.forall (knownPackages.Contains))
        let newKnownPackages =
          leafPackages
          |> Seq.fold (fun state { Name = n } -> state |> Set.add n) knownPackages
        let newState =
          state
          |> List.filter (fun p -> leafPackages |> Seq.forall (fun l -> l.Name <> p.Name))
        leafPackages, newKnownPackages, newState

    let handleParseState (packages:PackageResolver.ResolvedPackage list) =
      let rec step finalList knownPackages currentPackages =
        match currentPackages |> getLeafPackages knownPackages with
        | ([], _, _) -> finalList
        | (leafPackages, newKnownPackages, newState) ->
          step (leafPackages @ finalList) newKnownPackages newState
      step [] Set.empty packages
      |> List.rev
          

    let getPackageOrderFromDependenciesFile (depFile:string) =
        let depFile = LockFileParser.Parse (System.IO.File.ReadAllLines depFile)
        depFile
        |> Seq.map (fun p -> p.GroupName, handleParseState p.Packages)
        |> Map.ofSeq
        // generateScriptsFromDependenciesFile generateIncludeScript depFile

    let testOrdering =
      let testData =
          [ { PackageResolver.ResolvedPackage.Name = PackageName("Test1")
              PackageResolver.ResolvedPackage.Version = SemVer.Parse "1.0.0"
              PackageResolver.ResolvedPackage.Dependencies =
                Set.empty
                |> Set.add(
                    PackageName("other"), 
                    VersionRequirement(VersionRange.Specific (SemVer.Parse "1.0.0"), PreReleaseStatus.No),
                    Paket.Requirements.FrameworkRestrictions.Empty)
              PackageResolver.ResolvedPackage.Unlisted = false
              PackageResolver.ResolvedPackage.Settings = Requirements.InstallSettings.Default
              PackageResolver.ResolvedPackage.Source = PackageSources.PackageSource.NuGetV2 { Url = ""; Authentication = None } }
            { Name = PackageName("other")
              Version = SemVer.Parse "1.0.0"
              Dependencies = Set.empty
              Unlisted = false
              Settings = Requirements.InstallSettings.Default
              Source = PackageSources.PackageSource.NuGetV2 { Url = ""; Authentication = None } }
          ]
      let result =
        handleParseState testData
        |> List.map (fun p -> p.Name)

      System.Diagnostics.Debug.Assert(
        result = 
          [  PackageName("other")
             PackageName("Test1")
          ] : bool)
          
      let result2 =
        handleParseState (testData |> List.rev)
        |> List.map (fun p -> p.Name)

      System.Diagnostics.Debug.Assert(
        result2 = 
          [  PackageName("other")
             PackageName("Test1")
          ] : bool)


module ScriptGeneratingModule =
  open System.IO
  let getScriptName (package: PackageName) = sprintf "Include_%s.fsx" (package.GetCompareString())
  let generateFSharpScript packagesOrGroupFolder (knownIncludeScripts:Map<PackageName, string>) (package: PackageResolver.ResolvedPackage) =
    let packageFolder = Path.Combine (packagesOrGroupFolder, package.Name.GetCompareString())
    let scriptFile = Path.Combine (packageFolder, getScriptName package.Name)
    let relScriptFile = Path.Combine (package.Name.GetCompareString(), getScriptName package.Name)
    let depLines =
      package.Dependencies
      |> Seq.map (fun (depName,_,_) -> sprintf "#load \"../%s\"" ((knownIncludeScripts |> Map.find depName).Replace("\\", "/")))

    let dllFiles =
      if package.Name.GetCompareString().ToLowerInvariant() = "fsharp.core" then
        Seq.empty
      else
        let libDir = Path.Combine (packageFolder, "lib")
        // TODO: Replace with some intelligent code to use the correct framework dlls,
        // Generate multiple include scripts per-framework.
        let net40 = Path.Combine (libDir, "net40")
        let net45 = Path.Combine (libDir, "net45")
        let toRelative seq =
          seq
          |> Seq.map (Path.GetFullPath >> (fun f -> f.Substring(packageFolder.Length + 1)))
        if (Directory.Exists net45) then
          Directory.EnumerateFiles(net45, "*.dll")
          |> toRelative
        elif (Directory.Exists net40) then
          Directory.EnumerateFiles(net40, "*.dll")
          |> toRelative
        else
          printfn "Could not find either 'net45' nor 'net40' folder for package '%s'" (package.Name.GetCompareString())
          Seq.empty

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
  let generateFSharpScripts packagesOrGroupFolder (orderedPackages: PackageResolver.ResolvedPackage list) =
      orderedPackages
      |> Seq.fold (fun (knownIncludeScripts) p ->
        generateFSharpScript packagesOrGroupFolder knownIncludeScripts p) Map.empty
      |> ignore

        
  // Generate a fsharp script from the given order of packages, if a package is ordered before its dependencies this function will throw.
  let generateFSharpScriptsFromDepFile packagesFolder (depFile) =
      let depFile = LoadingScriptsGenerator.getPackageOrderFromDependenciesFile depFile
      
      depFile
      |> Map.map (fun k packages ->
        let packagesOrGroupFolder =
          let groupName = k.GetCompareString ()
          if groupName = "main" then packagesFolder else Path.Combine(packagesFolder, groupName)
        generateFSharpScripts packagesOrGroupFolder packages
        )
      |> ignore
      