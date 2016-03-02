module Paket.IntegrationTests.BindingRedirect

open System
open System.IO
open NUnit.Framework
open FsUnit
open System.Text.RegularExpressions
open Paket

[<Test>]
let ``install should redirect required assemblies only``() = 
    paket "install --redirects --createnewbindingfiles" "i001187-binding-redirect" |> ignore

    let path = Path.Combine(scenarioTempPath "i001187-binding-redirect")
    let config1Path = Path.Combine(path, "Project1", "app.config")
    let config2Path = Path.Combine(path, "Project2", "app.config")
    let config3Path = Path.Combine(path, "Project3", "app.config")
    let config4Path = Path.Combine(path, "Project4", "app.config")

    let config1 = File.ReadAllText(config1Path)
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

    config1 |> shouldContainText Albedo
    config1 |> shouldContainText AutoFixture
    config1.Contains ``AutoFixture.Idioms`` |> shouldEqual false
    config1.Contains ``AutoFixture.Xunit`` |> shouldEqual false
    config1.Contains log4net |> shouldEqual false
    config1 |> shouldContainText ``Newtonsoft.Json``
    config1.Contains ``Newtonsoft.Json.Schema`` |> shouldEqual false
    config1.Contains xunit |> shouldEqual false
    config1 |> shouldContainText ``xunit.extensions``
    config1 |> shouldContainText ``Castle.Core``
    config1.Contains ``Castle.Windsor`` |> shouldEqual false

    File.Exists config2Path |> shouldEqual false

    config3.Contains Albedo |> shouldEqual false
    config3.Contains AutoFixture |> shouldEqual false
    config3.Contains ``AutoFixture.Idioms`` |> shouldEqual false
    config3.Contains ``AutoFixture.Xunit`` |> shouldEqual false
    config3.Contains log4net |> shouldEqual false
    config3 |> shouldContainText ``Newtonsoft.Json``
    config3.Contains ``Newtonsoft.Json.Schema`` |> shouldEqual false
    config3.Contains xunit |> shouldEqual false
    config3.Contains ``xunit.extensions`` |> shouldEqual false
    config3 |> shouldContainText ``Castle.Core``
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
    config4 |> shouldContainText ``Castle.Core``
    config4.Contains ``Castle.Windsor`` |> shouldEqual false

[<Test>]
let ``#1195 should report broken app.config``() =
    try
        paket "install --redirects" "i001195-broken-appconfig" |> ignore
        failwith "paket should fail"
    with
    | exn when exn.Message.Contains("Project1") && exn.Message.Contains("app.config") -> ()

[<Test>]
let ``#1218 install hard should replace all assembly redirects with required only``() = 
    paket "install --redirects --createnewbindingfiles --hard" "i001218-binding-redirect" |> ignore

    let path = Path.Combine(scenarioTempPath "i001218-binding-redirect")
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

    config1 |> shouldContainText Albedo
    config1 |> shouldContainText AutoFixture
    config1.Contains ``AutoFixture.Idioms`` |> shouldEqual false
    config1.Contains ``AutoFixture.Xunit`` |> shouldEqual false
    config1.Contains log4net |> shouldEqual false
    config1 |> shouldContainText ``Newtonsoft.Json``
    config1.Contains ``Newtonsoft.Json.Schema`` |> shouldEqual false
    config1.Contains xunit |> shouldEqual false
    config1 |> shouldContainText ``xunit.extensions``
    config1 |> shouldContainText ``Castle.Core``
    config1.Contains ``Castle.Windsor`` |> shouldEqual false

    config2.Contains "<assemblyIdentity " |> shouldEqual false

    config3.Contains Albedo |> shouldEqual false
    config3.Contains AutoFixture |> shouldEqual false
    config3.Contains ``AutoFixture.Idioms`` |> shouldEqual false
    config3.Contains ``AutoFixture.Xunit`` |> shouldEqual false
    config3.Contains log4net |> shouldEqual false
    config3 |> shouldContainText ``Newtonsoft.Json``
    config3.Contains ``Newtonsoft.Json.Schema`` |> shouldEqual false
    config3.Contains xunit |> shouldEqual false
    config3.Contains ``xunit.extensions`` |> shouldEqual false
    config3 |> shouldContainText ``Castle.Core``
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
    config4 |> shouldContainText ``Castle.Core``
    config4.Contains ``Castle.Windsor`` |> shouldEqual false
    
