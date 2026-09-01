module Paket.NupkgWriter.Test

open System.IO
open System.IO.Compression
open Paket
open FsUnit
open NUnit.Framework

[<Test>]
let ``#3906 paket pack escapes square brackets in file names``() =
    let workingDir = Path.Combine(Path.GetTempPath(), "paket-nupkgwriter-test-" + System.Guid.NewGuid().ToString("N"))
    let outputDir = Path.Combine(workingDir, "out")
    let harvestDir = Path.Combine(workingDir, "harvest")
    Directory.CreateDirectory harvestDir |> ignore
    Directory.CreateDirectory outputDir |> ignore
    try
        let bracketFile = Path.Combine(harvestDir, "file[0].txt")
        File.WriteAllText(bracketFile, "content")

        let core : CompleteCoreInfo =
            { Id = "Test.Package"
              Version = Some (SemVer.Parse "1.0.0")
              Authors = ["Author"]
              Description = "Description"
              Symbols = false }

        let optional =
            { OptionalPackagingInfo.Empty with
                Files = ["harvest", "lib/net461"] }

        let outputPath = NupkgWriter.Write core optional workingDir outputDir

        use archive = ZipFile.OpenRead outputPath
        let entryNames = archive.Entries |> Seq.map (fun e -> e.FullName) |> Seq.toList

        entryNames |> shouldContain "lib/net461/file%5B0%5D.txt"
        entryNames |> List.contains "lib/net461/file[0].txt" |> shouldEqual false
    finally
        if Directory.Exists workingDir then Directory.Delete(workingDir, true)
