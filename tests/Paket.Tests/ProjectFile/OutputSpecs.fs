module Paket.ProjectFile.OutputSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.Xml
open System.Xml.Linq
open TestHelpers
open System.IO
open Pri.LongPath

[<Test>]
let ``should detect lib output type for Project1 proj file``() =
    ensureDir ()
    ProjectFile.TryLoad("./ProjectFile/TestData/Project1.fsprojtest").Value.OutputType
    |> shouldEqual ProjectOutputType.Library

[<Test>]
let ``should detect exe output type for Project2 proj file``() =
    ensureDir ()
    ProjectFile.TryLoad("./ProjectFile/TestData/Project2.fsprojtest").Value.OutputType
    |> shouldEqual ProjectOutputType.Exe

[<Test>]
let ``should detect exe output type for Project3 proj file``() =
    ensureDir ()
    ProjectFile.TryLoad("./ProjectFile/TestData/Project3.fsprojtest").Value.OutputType
    |> shouldEqual ProjectOutputType.Exe

[<Test>]
let ``should detect target framework for Project1 proj file``() =
    ensureDir ()
    ProjectFile.TryLoad("./ProjectFile/TestData/Project1.fsprojtest").Value.GetTargetProfile()
    |> shouldEqual (SinglePlatform(DotNetFramework(FrameworkVersion.V4_5)))

[<Test>]
let ``should detect target framework for Project2 proj file``() =
    ensureDir ()
    ProjectFile.TryLoad("./ProjectFile/TestData/Project2.fsprojtest").Value.GetTargetProfile()
    |> shouldEqual (SinglePlatform(DotNetFramework(FrameworkVersion.V4)))

[<Test>]
let ``should detect output path for proj file``
        ([<Values("Project1", "Project2", "Project3", "ProjectWithConditions")>] project)
        ([<Values("Debug", "Release", "dEbUg", "rElEaSe")>] configuration) =
    ensureDir ()
    let outPath = ProjectFile.TryLoad(sprintf "./ProjectFile/TestData/%s.fsprojtest" project).Value.GetOutputDirectory configuration ""
    let expected = (Path.Combine(@"bin", configuration) |> normalizePath)
    outPath.ToLowerInvariant() |> shouldEqual (expected.ToLowerInvariant())

[<Test>]
let ``should detect output path for netsdk csproj file``
        ([<Values("MicrosoftNetSdkWithTargetFramework.csprojtest")>] project)
        ([<Values("Debug", "Release", "dEbUg", "rElEaSe")>] configuration) =
    ensureDir ()
    let projectFile = ProjectFile.TryLoad(sprintf "./ProjectFile/TestData/%s" project).Value 
    let target = projectFile.GetTargetProfile().ToString()
    let outPath = projectFile.GetOutputDirectory configuration ""
    let expected = (Path.Combine(@"bin", configuration, target) |> normalizePath)
    outPath.ToLowerInvariant() |> shouldEqual (expected.ToLowerInvariant())

[<Test>]
let ``should detect output path for netsdk with outputPath csproj file``
        ([<Values("MicrosoftNetSdkWithTargetFrameworkAndOutputPath.csprojtest")>] project)
        ([<Values("Release")>] configuration) =
    ensureDir ()
    let projectFile = ProjectFile.TryLoad(sprintf "./ProjectFile/TestData/%s" project).Value 
    let target = projectFile.GetTargetProfile().ToString()
    let outPath = projectFile.GetOutputDirectory configuration ""
    let expected = (Path.Combine(@"bin", configuration,"netstandard1.4_bin", target) |> normalizePath)
    outPath.ToLowerInvariant() |> shouldEqual (expected.ToLowerInvariant())


[<Test>]
let ``should detect framework profile for ProjectWithConditions file`` () =
    ensureDir ()
    ProjectFile.TryLoad("./ProjectFile/TestData/ProjectWithConditions.fsprojtest").Value.GetTargetProfile()
    |> shouldEqual (SinglePlatform(DotNetFramework(FrameworkVersion.V4_6)))

[<Test>]
let ``should detect assembly name for Project1 proj file`` () =
    ensureDir ()
    ProjectFile.TryLoad("./ProjectFile/TestData/Project1.fsprojtest").Value.GetAssemblyName()
    |> shouldEqual ("Paket.Tests.dll")

[<Test>]
let ``should detect assembly name for Project2 proj file`` () =
    ensureDir ()
    ProjectFile.TryLoad("./ProjectFile/TestData/Project2.fsprojtest").Value.GetAssemblyName()
    |> shouldEqual ("Paket.Tests.exe")

[<Test>]
let ``should detect assembly name for Project3 proj file`` () =
    ensureDir ()
    ProjectFile.TryLoad("./ProjectFile/TestData/Project3.fsprojtest").Value.GetAssemblyName()
    |> shouldEqual ("Paket.Tests.Win.exe")

