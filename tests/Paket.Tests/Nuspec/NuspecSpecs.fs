module Paket.NuspecSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.Requirements
open Domain

[<Test>]
let ``can detect explicit references``() = 
    Nuspec.Load("Nuspec/FSharp.Data.nuspec").References
    |> shouldEqual (NuspecReferences.Explicit ["FSharp.Data.dll"])

[<Test>]
let ``can detect explicit in self made nuspec``() = 
    Nuspec.Load("Nuspec/FSharp.Data.Prerelease.nuspec").References
    |> shouldEqual (NuspecReferences.Explicit ["FSharp.Data.dll"])

[<Test>]
let ``can detect all references``() = 
    Nuspec.Load("Nuspec/Octokit.nuspec").References
    |> shouldEqual NuspecReferences.All

[<Test>]
let ``can detect all references for FsXaml``() = 
    Nuspec.Load("Nuspec/FsXaml.Wpf.nuspec").References
    |> shouldEqual NuspecReferences.All

[<Test>]
let ``can detect all references for ReadOnlyCollectionExtions``() = 
    Nuspec.Load("Nuspec/ReadOnlyCollectionExtensions.nuspec").References
    |> shouldEqual NuspecReferences.All

[<Test>]
let ``can detect developmentDependency is false for ReadOnlyCollectionExtions``() = 
    Nuspec.Load("Nuspec/ReadOnlyCollectionExtensions.nuspec").IsDevelopmentDependency
    |> shouldEqual false

[<Test>]
let ``can detect developmentDependency for LiteGuard.Source``() = 
    Nuspec.Load("Nuspec/LiteGuard.Source.nuspec").IsDevelopmentDependency
    |> shouldEqual true

[<Test>]
let ``can detect all references for log4net``() = 
    Nuspec.Load("Nuspec/log4net.nuspec").References
    |> shouldEqual NuspecReferences.All

[<Test>]
let ``if nuspec is not found we assume all references``() = 
    Nuspec.Load("Nuspec/blablub.nuspec").References
    |> shouldEqual NuspecReferences.All

[<Test>]
let ``can detect explicit references for Fantomas``() = 
    Nuspec.Load("Nuspec/Fantomas.nuspec").References
    |> shouldEqual (NuspecReferences.Explicit ["FantomasLib.dll"])

[<Test>]
let ``can detect no framework assemblies for Fantomas``() = 
    Nuspec.Load("Nuspec/Fantomas.nuspec").FrameworkAssemblyReferences
    |> shouldEqual []

[<Test>]
let ``if nuspec is not found we assume no framework references``() = 
    Nuspec.Load("Nuspec/blablub.nuspec").FrameworkAssemblyReferences
    |> shouldEqual []

[<Test>]
let ``can detect framework assemblies for Microsoft.Net.Http``() = 
    Nuspec.Load("Nuspec/Microsoft.Net.Http.nuspec").FrameworkAssemblyReferences
    |> shouldEqual 
        [{ AssemblyName = "System.Net.Http"
           FrameworkRestrictions = 
             [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_5))
              FrameworkRestriction.Exactly(MonoTouch)
              FrameworkRestriction.Exactly(MonoAndroid)] }
         { AssemblyName = "System.Net.Http.WebRequest"
           FrameworkRestrictions = 
             [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_5))] }]

[<Test>]
let ``can detect framework assemblies for FluentAssertions``() = 
    Nuspec.Load("Nuspec/FluentAssertions.nuspec").FrameworkAssemblyReferences
    |> shouldEqual 
        [{ AssemblyName = "System.Xml"; FrameworkRestrictions = [] }
         { AssemblyName = "System.Xml.Linq"; FrameworkRestrictions = [] } ]

[<Test>]
let ``can detect framework assemblies for SqlCLient``() = 
    Nuspec.Load("Nuspec/FSharp.Data.SqlClient.nuspec").FrameworkAssemblyReferences
    |> shouldEqual 
        [{ AssemblyName = "System.Data"; FrameworkRestrictions = [] }
         { AssemblyName = "System.Xml"; FrameworkRestrictions = [] } ]

[<Test>]
let ``can detect license for SqlCLient``() = 
    Nuspec.Load("Nuspec/FSharp.Data.SqlClient.nuspec").LicenseUrl
    |> shouldEqual "http://github.com/fsprojects/FSharp.Data.SqlClient/blob/master/LICENSE.md"

[<Test>]
let ``can detect dependencies for SqlCLient``() = 
    Nuspec.Load("Nuspec/FSharp.Data.SqlClient.nuspec").Dependencies
    |> shouldEqual 
        [PackageName "Microsoft.SqlServer.Types",DependenciesFileParser.parseVersionRequirement(">= 11.0.0"), []]

[<Test>]
let ``can detect reference files for SqlCLient``() = 
    Nuspec.Load("Nuspec/FSharp.Data.SqlClient.nuspec").References
    |> shouldEqual (NuspecReferences.Explicit ["FSharp.Data.SqlClient.dll"])

[<Test>]
let ``can detect framework assemblies for Octokit``() = 
    Nuspec.Load("Nuspec/Octokit.nuspec").FrameworkAssemblyReferences
    |> shouldEqual 
        [{ AssemblyName = "System.Net.Http"
           FrameworkRestrictions = 
            [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_5))
             FrameworkRestriction.Exactly(Windows "v4.5")] }]

