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

let private findReferencesForPackageOld (dependenciesFileName, package:PackageName) =
    let root = Path.GetDirectoryName dependenciesFileName
    let projectFiles = ProjectFile.FindAllProjects root
    let lockFile = LockFile.LoadFrom((DependenciesFile.FindLockfile dependenciesFileName).FullName)

    findReferencesFor package lockFile (InstallProcess.findAllReferencesFiles root |> returnOrFail)

let ShowReferencesFor (dependenciesFileName, packages : PackageName list) =
    packages
    |> Seq.map (fun package -> package,findReferencesForPackageOld(dependenciesFileName,package))
    |> Seq.iter (fun (PackageName k, vs) ->
        tracefn "%s" k
        vs |> Seq.iter (tracefn "%s")        
        tracefn "")