module Paket.DependencyModel

open System.IO
open Paket

/// Calculates the used dependencies for the given direct references.
let CalcDependenciesForDirectPackages(dependenciesFile : DependenciesFile, references) = 
    dependenciesFile.DirectDependencies 
    |> Map.filter (fun name _ -> references |> List.exists (fun (reference : string) -> name.ToLower() = reference.ToLower()))

/// Calculates the used dependencies for given references file.
let CalcDependenciesForReferencesFile(dependenciesFile : DependenciesFile, referencesFile) = 
    CalcDependenciesForDirectPackages(dependenciesFile, (ReferencesFile.FromFile referencesFile).NugetPackages)

/// Calculates the used dependencies for a project.
let CalcDependencies(dependenciesFile : DependenciesFile, projectFileName) = 
    match ProjectFile.FindReferencesFile(FileInfo(projectFileName)) with
    | Some referencesFile -> CalcDependenciesForReferencesFile(dependenciesFile, referencesFile)
    | None -> Map.empty
