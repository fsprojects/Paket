module Paket.VSIntegration

open System.IO
open Logging
open System
open Chessie.ErrorHandling
open Domain
open Releases
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
    let exeDir = Path.Combine(environment.RootDirectory.FullName, Constants.PaketFolderName)

    trial {
        do! TurnOffAutoRestore environment
#if NO_BOOTSTRAPPER
        do! downloadLatestTargets environment 
#else
        do! downloadLatestBootstrapperAndTargets environment 
#endif
        let paketTargetsPath = Path.Combine(exeDir, Constants.TargetsFileName)

#if !NO_BOOTSTRAPPER
        let bootStrapperFileName = Path.Combine(environment.RootDirectory.FullName, Constants.PaketFolderName, Constants.BootstrapperFileName)
        let paketFileName = FileInfo(Path.Combine(environment.RootDirectory.FullName, Constants.PaketFolderName, Constants.PaketFileName))
        try
            if paketFileName.Exists then
                paketFileName.Delete()
            File.Move(bootStrapperFileName,paketFileName.FullName)
        with
        | _ -> ()
#endif

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