module Paket.NuspecWriterSpecs

open System.IO
open Paket
open Chessie.ErrorHandling
open FsUnit
open NUnit.Framework
open TestHelpers

[<Test>]
let ``should serialize cor info``() = 
    let result = """<package xmlns="http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd">
  <metadata>
    <id>Paket.Tests</id>
    <version>1.0.0.0</version>
    <authors>Two, Authors</authors>
    <description>A description</description>
  </metadata>
</package>"""
    
    let core = 
        { Id = "Paket.Tests"
          Version = SemVer.Parse "1.0.0.0" |> Some
          Authors = [ "Two"; "Authors" ]
          Description = "A description" }
    
    let doc = NupkgWriter.nuspecDoc (core, OptionalPackagingInfo.Epmty)
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
      <dependency id="Paket.Core" version="[3.1]" />
      <dependency id="xUnit" version="2.0" />
    </dependencies>
  </metadata>
</package>"""
    
    let core = 
        { Id = "Paket.Tests"
          Version = SemVer.Parse "1.0.0.0" |> Some
          Authors = [ "Two"; "Authors" ]
          Description = "A description" }
    
    let optional = 
        { OptionalPackagingInfo.Epmty with 
            Tags = [ "f#"; "rules" ]
            Dependencies = 
                [ "Paket.Core", VersionRequirement.Parse "[3.1]"
                  "xUnit", VersionRequirement.Parse "2.0" ] }
    
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
    
    let core = 
        { Id = "Paket.Core"
          Version = SemVer.Parse "4.2" |> Some
          Authors = [ "Michael"; "Steffen" ]
          Description = "A description" }
    
    let optional = 
        { OptionalPackagingInfo.Epmty with 
            Owners = [ "Michael"; "Steffen" ]
            Files = 
                [ "Paket.Core.del", "lib"
                  "bin/xUnit.64.dll", "lib40" ] }
                       
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
    <licenseUrl>http://www.somewhere.com/license.html</licenseUrl>
    <projectUrl>http://www.somewhere.com</projectUrl>
    <iconUrl>http://www.somewhere.com/Icon</iconUrl>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
    <description>A description</description>
    <summary>summary</summary>
    <releaseNotes>A release notes
second line</releaseNotes>
    <copyright>Paket owners 2015</copyright>
    <language>en-US</language>
    <tags>aa bb</tags>
    <developmentDependency>true</developmentDependency>
  </metadata>
</package>"""
    
    let core = 
        { Id = "Paket.Core"
          Version = SemVer.Parse "4.2" |> Some
          Authors = [ "Michael"; "Steffen" ]
          Description = "A description" }
    
    let optional = 
        { OptionalPackagingInfo.Epmty with 
              Title = Some "A title"
              Owners = ["Steffen"; "Alex"]
              ReleaseNotes = Some"A release notes\r\nsecond line"
              Summary = Some "summary"
              Language = Some "en-US"
              ProjectUrl = Some "http://www.somewhere.com"
              LicenseUrl = Some "http://www.somewhere.com/license.html"
              IconUrl = Some "http://www.somewhere.com/Icon"
              Copyright = Some "Paket owners 2015"
              RequireLicenseAcceptance = true
              Tags = ["aa"; "bb"]
              DevelopmentDependency = true }
                       
    let doc = NupkgWriter.nuspecDoc (core, optional)
    doc.ToString()
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings result)
