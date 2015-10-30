module Paket.IntegrationTests.BindingRedirect

open System
open System.IO
open NUnit.Framework
open FsUnit

[<Test>]
let ``install should redirect referenced assemblies only``() = 
    paket "install --redirects --createnewbindingfiles" "binding-redirect-adds-referenced-assemblies-only" |> ignore

    let path = Path.Combine(scenarioTempPath "binding-redirect-adds-referenced-assemblies-only")
    let config1Path = Path.Combine(path, "Project1", "app.config")
    let config2Path = Path.Combine(path, "Project2", "app.config")
    let config3Path = Path.Combine(path, "Project3", "app.config")
    let config4Path = Path.Combine(path, "Project4", "app.config")

    let config1 = File.ReadAllText(config1Path)
    let config2 = File.ReadAllText(config2Path)
    let config3 = File.ReadAllText(config3Path)
    let config4 = File.ReadAllText(config4Path)

    let Albedo = """<assemblyIdentity name="Ploeh.Albedo" publicKeyToken="179ef6dd03497bbd" culture="neutral" />"""
    let AutoFixture = """<assemblyIdentity name="Ploeh.AutoFixture" publicKeyToken="b24654c590009d4f" culture="neutral" />"""
    let ``AutoFixture.Idioms`` = """<assemblyIdentity name="Ploeh.AutoFixture.Idioms" publicKeyToken="b24654c590009d4f" culture="neutral" />"""
    let ``AutoFixture.Xunit`` = """<assemblyIdentity name="Ploeh.AutoFixture.Xunit" publicKeyToken="b24654c590009d4f" culture="neutral" />"""
    let log4net = """<assemblyIdentity name="log4net" publicKeyToken="669e0ddf0bb1aa2a" culture="neutral" />"""
    let ``Newtonsoft.Json`` = """<assemblyIdentity name="Newtonsoft.Json" publicKeyToken="30ad4fe6b2a6aeed" culture="neutral" />"""
    let ``Newtonsoft.Json.Schema`` = """<assemblyIdentity name="Newtonsoft.Json.Schema" publicKeyToken="30ad4fe6b2a6aeed" culture="neutral" />"""
    let xunit = """<assemblyIdentity name="xunit" publicKeyToken="8d05b1bb7a6fdb6c" culture="neutral" />"""
    let ``xunit.extensions`` = """<assemblyIdentity name="xunit.extensions" publicKeyToken="8d05b1bb7a6fdb6c" culture="neutral" />"""
    let ``Castle.Core`` = """<assemblyIdentity name="Castle.Core" publicKeyToken="407dd0808d44fbdc" culture="neutral" />"""
    let ``Castle.Windsor`` = """<assemblyIdentity name="Castle.Windsor" publicKeyToken="407dd0808d44fbdc" culture="neutral" />"""

    config1.Contains Albedo |> shouldEqual false
    config1.Contains AutoFixture |> shouldEqual false
    config1 |> shouldContainText ``AutoFixture.Idioms``
    config1 |> shouldContainText ``AutoFixture.Xunit``
    config1.Contains log4net |> shouldEqual false
    config1 |> shouldContainText ``Newtonsoft.Json``
    config1.Contains ``Newtonsoft.Json.Schema`` |> shouldEqual false
    config1.Contains xunit |> shouldEqual false
    config1.Contains ``xunit.extensions`` |> shouldEqual false
    config1.Contains ``Castle.Core`` |> shouldEqual false
    config1.Contains ``Castle.Windsor`` |> shouldEqual false

    config2.Contains Albedo |> shouldEqual false
    config2.Contains AutoFixture |> shouldEqual false
    config2.Contains ``AutoFixture.Idioms`` |> shouldEqual false
    config2.Contains ``AutoFixture.Xunit`` |> shouldEqual false
    config2.Contains log4net |> shouldEqual false
    config2 |> shouldContainText ``Newtonsoft.Json``
    config2.Contains ``Newtonsoft.Json.Schema`` |> shouldEqual false
    config2.Contains xunit |> shouldEqual false
    config2.Contains ``xunit.extensions`` |> shouldEqual false
    config2.Contains ``Castle.Core`` |> shouldEqual false
    config2.Contains ``Castle.Windsor`` |> shouldEqual false

    config3.Contains Albedo |> shouldEqual false
    config3.Contains AutoFixture |> shouldEqual false
    config3.Contains ``AutoFixture.Idioms`` |> shouldEqual false
    config3.Contains ``AutoFixture.Xunit`` |> shouldEqual false
    config3 |> shouldContainText log4net
    config3.Contains ``Newtonsoft.Json`` |> shouldEqual false
    config3 |> shouldContainText ``Newtonsoft.Json.Schema``
    config3.Contains xunit |> shouldEqual false
    config3.Contains ``xunit.extensions`` |> shouldEqual false
    config3.Contains ``Castle.Core`` |> shouldEqual false
    config3.Contains ``Castle.Windsor`` |> shouldEqual false

    config4.Contains Albedo |> shouldEqual false
    config4.Contains AutoFixture |> shouldEqual false
    config4.Contains ``AutoFixture.Idioms`` |> shouldEqual false
    config4.Contains ``AutoFixture.Xunit`` |> shouldEqual false
    config4.Contains log4net |> shouldEqual false
    config4.Contains ``Newtonsoft.Json`` |> shouldEqual false
    config4.Contains ``Newtonsoft.Json.Schema`` |> shouldEqual false
    config4.Contains xunit |> shouldEqual false
    config4.Contains ``xunit.extensions`` |> shouldEqual false
    config4.Contains ``Castle.Core`` |> shouldEqual false
    config4.Contains ``Castle.Windsor`` |> shouldEqual false
