module Paket.DependenciesFile.ParserSpecs

open Paket
open Paket.PackageSources
open NUnit.Framework
open FsUnit
open TestHelpers
open System
open Paket.Domain
open Paket.Requirements
open Paket.ModuleResolver

[<Test>]
let ``should read empty config``() = 
    let cfg = DependenciesFile.FromSource("")
    cfg.Groups.[Constants.MainDependencyGroup].Options.Strict |> shouldEqual false

    cfg.Groups.[Constants.MainDependencyGroup].Packages.Length |> shouldEqual 0
    cfg.Groups.[Constants.MainDependencyGroup].RemoteFiles.Length |> shouldEqual 0

let configWithSourceOnly = """
source http://www.nuget.org/api/v2
"""

[<Test>]
let ``should read config which only contains a source``() = 
    let cfg = DependenciesFile.FromSource(configWithSourceOnly)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Strict |> shouldEqual false

    cfg.Groups.[Constants.MainDependencyGroup].Sources.Length |> shouldEqual 1
    cfg.Groups.[Constants.MainDependencyGroup].Sources.Head  |> shouldEqual (NuGetV2({ Url = "http://www.nuget.org/api/v2"; Authentication = AuthProvider.empty }))
    cfg.Groups.[Constants.MainDependencyGroup].Sources.Head.Auth.Retrieve true
        |> shouldEqual None
let config1 = """
source "http://www.nuget.org/api/v2"

nuget "Castle.Windsor-log4net" "~> 3.2"
nuget "Rx-Main" "~> 2.0"
nuget "FAKE" "= 1.1"
nuget "SignalR" "= 3.3.2"
"""

[<Test>]
let ``should read simple config``() = 
    let cfg = DependenciesFile.FromSource(config1)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Strict |> shouldEqual false

    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "Rx-Main"].Range |> shouldEqual (VersionRange.Between("2.0", "3.0"))
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "Castle.Windsor-log4net"].Range |> shouldEqual (VersionRange.Between("3.2", "4.0"))
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "FAKE"].Range |> shouldEqual (VersionRange.Exactly "1.1")
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "SignalR"].Range |> shouldEqual (VersionRange.Exactly "3.3.2")

let configWithComment = """
source "http://www.nuget.org/api/v2"

nuget Castle.Windsor-log4net >= 3.2 prerelease # test
"""

[<Test>]
let ``should read simple config with prerelease and comment``() = 
    let cfg = DependenciesFile.FromSource(configWithComment)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Strict |> shouldEqual false

    let packageDefinition = cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "Castle.Windsor-log4net"]
    packageDefinition.Range |> shouldEqual (VersionRange.AtLeast("3.2"))
    packageDefinition.PreReleases |> shouldEqual PreReleaseStatus.All

let configWithVersionLine = """
version 1.2.3 --prefer-nuget
source "http://www.nuget.org/api/v2"
nuget Castle.Windsor-log4net
"""

[<Test>]
let ``should read simple config with version line for bootstrapper``() = 
    DependenciesFile.FromSource(configWithVersionLine) |> ignore


let config2 = """
source "http://www.nuget.org/api/v2"

// this rocks
nuget "FAKE" "~> 3.0"
nuget "Rx-Main" "~> 2.2"
nuget "MinPackage" "1.1.3"
"""

[<Test>]
let ``should read simple config with additional comment``() = 
    let cfg = DependenciesFile.FromSource(config2)
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "Rx-Main"].Range |> shouldEqual (VersionRange.Between("2.2", "3.0"))
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "FAKE"].Range |> shouldEqual (VersionRange.Between("3.0", "4.0"))
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "MinPackage"].Range |> shouldEqual (VersionRange.Exactly "1.1.3")

let config3 = """
source "https://www.nuget.org/api/v2" // here we are

nuget "FAKE" "~> 3.0" // born to rule
nuget "Rx-Main" "~> 2.2"
nuget "MinPackage" "1.1.3"
"""

[<Test>]
let ``should read simple config with comments``() = 
    let cfg = DependenciesFile.FromSource(config3)
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "Rx-Main"].Range |> shouldEqual (VersionRange.Between("2.2", "3.0"))
    cfg.Groups.[Constants.MainDependencyGroup].Sources |> List.head |> shouldEqual PackageSources.DefaultNuGetSource
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "FAKE"].Range |> shouldEqual (VersionRange.Between("3.0", "4.0"))

let config4 = """
source "https://www.nuget.org/api/v2" // first source
source "http://nuget.org/api/v3"  // second

nuget "FAKE" "~> 3.0" 
nuget "Rx-Main" "~> 2.2"
nuget "MinPackage" "1.1.3"
"""

[<Test>]
let ``should read config with multiple sources``() = 
    let cfg = DependenciesFile.FromSource(config4)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Strict |> shouldEqual false

    cfg.Groups.[Constants.MainDependencyGroup].Sources |> shouldEqual [PackageSources.DefaultNuGetSource; PackageSource.NuGetV2Source "http://nuget.org/api/v3"]

[<Test>]
let ``should read source file from config``() =
    let config = """github "fsharp/FAKE:master" "src/app/FAKE/Cli.fs"
                    github "fsharp/FAKE:bla123zxc" "src/app/FAKE/FileWithCommit.fs"
                    github "fsharp/FAKE" "src/app/FAKE/FileAuth.fs" github
                 """
    let dependencies = DependenciesFile.FromSource(config)
    dependencies.Groups.[Constants.MainDependencyGroup].RemoteFiles
    |> shouldEqual
        [ { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/Cli.fs"
            Origin = ModuleResolver.Origin.GitHubLink
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            Version = VersionRestriction.Concrete "master"
            AuthKey = None }
          { Owner = "fsharp"
            Project = "FAKE"
            Origin = ModuleResolver.Origin.GitHubLink
            Name = "src/app/FAKE/FileWithCommit.fs"
            Version = VersionRestriction.Concrete "bla123zxc" 
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            AuthKey = None }
          { Owner = "fsharp"
            Project = "FAKE"
            Origin = ModuleResolver.Origin.GitHubLink
            Name = "src/app/FAKE/FileAuth.fs"
            Version = VersionRestriction.NoVersionRestriction
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            AuthKey = Some "github" } ]

let strictConfig = """
references strict
source "http://www.nuget.org/api/v2" // first source

nuget "FAKE" "~> 3.0"
"""

[<Test>]
let ``should read strict config``() = 
    let cfg = DependenciesFile.FromSource(strictConfig)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Strict |> shouldEqual true
    cfg.Groups.[Constants.MainDependencyGroup].Options.Redirects |> shouldEqual None

    cfg.Groups.[Constants.MainDependencyGroup].Sources |> shouldEqual [PackageSource.NuGetV2Source "http://www.nuget.org/api/v2"]

let redirectsConfig = """
redirects on
source "http://www.nuget.org/api/v2" // first source

nuget "FAKE" "~> 3.0"
"""

[<Test>]
let ``should read config with redirects``() = 
    let cfg = DependenciesFile.FromSource(redirectsConfig)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Strict |> shouldEqual false
    cfg.Groups.[Constants.MainDependencyGroup].Options.Redirects |> shouldEqual (Some BindingRedirectsSettings.On)

    cfg.Groups.[Constants.MainDependencyGroup].Sources |> shouldEqual [PackageSource.NuGetV2Source "http://www.nuget.org/api/v2"]

let noRedirectsConfig = """
redirects off
source "http://www.nuget.org/api/v2" // first source

nuget "FAKE" "~> 3.0"
"""

[<Test>]
let ``should read config with no redirects``() = 
    let cfg = DependenciesFile.FromSource(noRedirectsConfig)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Strict |> shouldEqual false
    cfg.Groups.[Constants.MainDependencyGroup].Options.Redirects |> shouldEqual (Some BindingRedirectsSettings.Off)

    cfg.Groups.[Constants.MainDependencyGroup].Sources |> shouldEqual [PackageSource.NuGetV2Source "http://www.nuget.org/api/v2"]

let noneContentConfig = """
content none
source "http://www.nuget.org/api/v2" // first source

nuget "Microsoft.SqlServer.Types"
"""

[<Test>]
let ``should read content none config``() = 
    let cfg = DependenciesFile.FromSource(noneContentConfig)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.OmitContent |> shouldEqual (Some ContentCopySettings.Omit)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.CopyLocal |> shouldEqual None
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.SpecificVersion |> shouldEqual None
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.ImportTargets |> shouldEqual None

    cfg.Groups.[Constants.MainDependencyGroup].Sources |> shouldEqual [PackageSource.NuGetV2Source "http://www.nuget.org/api/v2"]

let specificFrameworkConfig = """
framework net40, net35
source "http://www.nuget.org/api/v2" // first source

nuget "Microsoft.SqlServer.Types"
"""

