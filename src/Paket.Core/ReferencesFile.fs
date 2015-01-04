namespace Paket

open System
open System.IO
open Logging
open Paket.Domain

type RemoteFileReference = 
    { Name : string
      Link : string }

type ReferencesFile = 
    { FileName: string
      NugetPackages: PackageName list
      RemoteFiles: RemoteFileReference list } 
    
    static member DefaultLink = Constants.PaketFilesFolderName

    static member New(fileName) = 
        { FileName = fileName
          NugetPackages = []
          RemoteFiles = [] }

    static member FromLines(lines : string[]) = 
        let isSingleFile (line: string) = line.StartsWith "File:"
        let notEmpty (line: string) = not <| String.IsNullOrWhiteSpace line
        let remoteLines,nugetLines =
            lines 
            |> Array.filter notEmpty 
            |> Array.map (fun s -> s.Trim())
            |> Array.toList
            |> List.partition isSingleFile 

        { FileName = ""
          NugetPackages =
            nugetLines
            |> List.map PackageName
          RemoteFiles = 
            remoteLines
            |> List.map (fun s -> s.Replace("File:","").Split([|' '|], StringSplitOptions.RemoveEmptyEntries))
            |> List.map (fun segments -> 
                            { Name = segments.[0]
                              Link = if segments.Length = 2 then segments.[1] else ReferencesFile.DefaultLink } ) }

    static member FromFile(fileName : string) =
        let lines = File.ReadAllLines(fileName)
        { ReferencesFile.FromLines lines with FileName = fileName }

    member this.AddNuGetReference(packageName : PackageName) =
        let (PackageName referenceName) = packageName
        let normalized = NormalizedPackageName packageName
        if this.NugetPackages |> Seq.exists (fun p -> NormalizedPackageName p = normalized) then
            this
        else
            tracefn "Adding %s to %s" referenceName (this.FileName)
            { this with NugetPackages = this.NugetPackages @ [packageName] }

    member this.AddRemoteReference(url: string, remoteFileName: string) =
        if this.RemoteFiles |> Seq.exists (fun r -> r.Name = remoteFileName) then
            this
        else
            tracefn "Adding %s to %s" remoteFileName this.FileName
            { this with RemoteFiles = this.RemoteFiles @ [ { Name = remoteFileName; Link = ReferencesFile.DefaultLink } ] }

    member this.Save() =
        File.WriteAllText(this.FileName, this.ToString())
        tracefn "References file saved to %s" this.FileName

    override this.ToString() =
        List.append
            (this.NugetPackages |> List.map (|PackageName|))
            (this.RemoteFiles |> List.map (fun s -> "File:" + s.Name + if s.Link <> ReferencesFile.DefaultLink then " " + s.Link else ""))
            |> String.concat Environment.NewLine