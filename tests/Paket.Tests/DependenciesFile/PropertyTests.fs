module Paket.DependenciesFile.PropertyTests

open System
open FsCheck
open FsCheck.NUnit
open Paket
open TestHelpers

let nl = Environment.NewLine
let linesToString s = String.concat nl s

let source = Gen.constant "source https://nuget.org/api/v2"

let nuget = Gen.constant "nuget FsCheck"

let github = Gen.constant "github forki/FsUnit FsUnit.fs"

let gist = Gen.constant "gist Thorium/1972349 timestamp.fs"

let http = Gen.constant "http http://www.fssnip.net/1n decrypt.fs"

let empty = Gen.constant ""

let line = Gen.oneof [source; nuget; github; gist; http; empty]

let slashComment = Gen.constant "//comment"
let lineWComment = 
    let line = Gen.oneof [source; nuget; empty]
    (line, slashComment)
    ||> Gen.map2 (fun l c -> l + " " + c)

let hashComment = Gen.constant "#comment"
let comment = Gen.oneof [slashComment; hashComment]

let depLine = Gen.frequency [80, line; 10, lineWComment; 10, comment]

let framework = 
    Arb.generate<FrameworkVersion>
    |> Gen.nonEmptyListOf
    |> Gen.map (Seq.distinct 
                >> Array.ofSeq 
                >> Array.map (DotNetFramework >> string)
                >> String.concat ", " 
                >> (fun x -> "framework: ", x))

let globalOpts = 
    Gen.oneof 
        [ Gen.constant ("references: ", "strict")
          framework
          Gen.constant ("content: ", "none")
          Gen.constant ("import_targets: ", "false")
          Gen.constant ("copy_local: ", "false") ]
    |> Gen.arrayOf
    |> Gen.map (Seq.distinctBy fst >> Seq.map (fun (a,b) -> a+b) >> Array.ofSeq)

let generator = 
    (Gen.arrayOf depLine, globalOpts)
    ||> Gen.map2 (fun lines globalOpts -> Array.append globalOpts lines)
    |> Gen.map linesToString

let shrinker s =
    let lines = s |> toLines
    seq { 
        for i in [0 .. lines.Length - 1] do
            yield seq { 
                for j in [0 .. lines.Length - 1] do
                    if i <> j then yield lines.[j] }
                |> linesToString}

type DFFileGenerator =
    static member StringArray() = 
        {new Arbitrary<string>() with
            override x.Generator = generator
            override x.Shrinker t = shrinker t }

let _ = PropertyAttribute(Verbose = true)

[<Property(
    Arbitrary = [|typeof<DFFileGenerator>|],
    Verbose = true)>]
let ``round trip`` (contents : string) =
    let lines = toLines contents
    let df = DependenciesFile(DependenciesFileParser.parseDependenciesFile "dummy" lines)
    df.ToString() = String.concat Environment.NewLine lines 