[<Test>]
let ``should read config with specific framework``() = 
    let cfg = DependenciesFile.FromSource(specificFrameworkConfig)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.OmitContent |> shouldEqual None
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.CopyLocal |> shouldEqual None
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.SpecificVersion |> shouldEqual None
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.ImportTargets |> shouldEqual None

    cfg.Groups.[Constants.MainDependencyGroup].Sources |> shouldEqual [PackageSource.NuGetV2Source "http://www.nuget.org/api/v2"]

let noTargetsImportConfig = """
import_targets false
copy_local false
specific_version false
source "http://www.nuget.org/api/v2" // first source

nuget "Microsoft.SqlServer.Types"
"""

[<Test>]
let ``should read no targets import config``() = 
    let cfg = DependenciesFile.FromSource(noTargetsImportConfig)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.ImportTargets |> shouldEqual (Some false)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.CopyLocal |> shouldEqual (Some false)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.SpecificVersion |> shouldEqual (Some false)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.OmitContent |> shouldEqual None
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.LicenseDownload |> shouldEqual None

    cfg.Groups.[Constants.MainDependencyGroup].Sources |> shouldEqual [PackageSource.NuGetV2Source "http://www.nuget.org/api/v2"]

let downloadLicenseConfig = """
license_download: true
copy_local false
specific_version false
source "http://www.nuget.org/api/v2" // first source

nuget "Microsoft.SqlServer.Types"
"""

[<Test>]
let ``should read license_download config``() =
    let cfg = DependenciesFile.FromSource(downloadLicenseConfig)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.LicenseDownload |> shouldEqual (Some true)

let copyContent = """
copy_content_to_output_dir always
source "http://www.nuget.org/api/v2" // first source

nuget "Microsoft.SqlServer.Types"
"""

[<Test>]
let ``should read no copy_content_to_output_dir config``() = 
    let cfg = DependenciesFile.FromSource(copyContent)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.CopyContentToOutputDirectory |> shouldEqual (Some CopyToOutputDirectorySettings.Always)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.OmitContent |> shouldEqual None

    cfg.Groups.[Constants.MainDependencyGroup].Sources |> shouldEqual [PackageSource.NuGetV2Source "http://www.nuget.org/api/v2"]

let configWithoutQuotes = """
source http://www.nuget.org/api/v2

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget SignalR = 3.3.2
"""

[<Test>]
let ``should read config without quotes``() = 
    let cfg = DependenciesFile.FromSource(configWithoutQuotes)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Strict |> shouldEqual false
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).Count |> shouldEqual 4

    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "Rx-Main"].Range |> shouldEqual (VersionRange.Between("2.0", "3.0"))
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "Castle.Windsor-log4net"].Range |> shouldEqual (VersionRange.Between("3.2", "4.0"))
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "FAKE"].Range |> shouldEqual (VersionRange.Exactly "1.1")
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "SignalR"].Range |> shouldEqual (VersionRange.Exactly "3.3.2")

let configLocalQuotedSource = """source "D:\code\temp with space"

nuget Castle.Windsor-log4net ~> 3.2
nuget Rx-Main ~> 2.0
nuget FAKE = 1.1
nuget SignalR = 3.3.2
"""

[<Test>]
let ``should read config local quoted source``() = 
    let cfg = DependenciesFile.FromSource(configLocalQuotedSource)
    cfg.Groups.[Constants.MainDependencyGroup].Sources.Head.Url |> shouldEqual "D:\code\\temp with space"
    cfg.Groups.[Constants.MainDependencyGroup].Options.Strict |> shouldEqual false
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).Count |> shouldEqual 4

    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "Rx-Main"].Range |> shouldEqual (VersionRange.Between("2.0", "3.0"))
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "Castle.Windsor-log4net"].Range |> shouldEqual (VersionRange.Between("3.2", "4.0"))
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "FAKE"].Range |> shouldEqual (VersionRange.Exactly "1.1")
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "SignalR"].Range |> shouldEqual (VersionRange.Exactly "3.3.2")

let configWithoutQuotesButLotsOfWhiteSpace = """
source      http://www.nuget.org/api/v2

nuget   Castle.Windsor-log4net   ~>     3.2
nuget Rx-Main ~> 2.0
nuget FAKE =    1.1
nuget SignalR    = 3.3.2
"""

[<Test>]
let ``should read config without quotes but lots of whitespace``() = 
    let cfg = DependenciesFile.FromSource(configWithoutQuotes)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Strict |> shouldEqual false
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).Count |> shouldEqual 4

    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "Rx-Main"].Range |> shouldEqual (VersionRange.Between("2.0", "3.0"))
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "Castle.Windsor-log4net"].Range |> shouldEqual (VersionRange.Between("3.2", "4.0"))
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "FAKE"].Range |> shouldEqual (VersionRange.Exactly "1.1")
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "SignalR"].Range |> shouldEqual (VersionRange.Exactly "3.3.2")


[<Test>]
let ``should read github source file from config without quotes``() =
    let config = """github fsharp/FAKE:master   src/app/FAKE/Cli.fs
                    github    fsharp/FAKE:bla123zxc src/app/FAKE/FileWithCommit.fs 
                    github    fsharp/FAKE src/app/FAKE/FileWithCommit.fs github
                 """
    let dependencies = DependenciesFile.FromSource(config)
    dependencies.Groups.[Constants.MainDependencyGroup].RemoteFiles
    |> shouldEqual
        [ { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/Cli.fs"
            Origin = ModuleResolver.Origin.GitHubLink
            Version = VersionRestriction.Concrete "master"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            AuthKey = None }
          { Owner = "fsharp"
            Project = "FAKE"
            Origin = ModuleResolver.Origin.GitHubLink
            Name = "src/app/FAKE/FileWithCommit.fs"
            Version = VersionRestriction.Concrete "bla123zxc"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            AuthKey = None } 
          { Owner = "fsharp"
            Project = "FAKE"
            Origin = ModuleResolver.Origin.GitHubLink
            Name = "src/app/FAKE/FileWithCommit.fs"
            Version = VersionRestriction.NoVersionRestriction
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            AuthKey = Some "github" }]

[<Test>]
let ``should read github source file from config with quotes``() =
    let config = """github fsharp/FAKE:master  "src/app/FAKE/Cli.fs"
                    github fsharp/FAKE:bla123zxc "src/app/FAKE/FileWith Space.fs" 
                    github fsharp/FAKE "src/app/FAKE/FileWith Space.fs" github
                 """
    let dependencies = DependenciesFile.FromSource(config)
    dependencies.Groups.[Constants.MainDependencyGroup].RemoteFiles
    |> shouldEqual
        [ { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/Cli.fs"
            Origin = ModuleResolver.Origin.GitHubLink
            Version = VersionRestriction.Concrete "master"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            AuthKey = None }
          { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/FileWith Space.fs"
            Origin = ModuleResolver.Origin.GitHubLink
            Version = VersionRestriction.Concrete "bla123zxc"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            AuthKey = None }
          { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/FileWith Space.fs"
            Origin = ModuleResolver.Origin.GitHubLink
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            Version = VersionRestriction.NoVersionRestriction
            AuthKey = Some "github" }  ]

[<Test>]
let ``should read github source files withou sha1``() =
    let config = """github fsharp/FAKE  src/app/FAKE/Cli.fs
                    github    fsharp/FAKE:bla123zxc src/app/FAKE/FileWithCommit.fs """
    let dependencies = DependenciesFile.FromSource(config)
    dependencies.Groups.[Constants.MainDependencyGroup].RemoteFiles
    |> shouldEqual
        [ { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/Cli.fs"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            Origin = ModuleResolver.Origin.GitHubLink
            Version = VersionRestriction.NoVersionRestriction
            AuthKey = None }
          { Owner = "fsharp"
            Project = "FAKE"
            Name = "src/app/FAKE/FileWithCommit.fs"
            Origin = ModuleResolver.Origin.GitHubLink
            Version = VersionRestriction.Concrete "bla123zxc"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            AuthKey = None } ]

[<Test>]
let ``should read http source file from config without quotes with file specs``() =
    let config = """http http://www.fssnip.net/raw/1M test1.fs
                    http http://www.fssnip.net/raw/1M/1 src/test2.fs """
    let dependencies = DependenciesFile.FromSource(config)
    dependencies.Groups.[Constants.MainDependencyGroup].RemoteFiles
    |> shouldEqual
        [ { Owner = "www.fssnip.net"
            Project = ""
            Name = "test1.fs"
            Origin = ModuleResolver.Origin.HttpLink "http://www.fssnip.net"
            Version = VersionRestriction.Concrete "/raw/1M"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            AuthKey = None }
          { Owner = "www.fssnip.net"
            Project = ""
            Name = "src/test2.fs"
            Origin = ModuleResolver.Origin.HttpLink "http://www.fssnip.net"
            Version = VersionRestriction.Concrete "/raw/1M/1"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            AuthKey = None } ]


