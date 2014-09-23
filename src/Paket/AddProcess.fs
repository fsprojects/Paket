/// Contains methods for addition of new packages
module Paket.AddProcess

open Paket

let Add(package, version, force, hard, installAfter, dependenciesFileName) = 
    DependenciesFile.ReadFromFile(dependenciesFileName)
      .Add(package,version)
      .Save()

    if installAfter then
        UpdateProcess.Update(Constants.DependenciesFile,true,force,hard)