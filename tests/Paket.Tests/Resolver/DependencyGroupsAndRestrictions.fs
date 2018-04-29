module DependencyGroupsAndRestrictions

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.PackageResolver
open Paket.Requirements

let resolve graph updateMode (cfg : DependenciesFile) =
    let groups = [Constants.MainDependencyGroup, None ] |> Map.ofSeq
    cfg.Resolve(true,noSha1,VersionsFromGraphAsSeq graph, (fun _ _ -> []),PackageDetailsFromGraph graph,(fun _ _ _ -> None),groups,updateMode).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()

let graph1 = 
  GraphOfNuspecs [
    """<?xml version="1.0" encoding="utf-8" standalone="yes"?>
<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
  <metadata>
    <id>Chessie</id>
    <version>0.6.0</version>
    <dependencies>
      <group>
        <dependency id="FSharp.Core"></dependency>
      </group>
      <group targetFramework=".NETStandard1.6">
        <dependency id="MyNetStandardDummy" version="[1.6.0, )" />
        <dependency id="FSharp.Core" version="[4.0.1.7-alpha, )"></dependency>
      </group>
    </dependencies>
  </metadata>
</package>
    """
    """<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>FSharp.Core</id>
    <version>4.0.0.1</version>
  </metadata>
</package>
    """
    """<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
  <metadata>
    <id>FSharp.Core</id>
    <version>4.0.1.7-alpha</version>
    <dependencies>
      <group targetFramework=".NETStandard1.6">
        <dependency id="MyNetStandardDummy" version="[1.6.0, )" />
      </group>
    </dependencies>
  </metadata>
</package>"""
  ]


[<Test>]
let ``should prefer all framework dependency to netstandard1.6``() = 
    let config = """
source http://www.nuget.org/api/v2
framework net46

nuget Chessie"""
    let resolved =
        DependenciesFile.FromSource(config)
        |> resolve graph1 UpdateMode.UpdateAll
    getVersion resolved.[PackageName "FSharp.Core"] |> shouldEqual "4.0.0.1"
     
let graph3 = 
  GraphOfNuspecs [
    """<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
  <metadata>
    <id>Exceptionless</id>
    <version>4.0.1902</version>
    <dependencies>
      <group targetFramework=".NETFramework4.5" />
      <group targetFramework=".NETStandard1.2">
        <dependency id="MyNetStandardDummy" version="[1.6.0, )" />
      </group>
    </dependencies>
  </metadata>
</package>
    """
  ]


[<Test>]
let ``should ignore netstandard deps when framework restrictions are enabled (prefer net45 for net45)``() = 
    let config = """
source http://www.nuget.org/api/v2
framework net45

nuget Exceptionless"""
    let resolved =
        DependenciesFile.FromSource(config)
        |> resolve graph3 UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Exceptionless"] |> shouldEqual "4.0.1902"
    
[<Test>]
let ``should ignore netstandard deps when framework restrictions are enabled (prefer net45 for net46)``() = 
    let config = """
source http://www.nuget.org/api/v2
framework net46

nuget Exceptionless"""
    let resolved =
        DependenciesFile.FromSource(config)
        |> resolve graph3 UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Exceptionless"] |> shouldEqual "4.0.1902"
    
    
[<Test>]
let ``should ignore netstandard deps when framework restrictions are enabled (prefer net45 for net463)``() = 
    let config = """
source http://www.nuget.org/api/v2
framework net463

nuget Exceptionless"""
    let resolved =
        DependenciesFile.FromSource(config)
        |> resolve graph3 UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Exceptionless"] |> shouldEqual "4.0.1902"

   
let graph4 = 
  GraphOfNuspecs [
    """<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
  <metadata>
    <id>Marten</id>
    <version>0.9.12.563</version>
    <dependencies>
      <group targetFramework=".NETStandard1.3">
        <dependency id="Npgsql" version="[3.1.4, )" />
      </group>
    </dependencies>
  </metadata>
</package>
    """
    """<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>Npgsql</id>
    <version>3.1.4</version>
  </metadata>
</package>
    """
  ]
  
    
[<Test>]
let ``should use netstandard when no other choice exists``() = 
    let config = """
source http://www.nuget.org/api/v2
framework net46

nuget Marten"""
    let resolved =
        DependenciesFile.FromSource(config)
        |> resolve graph4 UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Marten"] |> shouldEqual "0.9.12.563"
    getVersion resolved.[PackageName "Npgsql"] |> shouldEqual "3.1.4"

    
