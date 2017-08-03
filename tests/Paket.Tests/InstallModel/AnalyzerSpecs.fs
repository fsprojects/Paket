module Paket.InstallModel.AnalyzerSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.IO
open Pri.LongPath

[<Test>]
let ``Can convert directory name to language``() = 
    AnalyzerLanguage.FromDirectoryName("cs") |> shouldEqual AnalyzerLanguage.CSharp
    AnalyzerLanguage.FromDirectoryName("fs") |> shouldEqual AnalyzerLanguage.FSharp
    AnalyzerLanguage.FromDirectoryName("vb") |> shouldEqual AnalyzerLanguage.VisualBasic

[<Test>]
let ``Other directory name is Any``() = 
    AnalyzerLanguage.FromDirectoryName("foo") |> shouldEqual AnalyzerLanguage.Any

[<Test>]
let ``Directory name is cases sensitive``() = 
    AnalyzerLanguage.FromDirectoryName("Cs") |> shouldEqual AnalyzerLanguage.Any

[<Test>]
let ``Can convert directory to language``() = 
    let mkLanguage lang = DirectoryInfo(Path.Combine("foo", "bar", lang)) |> AnalyzerLanguage.FromDirectory

    mkLanguage "cs" |> shouldEqual AnalyzerLanguage.CSharp
    mkLanguage "fs" |> shouldEqual AnalyzerLanguage.FSharp
    mkLanguage "vb" |> shouldEqual AnalyzerLanguage.VisualBasic

[<Test>]
let ``Can create analyzer lib``() = 
    let fileInfo = FileInfo(Path.Combine("foo", "bar", "cs", "Analyzer.dll"))
    let lib = fileInfo |> AnalyzerLib.FromFile
    
    lib.Language |> shouldEqual AnalyzerLanguage.CSharp
    lib.Path |> shouldEqual fileInfo.FullName