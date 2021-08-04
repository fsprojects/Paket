module Paket.NuspecWriterSpecs

open Paket
open FsUnit
open NUnit.Framework
open Paket.Domain

[<Test>]
let ``should serialize core info``() = 
    let result = """<package xmlns="http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd">
  <metadata>
    <id>Paket.Tests</id>
    <version>1.0.0.0</version>
    <authors>Two, Authors</authors>
    <description>A description</description>
  </metadata>
</package>"""

    let core : CompleteCoreInfo =
        { Id = "Paket.Tests"
          Version = SemVer.Parse "1.0.0.0" |> Some
          Authors = [ "Two"; "Authors" ]
          Description = "A description"
          Symbols = false }
    
    let doc = NupkgWriter.nuspecDoc (core, OptionalPackagingInfo.Empty)
    doc.ToString()
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings result)

[<Test>]
let ``should serialize dependencies``() = 
    let result = """<package xmlns="http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd">
  <metadata>
    <id>Paket.Tests</id>
    <version>1.0.0.0</version>
    <authors>Two, Authors</authors>
    <description>A description</description>
    <tags>f# rules</tags>
    <dependencies>
      <dependency id="Paket.Core" version="[3.1.0]" />
      <dependency id="xUnit" version="2.0.0" />
    </dependencies>
  </metadata>
</package>"""

    let core : CompleteCoreInfo =
        { Id = "Paket.Tests"
          Version = SemVer.Parse "1.0.0.0" |> Some
          Authors = [ "Two"; "Authors" ]
          Description = "A description"
          Symbols = false }
    
    let optional = 
        { OptionalPackagingInfo.Empty with 
            Tags = [ "f#"; "rules" ]
            DependencyGroups =
                [
                   { Framework = None
                     Dependencies =
                        [ PackageName "Paket.Core", VersionRequirement.Parse "[3.1]"
                          PackageName "xUnit", VersionRequirement.Parse "2.0" ] }
                ] }
    
    let doc = NupkgWriter.nuspecDoc (core, optional)
    doc.ToString() 
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings result)


[<Test>]
let ``#913 should serialize dependencies by group``() = 
    let result = """<package xmlns="http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd">
  <metadata>
    <id>Paket.Tests</id>
    <version>1.0.0.0</version>
    <authors>Two, Authors</authors>
    <description>A description</description>
    <tags>f# rules</tags>
    <dependencies>
      <group targetFramework="net35">
        <dependency id="Paket.Core" version="[3.1.0]" />
        <dependency id="xUnit" version="2.0.0" />
      </group>
    </dependencies>
  </metadata>
</package>"""

    let core : CompleteCoreInfo =
        { Id = "Paket.Tests"
          Version = SemVer.Parse "1.0.0.0" |> Some
          Authors = [ "Two"; "Authors" ]
          Description = "A description"
          Symbols = false }
    
    let optional = 
        { OptionalPackagingInfo.Empty with 
            Tags = [ "f#"; "rules" ]
            DependencyGroups =
              [
                { Framework = Some(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V3_5))
                  Dependencies =
                    [ PackageName "Paket.Core", VersionRequirement.Parse "[3.1]"
                      PackageName "xUnit", VersionRequirement.Parse "2.0" ] }
              ] }
    
    let doc = NupkgWriter.nuspecDoc (core, optional)
    doc.ToString() 
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings result)

[<Test>]
let ``#913 should serialize dependencies by group with 2 group``() = 
    let result = """<package xmlns="http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd">
  <metadata>
    <id>Paket.Tests</id>
    <version>1.0.0.0</version>
    <authors>Two, Authors</authors>
    <description>A description</description>
    <tags>f# rules</tags>
    <dependencies>
      <group targetFramework="net35">
        <dependency id="Paket.Core" version="[3.1.0]" />
        <dependency id="xUnit" version="2.0.0" />
      </group>
      <group targetFramework="netstandard1.3">
        <dependency id="Paket.Core" version="[3.1.0]" />
        <dependency id="xUnit" version="2.0.0" />
      </group>
    </dependencies>
  </metadata>
</package>"""

    let core : CompleteCoreInfo =
        { Id = "Paket.Tests"
          Version = SemVer.Parse "1.0.0.0" |> Some
          Authors = [ "Two"; "Authors" ]
          Description = "A description"
          Symbols = false }
    
    let optional = 
        { OptionalPackagingInfo.Empty with 
            Tags = [ "f#"; "rules" ]
            DependencyGroups =
              [
                { Framework = Some(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V3_5))
                  Dependencies =
                    [ PackageName "Paket.Core", VersionRequirement.Parse "[3.1]"
                      PackageName "xUnit", VersionRequirement.Parse "2.0" ] }
                { Framework = Some(FrameworkIdentifier.DotNetStandard(DotNetStandardVersion.V1_3))
                  Dependencies =
                    [ PackageName "Paket.Core", VersionRequirement.Parse "[3.1]"
                      PackageName "xUnit", VersionRequirement.Parse "2.0" ] }
              ] }
    
    let doc = NupkgWriter.nuspecDoc (core, optional)
    doc.ToString() 
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings result)