let graph5 = 
  GraphOfNuspecs [
    """<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
  <metadata>
    <id>Marten</id>
    <version>0.9.12.563</version>
    <dependencies>
      <group targetFramework=".NETStandard1.3">
        <dependency id="Npgsql" version="[3.1.4, )" />
      </group>
    </dependencies>
  </metadata>
</package>
    """
    """<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>Npgsql</id>
    <version>3.1.4</version>
  </metadata>
</package>
    """
    """<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
  <metadata>
    <id>Exceptionless</id>
    <version>4.0.1902</version>
    <dependencies>
      <group targetFramework=".NETFramework4.5" ></group>
      <group targetFramework=".NETStandard1.3">
        <dependency id="Npgsql" version="[3.1.4, )" />
      </group>
    </dependencies>
  </metadata>
</package>
    """
  ]
    
[<Test>]
let ``should use package even when not needed by one``() = 
    let config = """
source http://www.nuget.org/api/v2
framework net46

nuget Exceptionless
nuget Marten"""
    let resolved =
        DependenciesFile.FromSource(config)
        |> resolve graph5 UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Marten"] |> shouldEqual "0.9.12.563"
    getVersion resolved.[PackageName "Npgsql"] |> shouldEqual "3.1.4"

let graph6 =
  GraphOfNuspecs [
    """<?xml version="1.0" encoding="utf-8" standalone="yes"?>
<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
  <metadata>
    <id>Chessie</id>
    <version>0.6.0</version>
    <dependencies>
      <group>
        <dependency id="FSharp.Core"></dependency>
      </group>
      <group targetFramework=".NETFramework4.5">
        <dependency id="FSharp.Core"></dependency>
      </group>
      <group targetFramework=".NETStandard1.6">
        <dependency id="MyNetStandardDummy" version="[1.6.0, )" />
        <dependency id="FSharp.Core" version="[4.0.1.7-alpha, )"></dependency>
      </group>
    </dependencies>
  </metadata>
</package>
    """
    """<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>FSharp.Core</id>
    <version>4.0.0.1</version>
  </metadata>
</package>
    """
    """<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
  <metadata>
    <id>FSharp.Core</id>
    <version>4.0.1.7-alpha</version>
    <dependencies>
      <group targetFramework=".NETStandard1.6">
        <dependency id="MyNetStandardDummy" version="[1.6.0, )" />
      </group>
    </dependencies>
  </metadata>
</package>"""
    """<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
  <metadata>
    <id>MyNetStandardDummy</id>
    <version>1.6.0</version>
    <dependencies>
      <group targetFramework=".NETStandard1.3">
      </group>
      <group targetFramework=".NETFramework4.5">
        <dependency id="UnknownPackage" version="[3.1.4, )" />
      </group>
    </dependencies>
  </metadata>
</package>
    """ ]
    
[<Test>]
let ``should properly delegate restriction when no global restriction is given``() = 
    let config = """
source http://www.nuget.org/api/v2

nuget Chessie"""
    let resolved =
        DependenciesFile.FromSource(config)
        |> resolve graph6 UpdateMode.UpdateAll
    let chessie = resolved.[PackageName "Chessie"]
    let fsharpCore = resolved.[PackageName "FSharp.Core"]
    let netStandard = resolved.[PackageName "MyNetStandardDummy"]
    getVersion chessie |> shouldEqual "0.6.0"
    // Discuss: Unification properly should take this version, as it is the only one matching all restrictions
    // But is still feels wrong to take an alpha package here...
    getVersion fsharpCore |> shouldEqual "4.0.1.7-alpha"
    getVersion netStandard |> shouldEqual "1.6.0"
    // Don't install netstandard to net45
    Requirements.isTargetMatchingRestrictions 
      (Requirements.getExplicitRestriction netStandard.Settings.FrameworkRestrictions,
       (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_5)))
      |> shouldEqual false
    // Don't install netstandard to net463
    Requirements.isTargetMatchingRestrictions 
      (Requirements.getExplicitRestriction netStandard.Settings.FrameworkRestrictions,
       (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_6_3)))
      |> shouldEqual false
    // This also tests that "UnknownPackage" is not pulled unexpectedly (because this dependency is never relevant)


    
