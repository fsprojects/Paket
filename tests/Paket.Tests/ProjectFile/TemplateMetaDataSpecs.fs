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

    CollectionAssert.AreEqual([|"tag1"; "tag2"|], optionalInfo.Tags)
    

