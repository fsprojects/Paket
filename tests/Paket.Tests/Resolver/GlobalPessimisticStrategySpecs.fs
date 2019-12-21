module Paket.Resolver.GlobalPessimisticStrategySpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.PackageResolver

let resolve graph updateMode (cfg : DependenciesFile) =
    let groups = [Constants.MainDependencyGroup, None ] |> Map.ofSeq
    cfg.Resolve(true,noSha1,VersionsFromGraphAsSeq graph, (fun _ _ -> []),PackageDetailsFromGraph graph,(fun _ _ _ -> None),groups,updateMode).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()

let graph = 
  OfSimpleGraph [
    "Nancy.Bootstrappers.Windsor","0.23",["Castle.Windsor",VersionRequirement(VersionRange.AtLeast "3.2.1",PreReleaseStatus.No)]
    "Castle.Windsor","3.2.1",[]
    "Castle.Windsor","3.3.0",[]
  ]

let config1 = """
strategy min
source http://www.nuget.org/api/v2

nuget Nancy.Bootstrappers.Windsor ~> 0.23
"""

[<Test>]
let ``should resolve simple config1``() = 
    let resolved =
        DependenciesFile.FromSource(config1)
        |> resolve graph UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.2.1"
    getVersion resolved.[PackageName "Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"

let config2 = """
strategy min
source http://www.nuget.org/api/v2

nuget Castle.Windsor
nuget Nancy.Bootstrappers.Windsor ~> 0.23
"""

[<Test>]
let ``should resolve simple config2``() = 
    let resolved =
        DependenciesFile.FromSource(config2)
        |> resolve graph UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.2.1"
    getVersion resolved.[PackageName "Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"


let config3 = """
strategy min
source http://www.nuget.org/api/v2

nuget Nancy.Bootstrappers.Windsor ~> 0.23
nuget Castle.Windsor
"""

[<Test>]
let ``should resolve simple config3``() = 
    let resolved =
        DependenciesFile.FromSource(config3)
        |> resolve graph UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.2.1"
    getVersion resolved.[PackageName "Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"


let graph2 = 
  OfSimpleGraph [
    "Nancy.Bootstrappers.Windsor","0.23",["Castle.Windsor",VersionRequirement(VersionRange.AtLeast "3.2.1",PreReleaseStatus.No)]
    "Castle.Windsor","3.2.0",["Castle.Core",VersionRequirement(VersionRange.AtLeast "3.2.0",PreReleaseStatus.No)]
    "Castle.Windsor","3.2.1",["Castle.Core",VersionRequirement(VersionRange.AtLeast "3.2.0",PreReleaseStatus.No)]
    "Castle.Windsor","3.3.0",["Castle.Core",VersionRequirement(VersionRange.AtLeast "3.3.0",PreReleaseStatus.No)]
    "Castle.Windsor-NLog","3.2.0.1",["Castle.Core-NLog",VersionRequirement(VersionRange.AtLeast "3.2.0",PreReleaseStatus.No)]
    "Castle.Windsor-NLog","3.3.0",["Castle.Core-NLog",VersionRequirement(VersionRange.AtLeast "3.3.0",PreReleaseStatus.No)]
    "Castle.Core-NLog","3.2.0",["Castle.Core",VersionRequirement(VersionRange.AtLeast "3.2.0",PreReleaseStatus.No)]
    "Castle.Core-NLog","3.3.0",["Castle.Core",VersionRequirement(VersionRange.AtLeast "3.3.0",PreReleaseStatus.No)]
    "Castle.Core-NLog","3.3.1",["Castle.Core",VersionRequirement(VersionRange.AtLeast "3.3.1",PreReleaseStatus.No)]
    "Castle.Core","3.2.0",[]
    "Castle.Core","3.2.1",[]
    "Castle.Core","3.2.2",[]
    "Castle.Core","3.3.0",[]
    "Castle.Core","3.3.1",[]
  ]

let config4 = """
strategy min
source http://www.nuget.org/api/v2

nuget Castle.Windsor-NLog
nuget Nancy.Bootstrappers.Windsor ~> 0.23
"""

[<Test>]
let ``should resolve simple config4``() = 
    let resolved =
        DependenciesFile.FromSource(config4)
        |> resolve graph2 UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.2.1"
    getVersion resolved.[PackageName "Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"

let config5 = """
strategy min
source http://www.nuget.org/api/v2

nuget Nancy.Bootstrappers.Windsor @~> 0.23
"""

[<Test>]
let ``should override global strategy``() = 
    let resolved =
        DependenciesFile.FromSource(config5)
        |> resolve graph2 UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.3.0"
    getVersion resolved.[PackageName "Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"

let config6 = """
strategy min
source http://www.nuget.org/api/v2

nuget Castle.Core-NLog @~> 3.2
nuget Castle.Windsor-NLog !~> 3.2
"""

[<Test>]
let ``should favor strategy override when updating all``() = 
    let resolved =
        DependenciesFile.FromSource(config6)
        |> resolve graph2 UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor-NLog"] |> shouldEqual "3.3.0"
    getVersion resolved.[PackageName "Castle.Core-NLog"] |> shouldEqual "3.3.1"
    getVersion resolved.[PackageName "Castle.Core"] |> shouldEqual "3.3.1"

[<Test>]
let ``should respect overrides when updating single package``() = 
    let resolved =
        DependenciesFile.FromSource(config6)
        |> resolve graph2 (UpdateMode.UpdateFiltered(Constants.MainDependencyGroup, PackageName "Castle.Windsor-NLog" |> PackageFilter.ofName))
    getVersion resolved.[PackageName "Castle.Windsor-NLog"] |> shouldEqual "3.3.0"
    getVersion resolved.[PackageName "Castle.Core-NLog"] |> shouldEqual "3.3.1"
    getVersion resolved.[PackageName "Castle.Core"] |> shouldEqual "3.3.1"

[<Test>]
let ``should favor strategy from parent when it overrides``() = 
    let config = """
    strategy min
    source http://www.nuget.org/api/v2

    nuget Nancy.Bootstrappers.Windsor @~> 0.23
    nuget Castle.Windsor !> 0
    nuget Castle.Windsor-NLog @> 0
    """

    let resolved =
        DependenciesFile.FromSource(config)
        |> resolve graph2 UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.3.0"
    getVersion resolved.[PackageName "Castle.Windsor-NLog"] |> shouldEqual "3.3.0"
    getVersion resolved.[PackageName "Castle.Core-NLog"] |> shouldEqual "3.3.0"
    getVersion resolved.[PackageName "Castle.Core"] |> shouldEqual "3.3.0"
    getVersion resolved.[PackageName "Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"

[<Test>]
let ``should favor strategy from parent that overrides strategy``() = 
    let config = """
    strategy min
    source http://www.nuget.org/api/v2

    nuget Nancy.Bootstrappers.Windsor @~> 0.23
    nuget Castle.Windsor
    nuget Castle.Windsor-NLog @> 0
    """

    let resolved =
        DependenciesFile.FromSource(config)
        |> resolve graph2 UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.3.0"
    getVersion resolved.[PackageName "Castle.Windsor-NLog"] |> shouldEqual "3.3.0"
    getVersion resolved.[PackageName "Castle.Core-NLog"] |> shouldEqual "3.3.1"
    getVersion resolved.[PackageName "Castle.Core"] |> shouldEqual "3.3.1"
    getVersion resolved.[PackageName "Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"

let config8 = """
strategy min
source http://www.nuget.org/api/v2

nuget Nancy.Bootstrappers.Windsor @~> 0.23
nuget Castle.Windsor-NLog @> 0
"""

[<Test>]
let ``should favor strategy from top-level dependencies``() = 
    let resolved =
        DependenciesFile.FromSource(config8)
        |> resolve graph2 UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.3.0"
    getVersion resolved.[PackageName "Castle.Windsor-NLog"] |> shouldEqual "3.3.0"
    getVersion resolved.[PackageName "Castle.Core-NLog"] |> shouldEqual "3.3.1"
    getVersion resolved.[PackageName "Castle.Core"] |> shouldEqual "3.3.1"
    getVersion resolved.[PackageName "Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"

let config9 = """
strategy min
source http://www.nuget.org/api/v2

nuget Castle.Windsor @= 3.2.0
nuget Castle.Core-NLog != 3.2.0
"""

[<Test>]
let ``should favor global strategy to resolve strategy override conflicts``() = 
    let resolved =
        DependenciesFile.FromSource(config9)
        |> resolve graph2 UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.2.0"
    getVersion resolved.[PackageName "Castle.Core-NLog"] |> shouldEqual "3.2.0"
    getVersion resolved.[PackageName "Castle.Core"] |> shouldEqual "3.2.0"
