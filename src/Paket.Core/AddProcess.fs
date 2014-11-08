/// Contains methods for addition of new packages
module Paket.AddProcess

open Paket
open System.IO

let Add(package, version, force, hard, interactive, installAfter) =
    let exisitingDependenciesFile = DependenciesFile.ReadFromFile(Settings.DependenciesFile)
    let dependenciesFile =
        exisitingDependenciesFile
          .Add(package,version)

    if exisitingDependenciesFile <> dependenciesFile then

        let lockFile = UpdateProcess.updateWithModifiedDependenciesFile(dependenciesFile,package,force)
    
        if interactive then
            for project in ProjectFile.FindAllProjects(".") do
                if Utils.askYesNo(sprintf "  Install to %s?" project.Name) then
                    let proj = FileInfo(project.FileName)
                    match ProjectFile.FindReferencesFile proj with
                    | None ->
                        let newFileName =
                            let fi = FileInfo(Path.Combine(proj.Directory.FullName,Constants.ReferencesFile))
                            if fi.Exists then
                                Path.Combine(proj.Directory.FullName,proj.Name + "." + Constants.ReferencesFile)
                            else
                                fi.FullName

                        File.WriteAllLines(newFileName,[package])
                    | Some fileName -> File.AppendAllLines(fileName,["";package])

        if installAfter then
            let sources =
                Settings.DependenciesFile
                |> File.ReadAllLines
                |> PackageSourceParser.getSources 

            InstallProcess.Install(sources, force, hard, lockFile)

        dependenciesFile.Save()