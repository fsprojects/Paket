module paket.dependenciesFile.VersionRangeSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``can detect minimum version``() = 
    DependenciesFileParser.parseVersionRange ">= 2.2" |> shouldEqual (VersionRange.AtLeast "2.2")
    DependenciesFileParser.parseVersionRange ">= 1.2" |> shouldEqual (VersionRange.AtLeast "1.2")

[<Test>]
let ``can detect specific version``() = 
    DependenciesFileParser.parseVersionRange "2.2" |> shouldEqual (VersionRange.Exactly "2.2")
    DependenciesFileParser.parseVersionRange "1.2" |> shouldEqual (VersionRange.Exactly "1.2")

    DependenciesFileParser.parseVersionRange "= 2.2" |> shouldEqual (VersionRange.Exactly "2.2")
    DependenciesFileParser.parseVersionRange "= 1.2" |> shouldEqual (VersionRange.Exactly "1.2")

[<Test>]
let ``can detect ordinary Between``() = 
    DependenciesFileParser.parseVersionRange "~> 2.2" |> shouldEqual (VersionRange.Between("2.2","3.0"))
    DependenciesFileParser.parseVersionRange "~> 1.2" |> shouldEqual (VersionRange.Between("1.2","2.0"))

[<Test>]
let ``can detect lower versions for ~>``() = 
    DependenciesFileParser.parseVersionRange "~> 3.2.0.0" |> shouldEqual (VersionRange.Between("3.2.0.0","3.2.1.0"))

    DependenciesFileParser.parseVersionRange "~> 1.2.3.4" |> shouldEqual (VersionRange.Between("1.2.3.4","1.2.4.0"))    
    DependenciesFileParser.parseVersionRange "~> 1.2.3" |> shouldEqual (VersionRange.Between("1.2.3","1.3.0"))
    DependenciesFileParser.parseVersionRange "~> 1.2" |> shouldEqual (VersionRange.Between("1.2","2.0"))
    DependenciesFileParser.parseVersionRange "~> 1.0" |> shouldEqual (VersionRange.Between("1.0","2.0"))
    DependenciesFileParser.parseVersionRange "~> 1" |> shouldEqual (VersionRange.Between("1","2"))

[<Test>]
let ``can detect minimum NuGet version``() = 
    Nuget.parseVersionRange "0" |> shouldEqual (DependenciesFileParser.parseVersionRange ">= 0")
    Nuget.parseVersionRange "" |> shouldEqual (DependenciesFileParser.parseVersionRange ">= 0")
    Nuget.parseVersionRange null |> shouldEqual (DependenciesFileParser.parseVersionRange ">= 0")

    DependenciesFileParser.parseVersionRange "" |> shouldEqual (DependenciesFileParser.parseVersionRange ">= 0")
    DependenciesFileParser.parseVersionRange null |> shouldEqual (DependenciesFileParser.parseVersionRange ">= 0")