[<Test>]
let ``should serialize dependencies by group with empty group``() = 
    let result = """<package xmlns="http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd">
  <metadata>
    <id>Paket.Tests</id>
    <version>1.0.0.0</version>
    <authors>Two, Authors</authors>
    <description>A description</description>
    <tags>f# rules</tags>
    <dependencies>
      <group targetFramework="net461" />
      <group targetFramework="netstandard1.3">
        <dependency id="Paket.Core" version="[3.1.0]" />
        <dependency id="xUnit" version="2.0.0" />
      </group>
    </dependencies>
  </metadata>
</package>"""

    let core : CompleteCoreInfo =
        { Id = "Paket.Tests"
          Version = SemVer.Parse "1.0.0.0" |> Some
          Authors = [ "Two"; "Authors" ]
          Description = "A description"
          Symbols = false }
    
    let optional = 
        { OptionalPackagingInfo.Empty with 
            Tags = [ "f#"; "rules" ]
            DependencyGroups =
              [
                { Framework = Some(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4_6_1))
                  Dependencies = [] }
                { Framework = Some(FrameworkIdentifier.DotNetStandard(DotNetStandardVersion.V1_3))
                  Dependencies =
                    [ PackageName "Paket.Core", VersionRequirement.Parse "[3.1]"
                      PackageName "xUnit", VersionRequirement.Parse "2.0" ] }
              ] }
    
    let doc = NupkgWriter.nuspecDoc (core, optional)
    doc.ToString() 
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings result)

[<Test>]
let ``should serialize dependencies with global group``() = 
    let result = """<package xmlns="http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd">
  <metadata>
    <id>Paket.Tests</id>
    <version>1.0.0.0</version>
    <authors>Two, Authors</authors>
    <description>A description</description>
    <tags>f# rules</tags>
    <dependencies>
      <group>
        <dependency id="FSharp.Core" version="1.0.0" />
      </group>
      <group targetFramework="net461" />
      <group targetFramework="netstandard1.3">
        <dependency id="Paket.Core" version="[3.1.0]" />
        <dependency id="xUnit" version="2.0.0" />
      </group>
    </dependencies>
  </metadata>
</package>"""

    let core : CompleteCoreInfo =
        { Id = "Paket.Tests"
          Version = SemVer.Parse "1.0.0.0" |> Some
          Authors = [ "Two"; "Authors" ]
          Description = "A description"
          Symbols = false }
    
    let optional = 
        { OptionalPackagingInfo.Empty with 
            Tags = [ "f#"; "rules" ]
            DependencyGroups =
              [
                { Framework = None
                  Dependencies =
                    [ PackageName "FSharp.Core", VersionRequirement.Parse "1.0.0" ] }
                { Framework = Some(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4_6_1))
                  Dependencies = [] }
                { Framework = Some(FrameworkIdentifier.DotNetStandard(DotNetStandardVersion.V1_3))
                  Dependencies =
                    [ PackageName "Paket.Core", VersionRequirement.Parse "[3.1]"
                      PackageName "xUnit", VersionRequirement.Parse "2.0" ] }
              ] }
    
    let doc = NupkgWriter.nuspecDoc (core, optional)
    doc.ToString() 
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings result)