[<Test>]
let ``#1218 install should replace paket's binding redirects with required only``() = 
    paket "install --redirects --createnewbindingfiles" "i001218-binding-redirect" |> ignore

    let path = Path.Combine(scenarioTempPath "i001218-binding-redirect")
    let config1Path = Path.Combine(path, "Project1", "app.config")
    let config2Path = Path.Combine(path, "Project2", "app.config")
    let config3Path = Path.Combine(path, "Project3", "app.config")
    let config4Path = Path.Combine(path, "Project4", "app.config")

    let config1 = File.ReadAllText(config1Path)
    let config2 = File.ReadAllText(config2Path)
    let config3 = File.ReadAllText(config3Path)
    let config4 = File.ReadAllText(config4Path)

    let paketMark = "<Paket>True</Paket>\s*"

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

    Regex.IsMatch(config1, paketMark + Albedo) |> shouldEqual true
    Regex.IsMatch(config1, paketMark + AutoFixture) |> shouldEqual true
    config1.Contains ``AutoFixture.Idioms`` |> shouldEqual false
    config1.Contains ``AutoFixture.Xunit`` |> shouldEqual false
    config1.Contains log4net |> shouldEqual false
    Regex.IsMatch(config1, paketMark + ``Newtonsoft.Json``) |> shouldEqual true
    config1.Contains ``Newtonsoft.Json.Schema`` |> shouldEqual false
    config1.Contains xunit |> shouldEqual false
    Regex.IsMatch(config1, paketMark + ``xunit.extensions``) |> shouldEqual true
    Regex.IsMatch(config1, paketMark + ``Castle.Core``) |> shouldEqual true
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
    config3 |> shouldContainText AutoFixture
    config3.Contains ``AutoFixture.Idioms`` |> shouldEqual false
    config3.Contains ``AutoFixture.Xunit`` |> shouldEqual false
    config3.Contains log4net |> shouldEqual false
    Regex.IsMatch(config3, paketMark + ``Newtonsoft.Json``) |> shouldEqual true
    config3.Contains ``Newtonsoft.Json.Schema`` |> shouldEqual false
    config3.Contains xunit |> shouldEqual false
    config3.Contains ``xunit.extensions`` |> shouldEqual false
    Regex.IsMatch(config3, paketMark + ``Castle.Core``) |> shouldEqual true
    config3.Contains ``Castle.Windsor`` |> shouldEqual false

    config4.Contains Albedo |> shouldEqual false
    config4.Contains AutoFixture |> shouldEqual false
    config4.Contains ``AutoFixture.Idioms`` |> shouldEqual false
    config4.Contains ``AutoFixture.Xunit`` |> shouldEqual false
    config4.Contains log4net |> shouldEqual false
    config4 |> shouldContainText ``Newtonsoft.Json``
    config4.Contains ``Newtonsoft.Json.Schema`` |> shouldEqual false
    config4.Contains xunit |> shouldEqual false
    config4.Contains ``xunit.extensions`` |> shouldEqual false
    Regex.IsMatch(config4, paketMark + ``Castle.Core``) |> shouldEqual true
    config4.Contains ``Castle.Windsor`` |> shouldEqual false


[<Test>]
let ``#1248 install should replace paket's binding redirects with required only and keep stable``() = 
    paket "install --redirects --hard --createnewbindingfiles" "i001248-stable-redirect" |> ignore

    let originalConfig2Path = Path.Combine(originalScenarioPath "i001248-stable-redirect", "Project2", "app.config")
    
    let config2Path = Path.Combine(scenarioTempPath "i001248-stable-redirect", "Project2", "app.config")
    
    let originalConfig2 = File.ReadAllText(originalConfig2Path)
    let config2 = File.ReadAllText(config2Path)

    config2
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings originalConfig2)

[<Test>]
let ``#1270 force redirects``() = 
    paket "install --createnewbindingfiles" "i001270-force-redirects" |> ignore
    let path = Path.Combine(scenarioTempPath "i001270-force-redirects")
    let configPath = Path.Combine(path, "MyClassLibrary", "MyClassLibrary", "app.config")

    let ``FSharp.Core`` = """<assemblyIdentity name="FSharp.Core" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />"""
    let AlphaFS = """<assemblyIdentity name="AlphaFS" publicKeyToken="4d31a58f7d7ad5c9" culture="neutral" />"""
    let ``Newtonsoft.Json`` = """<assemblyIdentity name="Newtonsoft.Json" publicKeyToken="30ad4fe6b2a6aeed" culture="neutral" />"""
    let ``Newtonsoft.Json.Schema`` = """<assemblyIdentity name="Newtonsoft.Json.Schema" publicKeyToken="30ad4fe6b2a6aeed" culture="neutral" />"""

    let config = File.ReadAllText(configPath)

    config |> shouldContainText ``FSharp.Core``
    config.Contains AlphaFS |> shouldEqual false
    config.Contains ``Newtonsoft.Json`` |> shouldEqual false
    config.Contains ``Newtonsoft.Json.Schema`` |> shouldEqual false

