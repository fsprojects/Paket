module Paket.DependencyModel

/// Calculates the used dependencies for a project.
let CalcDependencies(dependenciesFile:DependenciesFile,references) = 
    dependenciesFile.DirectDependencies
    |> Map.filter (fun name _ -> references |> List.exists (fun (reference:string) -> name.ToLower() = reference.ToLower()))