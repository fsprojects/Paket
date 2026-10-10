module Paket.VSIntegration

open System.IO
open Logging
open System
open Chessie.ErrorHandling
open Domain
open InstallProcess

/// Deactivates the Visual Studio NuGet autorestore feature in all projects
let TurnOffAutoRestore environment = 
    let exeDir = Path.Combine(environment.RootDirectory.FullName, Constants.PaketFolderName)
    
    trial {
        let paketTargetsPath = Path.Combine(exeDir, Constants.TargetsFileName)
        do! removeFile paketTargetsPath

        let projects =
            environment.Projects
            |> List.map fst
        for project in projects do
            let toolsVersion = project.GetToolsVersion()
            if toolsVersion < 15.0 then 
                project.RemoveImportForPaketTargets()
                project.Save false
    }

/// Activates the Visual Studio NuGet autorestore feature in all projects
let TurnOnAutoRestore environment =
    trial {
        do! TurnOffAutoRestore environment
        let paketTargetsPath = RestoreProcess.extractPaketTargets environment.RootDirectory.FullName

        let projects =
            environment.Projects
            |> List.map fst

        for project in projects do
            let toolsVersion = project.GetToolsVersion()
            if toolsVersion < 15.0 then 
                // refreshing project as it can be dirty from call to TurnOffAutoRestore
                let project = ProjectFile.LoadFromFile project.FileName
                let relativePath = createRelativePath project.FileName paketTargetsPath
                project.AddImportForPaketTargets relativePath
                project.Save false
    } 