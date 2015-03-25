module Paket.ProjectFile.OutputSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.Xml
open System.Xml.Linq

[<Test>]
let ``should detect lib output type for Project1 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project1.fsprojtest").Value.OutputType
    |> shouldEqual ProjectOutputType.Library

[<Test>]
let ``should detect exe output type for Project2 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project2.fsprojtest").Value.OutputType
    |> shouldEqual ProjectOutputType.Exe

[<Test>]
let ``should detect exe output type for Project3 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project3.fsprojtest").Value.OutputType
    |> shouldEqual ProjectOutputType.Exe

[<Test>]
let ``should detect target framework for Project1 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project1.fsprojtest").Value.GetTargetFramework()
    |> shouldEqual (DotNetFramework(FrameworkVersion.V4_5))

[<Test>]
let ``should detect target framework for Project2 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project2.fsprojtest").Value.GetTargetFramework()
    |> shouldEqual (DotNetFramework(FrameworkVersion.V4_Client))

[<Test>]
let ``should detect output path for proj file``
        ([<Values("Project1", "Project2")>] project)
        ([<Values("Debug", "Release")>] configuration) =
    ProjectFile.Load(sprintf "./ProjectFile/TestData/%s.fsprojtest" project).Value.GetOutputDirectory configuration
    |> shouldEqual (System.IO.Path.Combine(@"bin", configuration) |> normalizePath)

[<Test>]
let ``should detect assembly name for Project1 proj file`` () =
    ProjectFile.Load("./ProjectFile/TestData/Project1.fsprojtest").Value.GetAssemblyName()
    |> shouldEqual ("Paket.Tests.dll")

[<Test>]
let ``should detect assembly name for Project2 proj file`` () =
    ProjectFile.Load("./ProjectFile/TestData/Project2.fsprojtest").Value.GetAssemblyName()
    |> shouldEqual ("Paket.Tests.exe")

[<Test>]
let ``should detect assembly name for Project3 proj file`` () =
    ProjectFile.Load("./ProjectFile/TestData/Project3.fsprojtest").Value.GetAssemblyName()
    |> shouldEqual ("Paket.Tests.Win.exe")

[<Test>]
let ``should maintain order when updating project file items`` () = 
    
    let projFile =  ProjectFile.Load("./ProjectFile/TestData/MaintainsOrdering.fsprojtest").Value
    let fileItems = [
        { BuildAction = "Compile"; Include = "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\Pluralizer.fs"; Link = Some("fsharp_data\\Pluralizer.fs") }
        { BuildAction = "Compile"; Include = "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\NameUtils.fs"; Link = Some("fsharp_data\\NameUtils.fs") }
        { BuildAction = "Compile"; Include = "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\TextConversions.fs"; Link = Some("fsharp_data\\TextConversions.fs") }
        { BuildAction = "Compile"; Include = "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\StructuralTypes.fs"; Link = Some("fsharp_data\\StructuralTypes.fs") }
        { BuildAction = "Compile"; Include = "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\StructuralInference.fs"; Link = Some("fsharp_data\\StructuralInference.fs") }
        { BuildAction = "Compile"; Include = "..\\..\\paket-files\\fsharp\\FSharp.Data\\src\\CommonRuntime\\TextRuntime.fs"; Link = Some("fsharp_data\\TextRuntime.fs") }
        { BuildAction = "Compile"; Include = "DebugProvidedTypes.fs"; Link = None }
        { BuildAction = "Compile"; Include = "ProvidedTypes.fs"; Link = None }
        { BuildAction = "Content"; Include = "ProvidedTypes.fsi"; Link = None }
    ]
    projFile.UpdateFileItems(fileItems, false)

    let rec nodes node = 
        seq {
            for node in node |> Seq.cast<XmlNode> do
                if node.Name = "Compile" || node.Name = "Content"
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
    
    let projFile =  ProjectFile.Load("./ProjectFile/TestData/MaintainsOrdering.fsprojtest").Value
    let fileItems = [
        { BuildAction = "Compile"; Include = "DebugProvidedTypes.fs"; Link = None }
        { BuildAction = "Compile"; Include = "ProvidedTypes.fs"; Link = None }
        { BuildAction = "Content"; Include = "ProvidedTypes.fsi"; Link = None }
    ]
    projFile.UpdateFileItems(fileItems, false)

    let rec nodes node = 
        seq {
            for node in node |> Seq.cast<XmlNode> do
                if node.Name = "Compile" || node.Name = "Content"
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