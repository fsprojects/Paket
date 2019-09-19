module Paket.FindReferences

open Logging
open Chessie.ErrorHandling

let private findReferencesFor groupName package (lockFile: LockFile) projects = trial {
    let! referencedIn =
        projects
        |> Seq.map (fun (project : ProjectFile, referencesFile) -> trial {
            let! installedPackages = lockFile.GetPackageHullSafe(referencesFile,groupName)

            let referenced =
                installedPackages
                |> Set.contains package

            return if referenced then Some project else None })
        |> collect

    return referencedIn |> List.choose id
}

let FindReferencesForPackage groupName package environment = trial {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists

    return! findReferencesFor groupName package lockFile environment.Projects
}

let TouchReferencesOfPackages packages environment = trial {
    let! references =
        packages
        |> List.map (fun (group,package) -> FindReferencesForPackage group package environment)
        |> collect

    let projects =
        references
        |> List.collect id
        |> List.distinctBy (fun project-> project.FileName)
    for project in projects do
        if verbose then
            verbosefn "Touching project %s" project.FileName
        project.Save true
}

let ShowReferencesFor packages environment = trial {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists
    let! projectsPerPackage =
        packages
        |> Seq.map (fun (groupName,package) -> trial {
            let! projects = findReferencesFor groupName package lockFile environment.Projects
            return groupName, package, projects })
        |> collect

    for g, k, vs in projectsPerPackage do
        tracefn "%O %O" g k
        for v in vs |> Seq.map (fun p -> p.FileName) do
            tracefn "%s" v
        tracefn ""
}