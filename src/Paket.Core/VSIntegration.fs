module Paket.VSIntegration

open System.IO
open Logging
open System
open Chessie.ErrorHandling
open Domain
open Releases
open InstallProcess

/// Activates the Visual Studio Nuget autorestore feature in all projects
let TurnOnAutoRestore environment =
    let exeDir = Path.Combine(environment.RootDirectory.FullName, ".paket")

    trial {
        do! downloadLatestBootstrapperAndTargets environment
        let paketTargetsPath = Path.Combine(exeDir, "paket.targets")

        environment.Projects
        |> List.map fst
        |> List.iter (fun project ->
            match project with
            | ProjectType.Project project -> 
                let relativePath = createRelativePath project.FileName paketTargetsPath
                project.AddImportForPaketTargets(relativePath)
                project.Save()
            | _ -> ()
        )
    } 

/// Deactivates the Visual Studio Nuget autorestore feature in all projects
let TurnOffAutoRestore environment = 
    let exeDir = Path.Combine(environment.RootDirectory.FullName, ".paket")
    
    trial {
        let paketTargetsPath = Path.Combine(exeDir, "paket.targets")
        do! removeFile paketTargetsPath

        environment.Projects
        |> List.map fst
        |> List.iter (fun project ->
            match project with
            | ProjectType.Project project -> 
                let relativePath = createRelativePath project.FileName paketTargetsPath
                project.RemoveImportForPaketTargets(relativePath)
                project.Save()
            | _ -> ()
        )
    }