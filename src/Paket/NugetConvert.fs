/// Contains methods for NuGet conversion
module Paket.NuGetConvert

open Paket
open System
open System.IO
open System.Xml
open Paket.Logging

let private readPackageSources(configFile : FileInfo) =
    let doc = XmlDocument()
    doc.Load configFile.FullName
    [for node in doc.SelectNodes("//packageSources/add[@value]") -> node.Attributes.["value"].Value]

let private convertNugetsToDepFile(nugetPackagesConfigs) =
    let allVersions =
        nugetPackagesConfigs
        |> Seq.collect (fun c -> c.Packages)
        |> Seq.groupBy fst
        |> Seq.map (fun (name, packages) -> name, packages |> Seq.map snd |> Seq.distinct)
    
    for (name, versions) in allVersions do
        if Seq.length versions > 1 
        then traceWarnfn "Package %s is referenced multiple times in different versions: %A. Paket will choose the latest one." 
                            name    
                            (versions |> Seq.map string |> Seq.toList)
    
    let latestVersions = allVersions |> Seq.map (fun (name,versions) -> name, versions |> Seq.max |> string)
    
    let depFileExists = File.Exists Constants.DepsFile 
    let existingPackages = 
        if depFileExists 
        then (DependenciesFile.ReadFromFile Constants.DepsFile).Packages |> List.map (fun p -> p.Name.ToLower()) |> Set.ofList
        else Set.empty
    let confictingPackages = Set.intersect existingPackages (latestVersions |> Seq.map fst |> Seq.map (fun n -> n.ToLower()) |> Set.ofSeq)
    confictingPackages |> Set.iter (fun name -> traceWarnfn "Package %s is already defined in %s" name Constants.DepsFile)

    let dependencyLines = 
        latestVersions 
        |> Seq.filter (fun (name,_) -> not (confictingPackages |> Set.contains (name.ToLower())))
        |> Seq.map (fun (name,version) -> sprintf "nuget %s %s" name version)
        |> Seq.toList
    
    if not depFileExists 
        then
            let packageSources =
                match FindAllFiles(".", "nuget.config") |> Seq.firstOrDefault with
                | Some configFile -> 
                    let sources = readPackageSources(configFile) 
                    File.Delete(configFile.FullName)
                    sources @ [Constants.DefaultNugetStream]
                | None -> [Constants.DefaultNugetStream]
                |> Set.ofList
                |> Set.toList
                |> List.map (sprintf "source %s")                
                
            File.WriteAllLines(Constants.DepsFile, packageSources @ [String.Empty] @ dependencyLines)
            tracefn "Generated %s file" Constants.DepsFile 
    elif not (dependencyLines |> Seq.isEmpty)
        then
            File.WriteAllLines(Constants.DepsFile, Seq.append (File.ReadAllLines(Constants.DepsFile)) dependencyLines)
            traceWarnfn "Overwritten %s file" Constants.DepsFile 
    else tracefn "%s is up to date" Constants.DepsFile

let private convertNugetToRefFile(nugetPackagesConfig) =
    let refFile = Path.Combine(nugetPackagesConfig.File.DirectoryName, "paket.references")
    let refFileExists = File.Exists refFile
    let existingReferences = 
        if refFileExists
        then (File.ReadAllLines refFile) |> Array.map (fun p -> p.ToLower()) |> Set.ofArray
        else Set.empty
    let confictingReferences = Set.intersect existingReferences (nugetPackagesConfig.Packages |> List.map fst |> Seq.map (fun n -> n.ToLower()) |> Set.ofSeq)
    confictingReferences |> Set.iter (fun name -> traceWarnfn "Reference %s is already defined in %s" name refFile)

    let referencesLines = 
        nugetPackagesConfig.Packages 
        |> List.map fst 
        |> List.filter (fun name -> not (confictingReferences |> Set.contains (name.ToLower())))
                            
    if not refFileExists 
        then
            File.WriteAllLines(refFile, referencesLines)
            tracefn "Converted %s to paket.references" nugetPackagesConfig.File.FullName 
    elif not (referencesLines |> List.isEmpty)
        then
            File.WriteAllLines(refFile, Seq.append (File.ReadAllLines(refFile)) referencesLines)
            traceWarnfn "Overwritten %s file" refFile
    else tracefn "%s is up to date" refFile

/// Converts all projects from NuGet to Paket
let ConvertFromNuget(force, installAfter) =
    if File.Exists Constants.DepsFile && not force then failwithf "%s already exists, use --force to overwrite" Constants.DepsFile

    let nugetPackagesConfigs = FindAllFiles(".", "packages.config") |> Seq.map Nuget.ReadPackagesConfig
    convertNugetsToDepFile(nugetPackagesConfigs)
        
    for nugetPackagesConfig  in nugetPackagesConfigs do
        let packageFile = nugetPackagesConfig.File
        match nugetPackagesConfig.Type with
        | ProjectLevel ->
            let refFile = Path.Combine(packageFile.DirectoryName, "paket.references")
            if File.Exists refFile && not force then failwithf "%s already exists, use --force to overwrite" refFile
            convertNugetToRefFile(nugetPackagesConfig)
        | SolutionLevel -> ()

    for slnFile in FindAllFiles(".", "*.sln") do
        SolutionFile.RemoveNugetEntries(slnFile.FullName)

    for projFile in ProjectFile.FindAllProjects(".") do
        let project = ProjectFile.Load(projFile.FullName)
        project.ReplaceNugetPackagesFile()
        project.RemoveNugetTargetsEntries()
        project.Save()

    for packagesConfigFile in nugetPackagesConfigs |> Seq.map (fun f -> f.File) do
        File.Delete(packagesConfigFile.FullName)

    match Directory.EnumerateDirectories(".", ".nuget", SearchOption.AllDirectories) |> Seq.firstOrDefault with
    | Some nugetDir ->
        let nugetTargets = Path.Combine(nugetDir, "nuget.targets")
        if File.Exists nugetTargets then
            File.Delete(nugetTargets)
            tracefn "Deleted %s" nugetTargets
            VSIntegration.InitAutoRestore()

        if Directory.EnumerateFileSystemEntries(nugetDir) |> Seq.isEmpty 
            then Directory.Delete nugetDir
    | None -> ()

    if installAfter then
        UpdateProcess.Update(Constants.DepsFile,true,false,true)