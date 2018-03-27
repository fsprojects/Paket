module StorageSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``should configure the symlink option``() = 
    let dependencies = """framework: >= net40
storage: symlink
source https://www.nuget.org/api/v2

nuget NLog framework: net40
nuget NLog.Contrib"""

    let cfg = DependenciesFile.FromSource(dependencies)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.StorageConfig |> shouldEqual (Some PackagesFolderGroupConfig.SymbolicLink)