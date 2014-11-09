namespace Paket

open System
open System.IO
open Logging

type GitHubReference = 
    { Name : string
      Link : string }

type ReferencesFile = 
    { FileName: string
      NugetPackages: list<string>
      GitHubFiles: list<GitHubReference> } 
    
    static member DefaultLink = Constants.PaketFilesFolderName

    static member FromLines(lines : string[]) = 
        let isGitHubFile (line: string) = line.StartsWith "File:"
        let notEmpty (line: string) = not <| String.IsNullOrWhiteSpace line

        { FileName = ""
          NugetPackages = lines |> Array.filter notEmpty |> Array.filter (isGitHubFile >> not) |> Array.toList
          GitHubFiles = 
            lines 
            |> Array.filter notEmpty 
            |> Array.filter isGitHubFile 
            |> Array.map (fun s -> s.Replace("File:","").Split([|' '|], StringSplitOptions.RemoveEmptyEntries))
            |> Array.map (fun segments -> { Name = segments.[0]; Link = if segments.Length = 2 
                                                                        then segments.[1]
                                                                        else ReferencesFile.DefaultLink} )
            |> Array.toList }

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
            (this.GitHubFiles |> List.map (fun s -> "File:" + s.Name + if s.Link <> ReferencesFile.DefaultLink then " " + s.Link else ""))
            |> String.concat Environment.NewLine