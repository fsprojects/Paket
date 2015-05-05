module Paket.DependenciesFile.PropertyTests

open System
open FsCheck
open FsCheck.NUnit
open Paket
open TestHelpers

let nl = Environment.NewLine
let linesToString s = String.concat nl s

let alphaNumString =
    Arb.generate<char> 
    |> Gen.suchThat Char.IsLetterOrDigit 
    |> Gen.nonEmptyListOf 
    |> Gen.map (fun xs -> String(xs |> Array.ofList))

let smallAlphaNum size = alphaNumString |> Gen.resize (size |> float |> sqrt |> int)

let remoteSource = Gen.sized (fun size -> gen {
    let builder = UriBuilder()
    let! host = smallAlphaNum size
    builder.Host <- host
    let! scheme = Gen.elements ["http"; "https"]
    builder.Scheme <- scheme
    let! path =
        smallAlphaNum size
        |> Gen.nonEmptyListOf
        |> Gen.map (String.concat "/")
    builder.Path <- path
    let! creds = 
        Gen.frequency [
            70, Gen.constant ""
            15, smallAlphaNum size
                |> Gen.two
                |> Gen.map (fun (x,y) -> sprintf " username: \"%s\" password: \"%s\"" x y)
            15, smallAlphaNum size
                |> Gen.two
                |> Gen.map (fun (x,y) -> sprintf " username: \"%%%s%%\" password: \"%%%s%%\"" x y)
        ]
    return "source " + builder.ToString() + creds
})

let pathSource = Gen.constant "source C:"

let source = Gen.oneof [remoteSource; pathSource] 

let semVer = gen {
    let! major = Arb.generate<PositiveInt>
    let! minor = Arb.generate<PositiveInt>
    let! patch = Arb.generate<PositiveInt>
    return { 
        Major = major.Get
        Minor = minor.Get
        Patch = patch.Get
        PreRelease = None
        Build = ""
        PreReleaseBuild = ""
        Original = None }
}

let nuget = 
    let packageId =
        Gen.sized (fun size -> 
        smallAlphaNum size
        |> Gen.nonEmptyListOf
        |> Gen.map (String.concat "."))

    let g = Gen.elements [">"; ">="]
    let l = Gen.elements ["<"; "<="]
    let gOrL = Gen.oneof [g; l]

    let _constraint =
        Gen.oneof [
            Gen.constant ""
            semVer |> Gen.map (sprintf " ~> %O")
            semVer |> Gen.map (sprintf " == %O")
            (gOrL, semVer) ||> Gen.map2  (sprintf " %s %O")
            Gen.map2 (sprintf " %s %O") gOrL semVer
            Gen.map4 (sprintf " %s %O %s %O") g semVer l semVer
        ]

    (packageId, _constraint)
    ||> Gen.map2 (fun p c -> sprintf "nuget %s%s" p c)

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