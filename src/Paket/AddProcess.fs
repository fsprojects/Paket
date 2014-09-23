/// Contains methods for addition of new packages
module Paket.AddProcess

open Paket

let Add(package, version, force, hard, installAfter, dependenciesFileName) = 
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName).Add(package,version)
    if installAfter then
        UpdateProcess.Update(Constants.DependenciesFile,true,force,hard)