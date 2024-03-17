module Paket.FindReferences

open Logging
open FsToolkit.ErrorHandling

let private findReferencesFor groupName package (lockFile: LockFile) projects = validation {
    let! referencedIn =
        projects
        |> List.map (fun (project : ProjectFile, referencesFile) -> result {
            let! installedPackages = lockFile.GetPackageHullSafe(referencesFile,groupName)

            let referenced =
                installedPackages
                |> Set.contains package

            return if referenced then Some project else None })
        |> List.sequenceResultA

    return referencedIn |> List.choose id
}

let FindReferencesForPackage groupName package environment = validation {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists
    return! findReferencesFor groupName package lockFile environment.Projects
}

let TouchReferencesOfPackages packages environment = result {
    let! references =
        packages
        |> List.map (fun (group,package) -> FindReferencesForPackage group package environment)
        |> List.sequenceResultA

    let projects =
        references
        |> List.collect id
        |> List.distinctBy (fun project-> project.FileName)
    for project in projects do
        if verbose then
            verbosefn "Touching project %s" project.FileName
        project.Save true
}

let ShowReferencesFor packages environment = validation {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists
    let! projectsPerPackage =
        packages
        |> List.map (fun (groupName,package) -> result {
            let! projects = findReferencesFor groupName package lockFile environment.Projects
            return groupName, package, projects })
        |> List.sequenceResultA
        |> Result.mapError List.concat

    for g, k, vs in projectsPerPackage do
        tracefn "%O %O" g k
        for v in vs |> Seq.map (fun p -> p.FileName) do
            tracefn "%s" v
        tracefn ""
}