[<Test>]
let ``#1270 redirects from references``() = 
    paket "install --createnewbindingfiles" "i001270-force-redirects" |> ignore
    let path = Path.Combine(scenarioTempPath "i001270-force-redirects")
    let configPath = Path.Combine(path, "MyClassLibrary", "MyClassLibrary2", "app.config")

    let ``FSharp.Core`` = """<assemblyIdentity name="FSharp.Core" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />"""
    let AlphaFS = """<assemblyIdentity name="AlphaFS" publicKeyToken="4d31a58f7d7ad5c9" culture="neutral" />"""
    let ``Newtonsoft.Json`` = """<assemblyIdentity name="Newtonsoft.Json" publicKeyToken="30ad4fe6b2a6aeed" culture="neutral" />"""
    let ``Newtonsoft.Json.Schema`` = """<assemblyIdentity name="Newtonsoft.Json.Schema" publicKeyToken="30ad4fe6b2a6aeed" culture="neutral" />"""

    let config = File.ReadAllText(configPath)

    config.Contains ``FSharp.Core`` |> shouldEqual false
    config.Contains AlphaFS |> shouldEqual false
    config |> shouldContainText ``Newtonsoft.Json.Schema``
    config.Contains ``Newtonsoft.Json`` |> shouldEqual false
    
[<Test>]
let ``#1248 redirects off``() = 
    paket "install" "i001248-redirects-off" |> ignore
    let path = Path.Combine(scenarioTempPath "i001248-redirects-off")
    let configPath = Path.Combine(path, "MyClassLibrary", "MyClassLibrary", "app.config")
    let originalPath = Path.Combine(originalScenarioPath "i001248-redirects-off")
    let originalConfigPath = Path.Combine(originalPath, "MyClassLibrary", "MyClassLibrary", "app.config")

    let config = File.ReadAllText(configPath)
    let originalConfig = File.ReadAllText(originalConfigPath)

    config |> shouldEqual originalConfig
    
[<Test>]
let ``#1248 redirects off for main only``() = 
    paket "install" "i001248-redirects-off" |> ignore
    let path = Path.Combine(scenarioTempPath "i001248-redirects-off")
    let configPath = Path.Combine(path, "MyClassLibrary", "MyClassLibrary2", "app.config")

    let ``FSharp.Core`` = """<assemblyIdentity name="FSharp.Core" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" />"""
    let AlphaFS = """<assemblyIdentity name="AlphaFS" publicKeyToken="4d31a58f7d7ad5c9" culture="neutral" />"""
    let ``Newtonsoft.Json`` = """<assemblyIdentity name="Newtonsoft.Json" publicKeyToken="30ad4fe6b2a6aeed" culture="neutral" />"""
    let ``Newtonsoft.Json.Schema`` = """<assemblyIdentity name="Newtonsoft.Json.Schema" publicKeyToken="30ad4fe6b2a6aeed" culture="neutral" />"""

    let config = File.ReadAllText(configPath)

    config |> shouldContainText ``FSharp.Core``
    config |> shouldContainText AlphaFS
    config.Contains ``Newtonsoft.Json`` |> shouldEqual false
    config.Contains ``Newtonsoft.Json.Schema`` |> shouldEqual false
    
[<Test>]
let ``#1248 redirects off with --redirects``() = 
    paket "install --redirects" "i001248-redirects-off" |> ignore
    let path = Path.Combine(scenarioTempPath "i001248-redirects-off")
    let configPath = Path.Combine(path, "MyClassLibrary", "MyClassLibrary", "app.config")
    let originalPath = Path.Combine(originalScenarioPath "i001248-redirects-off")
    let originalConfigPath = Path.Combine(originalPath, "MyClassLibrary", "MyClassLibrary", "app.config")

    let config = File.ReadAllText(configPath)
    let originalConfig = File.ReadAllText(originalConfigPath)

    config |> shouldEqual originalConfig
    
[<Test>]
let ``#1477 assembly redirects lock files``() = 
    let scenario =  "i001474-restore-no-locks"
    prepare scenario
    let p = Paket.Dependencies.Locate (Path.Combine(scenarioTempPath scenario, "paket.dependencies"))
    p.Install(true,true)
    p.Restore()

    try
        Directory.Delete(scenarioTempPath scenario, true)
    with e ->
        failwith "could not delete directory, i.e. restore holds on to files"

    
    