let graph7 =
  GraphOfNuspecs [
    """<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd">
  <metadata>
    <id>Nancy.Serialization.JsonNet</id>
    <version>1.4.1</version>
    <authors>Andreas Håkansson, Steven Robbins and contributors</authors>
    <owners>Andreas Håkansson, Steven Robbins and contributors</owners>
    <licenseUrl>https://github.com/NancyFx/Nancy.Serialization.JsonNet/blob/master/license.txt</licenseUrl>
    <projectUrl>http://nancyfx.org</projectUrl>
    <iconUrl>http://nancyfx.org/nancy-nuget.png</iconUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Provides JSON (de)serialization support using Newtonsoft.JsonNet.</description>
    <summary>Nancy is a lightweight web framework for the .Net platform, inspired by Sinatra. Nancy aim at delivering a low ceremony approach to building light, fast web applications.</summary>
    <copyright>Andreas Håkansson, Steven Robbins and contributors</copyright>
    <language>en-US</language>
    <tags>Nancy Json JsonNet</tags>
    <dependencies>
      <dependency id="Nancy" version="1.4.1" />
      <dependency id="Newtonsoft.Json" />
    </dependencies>
  </metadata>
</package>
    """
    """<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>Newtonsoft.Json</id>
    <version>10.0.2</version>
    <title>Json.NET</title>
    <authors>James Newton-King</authors>
    <owners>James Newton-King</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <licenseUrl>https://raw.github.com/JamesNK/Newtonsoft.Json/master/LICENSE.md</licenseUrl>
    <projectUrl>http://www.newtonsoft.com/json</projectUrl>
    <iconUrl>http://www.newtonsoft.com/content/images/nugeticon.png</iconUrl>
    <description>Json.NET is a popular high-performance JSON framework for .NET</description>
    <language>en-US</language>
    <tags>json</tags>
    <dependencies>
      <group targetFramework=".NETFramework4.5" />
      <group targetFramework=".NETFramework4.0" />
      <group targetFramework=".NETFramework3.5" />
      <group targetFramework=".NETFramework2.0" />
      <group targetFramework=".NETPortable4.5-Profile259" />
      <group targetFramework=".NETPortable4.0-Profile328" />
      <group targetFramework=".NETStandard1.3">
        <dependency id="Microsoft.CSharp" version="4.3.0" />
        <dependency id="NETStandard.Library" version="1.6.1" />
        <dependency id="System.ComponentModel.TypeConverter" version="4.3.0" />
        <dependency id="System.Runtime.Serialization.Formatters" version="4.3.0" />
        <dependency id="System.Runtime.Serialization.Primitives" version="4.3.0" />
        <dependency id="System.Xml.XmlDocument" version="4.3.0" />
      </group>
      <group targetFramework=".NETStandard1.0">
        <dependency id="Microsoft.CSharp" version="4.3.0" />
        <dependency id="NETStandard.Library" version="1.6.1" />
        <dependency id="System.ComponentModel.TypeConverter" version="4.3.0" />
        <dependency id="System.Runtime.Serialization.Primitives" version="4.3.0" />
      </group>
    </dependencies>
  </metadata>
</package>
    """
    """<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd">
  <metadata>
    <id>Nancy</id>
    <version>1.4.3</version>
    <authors>Andreas Håkansson, Steven Robbins and contributors</authors>
    <owners>Andreas Håkansson, Steven Robbins and contributors</owners>
    <licenseUrl>https://github.com/NancyFx/Nancy/blob/master/license.txt</licenseUrl>
    <projectUrl>http://nancyfx.org</projectUrl>
    <iconUrl>http://nancyfx.org/nancy-nuget.png</iconUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Nancy is a lightweight web framework for the .Net platform, inspired by Sinatra. Nancy aim at delivering a low ceremony approach to building light, fast web applications.</description>
    <summary>Nancy is a lightweight web framework for the .Net platform, inspired by Sinatra. Nancy aim at delivering a low ceremony approach to building light, fast web applications.</summary>
    <copyright>Andreas Håkansson, Steven Robbins and contributors</copyright>
    <language>en-US</language>
    <tags>Nancy</tags>
  </metadata>
</package>
    """ ]
    
[<Test>]
let ``i001213 should not delegate restriction of transitive when globally no restriction is given``() = 
    let config = """
framework: >= net40

source https://nuget.org/api/v2

nuget Newtonsoft.Json redirects: on
nuget Nancy.Serialization.JsonNet ~> 1.2 framework: >= net451"""
    let resolved =
        DependenciesFile.FromSource(config)
        |> resolve graph7 UpdateMode.UpdateAll
    let newtonsoft = resolved.[PackageName "Newtonsoft.Json"]
    let nancy = resolved.[PackageName "Nancy.Serialization.JsonNet"]
    getVersion newtonsoft |> shouldEqual "10.0.2"
    getVersion nancy |> shouldEqual "1.4.1"

    (FrameworkRestriction.AtLeast (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4)).IsSubsetOf
        (Requirements.getExplicitRestriction newtonsoft.Settings.FrameworkRestrictions)
        |> shouldEqual true
        
    // install netstandard to net40
    Requirements.isTargetMatchingRestrictions 
      (Requirements.getExplicitRestriction newtonsoft.Settings.FrameworkRestrictions,
       (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4)))
      |> shouldEqual true
