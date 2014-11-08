module Paket.Settings

open System
open System.IO
open Paket.Logging

let rec internal findDependenciesFileInPath withError (dir:DirectoryInfo) =
    let path = Path.Combine(dir.FullName,Constants.DependenciesFileName)
    if File.Exists(path) then
        path
    else
        let parent = dir.Parent
        if parent = null then
            if withError then
                failwithf "Could not find %s" Constants.DependenciesFileName
            else 
                Constants.DependenciesFileName
        else
           findDependenciesFileInPath withError parent

let mutable DependenciesFile = findDependenciesFileInPath false (DirectoryInfo Environment.CurrentDirectory)

let GetRoot() = FileInfo(DependenciesFile).Directory.FullName