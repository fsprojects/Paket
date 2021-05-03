#if INTERACTIVE
System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__
#r "../../packages/test/NUnit/lib/net45/nunit.framework.dll"
#r "../../packages/build/FAKE/tools/Fakelib.dll"
#r "../../packages/Chessie/lib/net40/Chessie.dll"
#r "../../bin/paket.core.dll"
#load "../../paket-files/test/forki/FsUnit/FsUnit.fs"
#load "TestHelper.fs"
open Paket.IntegrationTests.TestHelpers
#else
module Paket.IntegrationTests.InstallSpecs
#endif


open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics
open Paket
open Paket.Domain

// set this to true and the tests will overwrite the baseline files with the new result.
// then you can easily diff them in git and decide if the changes are OK or represent a bug.
let updateBaselines = false


[<Test>]
let ``updateBaselines should be false``() =
    // updateBaselines should never be checked-in as true
    Assert.False(updateBaselines)


[<Test>]
let ``#1135 should keep lockfile stable when using framework restrictions``() =
    let cleanup, newLockFile = install "i001135-stable-install-on-framework-restrictions"
    use __ = cleanup
    let oldLockFile = LockFile.LoadFrom(Path.Combine(originalScenarioPath "i001135-stable-install-on-framework-restrictions","paket.lock"))
    newLockFile.ToString()
    |> shouldEqual (oldLockFile.ToString())

[<Test>]
let ``#1219 install props``() =
    use __ = install "i001219-props-files" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001219-props-files","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001219-props-files","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
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
    use __ = install "i001487-stable-props" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001487-stable-props","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001487-stable-props","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1585 install props with for websharper``() =
    use __ = install "i001585-websharper-props" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001585-websharper-props","xUnitTests","xUnitTests.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001585-websharper-props","xUnitTests","xUnitTests.expected.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1256 should report error in lock file``() =
    try
        use __ = install "i001256-wrong-lock" |> fst
        failwith "error expected"
    with
    | exn when exn.Message.Contains("FAKE") && exn.Message.Contains("paket.lock") -> ()

[<Test>]
let ``#1260 install wpf\xaml and media files``() =
    use __ = install "i001260-csharp-wpf-project" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001260-csharp-wpf-project","WpfApplication","WpfApplication.csproj")
    let project = ProjectFile.LoadFromFile(newFile)

    let countNodes name count =
        project.FindPaketNodes(name)
        |> List.length |> shouldEqual count

    countNodes "Page" 1
    countNodes "Resource" 1
    countNodes "Content" 2