[<Test>]
let ``should maintain order when updating project file items`` () = 
    ensureDir ()
    
    let projFile =  ProjectFile.TryLoad("./ProjectFile/TestData/MaintainsOrdering.fsprojtest").Value
    let fileItems = [
        { BuildAction = BuildAction.Compile; WithPaketSubNode = true; CopyToOutputDirectory = None; Include = "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\Pluralizer.fs"; Link = Some("fsharp_data\\Pluralizer.fs") }
        { BuildAction = BuildAction.Compile; WithPaketSubNode = true; CopyToOutputDirectory = None; Include = "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\NameUtils.fs"; Link = Some("fsharp_data\\NameUtils.fs") }
        { BuildAction = BuildAction.Compile; WithPaketSubNode = true; CopyToOutputDirectory = None; Include = "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\TextConversions.fs"; Link = Some("fsharp_data\\TextConversions.fs") }
        { BuildAction = BuildAction.Compile; WithPaketSubNode = true; CopyToOutputDirectory = None; Include = "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\StructuralTypes.fs"; Link = Some("fsharp_data\\StructuralTypes.fs") }
        { BuildAction = BuildAction.Compile; WithPaketSubNode = true; CopyToOutputDirectory = None; Include = "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\StructuralInference.fs"; Link = Some("fsharp_data\\StructuralInference.fs") }
        { BuildAction = BuildAction.Compile; WithPaketSubNode = true; CopyToOutputDirectory = None; Include = "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\TextRuntime.fs"; Link = Some("fsharp_data\\TextRuntime.fs") }
        { BuildAction = BuildAction.Compile; WithPaketSubNode = true; CopyToOutputDirectory = None; Include = "DebugProvidedTypes.fs"; Link = None }
        { BuildAction = BuildAction.Compile; WithPaketSubNode = true; CopyToOutputDirectory = None; Include = "ProvidedTypes.fs"; Link = None }
        { BuildAction = BuildAction.Content; WithPaketSubNode = true; CopyToOutputDirectory = None; Include = "ProvidedTypes.fsi"; Link = None }
    ]
    projFile.UpdateFileItems(fileItems)

    let rec nodes node = 
        seq {
            for node in node |> Seq.cast<XmlNode> do
                if List.contains node.Name BuildAction.PaketFileNodeNames
                then yield Paket.Xml.getAttribute "Include" node
                yield! nodes node 
        }
    
    let actual = 
        nodes projFile.Document
        |> Seq.choose id
        |> Seq.toList
    let expected = 
        [
         "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\Pluralizer.fs"
         "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\NameUtils.fs"
         "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\TextConversions.fs"
         "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\StructuralTypes.fs"
         "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\StructuralInference.fs"
         "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\TextRuntime.fs"
         "ProvidedTypes.fsi"
         "ProvidedTypes.fs"
         "DebugProvidedTypes.fs"
         "QuotationHelpers.fs"
         "CommonTypes.fs"
         "ExcelProvider.fs"
         "WordProvider.fs"
         "ProviderEntryPoint.fs"
        ]
    CollectionAssert.AreEqual(expected, actual)

[<Test>]
let ``should remove missing files that exist in the project`` () = 
    ensureDir ()
    
    let projFile =  ProjectFile.TryLoad("./ProjectFile/TestData/MaintainsOrdering.fsprojtest").Value
    let fileItems = [
        { BuildAction = BuildAction.Compile; WithPaketSubNode = true; CopyToOutputDirectory = None; Include = "DebugProvidedTypes.fs"; Link = None }
        { BuildAction = BuildAction.Compile; WithPaketSubNode = true; CopyToOutputDirectory = None; Include = "ProvidedTypes.fs"; Link = None }
        { BuildAction = BuildAction.Content; WithPaketSubNode = true; CopyToOutputDirectory = None; Include = "ProvidedTypes.fsi"; Link = None }
    ]
    projFile.UpdateFileItems(fileItems)

    let rec nodes node = 
        seq {
            for node in node |> Seq.cast<XmlNode> do
                if List.contains node.Name BuildAction.PaketFileNodeNames
                then yield Paket.Xml.getAttribute "Include" node
                yield! nodes node 
        }
    
    let actual = 
        nodes projFile.Document
        |> Seq.choose id
        |> Seq.toList
    let expected = 
        [
         "ProvidedTypes.fsi"
         "ProvidedTypes.fs"
         "DebugProvidedTypes.fs"
         "QuotationHelpers.fs"
         "CommonTypes.fs"
         "ExcelProvider.fs"
         "WordProvider.fs"
         "ProviderEntryPoint.fs"
        ]
    CollectionAssert.AreEqual(expected, actual)