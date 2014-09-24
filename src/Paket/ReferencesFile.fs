namespace Paket

open System
open System.IO

type ReferencesFile = 
    { FileName: string
      NugetPackages: list<string>
      GithubFiles: list<string> } 
    
    static member FromLines(lines : string[]) = 
        let isGitHubFile (line: string) = line.StartsWith "File:"
        let notEmpty (line: string) = not <| System.String.IsNullOrWhiteSpace line

        { FileName = ""
          NugetPackages = lines |> Array.filter notEmpty |> Array.filter (isGitHubFile >> not) |> Array.toList
          GithubFiles = lines |> Array.filter notEmpty |> Array.filter isGitHubFile |> Array.map (fun s -> s.Replace("File:","")) |> Array.toList }

    static member FromFile(fileName : string) =
        let lines = File.ReadAllLines(fileName)
        { ReferencesFile.FromLines lines with FileName = fileName }

    override this.ToString() =
        List.append
            this.NugetPackages
            (this.GithubFiles |> List.map (fun s -> "File:" + s))
            |> String.concat Environment.NewLine