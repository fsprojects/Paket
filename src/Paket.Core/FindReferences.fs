module Paket.FindReferences

open System
open System.IO
open Logging
open Paket.Domain
open Paket.Rop

let FindReferencesForPackage (dependenciesFileName, package:PackageName) =
    let root = Path.GetDirectoryName dependenciesFileName
    let projectFiles = ProjectFile.FindAllProjects root
    let lockFile = LockFile.LoadFrom((DependenciesFile.FindLockfile dependenciesFileName).FullName)

    [for project,referencesFile in InstallProcess.findAllReferencesFiles root |> returnOrFail do
        let installedPackages = 
            referencesFile
            |> lockFile.GetPackageHull
            |> Seq.map NormalizedPackageName
            |> Set.ofSeq

        if installedPackages.Contains(NormalizedPackageName package) then
            yield project.FileName ]

let ShowReferencesFor (dependenciesFileName, packages : PackageName list) =
    packages
    |> Seq.map (fun package -> package,FindReferencesForPackage(dependenciesFileName,package))
    |> Seq.iter (fun (PackageName k, vs) ->
        tracefn "%s" k
        vs |> Seq.iter (tracefn "%s")        
        tracefn "")