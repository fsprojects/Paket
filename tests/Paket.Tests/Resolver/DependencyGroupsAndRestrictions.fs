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
    cfg.Resolve(true,noSha1,VersionsFromGraphAsSeq graph, (fun _ _ _ _ -> []),PackageDetailsFromGraph graph,(fun _ _ -> None),groups,updateMode).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    
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