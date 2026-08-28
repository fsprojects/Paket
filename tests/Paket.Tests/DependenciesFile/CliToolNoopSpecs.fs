module Paket.DependenciesFile.CliToolNoopSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain

[<Test>]
let ``should detect no changes when clitool unchanged``() =
    let before = """source https://api.nuget.org/v3/index.json

clitool dotnet-fake >= 5.0
nuget FSharp.Core"""

    let lockFileData = """NUGET
  remote: https://api.nuget.org/v3/index.json
    dotnet-fake (5.0) - clitool: true
    FSharp.Core (4.5.4)
"""

    let after = before

    let cfg = DependenciesFile.FromSource(after)
    let lockFile = LockFile.Parse("",toLines lockFileData)
    let changedDependencies = DependencyChangeDetection.findNuGetChangesInDependenciesFile(cfg,lockFile,true)
    changedDependencies.IsEmpty |> shouldEqual true
