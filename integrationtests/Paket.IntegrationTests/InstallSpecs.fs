module Paket.IntegrationTests.InstallSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics
open Paket

[<Test>]
let ``#1135 should keep lockfile stable when using framework restrictions``() = 
    let newLockFile = install "i001135-stable-install-on-framework-restrictions"
    let oldLockFile = LockFile.LoadFrom(Path.Combine(originalScenarioPath "i001135-stable-install-on-framework-restrictions","paket.lock"))
    newLockFile.ToString()
    |> shouldEqual (oldLockFile.ToString())

[<Test>]
let ``#1219 install props``() = 
    let newLockFile = install "i001219-props-files"
    let newFile = Path.Combine(scenarioTempPath "i001219-props-files","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001219-props-files","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

    let newFile = Path.Combine(scenarioTempPath "i001219-props-files","MyClassLibrary","MyClassLibrary2","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001219-props-files","MyClassLibrary","MyClassLibrary2","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

    let newFile = Path.Combine(scenarioTempPath "i001219-props-files","MyClassLibrary","MyClassLibrary3","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001219-props-files","MyClassLibrary","MyClassLibrary2","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1


[<Test>]
let ``#1487 install props stays stable``() = 
    let newLockFile = install "i001487-stable-props"
    let newFile = Path.Combine(scenarioTempPath "i001487-stable-props","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001487-stable-props","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1233 install props``() = 
    let newLockFile = install "i001233-props-files"
    let newFile = Path.Combine(scenarioTempPath "i001233-props-files","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001233-props-files","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1


[<Test>]
let ``#1233 install props with framework restrictions``() = 
    let newLockFile = install "i001233-props-fw-files"
    let newFile = Path.Combine(scenarioTempPath "i001233-props-fw-files","xUnitTests","xUnitTests.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001233-props-fw-files","xUnitTests","xUnitTests.expected.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1256 should report error in lock file``() =
    try
        install "i001256-wrong-lock" |> ignore
        failwith "error expected"
    with
    | exn when exn.Message.Contains("FAKE") && exn.Message.Contains("paket.lock") -> ()

[<Test>]
let ``#1270 install net461``() = 
    let newLockFile = install "i001270-net461"
    let newFile = Path.Combine(scenarioTempPath "i001270-net461","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001270-net461","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1427 install content once from dependencies file``() = 
    let newLockFile = install "i001427-content-once"
    let newFile = Path.Combine(scenarioTempPath "i001427-content-once","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001427-content-once","MyClassLibrary","MyClassLibrary","MyClassLibrary.expected")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1
    s1.Contains "FodyWeavers.xml" |> shouldEqual true

    let newWeavers = Path.Combine(scenarioTempPath "i001427-content-once","MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let oldWeavers = Path.Combine(originalScenarioPath "i001427-content-once","MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let s1 = File.ReadAllText oldWeavers |> normalizeLineEndings
    let s2 = File.ReadAllText newWeavers |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1427 install content once from dependencies file stays stable``() = 
    let scenario = "i001427-content-once-stable"
    let newLockFile = install scenario

    let newFile = Path.Combine(scenarioTempPath scenario,"MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath scenario,"MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1
    s1.Contains "FodyWeavers.xml" |> shouldEqual true

    let newWeavers = Path.Combine(scenarioTempPath scenario,"MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let oldWeavers = Path.Combine(originalScenarioPath scenario,"MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let s1 = File.ReadAllText oldWeavers |> normalizeLineEndings
    let s2 = File.ReadAllText newWeavers |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1427 install content once from dependencies file removes paket tag``() = 
    let scenario = "i001427-content-once-remove"
    let newLockFile = install scenario

    let newFile = Path.Combine(scenarioTempPath scenario,"MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath scenario,"MyClassLibrary","MyClassLibrary","MyClassLibrary.expected")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1
    s1.Contains "FodyWeavers.xml" |> shouldEqual true

    let newWeavers = Path.Combine(scenarioTempPath scenario,"MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let oldWeavers = Path.Combine(originalScenarioPath scenario,"MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let s1 = File.ReadAllText oldWeavers |> normalizeLineEndings
    let s2 = File.ReadAllText newWeavers |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1427 install content once from dependencies file stays stable 2 installs``() = 
    let scenario = "i001427-content-once"
    let newLockFile = install scenario

    directPaket "install" scenario |> ignore

    let newFile = Path.Combine(scenarioTempPath scenario,"MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath scenario,"MyClassLibrary","MyClassLibrary","MyClassLibrary.expected")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1
    s1.Contains "FodyWeavers.xml" |> shouldEqual true

    let newWeavers = Path.Combine(scenarioTempPath scenario,"MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let oldWeavers = Path.Combine(originalScenarioPath scenario,"MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let s1 = File.ReadAllText oldWeavers |> normalizeLineEndings
    let s2 = File.ReadAllText newWeavers |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1427 install content once from references file``() = 
    let newLockFile = install "i001427-ref-content-once"
    let newFile = Path.Combine(scenarioTempPath "i001427-ref-content-once","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001427-ref-content-once","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1
    s1.Contains "FodyWeavers.xml" |> shouldEqual true

    let newWeavers = Path.Combine(scenarioTempPath "i001427-ref-content-once","MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let oldWeavers = Path.Combine(originalScenarioPath "i001427-ref-content-once","MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let s1 = File.ReadAllText oldWeavers |> normalizeLineEndings
    let s2 = File.ReadAllText newWeavers |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1427 install content``() = 
    let newLockFile = install "i001427-content-true"
    let newFile = Path.Combine(scenarioTempPath "i001427-content-true","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001427-content-true","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1
    s1.Contains "FodyWeavers.xml" |> shouldEqual true

    let newWeavers = Path.Combine(scenarioTempPath "i001427-content-true","MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let oldWeavers = Path.Combine(originalScenarioPath "i001427-content-true","MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let s1 = File.ReadAllText oldWeavers |> normalizeLineEndings
    let s2 = File.ReadAllText newWeavers |> normalizeLineEndings
    s1 |> shouldNotEqual s2

[<Test>]
let ``#1427 won't install content when content:none``() = 
    let newLockFile = install "i001427-content-none"
    let newFile = Path.Combine(scenarioTempPath "i001427-content-none","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001427-content-none","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1
    s1.Contains "FodyWeavers.xml" |> shouldEqual false

    let newWeavers = Path.Combine(scenarioTempPath "i001427-content-none","MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let oldWeavers = Path.Combine(originalScenarioPath "i001427-content-none","MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let s1 = File.ReadAllText oldWeavers |> normalizeLineEndings
    let s2 = File.ReadAllText newWeavers |> normalizeLineEndings
    s2 |> shouldEqual s1 // we do not touch it


[<Test>]
let ``#1522 install content and copy to output dir``() = 
    let newLockFile = install "i001522-copy-content"
    let newFile = Path.Combine(scenarioTempPath "i001522-copy-content","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001522-copy-content","MyClassLibrary","MyClassLibrary","MyClassLibrary.expected")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1
    s1.Contains "FodyWeavers.xml" |> shouldEqual true

    let newWeavers = Path.Combine(scenarioTempPath "i001522-copy-content","MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let oldWeavers = Path.Combine(originalScenarioPath "i001522-copy-content","MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let s1 = File.ReadAllText oldWeavers |> normalizeLineEndings
    let s2 = File.ReadAllText newWeavers |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1440 auto-detect framework``() = 
    let newLockFile = install "i001440-auto-detect"
    let newFile = Path.Combine(scenarioTempPath "i001440-auto-detect","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001440-auto-detect","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1466 install package with dll in name``() = 
    let newLockFile = install "i001466-expressive"
    let newFile = Path.Combine(scenarioTempPath "i001466-expressive","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001466-expressive","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1467 install package into vcxproj``() = 
    let newLockFile = install "i001467-cpp"
    let newFile = Path.Combine(scenarioTempPath "i001467-cpp","MyClassLibrary","ConsoleApplication1","ConsoleApplication1.vcxproj")
    let oldFile = Path.Combine(originalScenarioPath "i001467-cpp","MyClassLibrary","ConsoleApplication1","ConsoleApplication1.vcxprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1467 install native package into vcxproj``() = 
    let newLockFile = install "i001467-cpp-native"
    let newFile = Path.Combine(scenarioTempPath "i001467-cpp-native","MyClassLibrary","PaketTest.vcxproj")
    let oldFile = Path.Combine(originalScenarioPath "i001467-cpp-native","MyClassLibrary","PaketTest.vcxprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1458 should install non conflicting deps from different groups only once``() = 
    install "i001458-same-version-group" |> ignore
    let newFile = Path.Combine(scenarioTempPath "i001458-same-version-group","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001458-same-version-group","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1505 should install conditionals``() = 
    install "i001505-conditionals" |> ignore
    let newFile = Path.Combine(scenarioTempPath "i001505-conditionals","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001505-conditionals","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1
   
[<Test>]
let ``#1523 should install native in mixed setting``() = 
    install "i001523-mixed-native" |> ignore
    let newFile = Path.Combine(scenarioTempPath "i001523-mixed-native","TestPaket","TestPaket.vcxproj")
    let oldFile = Path.Combine(originalScenarioPath "i001523-mixed-native","TestPaket","TestPaket.vcxprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1523 should emit correct native in mixed setting``() = 
    install "i001523-not-true" |> ignore
    let newFile = Path.Combine(scenarioTempPath "i001523-not-true","TestPaket","TestPaket.vcxproj")
    let oldFile = Path.Combine(originalScenarioPath "i001523-not-true","TestPaket","TestPaket.vcxprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

    
[<Test>]
let ``#1523 should emit correct .NET in mixed setting``() = 
    install "i001523-not-true" |> ignore
    let newFile = Path.Combine(scenarioTempPath "i001523-not-true","TestPaketDotNet","TestPaketDotNet.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001523-not-true","TestPaketDotNet","TestPaketDotNet.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1458 should not install conflicting deps from different groups``() =
    try
        install "i001458-group-conflict" |> ignore
        failwith "error expected"
    with
    | exn when exn.Message.Contains "Package Nancy is referenced in different versions" -> ()


[<Test>]
let ``#1442 warn if install finds no libs``() = 
    let result = paket "install" "i001442-warn-if-empty"
    result |> shouldContainText "contains libraries, but not for the selected TargetFramework"

[<Test>]
let ``#1442 should not warn on SonarLint``() = 
    let result = paket "install" "i001442-dont-warn"
    result |> shouldNotContainText "contains libraries, but not for the selected TargetFramework"

[<Test>]
let ``#1442 should warn on Rx-WinRT``() = 
    let result = paket "install" "i001442-warn-Rx"
    result |> shouldContainText "contains libraries, but not for the selected TargetFramework"

[<Test>]
let ``#1334 without download fail``() = 
    install "i001334-download-fail" |> ignore

[<Test>]
let ``#1500 without install error``() = 
    install "i001500-auto-detect" |> ignore

[<Test>]
let ``#1371 without download fail``() = 
    paket "install -f"  "i001371-restore-error" |> ignore

[<Test>]
[<Ignore>]
let ``#1507 allows to download remote dependencies``() =
    let scenario = "i001507-privateeye"
    
    install scenario |> ignore

    File.Exists (Path.Combine(scenarioTempPath scenario, "paket-files", "forki", "PrivateEye", "privateeye.fsx")) |> shouldEqual true
    File.Exists (Path.Combine(scenarioTempPath scenario, "paket-files", "forki", "PrivateEye", "bin", "PrivateEye.Bridge.dll")) |> shouldEqual true
