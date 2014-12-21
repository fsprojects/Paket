module Paket.NuspecSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.Requirements

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
        [{ AssemblyName = "System.Net.Http"; FrameworkRestrictions = [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_5))] }
         { AssemblyName = "System.Net.Http.WebRequest"; FrameworkRestrictions = [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_5))] }
         { AssemblyName = "System.Net.Http"; FrameworkRestrictions = [FrameworkRestriction.Exactly(MonoTouch)] }
         { AssemblyName = "System.Net.Http"; FrameworkRestrictions = [FrameworkRestriction.Exactly(MonoAndroid)] } ]

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
let ``can detect dependencies for SqlCLient``() = 
    Nuspec.Load("Nuspec/FSharp.Data.SqlClient.nuspec").Dependencies
    |> shouldEqual 
        ["Microsoft.SqlServer.Types",DependenciesFileParser.parseVersionRequirement(">= 11.0.0"), []]

[<Test>]
let ``can detect reference files for SqlCLient``() = 
    Nuspec.Load("Nuspec/FSharp.Data.SqlClient.nuspec").References
    |> shouldEqual (NuspecReferences.Explicit ["FSharp.Data.SqlClient.dll"])

[<Test>]
let ``can detect framework assemblies for Octokit``() = 
    Nuspec.Load("Nuspec/Octokit.nuspec").FrameworkAssemblyReferences
    |> shouldEqual 
        [{ AssemblyName = "System.Net.Http"; FrameworkRestrictions = [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_5))] }
         { AssemblyName = "System.Net.Http"; FrameworkRestrictions = [FrameworkRestriction.Exactly(Windows "v4.5")] }]

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
    |> shouldEqual ["FSharp.Compiler.Service",DependenciesFileParser.parseVersionRequirement(">= 0.0.57"), []]

[<Test>]
let ``can detect explicit dependencies for ReadOnlyCollectionExtensions``() = 
    Nuspec.Load("Nuspec/ReadOnlyCollectionExtensions.nuspec").Dependencies
    |> shouldEqual 
        ["LinqBridge",DependenciesFileParser.parseVersionRequirement(">= 1.3.0"), [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V2))]
         "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V2))]
         "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V3_5))]
         "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_Client))]]

[<Test>]
let ``can detect framework assemblies for MathNet.Numerics``() = 
    Nuspec.Load("Nuspec/MathNet.Numerics.nuspec").FrameworkAssemblyReferences
    |> shouldEqual 
        [{ AssemblyName = "System.Numerics"; FrameworkRestrictions = [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_Client))] }
         { AssemblyName = "System.Numerics"; FrameworkRestrictions = [FrameworkRestriction.Exactly(Windows("v4.5"))] }
         { AssemblyName = "System.Numerics"; FrameworkRestrictions = [FrameworkRestriction.Exactly(Silverlight("v5.0"))] }
         { AssemblyName = "System.Numerics"; FrameworkRestrictions = [FrameworkRestriction.Exactly(MonoAndroid)] }
         { AssemblyName = "System.Numerics"; FrameworkRestrictions = [FrameworkRestriction.Exactly(MonoTouch)] }]
[<Test>]
let ``can detect explicit dependencies for WindowsAzure.Storage``() = 
    Nuspec.Load("Nuspec/WindowsAzure.Storage.nuspec").Dependencies
    |> Seq.take 2
    |> Seq.toList
    |> shouldEqual 
        ["Microsoft.Data.OData",DependenciesFileParser.parseVersionRequirement(">= 5.6.2"), [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_Client))]
         "Newtonsoft.Json",DependenciesFileParser.parseVersionRequirement(">= 5.0.8"), [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_Client))]]
