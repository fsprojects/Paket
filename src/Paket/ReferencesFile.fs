namespace Paket

open System
open System.IO
open Logging

type ReferencesFile = 
    { FileName: string
      NugetPackages: list<string>
      GitHubFiles: list<string> } 
    
    static member FromLines(lines : string[]) = 
        let isGitHubFile (line: string) = line.StartsWith "File:"
        let notEmpty (line: string) = not <| System.String.IsNullOrWhiteSpace line

        { FileName = ""
          NugetPackages = lines |> Array.filter notEmpty |> Array.filter (isGitHubFile >> not) |> Array.toList
          GitHubFiles = lines |> Array.filter notEmpty |> Array.filter isGitHubFile |> Array.map (fun s -> s.Replace("File:","")) |> Array.toList }

    static member FromFile(fileName : string) =
        let lines = File.ReadAllLines(fileName)
        { ReferencesFile.FromLines lines with FileName = fileName }

    member this.AddNugetRef(reference : string) =
        tracefn "Adding %s to %s" reference (this.FileName)
        { this with NugetPackages = this.NugetPackages @ [reference] }

    member this.Save() =
        File.WriteAllText(this.FileName, this.ToString())
        tracefn "References file saved to %s" this.FileName

    override this.ToString() =
        List.append
            this.NugetPackages
            (this.GitHubFiles |> List.map (fun s -> "File:" + s))
            |> String.concat Environment.NewLine