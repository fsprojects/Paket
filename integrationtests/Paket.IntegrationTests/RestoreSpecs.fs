module Paket.IntegrationTests.RestoreSpec

open System
open System.IO
open Fake
open NUnit.Framework
open FsUnit
open Paket

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
        if not (matchingStrings |> Seq.tryExactlyOne |> Option.exists ((=) str))
        then failwithf "Expected \"%s\" but got %A" str matchingStrings

    let scenario = "i002812"
    let packageName = "AutoMapper"
    let packageNameLowercase = packageName.ToLower()
    let packageVersion = "6.1.1"

    prepareSdk scenario
    [ packageName; packageNameLowercase ] |> Seq.iter clearPackage
    directPaket "restore" scenario |> ignore
    
    let packageCacheDirectory = Constants.UserNuGetPackagesFolder |> directoryInfo
    let packageFolderNames = packageCacheDirectory |> subDirectories |> Array.map (fun x -> x.Name)
    packageFolderNames |> shouldContainExactlyOneCasingVariant packageNameLowercase

    let packageContentFolder = packageCacheDirectory.FullName @@ packageNameLowercase @@ packageVersion
    let packageFiles = packageContentFolder |> directoryInfo |> filesInDir |> Array.map (fun x -> x.Name)

    let filesToFind = [ sprintf ".%s.nupkg" packageVersion
                        sprintf ".%s.nupkg.sha512" packageVersion
                        ".nuspec" ]
                      |> List.map (sprintf "%s%s" packageNameLowercase)
    filesToFind |> Seq.iter (fun x -> packageFiles |> shouldContainExactlyOneCasingVariant x)