[<Test>]
let ``should read http source file from config without quotes with file specs and project and query string after filename``() =
    let config = """http http://server-stash:7658/projects/proj1/repos/repo1/browse/Source/SolutionFolder/Rabbit.fs?at=a5457f3d811830059cd39d583f264eab340c273d&raw
                    http http://server-stash:7658/projects/proj1/repos/repo1/browse/Source/SolutionFolder/Rabbit.fs?at=a5457f3d811830059cd39d583f264eab340c273d&raw Rabbit.fs project"""
    let dependencies = DependenciesFile.FromSource(config)
    dependencies.Groups.[Constants.MainDependencyGroup].RemoteFiles
    |> shouldEqual
        [ { Owner = "server-stash_7658"
            Project = ""
            Name = "Rabbit.fs"
            Origin = ModuleResolver.Origin.HttpLink "http://server-stash:7658"
            Version = VersionRestriction.Concrete "/projects/proj1/repos/repo1/browse/Source/SolutionFolder/Rabbit.fs?at=a5457f3d811830059cd39d583f264eab340c273d&raw"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            AuthKey = None }
          { Owner = "server-stash_7658"
            Project = ""
            Name = "Rabbit.fs"
            Origin = ModuleResolver.Origin.HttpLink "http://server-stash:7658"
            Version = VersionRestriction.Concrete "/projects/proj1/repos/repo1/browse/Source/SolutionFolder/Rabbit.fs?at=a5457f3d811830059cd39d583f264eab340c273d&raw"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            AuthKey = Some "project" }
        ]

[<Test>]
let ``should read http source file from config without quotes with file specs and project``() =
    let config = """http http://www.fssnip.net/raw/1M test1.fs project
                    http http://www.fssnip.net/raw/1M/1 src/test2.fs project"""
    let dependencies = DependenciesFile.FromSource(config)
    dependencies.Groups.[Constants.MainDependencyGroup].RemoteFiles
    |> shouldEqual
        [ { Owner = "www.fssnip.net"
            Project = ""
            Name = "test1.fs"
            Origin = ModuleResolver.Origin.HttpLink "http://www.fssnip.net"
            Version = VersionRestriction.Concrete "/raw/1M"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            AuthKey = Some "project" }
          { Owner = "www.fssnip.net"
            Project = ""
            Name = "src/test2.fs"
            Origin = ModuleResolver.Origin.HttpLink "http://www.fssnip.net"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            Version = VersionRestriction.Concrete "/raw/1M/1"
            AuthKey = Some "project" } ]


[<Test>]
let ``should read gist source file from config without quotes with file specs``() =
    let config = """gist Thorium/1972308 gistfile1.fs
                    gist Thorium/6088882 """ //Gist supports multiple files also
    let dependencies = DependenciesFile.FromSource(config)
    dependencies.Groups.[Constants.MainDependencyGroup].RemoteFiles
    |> shouldEqual
        [ { Owner = "Thorium"
            Project = "1972308"
            Name = "gistfile1.fs"
            Origin = ModuleResolver.Origin.GistLink
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            Version = VersionRestriction.NoVersionRestriction
            AuthKey = None }
          { Owner = "Thorium"
            Project = "6088882"
            Name = "FULLPROJECT"
            Origin = ModuleResolver.Origin.GistLink
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            Version = VersionRestriction.NoVersionRestriction
            AuthKey = None } ]

[<Test>]
let ``should read gist source file``() =
    let config = """source https://www.nuget.org/api/v2

nuget JetBrainsAnnotations.Fody

gist misterx/5d9c6983004c1c9ec91f""" 
    let dependencies = DependenciesFile.FromSource(config)
    dependencies.Groups.[Constants.MainDependencyGroup].RemoteFiles
    |> shouldEqual
        [ { Owner = "misterx"
            Project = "5d9c6983004c1c9ec91f"
            Name = "FULLPROJECT"
            Origin = ModuleResolver.Origin.GistLink
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            Version = VersionRestriction.NoVersionRestriction
            AuthKey = None } ]

[<Test>]
let ``should read http source file from config without quotes, parsing rules``() =
    // The empty "/" should be ommited. After that, parsing amount of "/"-marks:
    let config = """
        http http://example/
        http http://example/item
        http http://example/item/
        http http://example/item/3
        http http://example/item/3/1"""
    let dependencies = DependenciesFile.FromSource(config)
    dependencies.Groups.[Constants.MainDependencyGroup].RemoteFiles
    |> shouldEqual
        [ { Owner = "example"
            Project = ""
            Name = "example.fs"
            Origin = ModuleResolver.Origin.HttpLink "http://example"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            Version = VersionRestriction.Concrete "/"
            AuthKey = None }
          { Owner = "example"
            Project = ""
            Name = "item.fs"
            Origin = ModuleResolver.Origin.HttpLink "http://example"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            Version = VersionRestriction.Concrete "/item"
            AuthKey = None }
          { Owner = "example"
            Project = ""
            Name = "item.fs"
            Origin = ModuleResolver.Origin.HttpLink "http://example"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            Version = VersionRestriction.Concrete "/item"
            AuthKey = None }
          { Owner = "example"
            Project = ""
            Name = "3.fs"
            Origin = ModuleResolver.Origin.HttpLink "http://example"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            Version = VersionRestriction.Concrete "/item/3"
            AuthKey = None }
          { Owner = "example"
            Project = ""
            Name = "1.fs"
            Origin = ModuleResolver.Origin.HttpLink "http://example"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            Version = VersionRestriction.Concrete "/item/3/1"
            AuthKey = None } ]

[<Test>]
let ``should read http binary references from config``() =
    let config = """
        http http://www.frijters.net/ikvmbin-8.0.5449.0.zip
        http http://www.frijters.net/ikvmbin-8.0.5449.0.zip ikvmbin.zip"""
    let dependencies = DependenciesFile.FromSource(config)
    dependencies.Groups.[Constants.MainDependencyGroup].RemoteFiles
    |> shouldEqual
        [ { Owner = "www.frijters.net"
            Project = ""
            Name = "ikvmbin-8.0.5449.0.zip"
            Origin = ModuleResolver.Origin.HttpLink "http://www.frijters.net"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            Version = VersionRestriction.Concrete "/ikvmbin-8.0.5449.0.zip"
            AuthKey = None }
          { Owner = "www.frijters.net"
            Project = ""
            Name = "ikvmbin.zip"
            Origin = ModuleResolver.Origin.HttpLink "http://www.frijters.net"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            Version = VersionRestriction.Concrete "/ikvmbin-8.0.5449.0.zip"
            AuthKey = None } ]


[<Test>]
let ``should read http file references from config``() =
    let config = """http file:///c:/code/uen/settings-gitlab.fsx"""
    let dependencies = DependenciesFile.FromSource(config)
    dependencies.Groups.[Constants.MainDependencyGroup].RemoteFiles
    |> shouldEqual
        [ { Owner = ""
            Project = ""
            Name = "settings-gitlab.fsx"
            Origin = ModuleResolver.Origin.HttpLink "file://"
            Command = None
            OperatingSystemRestriction = None
            PackagePath = None
            Version = VersionRestriction.Concrete "/c:/code/uen/settings-gitlab.fsx"
            AuthKey = None } ]


let configWithoutVersions = """
source "http://www.nuget.org/api/v2"

nuget Castle.Windsor-log4net
nuget Rx-Main
nuget "FAKE"
"""

[<Test>]
let ``should read config without versions``() = 
    let cfg = DependenciesFile.FromSource(configWithoutVersions)

    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "Rx-Main"] .Range|> shouldEqual (VersionRange.AtLeast "0")
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "Castle.Windsor-log4net"].Range |> shouldEqual (VersionRange.AtLeast "0")
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "FAKE"].Range |> shouldEqual (VersionRange.AtLeast "0")


let configWithPasswordNoAuthType = """
source http://www.nuget.org/api/v2 username: "tatü tata" password: "you got hacked!"
nuget Rx-Main
"""

[<Test>]
let ``should read config with encapsulated password source with no auth type specified``() = 
    let cfg = DependenciesFile.FromSource(configWithPasswordNoAuthType)
    
    cfg.Groups.[Constants.MainDependencyGroup].Sources
    |> shouldEqual [ 
        PackageSource.NuGetV2 { 
            Url = "http://www.nuget.org/api/v2"
            Authentication = AuthProvider.empty } ]
    cfg.Groups.[Constants.MainDependencyGroup].Sources.Head.Auth.Retrieve true
        |> shouldEqual (Some (Credentials{ Username = "tatü tata"; Password = "you got hacked!"; Type = NetUtils.AuthType.Basic}))

let configWithPasswordWithAuthType = """
source http://www.nuget.org/api/v2 username: "tatü tata" password: "you got hacked!" authtype: "ntlm"
nuget Rx-Main
"""

