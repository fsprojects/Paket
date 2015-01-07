module Paket.FindReferences

open System
open System.IO
open Logging
open Paket.Domain
open Paket.Rop

let private findReferencesFor package (lockFile: LockFile) projects = rop {
    let! referencedIn =
        projects
        |> Seq.map (fun (project, referencesFile) -> rop {
            let! installedPackages =
                referencesFile
                |> lockFile.GetPackageHullSafe

            let referenced =
                installedPackages
                |> Set.map NormalizedPackageName
                |> Set.contains (NormalizedPackageName package)

            return if referenced then Some project.FileName else None })
        |> Rop.collect

    return referencedIn |> List.choose id
}

let FindReferencesForPackage package environment = rop {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists

    return! findReferencesFor package lockFile environment.Projects
}

let ShowReferencesFor packages environment = rop {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists
    let! projectsPerPackage =
        packages
        |> Seq.map (fun package -> rop {
            let! projects = findReferencesFor package lockFile environment.Projects
            return package, projects })
        |> Rop.collect

    projectsPerPackage
    |> Seq.iter (fun (PackageName k, vs) ->
        tracefn "%s" k
        vs |> Seq.iter (tracefn "%s")
        tracefn "")
}