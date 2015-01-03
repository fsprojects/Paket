/// Contains methods for addition of new packages
module Paket.AddProcess

open Paket
open System.IO
open Paket.Domain

let install(dependenciesFile: DependenciesFile, force, hard, withBindingRedirects, lockFile) =
    let sources = dependenciesFile.GetAllPackageSources()
    InstallProcess.Install(sources, force, hard, withBindingRedirects, lockFile)

let Add(dependenciesFileName, package, version, force, hard, interactive, installAfter) =
    let existingDependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    let dependenciesFile =
        existingDependenciesFile
          .Add(package,version)

    dependenciesFile.Save()

    let lockFile = UpdateProcess.SelectiveUpdate(dependenciesFile,force)
    
    if interactive then
        for project in ProjectFile.FindAllProjects(Path.GetDirectoryName lockFile.FileName) do
            if Utils.askYesNo(sprintf "  Install to %s?" project.Name) then
                ProjectFile.FindOrCreateReferencesFile(FileInfo(project.FileName))
                    .AddNuGetReference(package)
                    .Save()

    if installAfter then
        install(dependenciesFile, force, hard, false, lockFile)

//let AddUrl(dependenciesFileName, url, fileName, force, hard, interactive, installAfter) =
//
//    if installAfter then
//        install(dependenciesFile, force, hard, false, lockFile)