[<Test>]
let ``can detect framework assemblies for FSharp.Data.SqlEnumProvider``() = 
    Nuspec.Load("Nuspec/FSharp.Data.SqlEnumProvider.nuspec").FrameworkAssemblyReferences
    |> shouldEqual 
        [{ AssemblyName = "System.Data"; FrameworkRestrictions = [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_Client))] }
         { AssemblyName = "System.Xml"; FrameworkRestrictions = [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_Client))] }]

[<Test>]
let ``can detect empty framework assemblies for ReadOnlyCollectionExtensions``() = 
    Nuspec.Load("Nuspec/ReadOnlyCollectionExtensions.nuspec").FrameworkAssemblyReferences
    |> shouldEqual [ ]

[<Test>]
let ``can detect empty dependencies for log4net``() = 
    Nuspec.Load("Nuspec/log4net.nuspec").Dependencies
    |> shouldEqual []

[<Test>]
let ``can detect explicit dependencies for Fantomas``() = 
    Nuspec.Load("Nuspec/Fantomas.nuspec").Dependencies
    |> shouldEqual [PackageName "FSharp.Compiler.Service",DependenciesFileParser.parseVersionRequirement(">= 0.0.57"), []]

[<Test>]
let ``can detect explicit dependencies for ReadOnlyCollectionExtensions``() = 
    Nuspec.Load("Nuspec/ReadOnlyCollectionExtensions.nuspec").Dependencies
    |> shouldEqual 
        [PackageName "LinqBridge",DependenciesFileParser.parseVersionRequirement(">= 1.3.0"), 
            [FrameworkRestriction.Between(DotNetFramework(FrameworkVersion.V2),DotNetFramework(FrameworkVersion.V3_5))]
         PackageName "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"),
            [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V2))
             FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V3_5))
             FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4_Client))]]

[<Test>]
let ``can detect framework assemblies for MathNet.Numerics``() = 
    Nuspec.Load("Nuspec/MathNet.Numerics.nuspec").FrameworkAssemblyReferences
    |> shouldEqual 
        [{ AssemblyName = "System.Numerics"
           FrameworkRestrictions = 
            [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_Client))
             FrameworkRestriction.Exactly(Windows("v4.5"))
             FrameworkRestriction.Exactly(Silverlight("v5.0"))
             FrameworkRestriction.Exactly(MonoAndroid)
             FrameworkRestriction.Exactly(MonoTouch)] }]


[<Test>]
let ``can detect dependencies for MathNet.Numerics``() = 
    Nuspec.Load("Nuspec/MathNet.Numerics.nuspec").Dependencies
    |> shouldEqual 
        [ PackageName "TaskParallelLibrary",
          DependenciesFileParser.parseVersionRequirement(">= 1.0.2856.0"),
            [FrameworkRestriction.Between(
                DotNetFramework(FrameworkVersion.V3_5),
                DotNetFramework(FrameworkVersion.V4_Client))] ]

[<Test>]
let ``can detect dependencies for MathNet.Numerics.FSharp``() = 
    Nuspec.Load("Nuspec/MathNet.Numerics.FSharp.nuspec").Dependencies
    |> Seq.head
    |> shouldEqual 
        (PackageName "MathNet.Numerics",
         DependenciesFileParser.parseVersionRequirement("3.3.0"),[])

[<Test>]
let ``can detect explicit dependencies for WindowsAzure.Storage``() = 
    Nuspec.Load("Nuspec/WindowsAzure.Storage.nuspec").Dependencies
    |> Seq.skip 1
    |> Seq.head
    |> shouldEqual 
        (PackageName "Newtonsoft.Json",
          DependenciesFileParser.parseVersionRequirement(">= 5.0.8"),
          [FrameworkRestriction.Exactly(WindowsPhoneSilverlight("v8.0"))
           FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4_Client))])

[<Test>]
let ``can detect framework assemblies for Microsoft.Framework.Logging``() = 
    let nuspec = Nuspec.Load("Nuspec/Microsoft.Framework.Logging.nuspec")
    nuspec.FrameworkAssemblyReferences.[0].AssemblyName |> shouldEqual "System.Collections.Concurrent"
    nuspec.FrameworkAssemblyReferences.[0].FrameworkRestrictions 
        |> shouldEqual         
            [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_5))
             FrameworkRestriction.Exactly(DNX(FrameworkVersion.V4_5_1))]

    let name,_,restrictions = nuspec.Dependencies.[0]
    name  |> shouldEqual (PackageName "Microsoft.Framework.DependencyInjection.Interfaces")
    restrictions|> shouldEqual []

    let name,_,restrictions = nuspec.Dependencies.[2]
    name  |> shouldEqual (PackageName "System.Collections.Concurrent")
    restrictions |> shouldEqual  [FrameworkRestriction.Exactly(DNXCore(FrameworkVersion.V5_0))]



[<Test>]
let ``can detect explicit dependencies for FluentAssertions 4``() = 
    let deps = Nuspec.Load("Nuspec/FluentAssertions4.nuspec").Dependencies |> List.toArray

    deps.[0]
    |> shouldEqual 
        (PackageName "System.Collections",
          DependenciesFileParser.parseVersionRequirement(">= 4.0.10"),
          [FrameworkRestriction.Exactly(DNXCore(FrameworkVersion.V5_0))])