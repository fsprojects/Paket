module Paket.FindReferences

open System
open System.IO
open Logging

let FindReferencesForPackage (dependenciesFileName, package:string) =
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
                    |> fun x -> x.Keys
                    |> Seq.map (fun x -> x.ToLower())
                    |> Set.ofSeq

                if installedPackages.Contains(package.ToLower()) then
                    yield project.FileName ]

let ShowReferencesFor (dependenciesFileName, packages : string list) =
    packages
    |> Seq.map (fun package -> package,FindReferencesForPackage(dependenciesFileName,package))
    |> Seq.iter (fun (k, vs) ->
        tracefn "%s" k
        vs |> Seq.iter (tracefn "%s")        
        tracefn "")