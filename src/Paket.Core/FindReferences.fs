module Paket.FindReferences

open System
open System.IO
open Logging
open Paket.Domain
open Chessie.ErrorHandling

let private findReferencesFor package (lockFile: LockFile) projects = attempt {
    let! referencedIn =
        projects
        |> Seq.map (fun (project : ProjectFile, referencesFile) -> attempt {
            let! installedPackages =
                referencesFile
                |> lockFile.GetPackageHullSafe

            let referenced =
                installedPackages
                |> Set.map NormalizedPackageName
                |> Set.contains (NormalizedPackageName package)

            return if referenced then Some project.FileName else None })
        |> collect

    return referencedIn |> List.choose id
}

let FindReferencesForPackage package environment = attempt {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists

    return! findReferencesFor package lockFile environment.Projects
}

let ShowReferencesFor packages environment = attempt {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists
    let! projectsPerPackage =
        packages
        |> Seq.map (fun package -> attempt {
            let! projects = findReferencesFor package lockFile environment.Projects
            return package, projects })
        |> collect

    projectsPerPackage
    |> Seq.iter (fun (PackageName k, vs) ->
        tracefn "%s" k
        vs |> Seq.iter (tracefn "%s")
        tracefn "")
}