[<Test>]
let ``should serialize frameworkAssemblues``() = 
    let result = """<package xmlns="http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd">
  <metadata>
    <id>Paket.Tests</id>
    <version>1.0.0.0</version>
    <authors>Two, Authors</authors>
    <description>A description</description>
    <tags>f# rules</tags>
    <frameworkAssemblies>
      <frameworkAssembly assemblyName="System.Xml" />
      <frameworkAssembly assemblyName="System.Xml.Linq" />
    </frameworkAssemblies>
  </metadata>
</package>"""

    let core : CompleteCoreInfo =
        { Id = "Paket.Tests"
          Version = SemVer.Parse "1.0.0.0" |> Some
          Authors = [ "Two"; "Authors" ]
          Description = "A description"
          Symbols = false }
    
    let optional = 
        { OptionalPackagingInfo.Empty with 
            Tags = [ "f#"; "rules" ]
            FrameworkAssemblyReferences = 
                [ "System.Xml"; "System.Xml.Linq" ] }
    
    let doc = NupkgWriter.nuspecDoc (core, optional)
    doc.ToString() 
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings result)

[<Test>]
let ``should not serialize files``() = 
    let result = """<package xmlns="http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd">
  <metadata>
    <id>Paket.Core</id>
    <version>4.2</version>
    <authors>Michael, Steffen</authors>
    <owners>Michael, Steffen</owners>
    <description>A description</description>
  </metadata>
</package>"""

    let core : CompleteCoreInfo =
        { Id = "Paket.Core"
          Version = SemVer.Parse "4.2" |> Some
          Authors = [ "Michael"; "Steffen" ]
          Description = "A description"
          Symbols = false }
    
    let optional = 
        { OptionalPackagingInfo.Empty with 
            Owners = [ "Michael"; "Steffen" ]
            Files = 
                [ "Paket.Core.del", "lib"
                  "bin/xUnit.64.dll", "lib40" ] }
                       
    let doc = NupkgWriter.nuspecDoc (core, optional)
    doc.ToString()
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings result)


[<Test>]
let ``should serialize packageTypes``() = 
    let result = """<package xmlns="http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd">
  <metadata>
    <id>Paket.Core</id>
    <version>4.2</version>
    <authors>Michael, Steffen</authors>
    <description>A description</description>
    <packageTypes>
      <packageType name="DotnetTool" />
      <packageType name="DotnetCliTool" />
    </packageTypes>
  </metadata>
</package>"""

    let core : CompleteCoreInfo =
        { Id = "Paket.Core"
          Version = SemVer.Parse "4.2" |> Some
          Authors = [ "Michael"; "Steffen" ]
          Description = "A description"
          Symbols = false }
    
    let optional = 
        { OptionalPackagingInfo.Empty with 
            PackageTypes = [ "DotnetTool"; "DotnetCliTool" ]  }
                       
    let doc = NupkgWriter.nuspecDoc (core, optional)
    doc.ToString()
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings result)

[<Test>]
let ``should not serialize all properties``() = 
    let result = """<package xmlns="http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd">
  <metadata>
    <id>Paket.Core</id>
    <version>4.2</version>
    <title>A title</title>
    <authors>Michael, Steffen</authors>
    <owners>Steffen, Alex</owners>
    <license type="expression">MIT</license>
    <projectUrl>http://www.somewhere.com</projectUrl>
    <iconUrl>http://www.somewhere.com/Icon</iconUrl>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
    <description>A description</description>
    <summary>summary</summary>
    <readme>README.md</readme>
    <releaseNotes>A release notes
second line</releaseNotes>
    <copyright>Paket owners 2015</copyright>
    <language>en-US</language>
    <tags>aa bb</tags>
    <developmentDependency>true</developmentDependency>
    <references>
      <reference file="file1.dll" />
      <reference file="file2.dll" />
    </references>
  </metadata>
</package>"""

    let core : CompleteCoreInfo =
        { Id = "Paket.Core"
          Version = SemVer.Parse "4.2" |> Some
          Authors = [ "Michael"; "Steffen" ]
          Description = "A description"
          Symbols = false }
    
    let optional = 
        { OptionalPackagingInfo.Empty with 
              Title = Some "A title"
              Owners = ["Steffen"; "Alex"]
              ReleaseNotes = Some"A release notes\r\nsecond line"
              Summary = Some "summary"
              Readme = Some "README.md"
              Language = Some "en-US"
              ProjectUrl = Some "http://www.somewhere.com"
              LicenseExpression = Some "MIT"
              IconUrl = Some "http://www.somewhere.com/Icon"
              Copyright = Some "Paket owners 2015"
              RequireLicenseAcceptance = true
              References = ["file1.dll";"file2.dll"]
              Tags = ["aa"; "bb"]
              DevelopmentDependency = true }

    let doc = NupkgWriter.nuspecDoc (core, optional)
    doc.ToString()
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings result)
