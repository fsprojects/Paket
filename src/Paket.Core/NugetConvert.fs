/// Contains methods for NuGet conversion
module Paket.NuGetConvert

open Paket
open System
open System.IO
open System.Xml
open Paket.Logging
open Paket.Nuget
open Paket.PackageSources

type NugetConfig = 
    { PackageSources : list<PackageSource>
      PackageRestoreEnabled : bool
      PackageRestoreAutomatic : bool }

let applyConfig config (doc : XmlDocument) =
    let clearSources = doc.SelectSingleNode("//packageSources/clear") <> null
    let sources = 
        [ for node in doc.SelectNodes("//packageSources/add[@value]") do
            if node.Attributes.["value"] <> null then 
                let url = node.Attributes.["value"].Value
                  
                let authNode = 
                    if node.Attributes.["key"] = null then None else 
                    let key  =XmlConvert.EncodeLocalName node.Attributes.["key"].Value
                    doc.SelectNodes(sprintf "//packageSourceCredentials/%s" key)
                    |> Seq.cast<XmlNode>
                    |> Seq.firstOrDefault
                  
                let auth =
                    match authNode with
                    | Some node ->
                          let userNode = node.SelectSingleNode("//add[@key='Username']")
                          let passwordNode = node.SelectSingleNode("//add[@key='ClearTextPassword']")
                          
                          if userNode = null || passwordNode = null then None else
                          let usernameAttr = userNode.Attributes.["value"]
                          let passwordAttr = passwordNode.Attributes.["value"]
                          
                          if usernameAttr = null || passwordAttr = null then None else
                          Some { Username = AuthEntry.Create usernameAttr.Value; Password = AuthEntry.Create passwordAttr.Value }
                    | None -> None

                yield PackageSource.Parse(url, auth) ]

    { PackageSources = if clearSources then sources else config.PackageSources @ sources
      PackageRestoreEnabled = 
        match doc.SelectNodes("//packageRestore/add[@key='enabled']") |> Seq.cast<XmlNode> |> Seq.firstOrDefault with
        | Some node -> bool.Parse(node.Attributes.["value"].Value)
        | None -> config.PackageRestoreEnabled
      PackageRestoreAutomatic = 
        match doc.SelectNodes("//packageRestore/add[@key='automatic']") |> Seq.cast<XmlNode> |> Seq.firstOrDefault with
        | Some node -> bool.Parse(node.Attributes.["value"].Value)
        | None -> config.PackageRestoreAutomatic }

let private readNugetConfig() =
    let config = 
        DirectoryInfo(".nuget")
        |> Seq.unfold (fun di -> if di = null 
                                 then None 
                                 else Some(FileInfo(Path.Combine(di.FullName, "nuget.config")), di.Parent)) 
        |> Seq.toList
        |> List.rev
        |> List.append [FileInfo(Path.Combine(
                                      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                      "nuget", 
                                      "nuget.config"))]
        |> List.filter (fun f -> f.Exists)
        |> List.map (fun f -> let doc = XmlDocument() in doc.Load(f.FullName); doc)
        |> List.fold applyConfig 
                     { PackageSources = [] 
                       PackageRestoreEnabled = false 
                       PackageRestoreAutomatic = false }
    {config with PackageSources = if config.PackageSources = [] then [Paket.PackageSources.DefaultNugetSource] else config.PackageSources }

let removeFile file = 
    File.Delete file
    tracefn "Deleted %s" file

let private convertNugetsToDepFile(nugetPackagesConfigs, nugetConfig) =
    let allVersions =
        nugetPackagesConfigs
        |> Seq.collect (fun c -> c.Packages)
        |> Seq.groupBy fst
        |> Seq.map (fun (name, packages) -> name, packages |> Seq.map snd |> Seq.distinct)
        |> Seq.sortBy (fun (name,_) -> name.ToLower())
    
    for (name, versions) in allVersions do
        if Seq.length versions > 1 
        then traceWarnfn "Package %s is referenced multiple times in different versions: %A. Paket will choose the latest one." 
                            name    
                            (versions |> Seq.map string |> Seq.toList)
    
    let latestVersions = allVersions |> Seq.map (fun (name,versions) -> name, versions |> Seq.max |> string) |> Seq.toList

    let existingDepFile = 
        if File.Exists Constants.DependenciesFile 
        then Some(DependenciesFile.ReadFromFile(Constants.DependenciesFile)) 
        else None

    let confictingPackages, packagesToAdd = 
        match existingDepFile with
        | Some depFile -> latestVersions |> List.partition (fun (name,_) -> depFile.HasPackage name)
        | None -> [], latestVersions
    
    for (name, _) in confictingPackages do traceWarnfn "Package %s is already defined in %s" name Constants.DependenciesFile

    let nugetPackageRequirement (name: string, v: string, sources : list<PackageSource>) =
        {Requirements.PackageRequirement.Name = name
         Requirements.PackageRequirement.VersionRequirement = VersionRequirement(VersionRange.Specific(SemVer.Parse v), PreReleaseStatus.No)
         Requirements.PackageRequirement.ResolverStrategy = Max
         Requirements.PackageRequirement.Sources = sources
         Requirements.PackageRequirement.Parent = Requirements.PackageRequirementSource.DependenciesFile(Constants.DependenciesFile)}

    match existingDepFile with
    | None ->
        let packages = packagesToAdd |> List.map (fun (name,v) -> nugetPackageRequirement(name,v,nugetConfig.PackageSources))
        DependenciesFile(Constants.DependenciesFile, InstallOptions.Default, packages, []).Save()
    | Some depFile ->
        if not (packagesToAdd |> List.isEmpty)
            then (packagesToAdd |> List.fold (fun (d : DependenciesFile) (name,version) -> d.Add(name,version)) depFile).Save()
        else tracefn "%s is up to date" depFile.FileName

