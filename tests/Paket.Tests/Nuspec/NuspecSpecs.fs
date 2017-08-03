module Paket.NuspecSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.Requirements
open Domain
open System.IO
open Pri.LongPath
open TestHelpers


[<Test>]
let ``can detect explicit references``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"FSharp.Data.nuspec")).References
    |> shouldEqual (NuspecReferences.Explicit ["FSharp.Data.dll"])

[<Test>]
let ``can detect explicit in self made nuspec``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"FSharp.Data.Prerelease.nuspec")).References
    |> shouldEqual (NuspecReferences.Explicit ["FSharp.Data.dll"])

[<Test>]
let ``can detect all references``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"Octokit.nuspec")).References
    |> shouldEqual NuspecReferences.All

[<Test>]
let ``can detect all references for FsXaml``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"FsXaml.Wpf.nuspec")).References
    |> shouldEqual NuspecReferences.All

[<Test>]
let ``can detect all references for ReadOnlyCollectionExtions``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"ReadOnlyCollectionExtensions.nuspec")).References
    |> shouldEqual NuspecReferences.All

[<Test>]
let ``can detect developmentDependency is false for ReadOnlyCollectionExtions``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"ReadOnlyCollectionExtensions.nuspec")).IsDevelopmentDependency
    |> shouldEqual false

[<Test>]
let ``can detect developmentDependency for LiteGuard.Source``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"LiteGuard.Source.nuspec")).IsDevelopmentDependency
    |> shouldEqual true

[<Test>]
let ``can detect all references for log4net``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"log4net.nuspec")).References
    |> shouldEqual NuspecReferences.All

[<Test>]
let ``if nuspec is not found we assume all references``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"blablub.nuspec")).References
    |> shouldEqual NuspecReferences.All

[<Test>]
let ``can detect explicit references for Fantomas``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"Fantomas.nuspec")).References
    |> shouldEqual (NuspecReferences.Explicit ["FantomasLib.dll"])

[<Test>]
let ``can detect no framework assemblies for Fantomas``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"Fantomas.nuspec")).FrameworkAssemblyReferences
    |> shouldEqual []

[<Test>]
let ``if nuspec is not found we assume no framework references``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"blablub.nuspec")).FrameworkAssemblyReferences
    |> shouldEqual []

[<Test>]
let ``can detect framework assemblies for Microsoft.Net.Http``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"Microsoft.Net.Http.nuspec")).FrameworkAssemblyReferences
    |> shouldEqual 
        [{ AssemblyName = "System.Net.Http"
           FrameworkRestrictions = 
            makeOrList
             [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_5))
              FrameworkRestriction.Exactly(MonoTouch)
              FrameworkRestriction.Exactly(MonoAndroid MonoAndroidVersion.V1)] }
         { AssemblyName = "System.Net.Http.WebRequest"
           FrameworkRestrictions = 
             makeOrList [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_5))] }]

[<Test>]
let ``can detect deps assemblies for RazorEngine``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"RazorEngine.nuspec")).Dependencies
    |> shouldEqual 
        [PackageName "Microsoft.AspNet.Razor",DependenciesFileParser.parseVersionRequirement("= 2.0.30506.0"), 
            makeOrList [FrameworkRestriction.Between(DotNetFramework(FrameworkVersion.V4), DotNetFramework(FrameworkVersion.V4_5))]
         PackageName "Microsoft.AspNet.Razor",DependenciesFileParser.parseVersionRequirement(">= 3.0.0"),
            makeOrList [FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4_5))]]

[<Test>]
let ``can detect framework assemblies for FluentAssertions``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"FluentAssertions.nuspec")).FrameworkAssemblyReferences
    |> shouldEqual 
        [{ AssemblyName = "System.Xml"; FrameworkRestrictions = makeOrList [] }
         { AssemblyName = "System.Xml.Linq"; FrameworkRestrictions = makeOrList [] } ]

