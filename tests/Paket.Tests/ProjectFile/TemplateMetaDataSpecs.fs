module Paket.ProjectFile.TemplateMetaDataSpecs

open Paket
open NUnit.Framework
open TestHelpers

[<Test>]
let ``Get template metadata from SDK project with package info`` () =
    ensureDir ()
    let project = ProjectFile.TryLoad("./ProjectFile/TestData/MicrosoftNetSdkWithTargetFrameworkAndPackageInfo.csprojtest").Value
    let projectInfo, optionalInfo = project.GetTemplateMetadata()
    
    Assert.AreEqual(Some("MicrosoftNetSdkWithTargetFrameworkAndPackageInfo"), projectInfo.Id)
    CollectionAssert.AreEqual([|"Author"|], projectInfo.Authors.Value)
    Assert.AreEqual("1.2.3.4", projectInfo.Version.Value.AsString)
    Assert.AreEqual(Some("A description."), projectInfo.Description)

    CollectionAssert.IsEmpty(optionalInfo.Owners)
    Assert.AreEqual(Some("MicrosoftNetSdkWithTargetFrameworkAndPackageInfo"), optionalInfo.Title)
    Assert.AreEqual(Some("Note 1"), optionalInfo.ReleaseNotes)
    Assert.AreEqual(Some("https://opensource.org/licenses/MIT"), optionalInfo.LicenseUrl)
    Assert.AreEqual(Some("https://fsprojects.github.io/Paket"), optionalInfo.ProjectUrl)
    Assert.AreEqual(Some("https://github.com/fsprojects/Paket"), optionalInfo.RepositoryUrl)
    Assert.AreEqual(Some("Copyright ©Paket 2014"), optionalInfo.Copyright)
    CollectionAssert.AreEqual([|"tag1"; "tag2"|], optionalInfo.Tags)

[<Test>]
let ``Get template metadata from empty SDK project`` () =
    ensureDir ()
    let project = ProjectFile.TryLoad("./ProjectFile/TestData/MicrosoftNetSdkWithTargetFramework.csprojtest").Value
    let projectInfo, optionalInfo = project.GetTemplateMetadata()
    
    Assert.AreEqual(Some("MicrosoftNetSdkWithTargetFramework"), projectInfo.Id)
    Assert.AreEqual(None, projectInfo.Authors)
    Assert.AreEqual("0.0.1", projectInfo.Version.Value.ToString())
    Assert.AreEqual(None, projectInfo.Description)

    CollectionAssert.IsEmpty(optionalInfo.Owners)
    Assert.AreEqual(Some("MicrosoftNetSdkWithTargetFramework"), optionalInfo.Title)
    Assert.AreEqual(None, optionalInfo.ReleaseNotes)
    Assert.AreEqual(None, optionalInfo.LicenseUrl)
    Assert.AreEqual(None, optionalInfo.ProjectUrl)
    Assert.AreEqual(None, optionalInfo.RepositoryUrl)
    Assert.AreEqual(None, optionalInfo.Copyright)
    CollectionAssert.IsEmpty(optionalInfo.Tags)
    

