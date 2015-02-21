module Paket.NuspecWriterSpecs

open System.IO
open Paket
open Paket.Rop
open FsUnit
open NUnit.Framework


[<Test>]
let ``should serialize cor info`` () =
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
        Version = SemVer.Parse "1.0.0.0"
        Authors = [ "Two"; "Authors" ]
        Description = "A description" }

    let doc = NupkgWriter.nuspecDoc(core,OptionalPackagingInfo.Epmty)

    doc.ToString()
    |> shouldEqual result


[<Test>]
let ``should serialize dependency info`` () =
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
        Version = SemVer.Parse "1.0.0.0"
        Authors = [ "Two"; "Authors" ]
        Description = "A description" }

    let optional =
     { OptionalPackagingInfo.Epmty
        with         
          Tags = Some ([ "f#"; "rules"])
          Dependencies = 
            ["Paket.Core",  NugetVersionRangeParser.parse "[3.1]"
             "xUnit",  NugetVersionRangeParser.parse "2.0" ] }

    let doc = NupkgWriter.nuspecDoc(core,optional)

    doc.ToString()
    |> shouldEqual result