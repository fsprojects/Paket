module Paket.Resolver.ConflictSourcesSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.Requirements

let noGitHubConfigured _ = failwith "no GitHub configured"

let config1 = """
source "http://www.nuget.org/api/v2"

github fsharp/fsharp:master foo.fs
github fsprojects/FAKE:master test.fs
"""

[<Test>]
let ``should resolve source files with correct sha``() =
    let name = PackageName "name"
    let dep =
      { Name = name
        ResolverStrategyForDirectDependencies = Some ResolverStrategy.Max 
        ResolverStrategyForTransitives = Some ResolverStrategy.Max
        Graph = Set.empty
        Sources = []
        TransitivePrereleases = false
        Parent = Requirements.PackageRequirementSource.DependenciesFile("",0)
        Settings = InstallSettings.Default
        Kind = PackageRequirementKind.Package
        VersionRequirement = VersionRequirement.NoRestriction }
    let sha = "sha1"
    let cfg = DependenciesFile.FromSource(config1)
    let resolved = ModuleResolver.Resolve((fun _ -> [dep],[]), (fun _ _ _ _ _ -> sha), cfg.Groups.[Constants.MainDependencyGroup].RemoteFiles)
    resolved
    |> shouldContain
      { Owner = "fsharp"
        Project = "fsharp"
        Name = "foo.fs"
        Commit = sha
        Dependencies = [name, VersionRequirement.NoRestriction] |> Set.ofList
        Origin = ModuleResolver.Origin.GitHubLink
        Command = None
        OperatingSystemRestriction = None
        PackagePath = None
        AuthKey = None }

let config2 = """
source "http://www.nuget.org/api/v2"

github fsharp/fsharp:master foo.fs
github fsharp/fsharp:fsharp4 foo.fs
github fsprojects/FAKE:master test.fs
github fsprojects/FAKE:vNext readme.md
"""

let expectedError = """Found conflicting source file requirements:
   - fsharp/fsharpfoo.fs
     Versions:
     - master
     - fsharp4
   Currently multiple versions for same source directory are not supported.
   Please adjust the dependencies file.""" |> normalizeLineEndings

[<Test>]
let ``should fail resolving same source files from same repository but different versions``() =
    try
        let cfg = DependenciesFile.FromSource(config2)
        ModuleResolver.Resolve(noGitHubConfigured, noGitHubConfigured, cfg.Groups.[Constants.MainDependencyGroup].RemoteFiles) |> ignore
    with
    | ex -> ex.Message |> shouldEqual expectedError