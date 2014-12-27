/// Contains methods for the smarter install process.
module Paket.SmartInstallProcess

open Paket
open System.IO
open Paket.Domain
open Paket.PackageResolver
open System.Collections.Generic

/// Smart install command
let SmartInstall(dependenciesFileName, force, hard, withBindingRedirects) = 
    let dependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    
    let lockFile = UpdateProcess.SelectiveUpdate(dependenciesFile,force)
    
    let sources = dependenciesFile.GetAllPackageSources()
    InstallProcess.Install(sources, force, hard, false, lockFile)