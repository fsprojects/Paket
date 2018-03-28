module Paket.IntegrationTests.SymbolicLinkSpecs

open NUnit.Framework
open Fake
open FsUnit
open Paket

[<Test>]
let ``#3127 symlink enabled on all dependencies and empty paket.lock``() =
    clearPackageAtVersion "NUnit" "2.6.3"
    let scenario = "i003127-storage-symlink"
    let workingDir = scenarioTempPath scenario
    paketEx true "update" scenario |> ignore
    
    workingDir </> "packages" </> "NUnit" 
    |> SymlinkUtils.isDirectoryLink 
    |> shouldEqual true

let paketDependencies workingDir content = 
    let d = DependenciesFile.FromSource(workingDir, content)
    d.Save()
    d

let storageConfig (x:DependenciesFile) = x.Groups.[Domain.GroupName Domain.MainGroup].Options.Settings.StorageConfig 

[<Test>]
let ``#3127 symlink enabled -> disabled on all dependencies on existing paket.lock``() =
    clearPackageAtVersion "NUnit" "2.6.3"
    
    let scenario = "i003127-storage-symlink"
    let workingDir = scenarioTempPath scenario
    
    let packagesDir = workingDir </> "packages"    
    
    paketEx true "install" scenario |> ignore
    
    workingDir </> "paket.dependencies" |> Paket.DependenciesFile.ReadFromFile     
    |> storageConfig
    |> shouldEqual (Some PackagesFolderGroupConfig.SymbolicLink)
    
    packagesDir </> "NUnit" 
    |> SymlinkUtils.isDirectoryLink 
    |> shouldEqual true

    let paketDependenciesWithoutConfig = """source https://www.nuget.org/api/v2
nuget NUnit < 3.0.0"""
    
    paketDependenciesWithoutConfig
    |> paketDependencies workingDir
    |> storageConfig
    |> shouldEqual None

    directPaketEx "update" scenario |> ignore

    packagesDir </> "NUnit" 
    |> SymlinkUtils.isDirectoryLink 
    |> shouldEqual false

[<Test>]
let ``#3127 symlink disabled -> enabled on all dependencies on existing paket.lock``() =
    clearPackageAtVersion "NUnit" "2.6.3"
    
    let scenario = "i003127-storage-symlink"
    prepare scenario
    let workingDir = scenarioTempPath scenario
    let packagesDir = workingDir </> "packages"
    
    let paketDependenciesWithoutConfig = """source https://www.nuget.org/api/v2
nuget NUnit < 3.0.0"""
    
    paketDependenciesWithoutConfig 
    |> paketDependencies workingDir
    |> storageConfig
    |> shouldEqual None

    directPaketEx "install" scenario |> ignore

    packagesDir </> "NUnit" 
    |> SymlinkUtils.isDirectoryLink 
    |> shouldEqual false

    let paketDependenciesWithConfig = """source https://www.nuget.org/api/v2
storage: symlink
nuget NUnit < 3.0.0"""
    
    paketDependenciesWithConfig 
    |> paketDependencies workingDir
    |> storageConfig
    |> shouldEqual (Some PackagesFolderGroupConfig.SymbolicLink)

    directPaketEx "update" scenario |> ignore

    packagesDir </> "NUnit" 
    |> SymlinkUtils.isDirectoryLink 
    |> shouldEqual true
