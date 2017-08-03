module Paket.DependencyModel

open System.IO
open Pri.LongPath
open Paket
open Paket.Domain

/// Calculates the used dependencies for the given direct references.
let CalcDependenciesForDirectPackages(dependenciesFile : DependenciesFile, groupName, references) = 
    dependenciesFile.GetDependenciesInGroup(groupName) 
    |> Map.filter (fun name _ -> references |> List.exists (fun reference -> name = reference))

/// Calculates the used dependencies for given references file.
let CalcDependenciesForReferencesFile(dependenciesFile : DependenciesFile, groupName, referencesFile) = 
    CalcDependenciesForDirectPackages(
        dependenciesFile,
        groupName,
        (ReferencesFile.FromFile referencesFile).Groups.[groupName].NugetPackages |> List.map (fun p -> p.Name))

/// Calculates the used dependencies for a project.
let CalcDependencies(dependenciesFile : DependenciesFile, groupName, projectFileName) = 
    match ProjectFile.FindReferencesFile(FileInfo(projectFileName)) with
    | Some referencesFile -> CalcDependenciesForReferencesFile(dependenciesFile, groupName, referencesFile)
    | None -> Map.empty
