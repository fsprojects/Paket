module Paket.FindReferences

open System
open System.IO
open Logging
open Paket.Domain

let FindReferencesForPackage (dependenciesFileName, package:PackageName) =
    let root = Path.GetDirectoryName dependenciesFileName
    let projectFiles = ProjectFile.FindAllProjects root
    let lockFile = LockFile.LoadFrom((DependenciesFile.FindLockfile dependenciesFileName).FullName)

    [for project in ProjectFile.FindAllProjects root do
        match ProjectFile.FindReferencesFile(FileInfo(project.FileName)) with
        | None -> ()
        | Some referencesFile ->
            let installedPackages = 
                referencesFile
                |> ReferencesFile.FromFile
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