[<Test>]
let ``should read config with encapsulated password source and auth type specified``() = 
    let cfg = DependenciesFile.FromSource(configWithPasswordWithAuthType)
    
    cfg.Groups.[Constants.MainDependencyGroup].Sources 
    |> shouldEqual [ 
        PackageSource.NuGetV2 { 
            Url = "http://www.nuget.org/api/v2"
            Authentication = AuthProvider.ofUserPassword { Username = "tatü tata"; Password = "you got hacked!"; Type = NetUtils.AuthType.NTLM} } ]

let configWithPasswordInSingleQuotes = """
source http://www.nuget.org/api/v2 username: 'tatü tata' password: 'you got hacked!'
nuget Rx-Main
"""

[<Test>]
let ``should read config with single-quoted password source``() = 
    try
        DependenciesFile.FromSource configWithPasswordInSingleQuotes |> ignore
        failwith "Expected error"
    with
    | exn when exn.Message <> "Expected error" -> ()

let configWithPasswordInEnvVariable = """
source http://www.nuget.org/api/v2 username: "%FEED_USERNAME%" password: "%FEED_PASSWORD%"
nuget Rx-Main
"""

[<Test>]
let ``should read config with password in env variable``() = 
    Environment.SetEnvironmentVariable("FEED_USERNAME", "user XYZ", EnvironmentVariableTarget.Process)
    Environment.SetEnvironmentVariable("FEED_PASSWORD", "pw Love", EnvironmentVariableTarget.Process)
    let cfg = DependenciesFile.FromSource( configWithPasswordInEnvVariable)
    
    cfg.Groups.[Constants.MainDependencyGroup].Sources 
    |> shouldEqual [ 
        PackageSource.NuGetV2 { 
            Url = "http://www.nuget.org/api/v2"
            Authentication = AuthProvider.empty} ]
    cfg.Groups.[Constants.MainDependencyGroup].Sources.Head.Auth.Retrieve true
        |> shouldEqual (Some (Credentials{ Username = "user XYZ"; Password = "pw Love"; Type = NetUtils.AuthType.Basic}))

let configWithPasswordInEnvVariableAndAuthType = """
source http://www.nuget.org/api/v2 username: "%FEED_USERNAME%" password: "%FEED_PASSWORD%" authtype: "nTlM"
nuget Rx-Main
"""

[<Test>]
let ``should read config with password in env variable and auth type specified``() = 
    Environment.SetEnvironmentVariable("FEED_USERNAME", "user XYZ", EnvironmentVariableTarget.Process)
    Environment.SetEnvironmentVariable("FEED_PASSWORD", "pw Love", EnvironmentVariableTarget.Process)
    let cfg = DependenciesFile.FromSource( configWithPasswordInEnvVariableAndAuthType)
    
    cfg.Groups.[Constants.MainDependencyGroup].Sources 
    |> shouldEqual [ 
        PackageSource.NuGetV2 { 
            Url = "http://www.nuget.org/api/v2"
            Authentication = AuthProvider.empty } ]
    cfg.Groups.[Constants.MainDependencyGroup].Sources.Head.Auth.Retrieve true
        |> shouldEqual (Some (Credentials{ Username = "user XYZ"; Password = "pw Love"; Type = NetUtils.AuthType.NTLM}))

let configWithExplicitVersions = """
source "http://www.nuget.org/api/v2"

nuget FSharp.Compiler.Service == 0.0.62 
nuget FsReveal == 0.0.5-beta
"""

[<Test>]
let ``should read config explicit versions``() = 
    let cfg = DependenciesFile.FromSource(configWithExplicitVersions)

    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "FSharp.Compiler.Service"].Range |> shouldEqual (VersionRange.OverrideAll (SemVer.Parse "0.0.62"))
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "FsReveal"].Range |> shouldEqual (VersionRange.OverrideAll (SemVer.Parse "0.0.5-beta"))

let configWithLocalSource = """
source ./nugets

nuget Nancy.Owin 0.22.2
"""

[<Test>]
let ``should read config with local source``() = 
    let cfg = DependenciesFile.FromSource(configWithLocalSource)

    let p = cfg.Groups.[Constants.MainDependencyGroup].Packages |> List.find (fun x-> x.Name = PackageName "Nancy.Owin")
    p.VersionRequirement.Range |> shouldEqual (VersionRange.Specific (SemVer.Parse "0.22.2"))
    p.Settings.FrameworkRestrictions |> getExplicitRestriction |> shouldEqual FrameworkRestriction.NoRestriction


[<Test>]
let ``should read config with package name containing nuget``() = 
    let config = """
    nuget nuget.Core 0.1
    """
    let cfg = DependenciesFile.FromSource(config)

    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "nuget.Core"].Range |> shouldEqual (VersionRange.Specific (SemVer.Parse "0.1"))

[<Test>]
let ``should read config with single framework restriction``() = 
    let config = """
    nuget Foobar 1.2.3 framework: >= net40
    """
    let cfg = DependenciesFile.FromSource(config)

    let p = cfg.Groups.[Constants.MainDependencyGroup].Packages |> List.find (fun x-> x.Name = PackageName "Foobar")
    p.VersionRequirement.Range |> shouldEqual (VersionRange.Specific (SemVer.Parse "1.2.3"))
    p.Settings.FrameworkRestrictions |> getExplicitRestriction |> shouldEqual (FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4)))
    p.Settings.ImportTargets |> shouldEqual None


[<Test>]
let ``should read config with framework restriction``() = 
    let config = """
    nuget Foobar 1.2.3 alpha beta framework: net35, >= net40
    """
    let cfg = DependenciesFile.FromSource(config)

    let p = cfg.Groups.[Constants.MainDependencyGroup].Packages |> List.find (fun x-> x.Name = PackageName "Foobar")
    p.VersionRequirement.Range |> shouldEqual (VersionRange.Specific (SemVer.Parse "1.2.3"))
    p.Settings.FrameworkRestrictions |> getExplicitRestriction |> shouldEqual (makeOrList [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V3_5)); FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4))] |> getExplicitRestriction)
    p.Settings.ImportTargets |> shouldEqual None
    p.Settings.CopyLocal |> shouldEqual None
    p.Settings.SpecificVersion |> shouldEqual None

[<Test>]
let ``should read config with no targets import``() = 
    let config = """
    nuget Foobar 1.2.3 alpha beta import_targets: false, copy_local: false, specific_version: false
    """
    let cfg = DependenciesFile.FromSource(config)

    let p = cfg.Groups.[Constants.MainDependencyGroup].Packages |> List.find (fun x-> x.Name = PackageName "Foobar")
    p.VersionRequirement.Range |> shouldEqual (VersionRange.Specific (SemVer.Parse "1.2.3"))
    p.Settings.FrameworkRestrictions |> getExplicitRestriction |> shouldEqual FrameworkRestriction.NoRestriction
    p.Settings.ImportTargets |> shouldEqual (Some false)
    p.Settings.CopyLocal |> shouldEqual (Some false)
    p.Settings.SpecificVersion |> shouldEqual (Some false)
    p.Settings.OmitContent |> shouldEqual None

[<Test>]
let ``should read config with content none``() = 
    let config = """
    nuget Foobar 1.2.3 alpha beta content: none, copy_local: false, specific_version: true
    """
    let cfg = DependenciesFile.FromSource(config)

    let p = cfg.Groups.[Constants.MainDependencyGroup].Packages |> List.find (fun x-> x.Name = PackageName "Foobar")
    p.VersionRequirement.Range |> shouldEqual (VersionRange.Specific (SemVer.Parse "1.2.3"))
    p.Settings.FrameworkRestrictions  |> getExplicitRestriction |> shouldEqual FrameworkRestriction.NoRestriction
    p.Settings.ImportTargets |> shouldEqual None
    p.Settings.CopyLocal |> shouldEqual (Some false)
    p.Settings.SpecificVersion |> shouldEqual (Some true)
    p.Settings.OmitContent |> shouldEqual (Some ContentCopySettings.Omit)

[<Test>]
let ``should read config with  !~> 3.3``() = 
    let config = """source https://www.nuget.org/api/v2

nuget AutoMapper ~> 3.2
nuget Castle.Windsor !~> 3.3
nuget DataAnnotationsExtensions 1.1.0.0
nuget EntityFramework 5.0.0
nuget FakeItEasy ~> 1.23
nuget FluentAssertions ~> 3.1
nuget Machine.Specifications ~> 0.9
nuget Machine.Specifications.Runner.Console ~> 0.9
nuget NDbfReader 1.1.1.0
nuget Newtonsoft.Json ~> 6.0
nuget Plossum.CommandLine != 0.3.0.14
nuget PostSharp 3.1.52
nuget SharpZipLib 0.86.0
nuget Topshelf ~> 3.1
nuget Caliburn.Micro !~> 2.0.2
    """
    let cfg = DependenciesFile.FromSource(config)
    ()


let configWithInvalidPrereleaseString = """
    nuget Plossum.CommandLine !0.3.0.14   
"""

