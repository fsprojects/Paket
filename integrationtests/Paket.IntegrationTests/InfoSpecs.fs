module Paket.IntegrationTests.InfoSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics

[<Test>]
let ``#3200 info should locate paket.dependencies``() = 
    let repoDir = createScenarioDir "i003200-info-paketdeps-dir"

    let subDir = repoDir </> "src" </> "app"
    Directory.CreateDirectory(subDir) |> ignore

    let ``paket info --paket-dependencies-dir`` workingDir =
        directPaketInPathEx "info --paket-dependencies-dir" workingDir
        |> Seq.map OutputMsg.getMessage
        |> List.ofSeq

    // paket.dependencies not exists
    
    CollectionAssert.DoesNotContain(``paket info --paket-dependencies-dir`` repoDir, repoDir)
    CollectionAssert.DoesNotContain(``paket info --paket-dependencies-dir`` subDir, repoDir)

    // empty paket.dependencies
    File.WriteAllText(repoDir </> "paket.dependencies", "")

    CollectionAssert.Contains(``paket info --paket-dependencies-dir`` repoDir, repoDir)
    CollectionAssert.Contains(``paket info --paket-dependencies-dir`` subDir, repoDir)