[<Test>]
let ``can detect framework assemblies for SqlCLient``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"FSharp.Data.SqlClient.nuspec")).FrameworkAssemblyReferences
    |> shouldEqual 
        [{ AssemblyName = "System.Data"; FrameworkRestrictions = makeOrList [] }
         { AssemblyName = "System.Xml"; FrameworkRestrictions = makeOrList [] } ]

[<Test>]
let ``can detect license for SqlCLient``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"FSharp.Data.SqlClient.nuspec")).LicenseUrl
    |> shouldEqual "http://github.com/fsprojects/FSharp.Data.SqlClient/blob/master/LICENSE.md"

[<Test>]
let ``can detect dependencies for SqlCLient``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"FSharp.Data.SqlClient.nuspec")).Dependencies
    |> shouldEqual 
        [PackageName "Microsoft.SqlServer.Types",DependenciesFileParser.parseVersionRequirement(">= 11.0.0"), makeOrList []]

[<Test>]
let ``can detect reference files for SqlCLient``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"FSharp.Data.SqlClient.nuspec")).References
    |> shouldEqual (NuspecReferences.Explicit ["FSharp.Data.SqlClient.dll"])

[<Test>]
let ``can detect framework assemblies for Octokit``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"Octokit.nuspec")).FrameworkAssemblyReferences
    |> shouldEqual 
        [{ AssemblyName = "System.Net.Http"
           FrameworkRestrictions = 
            makeOrList
              [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_5))
               FrameworkRestriction.Exactly(Windows WindowsVersion.V8)] }]

[<Test>]
let ``can detect framework assemblies for FSharp.Data.SqlEnumProvider``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"FSharp.Data.SqlEnumProvider.nuspec")).FrameworkAssemblyReferences
    |> shouldEqual 
        [{ AssemblyName = "System.Data"; FrameworkRestrictions = makeOrList [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4))] }
         { AssemblyName = "System.Xml"; FrameworkRestrictions = makeOrList [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4))] }]

[<Test>]
let ``can detect empty framework assemblies for ReadOnlyCollectionExtensions``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"ReadOnlyCollectionExtensions.nuspec")).FrameworkAssemblyReferences
    |> shouldEqual [ ]

[<Test>]
let ``can detect empty dependencies for log4net``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"log4net.nuspec")).Dependencies
    |> shouldEqual []

[<Test>]
let ``can detect explicit dependencies for Fantomas``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"Fantomas.nuspec")).Dependencies
    |> shouldEqual [PackageName "FSharp.Compiler.Service",DependenciesFileParser.parseVersionRequirement(">= 0.0.57"), makeOrList []]

[<Test>]
let ``can detect explicit dependencies for ReadOnlyCollectionExtensions``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"ReadOnlyCollectionExtensions.nuspec")).Dependencies
    |> shouldEqual 
        [PackageName "LinqBridge",DependenciesFileParser.parseVersionRequirement(">= 1.3.0"), 
            makeOrList [FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V2), DotNetFramework(FrameworkVersion.V3_5))]
         PackageName "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"),
            makeOrList
             [FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V2))]]

[<Test>]
let ``can detect explicit dependencies for Microsoft.AspNetCore.Antiforgery``() = 
    //ensureDir()
    let deps = Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"Microsoft.AspNetCore.Antiforgery.nuspec")).Dependencies

    let v =
        match DependenciesFileParser.parseVersionRequirement(">= 1.0.0-rc3-20550") with
        | VersionRequirement(v,_) -> v

    deps.[0]
    |> shouldEqual 
        (PackageName "Microsoft.AspNetCore.DataProtection", VersionRequirement(v,PreReleaseStatus.All), 
            makeOrList [FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4_5_1)); FrameworkRestriction.AtLeast (DotNetStandard(DotNetStandardVersion.V1_3))])

