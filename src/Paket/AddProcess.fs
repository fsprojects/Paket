/// Contains methods for addition of new packages
module Paket.AddProcess

open Paket
open System

let Add(package, version, force, hard, interactive, installAfter, dependenciesFileName) =
    let dependenciesFile =
        DependenciesFile.ReadFromFile(dependenciesFileName)
          .Add(package,version)

    let resolution = dependenciesFile.Resolve force |> UpdateProcess.getResolvedPackagesOrFail

    if interactive then
        for proj in ProjectFile.FindAllProjects(".") do
            if Utils.askYesNo(sprintf "  Install to %s"  proj.FullName) then
               ()
            

    if installAfter then
        let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    
        let lockFile =                
            let lockFile = LockFile(lockFileName.FullName, dependenciesFile.Strict, resolution, dependenciesFile.RemoteFiles)
            lockFile.Save()
            lockFile
        InstallProcess.Install(force, hard, lockFile)

    dependenciesFile.Save()