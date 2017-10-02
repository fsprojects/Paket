module Paket.IntegrationTests.RestoreSpec

open System
open System.IO
open Fake
open NUnit.Framework
open FsUnit
open Paket
open Paket.Utils

[<Test>]
let ``#2496 Paket fails on projects that target multiple frameworks``() = 
    let project = "EmptyTarget"
    let scenario = "i002496"
    prepareSdk scenario

    let wd = (scenarioTempPath scenario) @@ project
    directDotnet true (sprintf "restore %s.csproj" project) wd
        |> ignore

[<Test>]
let ``#2812 Lowercase package names in package cache``() =
    let getCasingVariantsOf (str: string) (sequence: string seq) =
        sequence |> Seq.filter (fun x -> x.Equals(str, StringComparison.InvariantCultureIgnoreCase))

    let shouldContainExactlyOneCasingVariant (str: string) (sequence: string seq) = 
        let matchingStrings = getCasingVariantsOf str sequence |> List.ofSeq
        shouldEqual [ str ] matchingStrings

    let fileNamesInDir (dir: string) = dir |> directoryInfo |> filesInDir |> Seq.map (fun x -> x.Name)
    let subDirectoryNames (dir: string) = dir |> directoryInfo |> subDirectories |> Seq.map (fun x -> x.Name)    
    let matchingFileNamesInDir (filter: string -> bool) (dir: string) = fileNamesInDir dir |> Seq.filter filter

    let scenario = "i002812"
    let packageName = "AutoMapper"
    let packageNameLowercase = packageName.ToLower()
    let packageVersion = "6.1.1"

    prepareSdk scenario
    [ packageName; packageNameLowercase ] |> Seq.iter clearPackage
    directPaket "restore" scenario |> ignore
    
    // check if packages stored in cache are properly lowercased
    Constants.UserNuGetPackagesFolder
        |> subDirectoryNames
        |> shouldContainExactlyOneCasingVariant packageNameLowercase
    Constants.UserNuGetPackagesFolder @@ packageNameLowercase @@ packageVersion
        |> matchingFileNamesInDir (String.startsWithIgnoreCase (sprintf "%s." packageName))
        |> Seq.map (fun x -> x.Substring(0, packageName.Length))
        |> Seq.distinct
        |> shouldContainExactlyOneCasingVariant packageNameLowercase

    // check if packages stored in solution package folder are left unchanged
    let packagesDirectory = (scenarioTempPath scenario) @@ "packages"
    packagesDirectory
        |> subDirectoryNames
        |> shouldContainExactlyOneCasingVariant packageName
    packagesDirectory @@ packageName
        |> matchingFileNamesInDir (String.startsWithIgnoreCase (sprintf "%s." packageName))
        |> Seq.map (fun x -> x.Substring(0, packageName.Length))
        |> Seq.distinct
        |> shouldContainExactlyOneCasingVariant packageName
