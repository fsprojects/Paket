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
    let exeDir = Path.Combine(environment.RootDirectory.FullName, Constants.PaketFolderName)

    trial {
        do! downloadLatestBootstrapperAndTargets environment
        let paketTargetsPath = Path.Combine(exeDir, Constants.TargetsFileName)

        environment.Projects
        |> List.map fst
        |> List.iter (fun project ->
            let relativePath = createRelativePath project.FileName paketTargetsPath
            project.AddImportForPaketTargets(relativePath)
            project.Save(false)
        )
    } 

/// Deactivates the Visual Studio Nuget autorestore feature in all projects
let TurnOffAutoRestore environment = 
    let exeDir = Path.Combine(environment.RootDirectory.FullName, Constants.PaketFolderName)
    
    trial {
        let paketTargetsPath = Path.Combine(exeDir, Constants.TargetsFileName)
        do! removeFile paketTargetsPath

        environment.Projects
        |> List.map fst
        |> List.iter (fun project ->
            let relativePath = createRelativePath project.FileName paketTargetsPath
            project.RemoveImportForPaketTargets(relativePath)
            project.Save(false)
        )
    }