[<Test>]
let ``i001213 should delegate restriction of transitive when package is not given``() = 
    let config = """
framework: >= net40

source https://nuget.org/api/v2

nuget Nancy.Serialization.JsonNet ~> 1.2 framework: >= net451"""
    let resolved =
        DependenciesFile.FromSource(config)
        |> resolve graph7 UpdateMode.UpdateAll
    let newtonsoft = resolved.[PackageName "Newtonsoft.Json"]
    let nancy = resolved.[PackageName "Nancy.Serialization.JsonNet"]
    getVersion newtonsoft |> shouldEqual "10.0.2"
    getVersion nancy |> shouldEqual "1.4.1"
        
    // don't install newtonsoft to net40, because restriction should be propagated.
    Requirements.isTargetMatchingRestrictions 
      (Requirements.getExplicitRestriction newtonsoft.Settings.FrameworkRestrictions,
       (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4)))
      |> shouldEqual false



    
let graph8 =
  GraphOfNuspecs [
    """<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd">
  <metadata>
    <id>MathNet.Numerics.FSharp</id>
    <version>3.2.3</version>
    <title>Math.NET Numerics for F#</title>
    <authors>Christoph Ruegg, Marcus Cuda, Jurgen Van Gael</authors>
    <owners>Christoph Ruegg, Marcus Cuda, Jurgen Van Gael</owners>
    <licenseUrl>http://numerics.mathdotnet.com/docs/License.html</licenseUrl>
    <projectUrl>http://numerics.mathdotnet.com/</projectUrl>
    <iconUrl>http://www.mathdotnet.com/images/MathNet128.png</iconUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Math.NET Numerics is the numerical foundation of the Math.NET project, aiming to provide methods and algorithms for numerical computations in science, engineering and every day use. Supports F# 3.0 on .Net 4.0, .Net 3.5 and Mono on Windows, Linux and Mac; Silverlight 5, WindowsPhone/SL 8, WindowsPhone 8.1 and Windows 8 with PCL Portable Profiles 47 and 328; Android/iOS with Xamarin.</description>
    <summary>F# Modules for Math.NET Numerics, providing methods and algorithms for numerical computations in science, engineering and every day use.</summary>
    <releaseNotes>Bug fix: MatrixNormal distribution: density for non-square matrices ~Evelina Gabasova</releaseNotes>
    <tags>fsharp F# math numeric statistics probability integration interpolation regression solve fit linear algebra matrix fft</tags>
    <dependencies>
      <dependency id="MathNet.Numerics" version="[3.2.3]" />
    </dependencies>
    <frameworkAssemblies>
      <frameworkAssembly assemblyName="System.Numerics" targetFramework=".NETFramework4.0, .NETFramework4.5, .NETCore4.5, Silverlight5.0, MonoAndroid1.0, MonoTouch1.0" />
    </frameworkAssemblies>
  </metadata>
</package>
    """
    """<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
  <metadata>
    <id>MathNet.Numerics</id>
    <version>3.2.3</version>
    <title>Math.NET Numerics</title>
    <authors>Christoph Ruegg, Marcus Cuda, Jurgen Van Gael</authors>
    <owners>Christoph Ruegg, Marcus Cuda, Jurgen Van Gael</owners>
    <licenseUrl>http://numerics.mathdotnet.com/docs/License.html</licenseUrl>
    <projectUrl>http://numerics.mathdotnet.com/</projectUrl>
    <iconUrl>http://www.mathdotnet.com/images/MathNet128.png</iconUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Math.NET Numerics is the numerical foundation of the Math.NET project, aiming to provide methods and algorithms for numerical computations in science, engineering and every day use. Supports .Net 4.0, .Net 3.5 and Mono on Windows, Linux and Mac; Silverlight 5, WindowsPhone/SL 8, WindowsPhone 8.1 and Windows 8 with PCL Portable Profiles 47 and 328; Android/iOS with Xamarin.</description>
    <summary>Math.NET Numerics, providing methods and algorithms for numerical computations in science, engineering and every day use.</summary>
    <releaseNotes>Bug fix: MatrixNormal distribution: density for non-square matrices ~Evelina Gabasova</releaseNotes>
    <tags>math numeric statistics probability integration interpolation regression solve fit linear algebra matrix fft</tags>
    <dependencies>
      <group targetFramework=".NETFramework3.5">
        <dependency id="TaskParallelLibrary" version="1.0.2856.0" />
      </group>
      <group targetFramework=".NETFramework4.0" />
    </dependencies>
    <frameworkAssemblies>
      <frameworkAssembly assemblyName="System.Numerics" targetFramework=".NETFramework4.0, .NETFramework4.5, .NETCore4.5, Silverlight5.0, MonoAndroid1.0, MonoTouch1.0" />
    </frameworkAssemblies>
  </metadata>
</package>
    """
    """<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd">
  <metadata>
    <version>1.0.2856.0</version>
    <authors>Microsoft Corporation</authors>
    <owners>Microsoft Corporation</owners>
    <licenseUrl>http://go.microsoft.com/fwlink/?LinkID=186234</licenseUrl>
    <projectUrl>http://msdn.microsoft.com/en-us/library/dd460717.aspx</projectUrl>
    <iconUrl>http://i.msdn.microsoft.com/ee402630.NET_lg.png</iconUrl>
    <id>TaskParallelLibrary</id>
    <title>Task Parallel Library for .NET 3.5</title>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
    <description>The package includes:
* Task&lt;T&gt; for executing asynchronous operations.
* Concurrent Collections such as ConcurrentStack, ConcurentQueue ad ConcurrentDictionary.
* PLINQ for writing parallel queries.
* additional Threading operations such as Barrier,SpinLock and SpinWait.</description>
    <summary>A complete and official Microsoft backport of the Task Parallel Library (TPL) for .NET 3.5.</summary>
    <releaseNotes>This backport was shipped with the Reactive Extensions (Rx) library up until v1.0.2856.0. It can be downloaded from http://www.microsoft.com/download/en/details.aspx?id=24940 .</releaseNotes>
    <language>en-us</language>
    <tags>tpl plinq pfx task parallel extensions .net35 backport</tags>
  </metadata>
</package>
    """
  ]

