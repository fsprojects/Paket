namespace Paket

open System
open System.IO
open Logging
open Paket.Domain

type RemoteFileReference = 
    { Name : string
      Link : string }

[<RequireQualifiedAccess>]
type CopyLocal = 
    | True
    | False

type PackageInstallSettings = 
    { Name : PackageName
      CopyLocal : CopyLocal }

type ReferencesFile = 
    { FileName: string
      NugetPackages: PackageInstallSettings list
      RemoteFiles: RemoteFileReference list } 
    
    static member DefaultLink = Constants.PaketFilesFolderName

    static member New(fileName) = 
        { FileName = fileName
          NugetPackages = []
          RemoteFiles = [] }

    static member FromLines(lines : string[]) = 
        let isSingleFile (line: string) = line.StartsWith "File:"
        let notEmpty (line: string) = not <| String.IsNullOrWhiteSpace line
        let parsePackageInstallSettings (line: string) = 
            let parts = line.Split(' ')
            let kvPairs = line.Replace(parts.[0],"") |> parseKeyValuePairs
            { Name = PackageName parts.[0]
              CopyLocal =         
                match kvPairs.TryGetValue "copy_local" with
                | true, "false" -> CopyLocal.False 
                | _ -> CopyLocal.True }


        let remoteLines,nugetLines =
            lines 
            |> Array.filter notEmpty 
            |> Array.map (fun s -> s.Trim())
            |> Array.toList
            |> List.partition isSingleFile 

        { FileName = ""
          NugetPackages =
            nugetLines
            |> List.map parsePackageInstallSettings
          RemoteFiles = 
            remoteLines
            |> List.map (fun s -> s.Replace("File:","").Split([|' '|], StringSplitOptions.RemoveEmptyEntries))
            |> List.map (fun segments -> 
                            { Name = segments.[0]
                              Link = if segments.Length = 2 then segments.[1] else ReferencesFile.DefaultLink } ) }

    static member FromFile(fileName : string) =
        let lines = File.ReadAllLines(fileName)
        { ReferencesFile.FromLines lines with FileName = fileName }

    member this.AddNuGetReference(packageName : PackageName, copyLocal: CopyLocal) =
        let (PackageName referenceName) = packageName
        let normalized = NormalizedPackageName packageName
        if this.NugetPackages |> Seq.exists (fun p -> NormalizedPackageName p.Name = normalized) then
            this
        else
            tracefn "Adding %s to %s" referenceName (this.FileName)
            { this with NugetPackages = this.NugetPackages @ [{ Name = packageName; CopyLocal = copyLocal }] }

    member this.AddNuGetReference(packageName : PackageName) = this.AddNuGetReference(packageName, CopyLocal.True)

    member this.RemoveNuGetReference(packageName : PackageName) =
        let (PackageName referenceName) = packageName
        let normalized = NormalizedPackageName packageName
        if this.NugetPackages |> Seq.exists (fun p -> NormalizedPackageName p.Name = normalized) |> not then
            this
        else
            tracefn "Removing %s from %s" referenceName (this.FileName)
            { this with NugetPackages = this.NugetPackages |> List.filter (fun p -> NormalizedPackageName p.Name <> normalized) }

    member this.Save() =
        File.WriteAllText(this.FileName, this.ToString())
        tracefn "References file saved to %s" this.FileName

    override this.ToString() =
        List.append
            (this.NugetPackages |> List.map (fun p -> p.Name.ToString() + if p.CopyLocal = CopyLocal.False then " copy_local:false" else ""))
            (this.RemoteFiles |> List.map (fun s -> "File:" + s.Name + if s.Link <> ReferencesFile.DefaultLink then " " + s.Link else ""))
            |> String.concat Environment.NewLine