let private convertNugetToRefFile(nugetPackagesConfig) =
    let refFilePath = Path.Combine(nugetPackagesConfig.File.DirectoryName, Constants.ReferencesFile)
    let existingRefFile = if File.Exists refFilePath then Some <| ReferencesFile.FromFile(refFilePath) else None

    let confictingRefs, refsToAdd =
        match existingRefFile with
        | Some refFile -> 
            nugetPackagesConfig.Packages 
            |> List.partition (fun (name,_) -> 
                                    refFile.NugetPackages |> List.exists (fun np -> String.Equals(name,np,StringComparison.InvariantCultureIgnoreCase)))
        | _ -> [], nugetPackagesConfig.Packages
    
    for (name,_) in confictingRefs do traceWarnfn "Reference %s is already defined in %s" name refFilePath
            
    match existingRefFile with 
    | None -> {ReferencesFile.FileName = refFilePath; NugetPackages = refsToAdd |> List.map fst; GitHubFiles = []}.Save()
    | Some refFile ->
        if not (refsToAdd |> List.isEmpty)
            then (refsToAdd |> List.fold (fun (refFile : ReferencesFile) (name,_) -> refFile.AddNugetRef(name)) refFile).Save()
        else tracefn "%s is up to date" refFilePath

/// Converts all projects from NuGet to Paket
let ConvertFromNuget(force, installAfter, initAutoRestore) =
    if File.Exists Constants.DependenciesFile && not force then failwithf "%s already exists, use --force to overwrite" Constants.DependenciesFile

    let nugetPackagesConfigs = FindAllFiles(".", "packages.config") |> Seq.map Nuget.ReadPackagesConfig
    let nugetConfig = readNugetConfig()
    FindAllFiles(".", "nuget.config") |> Seq.iter (fun f -> removeFile f.FullName)
    
    convertNugetsToDepFile(nugetPackagesConfigs, nugetConfig)
        
    for nugetPackagesConfig in nugetPackagesConfigs do
        let packageFile = nugetPackagesConfig.File
        match nugetPackagesConfig.Type with
        | ProjectLevel ->
            let refFile = Path.Combine(packageFile.DirectoryName, Constants.ReferencesFile)
            if File.Exists refFile && not force then failwithf "%s already exists, use --force to overwrite" refFile
            convertNugetToRefFile(nugetPackagesConfig)
        | SolutionLevel -> ()

    for slnFile in FindAllFiles(".", "*.sln") do
        let solution = SolutionFile(slnFile.FullName)
        solution.RemoveNugetEntries()
        let relativePath = createRelativePath solution.FileName Environment.CurrentDirectory 
        solution.AddPaketFolder(Path.Combine(relativePath, Constants.DependenciesFile), 
                                if installAfter then Some(Path.Combine(relativePath, "paket.lock")) else None)
        solution.Save()

    for project in ProjectFile.FindAllProjects(".") do
        project.ReplaceNugetPackagesFile()
        project.RemoveNugetTargetsEntries()
        project.Save()

    for packagesConfigFile in nugetPackagesConfigs |> Seq.map (fun f -> f.File) do
        removeFile packagesConfigFile.FullName

    let autoVsNugetRestore = nugetConfig.PackageRestoreEnabled && nugetConfig.PackageRestoreAutomatic
    let nugetTargets = FindAllFiles(".", "nuget.targets") |> Seq.firstOrDefault
    
    match nugetTargets with
    | Some nugetTargets ->
        removeFile nugetTargets.FullName
        let nugetExe = Path.Combine(nugetTargets.DirectoryName, "nuget.exe")
        if File.Exists nugetExe then 
            removeFile nugetExe
            let depFile = DependenciesFile.ReadFromFile(Constants.DependenciesFile)
            if not <| depFile.HasPackage("Nuget.CommandLine") then depFile.Add("Nuget.CommandLine", "").Save()
            
        if Directory.EnumerateFileSystemEntries(nugetTargets.DirectoryName) |> Seq.isEmpty 
            then Directory.Delete nugetTargets.DirectoryName
    | None -> ()

    if initAutoRestore && (autoVsNugetRestore || nugetTargets.IsSome) then 
        VSIntegration.InitAutoRestore()

    if installAfter then
        UpdateProcess.Update(true,false,true)