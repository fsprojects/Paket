module Paket.FindReferences

open System
open System.IO
open Logging
open Paket.Domain
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

let ShowReferencesFor groupName packages environment = trial {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists
    let! projectsPerPackage =
        packages
        |> Seq.map (fun package -> trial {
            let! projects = findReferencesFor groupName package lockFile environment.Projects
            return package, projects })
        |> collect

    projectsPerPackage
    |> Seq.iter (fun (PackageName k, vs) ->
        tracefn "%s" k
        vs |> Seq.map (fun p -> p.FileName) |> Seq.iter (tracefn "%s")
        tracefn "")
}