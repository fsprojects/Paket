module Paket.DependenciesFile.SaveSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open System

let config1 = """source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE 1.1
nuget SignalR 3.3.2"""

[<Test>]
let ``should serialize simple config``() = 
    let cfg = DependenciesFile.FromSource(config1)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings config1)


let strictConfig = """references: strict
source http://www.nuget.org/api/v2

nuget FAKE ~> 3.0"""


[<Test>]
let ``should serialize strict config``() = 
    let cfg = DependenciesFile.FromSource(strictConfig)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings strictConfig)


let contentNoneConfig = """redirects: on
content: none
source http://www.nuget.org/api/v2

nuget FAKE ~> 3.0"""


[<Test>]
let ``should serialize content none config``() = 
    let cfg = DependenciesFile.FromSource(contentNoneConfig)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings contentNoneConfig)

let noTargetsConfig = """import_targets: false
framework: >= net45
source "D:\code\temp with space"

nuget FAKE ~> 3.0"""


[<Test>]
let ``should serialize no targets config``() = 
    let cfg = DependenciesFile.FromSource(noTargetsConfig)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings noTargetsConfig)

let noLocalCopyConfig = """copy_local: false
source http://www.nuget.org/api/v2

nuget FAKE ~> 3.0"""


[<Test>]
let ``should serialize no local copy config``() = 
    let cfg = DependenciesFile.FromSource(noLocalCopyConfig)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings noLocalCopyConfig)

let noSpecificVersionConfig = """specific_version: false
source http://www.nuget.org/api/v2

nuget FAKE ~> 3.0"""


[<Test>]
let ``should serialize no specific version config``() = 
    let cfg = DependenciesFile.FromSource(noLocalCopyConfig)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings noLocalCopyConfig)

let simplestConfig = """nuget FAKE ~> 3.0"""

[<Test>]
let ``should serialize simplestConfig``() = 
    let cfg = DependenciesFile.FromSource(simplestConfig)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings simplestConfig)



let ownConfig = """source http://www.nuget.org/api/v2

nuget Octokit
nuget Newtonsoft.Json
nuget UnionArgParser
nuget NUnit.Runners >= 2.6
nuget NUnit >= 2.6 alpha
nuget FAKE alpha
nuget FSharp.Formatting
nuget DotNetZip ~> 1.9.3
nuget SourceLink.Fake
nuget NuGet.CommandLine

github forki/FsUnit FsUnit.fs"""

[<Test>]
let ``should serialize packet's own config``() = 
    let cfg = DependenciesFile.FromSource(ownConfig)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings ownConfig)


let configWithRemoteFile = """github fsharp/FAKE:master src/app/FAKE/Cli.fs
github fsharp/FAKE:bla123zxc src/app/FAKE/FileWithCommit.fs"""

[<Test>]
let ``should serialize remote files in config``() = 
    let cfg = DependenciesFile.FromSource(configWithRemoteFile)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings configWithRemoteFile)


let allVersionsConfig = """source http://www.nuget.org/api/v2

nuget Example > 1.2.3
nuget Example2 <= 1.2.3 alpha beta
nuget Example3 < 2.2.3 prerelease
nuget Example4 >= 1.2.3 < 1.5"""

[<Test>]
let ``should serialize config with all kinds of versions``() = 
    let cfg = DependenciesFile.FromSource(allVersionsConfig)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings allVersionsConfig)


let configWithPassword = """source http://www.nuget.org/api/v2 username: "user" password: "pass"

nuget Example > 1.2.3
nuget Example2 <= 1.2.3
nuget Example3 < 2.2.3
nuget Example4 == 2.2.3
nuget Example5 !== 2.2.3
nuget Example6 >= 1.2.3 < 1.5"""

[<Test>]
let ``should serialize config with password``() = 
    let cfg = DependenciesFile.FromSource(configWithPassword)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings configWithPassword)


let configWithEnvVarPassword = """source http://www.nuget.org/api/v2 username: "%FEED_USERNAME%" password: "%FEED_PASSWORD%"

nuget Example > 1.2.3
nuget Example2 <= 1.2.3
nuget Example3 < 2.2.3
nuget Example4 == 2.2.3
nuget Example5 !== 2.2.3
nuget Example6 >= 1.2.3 < 1.5"""

