module Paket.SolutionFileSpecs

open System
open System.IO
open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers

// The NuGet entries of a .sln

/// Runs RemoveNuGetEntries on a .sln made of the given lines and returns the saved lines
let private removeSlnEntries (lines: string list) (nugetFiles: string -> string list) =
    withTempDir (fun root ->
        let fileName = Path.Combine(root, "App.sln")
        File.WriteAllLines(fileName, slnLines lines)

        let solution = SolutionFile fileName
        solution.RemoveNuGetEntries(nugetFiles root)
        solution.Save()
        File.ReadAllLines fileName)

[<Test>]
let ``should only remove the sln entries of the deleted files``() =
    let lines =
        slnSolutionFolder ".nuget" "6A9DE8E5-3F35-4C0F-9F6B-0E2C46E3C0A1" [ @".nuget\NuGet.Config"; @".nuget\NuGet.exe"; @".nuget\packages.config" ]
    let saved = removeSlnEntries lines (fun root -> [ Path.Combine(root, ".nuget", "NuGet.Config"); Path.Combine(root, "NuGet.exe") ])

    saved |> Array.exists (fun line -> line.Contains @".nuget\NuGet.Config") |> shouldEqual false
    // the NuGet.exe deleted is the one of the root, not the one of .nuget
    saved |> Array.exists (fun line -> line.Contains @".nuget\NuGet.exe") |> shouldEqual true
    saved |> Array.exists (fun line -> line.Contains @".nuget\packages.config") |> shouldEqual true

[<Test>]
let ``should remove the sln entries of the deleted files by their path``() =
    let lines =
        [ yield! slnSolutionFolder "Solution Items" "0C5D4E35-6C1A-4C5E-9C0A-4E8C8D2F5B11" [ "NuGet.Config"; "build.fsx" ]
          yield! slnSolutionFolder "tools" "5E1C2A4B-8D3F-4F6A-A1B2-C3D4E5F60718" [ @"tools\.nuget\NuGet.Config" ]
          yield "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"nuget.exe\", \"tools\\.nuget\\nuget.exe.csproj\", \"{7A8B9C0D-1E2F-4A3B-8C4D-5E6F7A8B9C0D}\""
          yield "EndProject" ]
    let saved = removeSlnEntries lines (fun root -> [ Path.Combine(root, "NuGet.Config"); Path.Combine(root, ".nuget", "nuget.exe") ])

    saved |> Array.exists (fun line -> line.Trim() = "NuGet.Config = NuGet.Config") |> shouldEqual false
    saved |> Array.exists (fun line -> line.Contains "build.fsx") |> shouldEqual true
    saved |> Array.exists (fun line -> line.Contains @"tools\.nuget\NuGet.Config") |> shouldEqual true
    saved |> Array.exists (fun line -> line.Contains @"tools\.nuget\nuget.exe.csproj") |> shouldEqual true

[<Test>]
let ``should remove the .nuget folder of a sln once its entries are removed``() =
    let lines = slnSolutionFolder ".nuget" "6A9DE8E5-3F35-4C0F-9F6B-0E2C46E3C0A1" [ @".nuget\NuGet.Config"; @".nuget\NuGet.targets" ]
    let saved = removeSlnEntries lines (fun root -> [ Path.Combine(root, ".nuget", "NuGet.Config"); Path.Combine(root, ".nuget", "NuGet.targets") ])

    saved |> Array.exists (fun line -> line.Contains ".nuget") |> shouldEqual false
