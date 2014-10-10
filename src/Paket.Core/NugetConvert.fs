/// Contains methods for NuGet conversion
module Paket.NuGetConvert

open Paket
open System
open System.IO
open System.Xml
open Paket.Logging
open Paket.Nuget
open Paket.PackageSources

let private readPackageSources(configFile : FileInfo) =
    let doc = XmlDocument()
    doc.Load configFile.FullName
    [for node in doc.SelectNodes("//packageSources/add[@value]") -> 
        {PackageSources.NugetSource.Url = node.Attributes.["value"].Value
         PackageSources.NugetSource.Auth = doc.SelectNodes(sprintf "//packageSourceCredentials/%s" node.Attributes.["key"].Value) 
                                           |> Seq.cast<XmlNode> 
                                           |> Seq.firstOrDefault
                                           |> Option.map (fun node -> {Username = node.SelectSingleNode("//add[@key='Username']").Attributes.["value"].Value
                                                                       Password = node.SelectSingleNode("//add[@key='ClearTextPassword']").Attributes.["value"].Value})} ]

let removeFileIfExists file = 
    if File.Exists file then 
        File.Delete file
        tracefn "Deleted %s" file

let private convertNugetsToDepFile(nugetPackagesConfigs) =
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
    
    let defaultNugetSource = {NugetSource.Url = Constants.DefaultNugetStream; NugetSource.Auth = None}

    let nugetPackageRequirement (name: string, v: string, nugetSources : list<NugetSource>) =
        {Requirements.PackageRequirement.Name = name
         Requirements.PackageRequirement.VersionRequirement = VersionRequirement(VersionRange.Specific(SemVer.parse v), PreReleaseStatus.No)
         Requirements.PackageRequirement.ResolverStrategy = Max
         Requirements.PackageRequirement.Sources = nugetSources |> List.map (fun n -> PackageSources.PackageSource.Nuget(n))
         Requirements.PackageRequirement.Parent = Requirements.PackageRequirementSource.DependenciesFile(Constants.DependenciesFile)}

    match existingDepFile with
    | None ->
        let nugetSources =
            match FindAllFiles(".", "nuget.config") |> Seq.firstOrDefault with
            | Some configFile -> 
                let sources = readPackageSources(configFile) 
                removeFileIfExists configFile.FullName
                sources @ [defaultNugetSource]
            | None -> [defaultNugetSource]
            |> Set.ofList
            |> Set.toList        
        
        let packages = packagesToAdd |> List.map (fun (name,v) -> nugetPackageRequirement(name,v,nugetSources))
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
    convertNugetsToDepFile(nugetPackagesConfigs)
        
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
        removeFileIfExists packagesConfigFile.FullName

    match Directory.EnumerateDirectories(".", ".nuget", SearchOption.AllDirectories) |> Seq.firstOrDefault with
    | Some nugetDir ->
        let nugetTargets = Path.Combine(nugetDir, "nuget.targets")
        if File.Exists nugetTargets then
            let nugetExe = Path.Combine(nugetDir, "nuget.exe")
            removeFileIfExists nugetExe
            removeFileIfExists nugetTargets
            let depFile = DependenciesFile.ReadFromFile(Constants.DependenciesFile)
            if not <| depFile.HasPackage("Nuget.CommandLine") then depFile.Add("Nuget.CommandLine", "").Save()
            if initAutoRestore then
                VSIntegration.InitAutoRestore()

        if Directory.EnumerateFileSystemEntries(nugetDir) |> Seq.isEmpty 
            then Directory.Delete nugetDir
    | None -> ()

    if installAfter then
        UpdateProcess.Update(true,false,true)