[<Test>]
let ``should report error on invalid prerelease string``() = 
    try
        DependenciesFile.FromSource(configWithInvalidPrereleaseString) |> ignore
        failwith "error"
    with
    | exn -> Assert.IsTrue(exn.Message.Contains("Invalid prerelease version !0.3.0.14")) |> ignore

let html = """
<!DOCTYPE html><html><head></head></html>"
"""

[<Test>]
let ``should not read hhtml``() = 
    try
        DependenciesFile.FromSource(html) |> ignore
        failwith "error"
    with
    | exn -> Assert.IsTrue(exn.Message.Contains"Unrecognized token")

let configWithAdditionalGroup = """
source "http://www.nuget.org/api/v2"

nuget FSharp.Compiler.Service
nuget FsReveal

group Build

nuget FAKE
nuget NUnit
"""

[<Test>]
let ``should read config with additional group``() = 
    let cfg = DependenciesFile.FromSource(configWithAdditionalGroup)

    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "FSharp.Compiler.Service"].Range |> shouldEqual (VersionRange.Minimum (SemVer.Parse "0"))
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "FsReveal"].Range |> shouldEqual (VersionRange.Minimum (SemVer.Parse "0"))

    cfg.GetDependenciesInGroup(GroupName "Build").[PackageName "FAKE"].Range |> shouldEqual (VersionRange.Minimum (SemVer.Parse "0"))
    cfg.GetDependenciesInGroup(GroupName "Build").[PackageName "NUnit"].Range |> shouldEqual (VersionRange.Minimum (SemVer.Parse "0"))

let configWithNestedGroup = """
source "http://www.nuget.org/api/v2"

nuget FSharp.Compiler.Service
nuget FsReveal

group Build

    nuget FAKE
    nuget NUnit
"""

[<Test>]
let ``should read config with nested group``() = 
    let cfg = DependenciesFile.FromSource(configWithNestedGroup)

    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "FSharp.Compiler.Service"].Range |> shouldEqual (VersionRange.Minimum (SemVer.Parse "0"))
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "FsReveal"].Range |> shouldEqual (VersionRange.Minimum (SemVer.Parse "0"))

    cfg.GetDependenciesInGroup(GroupName "Build").[PackageName "FAKE"].Range |> shouldEqual (VersionRange.Minimum (SemVer.Parse "0"))
    cfg.GetDependenciesInGroup(GroupName "Build").[PackageName "NUnit"].Range |> shouldEqual (VersionRange.Minimum (SemVer.Parse "0"))

let configWithExplicitMainGroup = """
nuget Paket.Core

group Main
source "http://www.nuget.org/api/v2"

nuget FSharp.Compiler.Service
nuget FsReveal

group Build

    nuget FAKE
    nuget NUnit
"""

[<Test>]
let ``should read config with explizit main group``() = 
    let cfg = DependenciesFile.FromSource(configWithExplicitMainGroup)

    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "FSharp.Compiler.Service"].Range |> shouldEqual (VersionRange.Minimum (SemVer.Parse "0"))
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "FsReveal"].Range |> shouldEqual (VersionRange.Minimum (SemVer.Parse "0"))
    cfg.GetDependenciesInGroup(Constants.MainDependencyGroup).[PackageName "Paket.Core"].Range |> shouldEqual (VersionRange.Minimum (SemVer.Parse "0"))

    cfg.GetDependenciesInGroup(GroupName "Build").[PackageName "FAKE"].Range |> shouldEqual (VersionRange.Minimum (SemVer.Parse "0"))
    cfg.GetDependenciesInGroup(GroupName "Build").[PackageName "NUnit"].Range |> shouldEqual (VersionRange.Minimum (SemVer.Parse "0"))

let configWithReferenceCondition = """
source http://www.nuget.org/api/v2
condition: main-group

nuget Paket.Core
nuget FSharp.Compiler.Service
nuget FsReveal

group Build

    nuget FAKE redirects: on
    nuget NUnit condition: legacy
"""

[<Test>]
let ``should read config with reference condition``() = 
    let cfg = DependenciesFile.FromSource(configWithReferenceCondition)

    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.ReferenceCondition |> shouldEqual (Some "MAIN-GROUP")

    cfg.Groups.[GroupName "Build"].Packages.Head.Settings.ReferenceCondition |> shouldEqual None
    cfg.Groups.[GroupName "Build"].Packages.Head.Settings.CreateBindingRedirects |> shouldEqual (Some BindingRedirectsSettings.On)
    cfg.Groups.[GroupName "Build"].Packages.Tail.Head.Settings.ReferenceCondition |> shouldEqual (Some "LEGACY")
    cfg.Groups.[GroupName "Build"].Packages.Tail.Head.Settings.CreateBindingRedirects |> shouldEqual None


let configWithNugetV3Source = """
source https://api.nuget.org/v3/index.json

nuget Paket.Core
"""

[<Test>]
let ``should read config with NuGet v3 feed``() = 
    let cfg = DependenciesFile.FromSource(configWithNugetV3Source)

    cfg.Groups.[Constants.MainDependencyGroup].Sources.Head.Url |> shouldEqual Constants.DefaultNuGetV3Stream

let configWithNugetV3HTTPSource = """
source http://api.nuget.org/v3/index.json

nuget Paket.Core
"""

[<Test>]
let ``should read config with NuGet http v3 feed``() = 
    let cfg = DependenciesFile.FromSource(configWithNugetV3HTTPSource)

    cfg.Groups.[Constants.MainDependencyGroup].Sources.Head.Url |> shouldEqual (Constants.DefaultNuGetV3Stream.Replace("https://","http://"))

let configWithDuplicateSource = """
source https://www.nuget.org/api/v2
source https://www.nuget.org/api/v2

nuget Paket.Core
"""

[<Test>]
let ``should read config with duplicate NuGet source``() = 
    let cfg = DependenciesFile.FromSource(configWithDuplicateSource)

    cfg.Groups.[Constants.MainDependencyGroup].Sources.Length |> shouldEqual 1
    cfg.Groups.[Constants.MainDependencyGroup].Sources.Head |> shouldEqual PackageSources.DefaultNuGetSource

let configWithInvalidInstallSettings = """
source https://www.nuget.org/api/v2

nuget ABCpdf 10.1.0.3 framework: >= net40 content: none
nuget log4net 2.0.0
nuget Oracle.ManagedDataAccess framework: >= net40 content: none
"""

[<Test>]
let ``should not read config with invalid settings``() = 
    shouldFail (fun () -> DependenciesFile.FromSource(configWithInvalidInstallSettings) |> ignore)

let strategyConfig = sprintf """
strategy %s
source "http://www.nuget.org/api/v2" // first source

nuget FAKE ~> 3.0

group Test
    strategy %s
    nuget NUnit
"""

[<Test>]
let ``should read config with min and max strategy``() = 
    let cfg = DependenciesFile.FromSource(strategyConfig "min" "max")
    cfg.Groups.[Constants.MainDependencyGroup].Options.ResolverStrategyForTransitives |> shouldEqual (Some ResolverStrategy.Min)
    cfg.Groups.[GroupName "Test"].Options.ResolverStrategyForTransitives |> shouldEqual (Some ResolverStrategy.Max)

    cfg.Groups.[Constants.MainDependencyGroup].Sources |> shouldEqual [PackageSource.NuGetV2Source "http://www.nuget.org/api/v2"]
    
let noStrategyConfig = sprintf """
strategy %s
source "http://www.nuget.org/api/v2" // first source

nuget FAKE ~> 3.0

group Test
    nuget NUnit
"""

[<Test>]
let ``should read config with min and no strategy``() = 
    let cfg = DependenciesFile.FromSource(noStrategyConfig "min")
    cfg.Groups.[Constants.MainDependencyGroup].Options.ResolverStrategyForTransitives |> shouldEqual (Some ResolverStrategy.Min)
    cfg.Groups.[GroupName "Test"].Options.ResolverStrategyForTransitives |> shouldEqual None

    cfg.Groups.[Constants.MainDependencyGroup].Sources |> shouldEqual [PackageSource.NuGetV2Source "http://www.nuget.org/api/v2"]

let noStrategyConfig' = sprintf """
source "http://www.nuget.org/api/v2" // first source

nuget FAKE ~> 3.0

group Test
    nuget NUnit
"""

[<Test>]
let ``should read config with no strategy``() = 
    let cfg = DependenciesFile.FromSource(noStrategyConfig')
    cfg.Groups.[Constants.MainDependencyGroup].Options.ResolverStrategyForTransitives |> shouldEqual None
    cfg.Groups.[GroupName "Test"].Options.ResolverStrategyForTransitives |> shouldEqual None

    cfg.Groups.[Constants.MainDependencyGroup].Sources |> shouldEqual [PackageSource.NuGetV2Source "http://www.nuget.org/api/v2"]

let combinedStrategyConfig = sprintf """
strategy min
source "http://www.nuget.org/api/v2" // first source

nuget FAKE ~> 3.0

group Test
    nuget NUnit

