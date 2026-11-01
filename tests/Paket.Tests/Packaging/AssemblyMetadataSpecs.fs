module Paket.AssemblyMetadataSpecs

open System.IO
open System.Reflection
open Paket
open FsUnit
open NUnit.Framework

let private newtonsoftJson = typeof<Newtonsoft.Json.JsonConvert>.Assembly

[<Test>]
let ``reads the name, version, token and references of a signed assembly``() =
    let assembly = AssemblyMetadata.read newtonsoftJson.Location

    assembly.Name |> shouldEqual "Newtonsoft.Json"
    assembly.Version |> shouldEqual (newtonsoftJson.GetName().Version)
    assembly.PublicKeyToken |> shouldEqual (Some "30ad4fe6b2a6aeed")
    assembly.References |> List.map (fun r -> r.RefName, r.RefVersion) |> set
    |> shouldEqual (newtonsoftJson.GetReferencedAssemblies() |> Array.map (fun r -> r.Name, r.Version) |> set)
    assembly.Attributes |> shouldEqual []

[<Test>]
let ``reads the requested attributes of an assembly with their first argument``() =
    let assembly = AssemblyMetadata.readWithAttributes (set [ "AssemblyTitleAttribute" ]) newtonsoftJson.Location

    let title = newtonsoftJson.GetCustomAttribute<AssemblyTitleAttribute>().Title
    assembly.Attributes |> shouldEqual [ "AssemblyTitleAttribute", title ]

[<Test>]
let ``reads an attribute with an enum argument without failing``() =
    let assembly = AssemblyMetadata.readWithAttributes (set [ "DebuggableAttribute" ]) newtonsoftJson.Location

    assembly.Attributes |> List.map fst |> List.filter ((<>) "DebuggableAttribute") |> shouldEqual []

[<Test>]
let ``fails on a file that is not an assembly``() =
    let fileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".dll")
    File.WriteAllText(fileName, "not an assembly")
    try
        (fun () -> AssemblyMetadata.read fileName |> ignore) |> shouldFail<exn>
    finally
        File.Delete fileName

[<Test>]
let ``gives no token for an unsigned assembly``() =
    let assembly = AssemblyMetadata.read (Assembly.GetExecutingAssembly().Location)

    assembly.Name |> shouldEqual "Paket.Tests"
    assembly.PublicKeyToken |> shouldEqual None

[<Test>]
let ``doesn't keep the assembly file open``() =
    let fileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".dll")
    File.Copy(newtonsoftJson.Location, fileName)
    try
        AssemblyMetadata.readWithAttributes (set [ "AssemblyTitleAttribute" ]) fileName |> ignore
        use _ = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
        ()
    finally
        File.Delete fileName