[<Test>]
let ``should serialize config with envrionment variable password``() = 
    Environment.SetEnvironmentVariable("FEED_USERNAME", "user XYZ", EnvironmentVariableTarget.Process)
    Environment.SetEnvironmentVariable("FEED_PASSWORD", "pw Love", EnvironmentVariableTarget.Process)
    let cfg = DependenciesFile.FromSource(configWithEnvVarPassword)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings configWithEnvVarPassword)



let withGist = """source https://www.nuget.org/api/v2

nuget FakeItEasy 1.24.0
nuget json-ld.net 1.0.3

gist misterx/55555555"""

[<Test>]
let ``should serialize config with gist``() = 
    let cfg = DependenciesFile.FromSource(withGist)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings withGist)

let withGistAndFile = """source https://www.nuget.org/api/v2

nuget FakeItEasy 1.24.0
nuget json-ld.net 1.0.3

gist Thorium/1972308 gistfile1.fs"""

[<Test>]
let ``should serialize config with gist and file``() = 
    let cfg = DependenciesFile.FromSource(withGistAndFile)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings withGistAndFile)

let withHTTPLink = """source https://www.nuget.org/api/v2

nuget FakeItEasy 1.24.0
nuget json-ld.net 1.0.3

http http://www.fssnip.net/raw/1M test1.fs"""

[<Test>]
let ``should serialize config with http link``() = 
    let cfg = DependenciesFile.FromSource(withHTTPLink)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings withHTTPLink)


let withFrameworkRestrictions = """source https://www.nuget.org/api/v2

nuget FakeItEasy 1.24.0
nuget json-ld.net 1.0.3 framework: net35, net40
nuget Example3 !== 2.2.3 alpha beta framework: >= net40
nuget Example4 framework: net40
nuget Example5 prerelease framework: net40

http http://www.fssnip.net/raw/1M test1.fs"""

[<Test>]
let ``should serialize config with framework restrictions``() = 
    let cfg = DependenciesFile.FromSource(withFrameworkRestrictions)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings withFrameworkRestrictions)

let withNoImportsRestrictions = """source https://www.nuget.org/api/v2

nuget FakeItEasy 1.24.0
nuget json-ld.net 1.0.3 copy_local: false, import_targets: false, framework: net35, net40, specific_version: true
nuget Example3 !== 2.2.3 alpha beta import_targets: false, specific_version: false
nuget Example4 import_targets: false, content: none
nuget Example5 prerelease import_targets: false

http http://www.fssnip.net/raw/1M test1.fs"""

[<Test>]
let ``should serialize config with no imports``() = 
    let cfg = DependenciesFile.FromSource(withNoImportsRestrictions)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings withNoImportsRestrictions)

let withComments = """source https://www.nuget.org/api/v2

// ignore me
nuget FakeItEasy 1.24.0
nuget json-ld.net 1.0.3 framework: net35, net40
nuget Example3 !== 2.2.3 alpha beta framework: >= net40
// ... but save me
# and me too!
nuget Example4 framework: net40
nuget Example5 prerelease framework: net40

http http://www.fssnip.net/raw/1M test1.fs"""

[<Test>]
let ``should serialize config with comments``() = 
    let cfg = DependenciesFile.FromSource(withComments)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings withComments)

let withFrameworkRestriction = """framework: >= net40
source https://www.nuget.org/api/v2

// ignore me
nuget FakeItEasy 1.24.0
nuget json-ld.net 1.0.3 framework: net35, net40
nuget Example3 !== 2.2.3 alpha beta framework: >= net40
// ... but save me
# and me too!
nuget Example4 framework: net40
nuget Example5 prerelease framework: net40

http http://www.fssnip.net/raw/1M test1.fs"""

[<Test>]
let ``should serialize config with framework restriction``() = 
    let cfg = DependenciesFile.FromSource(withFrameworkRestriction)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings withFrameworkRestriction)


let configWithAdditionalGroup = """condition: LEGACY
source "http://www.nuget.org/api/v2"

nuget FSharp.Compiler.Service
nuget FsReveal

group Build

nuget FAKE
nuget NUnit condition: LEGACY
"""
[<Test>]
let ``should serialize config with additional group``() = 
    let cfg = DependenciesFile.FromSource(configWithAdditionalGroup)
    
    cfg.ToString()
    |> shouldEqual (normalizeLineEndings configWithAdditionalGroup)