group Main
    nuget Paket.Core

group Build
    strategy min
    nuget FAKE

group Test
    strategy min
    nuget Package

group Build
    strategy max
    nuget NUnit
"""

[<Test>]
let ``should read config with combined strategy``() = 
    let cfg = DependenciesFile.FromSource(combinedStrategyConfig)
    cfg.Groups.[Constants.MainDependencyGroup].Options.ResolverStrategyForTransitives |> shouldEqual (Some ResolverStrategy.Min)
    cfg.Groups.[GroupName "Test"].Options.ResolverStrategyForTransitives |> shouldEqual (Some ResolverStrategy.Min)
    cfg.Groups.[GroupName "Build"].Options.ResolverStrategyForTransitives |> shouldEqual (Some ResolverStrategy.Max)

    cfg.Groups.[Constants.MainDependencyGroup].Sources |> shouldEqual [PackageSource.NuGetV2Source "http://www.nuget.org/api/v2"]

let configWithVerySimilarFeeds = """
source http://nexus1:8081/nexus/service/local/nuget/nuget-repo
source http://nexus2:8081/nexus/service/local/nuget/nuget-repo  username: "xxx" password: "yyy"

nuget FSharp.Compiler.Service
nuget FsReveal
"""

[<Test>]
let ``should read config with very similar feeds``() = 
    let cfg = DependenciesFile.FromSource(configWithVerySimilarFeeds)

    try
        cfg.Groups.[Constants.MainDependencyGroup].Sources.Head.Auth.Retrieve true |> shouldEqual None
    with e ->
        System.Console.Error.WriteLine("Credential Provider failed: " + e.Message)
        () // Might throw when we have a global authentication provider
    
    cfg.Groups.[Constants.MainDependencyGroup].Sources.Head.Url |> shouldEqual "http://nexus1:8081/nexus/service/local/nuget/nuget-repo"

    try
        cfg.Groups.[Constants.MainDependencyGroup].Sources.Tail.Head.Auth.Retrieve false |> shouldNotEqual None
    with e -> 
        System.Console.Error.WriteLine("Credential Provider failed: " + e.Message)
        () // Might throw when we have a global authentication provider
    cfg.Groups.[Constants.MainDependencyGroup].Sources.Tail.Head.Url |> shouldEqual "http://nexus2:8081/nexus/service/local/nuget/nuget-repo"

let configTargetFramework = """source https://www.nuget.org/api/v2

framework: >=net40

nuget System.Data.SQLite 1.0.98.1 content: none
"""

[<Test>]
let ``should read config with target framework``() = 
    let cfg = DependenciesFile.FromSource(configTargetFramework)

    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.FrameworkRestrictions
    |> getExplicitRestriction
    |> shouldEqual (FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4)))

let configNET5TargetFramework = """source https://www.nuget.org/api/v2

framework: >= net5.0

nuget System.Data.SQLite 1.0.98.1 content: none
"""

[<Test>]
let ``should read config with .NET 5 target framework``() = 
    let cfg = DependenciesFile.FromSource(configNET5TargetFramework)

    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.FrameworkRestrictions
    |> getExplicitRestriction
    |> shouldEqual (FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V5)))

let configNET6TargetFramework = """source https://www.nuget.org/api/v2

framework: >= net6.0

nuget System.Data.SQLite 1.0.98.1 content: none
"""

[<Test>]
let ``should read config with .NET 6 target framework``() = 
    let cfg = DependenciesFile.FromSource(configNET6TargetFramework)

    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.FrameworkRestrictions
    |> getExplicitRestriction
    |> shouldEqual (FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V6)))

let configNET7TargetFramework = """source https://www.nuget.org/api/v2

framework: >= net7.0

nuget System.Data.SQLite 1.0.98.1 content: none
"""

[<Test>]
let ``should read config with .NET 7 target framework``() = 
    let cfg = DependenciesFile.FromSource(configNET7TargetFramework)

    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.FrameworkRestrictions
    |> getExplicitRestriction
    |> shouldEqual (FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V7)))

let validFrameworks =
    let net40 = DotNetFramework(FrameworkVersion.V4)
    let net45 = DotNetFramework(FrameworkVersion.V4_5)
    let profile78 = TargetProfile.PortableProfile PortableProfileType.Profile78
    let exactly  = FrameworkRestriction.Exactly
    let min  = FrameworkRestriction.AtLeast
    let minPlatform = FrameworkRestriction.AtLeastPlatform
    let between x y  = FrameworkRestriction.Between(x, y)
    let any = Seq.fold FrameworkRestriction.combineRestrictionsWithOr FrameworkRestriction.EmptySet

    [ TestCaseData("framework: net40").Returns(exactly net40)
      TestCaseData("frameworks: net40").Returns(exactly net40)
      TestCaseData("framework net40").Returns(exactly net40)
      TestCaseData("frameworks net40").Returns(exactly net40)
      TestCaseData("framework: = net40").Returns(exactly net40)
      TestCaseData("framework: =net40").Returns(exactly net40)
      TestCaseData("framework: portable-windows8+net45+wp8").Returns(minPlatform profile78)
      TestCaseData("framework: >= portable-windows8+net45+wp8").Returns(minPlatform profile78)
      TestCaseData("framework: >=portable-windows8+net45+wp8").Returns(minPlatform profile78)
      TestCaseData("framework: net40,net45").Returns(any [exactly net40; exactly net45])
      TestCaseData("framework: net40, net45").Returns(any [exactly net40; exactly net45])
      TestCaseData("framework: net40, >= net45").Returns(any [exactly net40; min net45])
      TestCaseData("framework: net40,>=net45").Returns(any [exactly net40; min net45])
      TestCaseData("framework: >= net40 <= net45").Returns(between net40 net45)
      TestCaseData("framework: >=net40<=net45").Returns(between net40 net45)
      TestCaseData("framework: >= net40 < net45").Returns(between net40 net45)
      TestCaseData("framework: >=net40<net45").Returns(between net40 net45)]

[<Test>]
[<TestCaseSource("validFrameworks")>]
let ``should read config with valid target framework`` config =
    let cfg = DependenciesFile.FromSource(config)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.FrameworkRestrictions |> getExplicitRestriction

[<TestCase("framework: netstandard 2.0")>]
[<TestCase("framework: = foo")>]
[<TestCase("framework: == net40")>]
[<TestCase("framework: > net40")>]
[<TestCase("framework: >= net40 = net45")>]
[<TestCase("framework: > net40 <= net45")>]
[<TestCase("framework: = net40 <= net45")>]
[<TestCase("framework: = portable-windows8+net45+wp8")>]
[<TestCase("framework: >= portable-windows8+net45+wp8 <= net45")>]
[<TestCase("framework: net40 net45")>]
let ``should throw on config with invalid target framework`` config =
    shouldFail<Exception> (fun () -> DependenciesFile.FromSource(config) |> ignore)

[<Test>]
let ``should read packages with redirects``() = 
    let config = """
redirects on
source http://www.nuget.org/api/v2

nuget Paket.Core redirects: on
nuget FSharp.Compiler.Service redirects: Off
nuget FsReveal redirects: foRce
nuget FAKE

