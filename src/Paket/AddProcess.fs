/// Contains methods for addition of new packages
module Paket.AddProcess

open Paket
open System.IO

let Add(package, version, force, hard, interactive, installAfter, dependenciesFileName) =
    let dependenciesFile =
        DependenciesFile.ReadFromFile(dependenciesFileName)
          .Add(package,version)

    let resolution = dependenciesFile.Resolve force 
    let resolvedPackages = UpdateProcess.getResolvedPackagesOrFail resolution

    if interactive then
        let di = DirectoryInfo(".")
        for proj in ProjectFile.FindAllProjects(".") do
            if Utils.askYesNo(sprintf "  Install to %s?" (proj.FullName.Replace(di.FullName,""))) then
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
        let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    
        let lockFile =                
            let lockFile = LockFile(lockFileName.FullName, dependenciesFile.Strict, resolvedPackages, resolution.ResolvedSourceFiles)
            lockFile.Save()
            lockFile
        InstallProcess.Install(force, hard, lockFile)

    dependenciesFile.Save()