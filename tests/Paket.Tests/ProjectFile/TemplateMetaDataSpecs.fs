module Paket.ProjectFile.TemplateMetaDataSpecs

open Paket
open NUnit.Framework
open TestHelpers

[<Test>]
let ``Get template metadata from SDK project`` () =
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
    