group Build
redirects off

    nuget FAKE redirects: on
    nuget NUnit redirects: force
    nuget Paket.Core redirects: off
    nuget FsReveal
    """

    let cfg = DependenciesFile.FromSource(config)

    cfg.Groups.[Constants.MainDependencyGroup].Options.Redirects |> shouldEqual (Some BindingRedirectsSettings.On)

    cfg.Groups.[Constants.MainDependencyGroup].Packages.Head.Settings.CreateBindingRedirects |> shouldEqual (Some BindingRedirectsSettings.On)
    cfg.Groups.[Constants.MainDependencyGroup].Packages.Tail.Head.Settings.CreateBindingRedirects |> shouldEqual (Some BindingRedirectsSettings.Off)
    cfg.Groups.[Constants.MainDependencyGroup].Packages.Tail.Tail.Head.Settings.CreateBindingRedirects |> shouldEqual (Some BindingRedirectsSettings.Force)
    cfg.Groups.[Constants.MainDependencyGroup].Packages.Tail.Tail.Tail.Head.Settings.CreateBindingRedirects |> shouldEqual None

    cfg.Groups.[GroupName "Build"].Options.Redirects |> shouldEqual (Some BindingRedirectsSettings.Off)
    cfg.Groups.[GroupName "Build"].Packages.Head.Settings.CreateBindingRedirects |> shouldEqual (Some BindingRedirectsSettings.On)
    cfg.Groups.[GroupName "Build"].Packages.Tail.Head.Settings.CreateBindingRedirects |> shouldEqual (Some BindingRedirectsSettings.Force)
    cfg.Groups.[GroupName "Build"].Packages.Tail.Tail.Head.Settings.CreateBindingRedirects |> shouldEqual (Some BindingRedirectsSettings.Off)
    cfg.Groups.[GroupName "Build"].Packages.Tail.Tail.Tail.Head.Settings.CreateBindingRedirects |> shouldEqual None

let paketGitConfig = """
git git@github.com:fsprojects/Paket.git
git file:///c:/code/Paket.VisualStudio
git https://github.com/fsprojects/Paket.git
git http://github.com/fsprojects/Chessie.git master
"""

[<Test>]
let ``should read paket git config``() = 
    let cfg = DependenciesFile.FromSource(paketGitConfig)
    let gitSource = cfg.Groups.[Constants.MainDependencyGroup].RemoteFiles.Head
    gitSource.GetCloneUrl() |> shouldEqual "git@github.com:fsprojects/Paket.git"
    gitSource.Owner |> shouldEqual "github.com/fsprojects"
    gitSource.Version |> shouldEqual VersionRestriction.NoVersionRestriction
    gitSource.Command |> shouldEqual None
    gitSource.OperatingSystemRestriction |> shouldEqual None
    gitSource.PackagePath |> shouldEqual None
    gitSource.Project |> shouldEqual "Paket"

    let localGitSource = cfg.Groups.[Constants.MainDependencyGroup].RemoteFiles.Tail.Head
    localGitSource.GetCloneUrl() |> shouldEqual "file:///c:/code/Paket.VisualStudio"
    localGitSource.Project |> shouldEqual "Paket.VisualStudio"
    localGitSource.Version |> shouldEqual VersionRestriction.NoVersionRestriction
    localGitSource.Owner |> shouldEqual "localfilesystem"

    let httpsGitSource = cfg.Groups.[Constants.MainDependencyGroup].RemoteFiles.Tail.Tail.Head
    httpsGitSource.GetCloneUrl() |> shouldEqual "https://github.com/fsprojects/Paket.git"
    httpsGitSource.Version |> shouldEqual VersionRestriction.NoVersionRestriction
    httpsGitSource.Project |> shouldEqual "Paket"
    httpsGitSource.Owner |> shouldEqual "github.com/fsprojects"

    let branchGitSource = cfg.Groups.[Constants.MainDependencyGroup].RemoteFiles.Tail.Tail.Tail.Head
    branchGitSource.GetCloneUrl() |> shouldEqual "http://github.com/fsprojects/Chessie.git"
    branchGitSource.Version |> shouldEqual (VersionRestriction.Concrete "master")
    branchGitSource.Project |> shouldEqual "Chessie"
    branchGitSource.Owner |> shouldEqual "github.com/fsprojects"

let paketGitConfigWithBuildCommand = """
source https://nuget.org/api/v2

nuget Newtonsoft.Json
nuget FSharp.Core

group Dev

    git https://github.com/fsprojects/Paket.git master build:"build.cmd NuGet", Packages: /temp/
    git https://github.com/fsprojects/Paket.VisualStudio.git os : Windows, Build:"build.cmd NuGet", Packages: "/tempFolder/Any where"
    git https://github.com/fsprojects/Paket.git Packages: "/temp Folder/Any where", os: OSX
    git https://github.com/forki/nupkgtest.git nugetsource Packages: /source/
    git https://github.com/forki/nupkgtest.git build build:"build.cmd", Packages: /source/

    nuget Argu
    nuget Paket.Core
"""

[<Test>]
let ``should read paket git config with build command``() = 
    let cfg = DependenciesFile.FromSource(paketGitConfigWithBuildCommand)
    let gitSource = cfg.Groups.[GroupName "Dev"].RemoteFiles.Head
    gitSource.GetCloneUrl() |> shouldEqual "https://github.com/fsprojects/Paket.git"
    gitSource.Owner |> shouldEqual "github.com/fsprojects"
    gitSource.Version |> shouldEqual (VersionRestriction.Concrete "master")
    gitSource.Command |> shouldEqual (Some "build.cmd NuGet")
    gitSource.OperatingSystemRestriction |> shouldEqual None
    gitSource.PackagePath |> shouldEqual (Some "/temp/")
    gitSource.Project |> shouldEqual "Paket"

    let gitVSSource = cfg.Groups.[GroupName "Dev"].RemoteFiles.Tail.Head
    gitVSSource.GetCloneUrl() |> shouldEqual "https://github.com/fsprojects/Paket.VisualStudio.git"
    gitVSSource.Owner |> shouldEqual "github.com/fsprojects"
    gitVSSource.Version |> shouldEqual VersionRestriction.NoVersionRestriction
    gitVSSource.Project |> shouldEqual "Paket.VisualStudio"
    gitVSSource.PackagePath |> shouldEqual (Some "/tempFolder/Any where")
    gitVSSource.Command |> shouldEqual (Some "build.cmd NuGet")
    gitVSSource.OperatingSystemRestriction |> shouldEqual (Some "Windows")

    let noBuildSource = cfg.Groups.[GroupName "Dev"].RemoteFiles.Tail.Tail.Head
    noBuildSource.GetCloneUrl() |> shouldEqual "https://github.com/fsprojects/Paket.git"
    noBuildSource.Owner |> shouldEqual "github.com/fsprojects"
    noBuildSource.Version |> shouldEqual VersionRestriction.NoVersionRestriction
    noBuildSource.Project |> shouldEqual "Paket"
    noBuildSource.PackagePath |> shouldEqual (Some "/temp Folder/Any where")
    noBuildSource.Command |> shouldEqual None
    noBuildSource.OperatingSystemRestriction |> shouldEqual (Some "OSX")

    let packagesSource = cfg.Groups.[GroupName "Dev"].RemoteFiles.Tail.Tail.Tail.Head
    packagesSource.GetCloneUrl() |> shouldEqual "https://github.com/forki/nupkgtest.git"
    packagesSource.Owner |> shouldEqual "github.com/forki"
    packagesSource.Version |> shouldEqual (VersionRestriction.Concrete "nugetsource")
    packagesSource.Project |> shouldEqual "nupkgtest"
    packagesSource.PackagePath |> shouldEqual (Some "/source/")
    packagesSource.Command |> shouldEqual None
    packagesSource.OperatingSystemRestriction |> shouldEqual None

    let nupkgtestSource = cfg.Groups.[GroupName "Dev"].Sources.Head
    nupkgtestSource.Url |> shouldEqual "paket-files/dev/github.com/forki/nupkgtest/source"

    
    let buildSource = cfg.Groups.[GroupName "Dev"].RemoteFiles.Tail.Tail.Tail.Tail.Head
    buildSource.GetCloneUrl() |> shouldEqual "https://github.com/forki/nupkgtest.git"
    buildSource.Owner |> shouldEqual "github.com/forki"
    buildSource.Version |> shouldEqual (VersionRestriction.Concrete "build")
    buildSource.Project |> shouldEqual "nupkgtest"
    buildSource.PackagePath |> shouldEqual (Some "/source/")
    buildSource.Command |> shouldEqual (Some "build.cmd")
    buildSource.OperatingSystemRestriction |> shouldEqual None
 
    cfg.Groups.[GroupName "Dev"].Sources
    |> List.map (fun x -> x.Url)
    |> shouldContain "paket-files/dev/github.com/forki/nupkgtest/source"


let paketGitTagsConfig = """
group Git

    git https://github.com/fsprojects/Paket.git >= 2.0.0 build:"build.cmd NuGet", Packages: /temp/
    git https://github.com/fsprojects/Paket.VisualStudio.git 2.0.0 build:"build.cmd NuGet", Packages: /temp/
    git https://github.com/fsprojects/Paket.git ~> 3.2.1 prerelease build:"build.cmd NuGet", Packages: /temp/
    git https://github.com/fsprojects/Paket.git >= 1.2.3 < 1.5 build:"build.cmd NuGet", Packages: /temp/
    git https://github.com/fsprojects/Paket.git >= 1.2.3 < 1.5 alpha build:"build.cmd NuGet", Packages: /temp/

    nuget Argu
    nuget Paket.Core