[<Test>]
let ``i000140 should properly resolve framework dependent dependencies``() = 
    let config = """
source https://nuget.org/api/v2

nuget MathNet.Numerics.FSharp ~> 3.2.1"""
    let resolved =
        DependenciesFile.FromSource(config)
        |> resolve graph8 UpdateMode.UpdateAll
    let tpl = resolved.[PackageName "TaskParallelLibrary"]
    let numeric = resolved.[PackageName "MathNet.Numerics"]
    getVersion numeric |> shouldEqual "3.2.3"
    getVersion tpl |> shouldEqual "1.0.2856.0"
        
    // don't install tpl to net40, because restriction should be propagated.
    Requirements.isTargetMatchingRestrictions 
      (Requirements.getExplicitRestriction tpl.Settings.FrameworkRestrictions,
       (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4)))
      |> shouldEqual false


      
let graph9 =
  GraphOfNuspecs [
    """<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd">
  <metadata>
    <id>A</id>
    <version>1.0.0</version>
    <dependencies>
      <group targetFramework=".NETFramework4.5">
        <dependency id="C" version="1.0.0" />
      </group>
    </dependencies>
  </metadata>
</package>
    """
    """<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd">
  <metadata>
    <id>B</id>
    <version>1.0.0</version>
    <dependencies>
      <group targetFramework=".NETStandard2.0">
        <dependency id="C" version="1.0.0" />
      </group>
    </dependencies>
  </metadata>
</package>
    """
    """<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd">
  <metadata>
    <id>C</id>
    <version>1.0.0</version>
  </metadata>
</package>
    """
  ]

[<Test>]
let ``resolver should return then correct framework restrictions for transitive dependencies``() = 
    let config = """
source https://nuget.org/api/v2

nuget A
nuget B"""
    let resolved =
        DependenciesFile.FromSource(config)
        |> resolve graph9 UpdateMode.UpdateAll
    let c = resolved.[PackageName "C"]

    getVersion c |> shouldEqual "1.0.0"
        
    // Install C in net45
    Requirements.isTargetMatchingRestrictions 
      (Requirements.getExplicitRestriction c.Settings.FrameworkRestrictions,
       (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_5)))
      |> shouldEqual true

    // Install C in netstandard2.0
    Requirements.isTargetMatchingRestrictions 
      (Requirements.getExplicitRestriction c.Settings.FrameworkRestrictions,
       (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetStandard DotNetStandardVersion.V2_0)))
      |> shouldEqual true
