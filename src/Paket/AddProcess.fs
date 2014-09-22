/// Contains methods for addition of new packages
module Paket.AddProcess

open Paket
open Paket.Logging
open System.IO
open System.Collections.Generic

let Add(package, version, force, hard, dependenciesFileName) = 
    let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
    ()