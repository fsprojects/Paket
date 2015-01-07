module Paket.FindReferences

open System
open System.IO
open Logging
open Paket.Domain
open Paket.Rop

let private findReferencesFor package (lockFile: LockFile) projects =
    [ for project,referencesFile in projects do
        let installedPackages =
            referencesFile
            |> lockFile.GetPackageHull
            |> Seq.map NormalizedPackageName
            |> Set.ofSeq

        if installedPackages.Contains(NormalizedPackageName package) then
            yield project.FileName ]

let FindReferencesForPackage package environment = rop {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists

    return findReferencesFor package lockFile environment.Projects
}

let ShowReferencesFor packages environment = rop {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists

    packages
    |> Seq.map (fun package -> package, findReferencesFor package lockFile environment.Projects)
    |> Seq.iter (fun (PackageName k, vs) ->
        tracefn "%s" k
        vs |> Seq.iter (tracefn "%s")
        tracefn "")
}