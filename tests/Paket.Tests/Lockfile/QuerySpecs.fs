module Paket.LockFile.QuerySpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain


let isDependencyOf(lockFile:LockFile,dependentPackage,(group,package)) =
    lockFile.GetAllDependenciesOf((group,package,"test")).Contains dependentPackage

let data = """NUGET
  remote: https://www.nuget.org/api/v2
  specs:
    Castle.Windsor (2.1)
    Castle.Windsor-log4net (3.3)
      Castle.Windsor (>= 2.0)
      log4net (>= 1.0)
    Rx-Core (2.1)
    Rx-Main (2.0)
      Rx-Core (>= 2.1)
    log (1.2)
    log4net (1.1)
      log (>= 1.0)
GITHUB
  remote: fsharp/FAKE
  specs:
    src/app/FAKE/Cli.fs (7699e40e335f3cc54ab382a8969253fecc1e08a9)
    src/app/Fake.Deploy.Lib/FakeDeployAgentHelper.fs (Globbing)
"""

let lockFile = LockFile.Parse("Test",toLines data)


[<Test>]
let ``should detect itself as dependency``() = 
    isDependencyOf(lockFile,PackageName "Rx-Core",(Constants.MainDependencyGroup,PackageName "Rx-Core"))
    |> shouldEqual true

[<Test>]
let ``should detect direct dependencies``() = 
    isDependencyOf(lockFile,PackageName "Rx-Core",(Constants.MainDependencyGroup,PackageName "Rx-Main"))
    |> shouldEqual true

[<Test>]
let ``should detect transitive dependencies``() = 
    isDependencyOf(lockFile,PackageName "log",(Constants.MainDependencyGroup,PackageName "Castle.Windsor-log4net"))
    |> shouldEqual true
    
[<Test>]
let ``should detect when packages are unrelated``() = 
    isDependencyOf(lockFile,PackageName "log",(Constants.MainDependencyGroup,PackageName "Rx-Core"))
    |> shouldEqual false