"""

[<Test>]
let ``should read paket git config with tags``() = 
    let cfg = DependenciesFile.FromSource(paketGitTagsConfig)
    
    let gitSource = cfg.Groups.[GroupName "git"].RemoteFiles.Head
    gitSource.GetCloneUrl() |> shouldEqual "https://github.com/fsprojects/Paket.git"
    gitSource.Owner |> shouldEqual "github.com/fsprojects"
    gitSource.Version |> shouldEqual (VersionRestriction.VersionRequirement (DependenciesFileParser.parseVersionRequirement ">= 2.0.0"))
    gitSource.Command |> shouldEqual (Some "build.cmd NuGet")
    gitSource.OperatingSystemRestriction |> shouldEqual None
    gitSource.PackagePath |> shouldEqual (Some "/temp/")
    gitSource.Project |> shouldEqual "Paket"

    let gitSource = cfg.Groups.[GroupName "git"].RemoteFiles.Tail.Head
    gitSource.GetCloneUrl() |> shouldEqual "https://github.com/fsprojects/Paket.VisualStudio.git"
    gitSource.Owner |> shouldEqual "github.com/fsprojects"
    gitSource.Version |> shouldEqual (VersionRestriction.VersionRequirement (DependenciesFileParser.parseVersionRequirement "= 2.0.0"))
    gitSource.Command |> shouldEqual (Some "build.cmd NuGet")
    gitSource.OperatingSystemRestriction |> shouldEqual None
    gitSource.PackagePath |> shouldEqual (Some "/temp/")
    gitSource.Project |> shouldEqual "Paket.VisualStudio"

    let gitSource = cfg.Groups.[GroupName "git"].RemoteFiles.Tail.Tail.Head
    gitSource.GetCloneUrl() |> shouldEqual "https://github.com/fsprojects/Paket.git"
    gitSource.Owner |> shouldEqual "github.com/fsprojects"
    gitSource.Version |> shouldEqual (VersionRestriction.VersionRequirement (DependenciesFileParser.parseVersionRequirement "~> 3.2.1 prerelease"))
    gitSource.Command |> shouldEqual (Some "build.cmd NuGet")
    gitSource.OperatingSystemRestriction |> shouldEqual None
    gitSource.PackagePath |> shouldEqual (Some "/temp/")
    gitSource.Project |> shouldEqual "Paket"
    
    let gitSource = cfg.Groups.[GroupName "git"].RemoteFiles.Tail.Tail.Tail.Head
    gitSource.GetCloneUrl() |> shouldEqual "https://github.com/fsprojects/Paket.git"
    gitSource.Owner |> shouldEqual "github.com/fsprojects"
    gitSource.Version |> shouldEqual (VersionRestriction.VersionRequirement (DependenciesFileParser.parseVersionRequirement ">= 1.2.3 < 1.5"))
    gitSource.Command |> shouldEqual (Some "build.cmd NuGet")
    gitSource.OperatingSystemRestriction |> shouldEqual None
    gitSource.PackagePath |> shouldEqual (Some "/temp/")
    gitSource.Project |> shouldEqual "Paket"
    
    let gitSource = cfg.Groups.[GroupName "git"].RemoteFiles.Tail.Tail.Tail.Tail.Head
    gitSource.GetCloneUrl() |> shouldEqual "https://github.com/fsprojects/Paket.git"
    gitSource.Owner |> shouldEqual "github.com/fsprojects"
    gitSource.Version |> shouldEqual (VersionRestriction.VersionRequirement (DependenciesFileParser.parseVersionRequirement ">= 1.2.3 < 1.5 alpha"))
    gitSource.Command |> shouldEqual (Some "build.cmd NuGet")
    gitSource.OperatingSystemRestriction |> shouldEqual None
    gitSource.PackagePath |> shouldEqual (Some "/temp/")
    gitSource.Project |> shouldEqual "Paket"


let simpleCacheConfig = """
source https://nuget.org/api/v2
cache ./dependencies versions:current
cache //hive/dependencies versions:all

nuget Newtonsoft.Json redirects: force
nuget Argu
nuget FSharp.Core redirects: force
nuget Chessie
"""

[<Test>]
let ``should read config with caches``() = 
    let cfg = DependenciesFile.FromSource(simpleCacheConfig)
    let main = cfg.Groups.[Constants.MainDependencyGroup]
    main.Caches |> List.length |> shouldEqual 2
    (main.Caches |> List.head).Location |> shouldEqual "./dependencies"
    (main.Caches |> List.head).CacheType |> shouldEqual (Some CacheType.CurrentVersion)
    (main.Caches |> List.item 1).Location |> shouldEqual "//hive/dependencies"
    (main.Caches |> List.item 1).CacheType |> shouldEqual (Some CacheType.AllVersions)

    (main.Sources |> List.item 0) |> shouldEqual PackageSources.DefaultNuGetSource
    (main.Sources |> List.item 1).Url |> shouldEqual "./dependencies"
    (main.Sources |> List.item 2).Url |> shouldEqual "//hive/dependencies"

[<Test>]
let ``async cache should work``() =
     // this test is already include in the to Visualfsharp repo
     let x = ref 0
     let someSlowFunc mykey = async { 
         Console.WriteLine "Simulated downloading..."
         do! Async.Sleep 400
         Console.WriteLine "Simulated downloading Done."
         x := !x + 1 // Side effect!
         return "" }
     let memFunc = memoizeAsync <| someSlowFunc
     async {
         do! memFunc "a" |> Async.Ignore
         do! memFunc "a" |> Async.Ignore
         do! memFunc "a" |> Async.Ignore
         do! [|1 .. 30|] |> Seq.map(fun _ -> (memFunc "a")) 
             |> Async.Parallel |> Async.Ignore
         for i = 1 to 30 do
             Async.Start( memFunc "a" |> Async.Ignore )
             Async.Start( memFunc "a" |> Async.Ignore )
         do! Async.Sleep 500
         do! memFunc "a" |> Async.Ignore
         do! memFunc "a" |> Async.Ignore
         for i = 1 to 30 do
             Async.Start( memFunc "a" |> Async.Ignore )
         do! [|1 .. 30|] |> Seq.map(fun _ -> (memFunc "a")) 
             |> Async.Parallel |> Async.Ignore
     } |> Async.RunSynchronously
     !x |> shouldEqual 1

let autodetectconfig = """
framework: auto-detect
source https://api.nuget.org/v3/index.json
nuget nlog

group build
framework: net4.5.2
source https://www.nuget.org/api/v2

nuget GitVersion

group tests
framework: net4.5.2
source https://www.nuget.org/api/v2

nuget xunit
"""

[<Test>]
let ``should read autodetect from main group``() = 
    let cfg = DependenciesFile.FromSource(autodetectconfig)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.FrameworkRestrictions 
    |> shouldEqual FrameworkRestrictions.AutoDetectFramework

let autodetectconfigSpecific = """
//`auto-detect` with explicit 'Main' group fails
group Main
framework: auto-detect
source https://api.nuget.org/v3/index.json
nuget nlog

group build
framework: net4.5.2
source https://www.nuget.org/api/v2

nuget GitVersion

group tests
framework: net4.5.2
source https://www.nuget.org/api/v2

nuget xunit
"""

[<Test>]
let ``should read autodetect from specific main group``() = 
    let cfg = DependenciesFile.FromSource(autodetectconfigSpecific)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Settings.FrameworkRestrictions 
    |> shouldEqual FrameworkRestrictions.AutoDetectFramework

[<Test>]
let ``parsing generate load scripts`` () =
    let casesAndExpectation = [
      Some "true"  , Some true
      Some "on"    , Some true
      Some "false" , Some false
      Some "off"   , Some false
      None         , None
    ]
    let results = [
        for case, expectation in casesAndExpectation do
            let config = 
                DependenciesFile.FromSource <|
                match case with
                | Some value ->
                    sprintf """
      source https://nuget.org/api/v2
      generate_load_scripts: %s""" value
                | None ->
                    """
      source https://nuget.org/api/v2
      """
            let result = config.Groups.[Constants.MainDependencyGroup].Options.Settings.GenerateLoadScripts
            yield (result = expectation), (case, expectation, result)
    ]

    let failedResults = results |> Seq.filter (fst >> not)
    if failedResults |> (Seq.isEmpty >> not) then
        for _, (case, expectation, result) in failedResults do
            printfn "case %A expected %A got %A" case expectation result
        failwith "failed"


let configWithCLitTool = """
source https://www.nuget.org/api/v2

clitool dotnet-fable
nuget FAKE
"""

[<Test>]
let ``should read config with cli tool``() = 
    let cfg = DependenciesFile.FromSource(configWithCLitTool)
    cfg.Groups.[Constants.MainDependencyGroup].Options.Strict |> shouldEqual false

    cfg.Groups.[Constants.MainDependencyGroup].Sources 
    |> shouldEqual [PackageSources.DefaultNuGetSource]

    let tool = cfg.Groups.[Constants.MainDependencyGroup].Packages.Head
    let nuget = cfg.Groups.[Constants.MainDependencyGroup].Packages.Tail.Head
    tool.Kind |> shouldEqual PackageRequirementKind.DotnetCliTool
    nuget.Kind |> shouldEqual PackageRequirementKind.Package

let paketCacheTestPath = System.IO.Path.Combine("~", ".paket-cache")
let configWithHomePathInCache = String.Concat("""
source https://www.nuget.org/api/v2

cache """, paketCacheTestPath)

[<Test>]
let ``should normalize home path``() =
    let expected = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".paket-cache")
    paketCacheTestPath |> Paket.Utils.normalizeHomeDirectory |> shouldEqual expected

[<Test>]
let ``should parse config with home path in cache``() =
    let cfg = DependenciesFile.FromSource(configWithHomePathInCache)
    let expected = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".paket-cache")
    cfg.Groups.[Constants.MainDependencyGroup].Caches.[0].Location |> shouldEqual expected