[<Test>]
let ``#1270 install net461``() =
    use __ = install "i001270-net461" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001270-net461","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001270-net461","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1427 install content once from dependencies file``() =
    use __ = install "i001427-content-once" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001427-content-once","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001427-content-once","MyClassLibrary","MyClassLibrary","MyClassLibrary.expected")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1
    s1.Contains "FodyWeavers.xml" |> shouldEqual true

    let newWeavers = Path.Combine(scenarioTempPath "i001427-content-once","MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let oldWeavers = Path.Combine(originalScenarioPath "i001427-content-once","MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
    let s1 = File.ReadAllText oldWeavers |> normalizeLineEndings
    let s2 = File.ReadAllText newWeavers |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1427 install content once from dependencies file stays stable``() =
    let scenario = "i001427-content-once-stable"
    use __ = install scenario |> fst

    let newFile = Path.Combine(scenarioTempPath scenario,"MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath scenario,"MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1
    s1.Contains "FodyWeavers.xml" |> shouldEqual true

    let newWeavers = Path.Combine(scenarioTempPath scenario,"MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let oldWeavers = Path.Combine(originalScenarioPath scenario,"MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    if updateBaselines then
        File.Copy (newWeavers, oldWeavers, overwrite=true)
    let s1 = File.ReadAllText oldWeavers |> normalizeLineEndings
    let s2 = File.ReadAllText newWeavers |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1427 install content once from dependencies file removes paket tag``() =
    let scenario = "i001427-content-once-remove"
    use __ = install scenario |> fst

    let newFile = Path.Combine(scenarioTempPath scenario,"MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath scenario,"MyClassLibrary","MyClassLibrary","MyClassLibrary.expected")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
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
    use __ = install scenario |> fst

    directPaket "install" scenario |> ignore

    let newFile = Path.Combine(scenarioTempPath scenario,"MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath scenario,"MyClassLibrary","MyClassLibrary","MyClassLibrary.expected")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
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
    use __ = install "i001427-ref-content-once" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001427-ref-content-once","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001427-ref-content-once","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1
    s1.Contains "FodyWeavers.xml" |> shouldEqual true

    let newWeavers = Path.Combine(scenarioTempPath "i001427-ref-content-once","MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    let oldWeavers = Path.Combine(originalScenarioPath "i001427-ref-content-once","MyClassLibrary","MyClassLibrary","FodyWeavers.xml")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
    let s1 = File.ReadAllText oldWeavers |> normalizeLineEndings
    let s2 = File.ReadAllText newWeavers |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1427 install content``() =
    use __ = install "i001427-content-true" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001427-content-true","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001427-content-true","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
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
    use __ = install "i001427-content-none" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001427-content-none","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001427-content-none","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
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
let ``#1701 won't install content when content:none and --keep-major``() =
    use __ = paket "update --keep-major" "i001701-keep-major" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001701-keep-major","TestPaket","TestPaket.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001701-keep-major","TestPaket","TestPaket.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1


[<Test>]
let ``#1522 install content and copy to output dir``() =
    use __ = install "i001522-copy-content" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001522-copy-content","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001522-copy-content","MyClassLibrary","MyClassLibrary","MyClassLibrary.expected")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
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
    use __ = install "i001440-auto-detect" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001440-auto-detect","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001440-auto-detect","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1466 install package with dll in name``() =
    use __ = install "i001466-expressive" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001466-expressive","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001466-expressive","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1458 should not install conflicting deps from different groups``() =
    try
        use __ = install "i001458-group-conflict" |> fst
        failwith "error expected"
    with
    | exn when exn.Message.Contains "Package Nancy is referenced in different versions" -> ()

[<Test>]
let ``#1442 should not warn on SonarLint``() =
    let cleanup, result = paket "install" "i001442-dont-warn"
    use __ = cleanup
    result |> shouldNotContainText "contains libraries, but not for the selected TargetFramework"

[<Test>]
let ``#1442 should warn on Rx-WinRT``() =
    let cleanup, result = paket "install" "i001442-warn-Rx"
    use __ = cleanup
    result |> shouldContainText "contains libraries, but not for the selected TargetFramework"


[<Test>]
let ``#1663 should import build targets``() =
    use __ = install "i001663-build-targets" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001663-build-targets","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001663-build-targets","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1145 don't install excludes``() =
    use __ = install "i001145-excludes" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001145-excludes","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001145-excludes","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#346 set aliases``() =
    use __ = install "i000346-aliases" |> fst
    let newFile = Path.Combine(scenarioTempPath "i000346-aliases","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i000346-aliases","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1720 install concrete net45``() =
    use __ = install "i001720-explicit-net45" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001720-explicit-net45","projectA","projectA.fsproj")
    let oldFile = Path.Combine(originalScenarioPath "i001720-explicit-net45","projectA","projectA.fsprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1732 aliases ignore cases``() =
    use __ = install "i001732-lowercase-aliases" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001732-lowercase-aliases","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001732-lowercase-aliases","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1746 hard should be softer``() =
    use __ = install "i001746-hard-legacy" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001746-hard-legacy","SilverlightClassLibrary1","SilverlightClassLibrary1.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001746-hard-legacy","SilverlightClassLibrary1","SilverlightClassLibrary1.csprojtemplate")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1779 net20 only in net461``() =
    use __ = install "i001779-net20-only-in-net461" |> fst
    let newFile = Path.Combine(scenarioTempPath "i001779-net20-only-in-net461","paket-net20-library-problem","paket-net20-library-problem.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001779-net20-only-in-net461","paket-net20-library-problem","paket-net20-library-problem.csprojtemplate")
    if updateBaselines then
        File.Copy (newFile, oldFile, overwrite=true)
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

[<Test>]
let ``#1871 should install suave``() =
    use __ = install "i001871-suave" |> fst
    ignore __

[<Test>]
let ``#1883 install FSharp.Core from Chessie``() =
    let cleanup, newLockFile = install "i001883-chessie"
    use __ = cleanup
    newLockFile.Groups.[GroupName "main"].Resolution.[PackageName "FSharp.Core"].Version |> shouldBeGreaterThan (SemVer.Parse "4.1")

[<Test>]
let ``#1883 should not install .NET Standard``() =
    let cleanup, newLockFile = install "i001883-machine"
    use __ = cleanup
    newLockFile.Groups.[GroupName "main"].Resolution.ContainsKey (PackageName "System.Reflection") |> shouldEqual false

[<Test>]
let ``#1860 faulty condition was generated`` () =
    let scenario = "i001860-attribute"
    use __ = install scenario |> fst
    let fsprojFile = (scenarioTempPath scenario) </> "Library1" </> "Library1.fsproj" |> File.ReadAllText
    Assert.IsFalse (fsprojFile.Contains(" And ()"))


[<Test>]
let ``#2777 should not conflict with locked packages``() =
    let cleanup, newLockFile = install "i002777"
    use __ = cleanup
    newLockFile.Groups.[GroupName "main"].Resolution.ContainsKey (PackageName "FsPickler") |> shouldEqual true

[<Test>]
let ``#3062 install should use external lock file``() =
    let cleanup, newLockFile = install "i003062-external-lock"
    use __ = cleanup
    newLockFile.Groups.[GroupName "main"].Resolution.ContainsKey (PackageName "FAKE") |> shouldEqual true
    newLockFile.Groups.[GroupName "main"].Resolution.[PackageName "Machine.Specifications"].Version |> shouldEqual (SemVer.Parse "0.12")

[<Test>]
let ``#4012 Support .net 6 (part 1)``() =
    let cleanup, newLockFile = install "i004012-dotnet6-part1"
    use __ = cleanup
    newLockFile.Groups.[GroupName "main"].Resolution.ContainsKey (PackageName "Argu") |> shouldEqual true

#if INTERACTIVE
;;

#endif