[<Test>]
let ``can detect explicit dependencies for Microsoft.AspNetCore.Mvc.ViewFeatures``() = 
    //ensureDir()
    let deps = Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"Microsoft.AspNetCore.Mvc.ViewFeatures.nuspec")).Dependencies

    let v =
        match DependenciesFileParser.parseVersionRequirement(">= 1.0.0-rc3-20550") with
        | VersionRequirement(v,_) -> v

    deps.[0]
    |> shouldEqual 
        (PackageName "Microsoft.AspNetCore.Antiforgery", VersionRequirement(v,PreReleaseStatus.All), 
            makeOrList [FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4_5_1)); FrameworkRestriction.AtLeast (DotNetStandard(DotNetStandardVersion.V1_5))])

[<Test>]
let ``can detect framework assemblies for MathNet.Numerics``() =
    //ensureDir() 
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"MathNet.Numerics.nuspec")).FrameworkAssemblyReferences
    |> shouldEqual 
        [{ AssemblyName = "System.Numerics"
           FrameworkRestrictions = 
             makeOrList
                [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4))
                 FrameworkRestriction.Exactly(Windows WindowsVersion.V8)
                 FrameworkRestriction.Exactly(Silverlight SilverlightVersion.V5)
                 FrameworkRestriction.Exactly(MonoAndroid MonoAndroidVersion.V1)
                 FrameworkRestriction.Exactly(MonoTouch)] }]


[<Test>]
let ``can detect dependencies for MathNet.Numerics``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"MathNet.Numerics.nuspec")).Dependencies
    |> shouldEqual 
        [ PackageName "TaskParallelLibrary",
          DependenciesFileParser.parseVersionRequirement(">= 1.0.2856.0"),
            makeOrList [FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V3_5),DotNetFramework(FrameworkVersion.V4))] ]

[<Test>]
let ``can detect dependencies for MathNet.Numerics.FSharp``() = 
    //ensureDir()
    let s =
        Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"MathNet.Numerics.FSharp.nuspec")).Dependencies
        |> Seq.head
    s
    |> shouldEqual 
        (PackageName "MathNet.Numerics",
         DependenciesFileParser.parseVersionRequirement("3.3.0"),makeOrList [])

[<Test>]
let ``can detect explicit dependencies for WindowsAzure.Storage``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"WindowsAzure.Storage.nuspec")).Dependencies
    |> Seq.skip 1
    |> Seq.head
    |> shouldEqual 
        (PackageName "Newtonsoft.Json",
          DependenciesFileParser.parseVersionRequirement(">= 5.0.8"),
          makeOrList
            [FrameworkRestriction.AtLeast(WindowsPhone WindowsPhoneVersion.V8)
             FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4))])

[<Test>]
let ``can detect framework assemblies for Microsoft.Framework.Logging``() = 
    //ensureDir()
    let nuspec = Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"Microsoft.Framework.Logging.nuspec"))
    nuspec.FrameworkAssemblyReferences.[0].AssemblyName |> shouldEqual "System.Collections.Concurrent"
    nuspec.FrameworkAssemblyReferences.[0].FrameworkRestrictions 
        |> shouldEqual
            (makeOrList
              [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_5))
               FrameworkRestriction.Exactly(DNX(FrameworkVersion.V4_5_1))])

    let name,_,restrictions = nuspec.Dependencies.[0]
    name  |> shouldEqual (PackageName "Microsoft.Framework.DependencyInjection.Interfaces")
    
    let name,_,restrictions = nuspec.Dependencies.[2]
    name  |> shouldEqual (PackageName "System.Collections.Concurrent")
    restrictions |> shouldEqual (makeOrList [FrameworkRestriction.AtLeast(DNXCore(FrameworkVersion.V5_0))])

[<Test>]
let ``can detect explicit dependencies for FluentAssertions 4``() = 
    //ensureDir()
    let deps = Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"FluentAssertions4.nuspec")).Dependencies |> List.toArray

    deps.[0]
    |> shouldEqual 
        (PackageName "System.Collections",
          DependenciesFileParser.parseVersionRequirement(">= 4.0.10"),
          makeOrList [FrameworkRestriction.AtLeast(DNXCore(FrameworkVersion.V5_0))])



