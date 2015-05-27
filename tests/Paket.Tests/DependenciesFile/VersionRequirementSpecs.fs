module paket.dependenciesFile.VersionRequirementSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.Requirements
open Paket.Domain

let parse text = DependenciesFileParser.parseVersionRequirement(text)

let require packageName strategy text : PackageRequirement = 
    { Name = PackageName packageName
      VersionRequirement = parse text
      ResolverStrategy = strategy
      Parent = PackageRequirementSource.DependenciesFile ""
      Settings = InstallSettings.Default
      Sources = [] }

[<Test>]
let ``can order simple at least requirements``() = 
    parse ">= 2.2" |> shouldBeSmallerThan (parse ">= 2.3")
    parse ">= 1.2" |> shouldBeSmallerThan (parse ">= 2.3")

[<Test>]
let ``can order twiddle-wakka``() = 
    parse "~> 2.2" |> shouldBeSmallerThan (parse "~> 2.3")
    parse "~> 1.2" |> shouldBeSmallerThan (parse "~> 2.3")

[<Test>]
let ``can order simple at least requirements in package requirement``() = 
    require "A" ResolverStrategy.Max ">= 2.2" |> shouldBeGreaterThan (require "A" ResolverStrategy.Max ">= 2.3")
    require "A" ResolverStrategy.Max ">= 1.2" |> shouldBeGreaterThan (require "A" ResolverStrategy.Max ">= 2.3")
    
    require "A" ResolverStrategy.Min ">= 2.2" |> shouldBeGreaterThan (require "A" ResolverStrategy.Min ">= 2.3")
    require "A" ResolverStrategy.Min ">= 1.2" |> shouldBeGreaterThan (require "A" ResolverStrategy.Min ">= 2.3")

[<Test>]
let ``can order alphabetical if everything else is equal``() = 
    require "B" ResolverStrategy.Max ">= 2.2" |> shouldBeGreaterThan (require "A" ResolverStrategy.Max ">= 2.2")
    require "B" ResolverStrategy.Max ">= 1.2" |> shouldBeGreaterThan (require "A" ResolverStrategy.Max ">= 1.2")
    
    require "C" ResolverStrategy.Min ">= 2.2" |> shouldBeGreaterThan (require "A" ResolverStrategy.Min ">= 2.2")
    require "C" ResolverStrategy.Min ">= 1.2" |> shouldBeGreaterThan (require "A" ResolverStrategy.Min ">= 1.2")

[<Test>]
let ``can order twiddle-wakka in package requirement``() = 
    require "A" ResolverStrategy.Max "~> 2.2" |> shouldBeGreaterThan (require "A" ResolverStrategy.Max "~> 2.3")
    require "A" ResolverStrategy.Max "~> 1.2" |> shouldBeGreaterThan (require "A" ResolverStrategy.Max "~> 2.3")
    
    require "A" ResolverStrategy.Min "~> 2.2" |> shouldBeGreaterThan (require "A" ResolverStrategy.Min "~> 2.3")
    require "A" ResolverStrategy.Min "~> 1.2" |> shouldBeGreaterThan (require "A" ResolverStrategy.Min "~> 2.3")

[<Test>]
let ``can order resolver strategy``() = 
    require "A" ResolverStrategy.Min "~> 2.2" |> shouldBeSmallerThan (require "A" ResolverStrategy.Max "~> 2.3")
    require "A" ResolverStrategy.Min "~> 2.2" |> shouldBeSmallerThan (require "A" ResolverStrategy.Max "~> 2.1")
    require "A" ResolverStrategy.Min "~> 2.4" |> shouldBeSmallerThan (require "A" ResolverStrategy.Max "~> 1.3")

[<Test>]
let ``can order naming and range``() = 
    require "Castle.Windsor-NLog" ResolverStrategy.Min ">= 0" |> shouldBeGreaterThan (require "Nancy.Bootstrappers.Windsor" ResolverStrategy.Min "~> 0.23")