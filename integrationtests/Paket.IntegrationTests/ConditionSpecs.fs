module Paket.IntegrationTests.ConditionSpecs

open System.Text.RegularExpressions
open System.Xml.Linq
open System.Xml.XPath
open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics
open Paket
open Paket.Domain

let preparePackages workingDir =
    let packagesDir = workingDir @@ "Packages"

    [1..5]
    |> List.filter (fun v -> fileExists (workingDir @@ "Packages" @@ $"PaketTest2394.PackageA.%d{v}.0.0.nupkg") |> not)
    |> List.map (fun v -> directDotnet false $"pack -o . -p:Version=%d{v}.0" packagesDir)
    |> ignore

let prepareBuildProps projectDir =
    let propsFile = (projectDir @@ "Directory.Build.props")
    let xmlDoc = XDocument.Load propsFile
    let propertyGroup = xmlDoc.Descendants("PropertyGroup")
                        |> Seq.head
    propertyGroup.Add(XElement("PaketExePath", paketToolPath |> snd))
    xmlDoc.Save propsFile

let dotnetRun projectDir project configuration =
    match configuration with
    | Some config -> directDotnet false $"run --project %s{project} --configuration %s{config}" projectDir
    | None -> directDotnet false $"run --project %s{project}" projectDir
    |> Seq.map (_.Message)

let dotnetPack projectDir project configuration =
    match configuration with
    | Some config -> directDotnet false $"pack %s{project} --configuration %s{config} -o ." projectDir
    | None -> directDotnet false $"pack %s{project} -o ." projectDir
    |> ignore

let private shouldIncludeVersionedString (pattern: string) (version: int) (inputs: string seq) =
    let expected = pattern.Replace("XX", version.ToString())
    let regex = $"""^%s{Regex.Escape(pattern).Replace("XX", "(\d)")}$"""

    inputs
    |> Seq.filter (fun input -> Regex.IsMatch(input, regex))
    |> Seq.iter (fun input -> Assert.That(input, Is.EqualTo expected))

let private shouldIncludePackageA v i = shouldIncludeVersionedString "PackageA XX.0" v i
let private shouldIncludePackageB v i = shouldIncludeVersionedString "PackageB XX.0 (references PackageB.Transient XX.0)" v i
let private shouldIncludePackageBTransient v i = shouldIncludeVersionedString "PackageB.Transient XX.0" v i
let private shouldIncludeConstant v i = shouldIncludeVersionedString "Constant PACKAGEA_XX set" v i

[<Test>]
let ``#2394 default group with no condition`` () =
    let scenario = "i002394-group-conditions"
    preparePackages (originalScenarioPath scenario)

    use __ = prepare scenario
    let root = scenarioTempPath scenario
    let projectDir = root @@ "TestProjects"
    prepareBuildProps projectDir

    directPaketInPath "install" projectDir |> ignore
    let output = dotnetRun projectDir "MainGroup.fsproj" None
    dotnetPack projectDir "MainGroup.fsproj" None

    output |> shouldIncludePackageA 1
    output |> shouldIncludePackageB 1
    output |> shouldIncludePackageBTransient 1
    output |> shouldIncludeConstant 1

    let nupkgPath = projectDir @@ "MainGroup.1.0.0.nupkg"

    if File.Exists nupkgPath |> not then Assert.Fail $"Expected '%s{nupkgPath}' to exist"
    let nuspec = NuGetCache.getNuSpecFromNupkg nupkgPath
    let dependencies = nuspec.Dependencies.Value
                       |> List.map (fun (n, v, _) -> n.Name, v.Range.ToString())

    let expected = [("PaketTest2394.PackageA", ">= 1.0 < 2.0"); ("PaketTest2394.PackageB", ">= 1.0 < 2.0")]
    Assert.That(dependencies, Is.SupersetOf expected)

[<Test>]
let ``#2394 alternate group with no condition`` () =
    let scenario = "i002394-group-conditions"
    preparePackages (originalScenarioPath scenario)

    use __ = prepare scenario
    let root = scenarioTempPath scenario
    let projectDir = root @@ "TestProjects"
    prepareBuildProps projectDir

    directPaketInPath "install" projectDir |> ignore
    let output = dotnetRun projectDir "NonConditionalGroup.fsproj" None
    dotnetPack projectDir "NonConditionalGroup.fsproj" None

    output |> shouldIncludePackageA 2
    output |> shouldIncludePackageB 2
    output |> shouldIncludePackageBTransient 2
    output |> shouldIncludeConstant 2

    let nupkgPath = projectDir @@ "NonConditionalGroup.1.0.0.nupkg"

    if File.Exists nupkgPath |> not then Assert.Fail $"Expected '%s{nupkgPath}' to exist"
    let nuspec = NuGetCache.getNuSpecFromNupkg nupkgPath
    let dependencies = nuspec.Dependencies.Value
                       |> List.map (fun (n, v, _) -> n.Name, v.Range.ToString())

    let expected = [("PaketTest2394.PackageA", ">= 2.0 < 3.0"); ("PaketTest2394.PackageB", ">= 2.0 < 3.0")]
    Assert.That(dependencies, Is.SupersetOf expected)

