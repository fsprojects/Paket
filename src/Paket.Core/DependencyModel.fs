module Paket.DependencyModel

open System.IO
open Paket
open Paket.Domain

/// Calculates the used dependencies for the given direct references.
let CalcDependenciesForDirectPackages(dependenciesFile : DependenciesFile, references) = 
    dependenciesFile.DirectDependencies 
    |> Map.filter (fun (NormalizedPackageName name) _ -> references |> List.exists (fun (NormalizedPackageName reference) -> name = reference))

/// Calculates the used dependencies for given references file.
let CalcDependenciesForReferencesFile(dependenciesFile : DependenciesFile, referencesFile) = 
    CalcDependenciesForDirectPackages(
        dependenciesFile, 
        (ReferencesFile.FromFile referencesFile).NugetPackages |> List.map (fun p -> p.Name))

/// Calculates the used dependencies for a project.
let CalcDependencies(dependenciesFile : DependenciesFile, projectFileName) = 
    match ProjectFile.FindReferencesFile(FileInfo(projectFileName)) with
    | Some referencesFile -> CalcDependenciesForReferencesFile(dependenciesFile, referencesFile)
    | None -> Map.empty