[<Test>]
let ``can detect explicit dependencies for EasyNetQ``() = 
    //ensureDir()
    let deps = Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"EasyNetQ.nuspec")).Dependencies |> Seq.toArray

    deps.[0]
    |> shouldEqual 
        (PackageName "RabbitMQ.Client",
          DependenciesFileParser.parseVersionRequirement(">= 3.5.7"),
          makeOrList [])
   
    deps.[1]
    |> shouldEqual 
        (PackageName "Microsoft.Bcl",
          DependenciesFileParser.parseVersionRequirement(">= 1.1.10"),
          makeOrList
            [FrameworkRestriction.And[
                FrameworkRestriction.NoRestriction
                FrameworkRestriction.NotAtLeast(DotNetFramework(FrameworkVersion.V4_5))]
            ])

[<Test>]
let ``can detect deps assemblies for Xamarin.Forms``() = 
    //ensureDir()
    Nuspec.Load(Path.Combine(__SOURCE_DIRECTORY__,"Xamarin.Forms.nuspec")).Dependencies
    |> shouldEqual 
        [PackageName "WPtoolkit",DependenciesFileParser.parseVersionRequirement(">= 4.2013.08.16"), 
            makeOrList [FrameworkRestriction.AtLeast(WindowsPhone(WindowsPhoneVersion.V8))]
         PackageName "Xamarin.Android.Support.v4",DependenciesFileParser.parseVersionRequirement("= 23.3.0"), 
            makeOrList [FrameworkRestriction.Between(MonoAndroid(MonoAndroidVersion.V1), MonoAndroid(MonoAndroidVersion.V7))]
         PackageName "Xamarin.Android.Support.Design",DependenciesFileParser.parseVersionRequirement("= 23.3.0"), 
            makeOrList [FrameworkRestriction.Between(MonoAndroid(MonoAndroidVersion.V1), MonoAndroid(MonoAndroidVersion.V7))]
         PackageName "Xamarin.Android.Support.v7.AppCompat",DependenciesFileParser.parseVersionRequirement("= 23.3.0"), 
            makeOrList [FrameworkRestriction.Between(MonoAndroid(MonoAndroidVersion.V1), MonoAndroid(MonoAndroidVersion.V7))]
         PackageName "Xamarin.Android.Support.v7.CardView",DependenciesFileParser.parseVersionRequirement("= 23.3.0"), 
            makeOrList [FrameworkRestriction.Between(MonoAndroid(MonoAndroidVersion.V1), MonoAndroid(MonoAndroidVersion.V7))]
         PackageName "Xamarin.Android.Support.v7.MediaRouter",DependenciesFileParser.parseVersionRequirement("= 23.3.0"), 
            makeOrList [FrameworkRestriction.Between(MonoAndroid(MonoAndroidVersion.V1), MonoAndroid(MonoAndroidVersion.V7))]         
         PackageName "Xamarin.Android.Support.v4",DependenciesFileParser.parseVersionRequirement(">= 23.3.0"), 
            makeOrList [FrameworkRestriction.AtLeast(MonoAndroid(MonoAndroidVersion.V7))]
         PackageName "Xamarin.Android.Support.Design",DependenciesFileParser.parseVersionRequirement(">= 23.3.0"), 
            makeOrList [FrameworkRestriction.AtLeast(MonoAndroid(MonoAndroidVersion.V7))]
         PackageName "Xamarin.Android.Support.v7.AppCompat",DependenciesFileParser.parseVersionRequirement(">= 23.3.0"), 
            makeOrList [FrameworkRestriction.AtLeast(MonoAndroid(MonoAndroidVersion.V7))]
         PackageName "Xamarin.Android.Support.v7.CardView",DependenciesFileParser.parseVersionRequirement(">= 23.3.0"), 
            makeOrList [FrameworkRestriction.AtLeast(MonoAndroid(MonoAndroidVersion.V7))]
         PackageName "Xamarin.Android.Support.v7.MediaRouter",DependenciesFileParser.parseVersionRequirement(">= 23.3.0"), 
            makeOrList [FrameworkRestriction.AtLeast(MonoAndroid(MonoAndroidVersion.V7))]
        ]