[<Test>]
let ``#2394 group with fixed property condition`` () =
    let scenario = "i002394-group-conditions"
    preparePackages (originalScenarioPath scenario)

    use __ = prepare scenario
    let root = scenarioTempPath scenario
    let projectDir = root @@ "TestProjects"
    prepareBuildProps projectDir

    directPaketInPath "install" projectDir |> ignore
    let output = dotnetRun projectDir "FixedProperty.fsproj" None
    dotnetPack projectDir "FixedProperty.fsproj" None

    output |> shouldIncludePackageA 3
    output |> shouldIncludePackageB 3
    output |> shouldIncludePackageBTransient 3
    output |> shouldIncludeConstant 3

    let nupkgPath = projectDir @@ "FixedProperty.1.0.0.nupkg"

    if File.Exists nupkgPath |> not then Assert.Fail $"Expected '%s{nupkgPath}' to exist"
    let nuspec = NuGetCache.getNuSpecFromNupkg nupkgPath
    let dependencies = nuspec.Dependencies.Value
                       |> List.map (fun (n, v, _) -> n.Name, v.Range.ToString())

    let expected = [("PaketTest2394.PackageA", ">= 3.0 < 4.0"); ("PaketTest2394.PackageB", ">= 3.0 < 4.0")]
    Assert.That(dependencies, Is.SupersetOf expected)

[<Test>]
let ``#2394 mix dependencies from multiple groups with conditions`` () =
    let scenario = "i002394-group-conditions"
    preparePackages (originalScenarioPath scenario)

    use __ = prepare scenario
    let root = scenarioTempPath scenario
    let projectDir = root @@ "TestProjects"
    prepareBuildProps projectDir

    directPaketInPath "install" projectDir |> ignore
    let output = dotnetRun projectDir "MixedProperties.fsproj" None
    dotnetPack projectDir "MixedProperties.fsproj" None

    output |> shouldIncludePackageA 4
    output |> shouldIncludePackageB 5
    output |> shouldIncludePackageBTransient 5
    output |> shouldIncludeConstant 4

    let expected = ["PackageA 4.0"; "PackageB 5.0 (references PackageB.Transient 5.0)"; "PackageB.Transient 5.0"; "Constant PACKAGEA_4 set"]
    let rejected = ["PackageA 1.0"; "PackageB.Transient 1.0"; "Constant PACKAGEA_1 set"
                    "PackageA 2.0"; "PackageB.Transient 2.0"; "Constant PACKAGEA_2 set"
                    "PackageA 3.0"; "PackageB.Transient 3.0"; "Constant PACKAGEA_3 set"
                    "PackageA 5.0"; "PackageB.Transient 4.0"; "Constant PACKAGEA_5 set"]
    Assert.That(output, Is.SupersetOf expected)
    Assert.That(output, Is.Not.SubsetOf rejected)

    let nupkgPath = projectDir @@ "MixedProperties.1.0.0.nupkg"

    if File.Exists nupkgPath |> not then Assert.Fail $"Expected '%s{nupkgPath}' to exist"
    let nuspec = NuGetCache.getNuSpecFromNupkg nupkgPath
    let dependencies = nuspec.Dependencies.Value
                       |> List.map (fun (n, v, _) -> n.Name, v.Range.ToString())

    let expected = [("PaketTest2394.PackageA", ">= 4.0 < 5.0"); ("PaketTest2394.PackageB", ">= 5.0 < 6.0")]
    Assert.That(dependencies, Is.SupersetOf expected)

[<Test>]
let ``#2394 project with dynamic condition based on configuration 1`` () =
    let scenario = "i002394-group-conditions"
    preparePackages (originalScenarioPath scenario)

    use __ = prepare scenario
    let root = scenarioTempPath scenario
    let projectDir = root @@ "TestProjects"
    prepareBuildProps projectDir

    directPaketInPath "install" projectDir |> ignore
    let output = dotnetRun projectDir "ConfigurationDependent.fsproj" None
    dotnetPack projectDir "ConfigurationDependent.fsproj" None

    output |> shouldIncludePackageA 3
    output |> shouldIncludePackageB 3
    output |> shouldIncludePackageBTransient 3
    output |> shouldIncludeConstant 3

    let nupkgPath = projectDir @@ "ConfigurationDependent.1.0.0.nupkg"

    if File.Exists nupkgPath |> not then Assert.Fail $"Expected '%s{nupkgPath}' to exist"
    let nuspec = NuGetCache.getNuSpecFromNupkg nupkgPath
    let dependencies = nuspec.Dependencies.Value
                       |> List.map (fun (n, v, _) -> n.Name, v.Range.ToString())

    let expected = [("PaketTest2394.PackageA", ">= 3.0 < 4.0"); ("PaketTest2394.PackageB", ">= 3.0 < 4.0")]
    Assert.That(dependencies, Is.SupersetOf expected)

[<Test>]
let ``#2394 project with dynamic condition based on configuration 2`` () =
    let scenario = "i002394-group-conditions"
    preparePackages (originalScenarioPath scenario)

    use __ = prepare scenario
    let root = scenarioTempPath scenario
    let projectDir = root @@ "TestProjects"
    prepareBuildProps projectDir

    directPaketInPath "install" projectDir |> ignore
    let output = dotnetRun projectDir "ConfigurationDependent.fsproj" (Some "Alternate")
    dotnetPack projectDir "ConfigurationDependent.fsproj" (Some "Alternate")

    output |> shouldIncludePackageA 4
    output |> shouldIncludePackageB 4
    output |> shouldIncludePackageBTransient 4
    output |> shouldIncludeConstant 4

    let nupkgPath = projectDir @@ "ConfigurationDependent.1.0.0.nupkg"

    if File.Exists nupkgPath |> not then Assert.Fail $"Expected '%s{nupkgPath}' to exist"
    let nuspec = NuGetCache.getNuSpecFromNupkg nupkgPath
    let dependencies = nuspec.Dependencies.Value
                       |> List.map (fun (n, v, _) -> n.Name, v.Range.ToString())

    let expected = [("PaketTest2394.PackageA", ">= 4.0 < 5.0"); ("PaketTest2394.PackageB", ">= 4.0 < 5.0")]
    Assert.That(dependencies, Is.SupersetOf expected)
