namespace Paket

open System
open System.IO
open Logging
open Paket.Domain
open Paket.Requirements

type RemoteFileReference = 
    { Name : string
      Link : string }

type PackageInstallSettings = 
    { Name : PackageName
      ImportTargets : bool
      FrameworkRestrictions: FrameworkRestrictions
      CopyLocal : bool }

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
              ImportTargets =
                match kvPairs.TryGetValue "import_targets" with
                | true, "false" -> false
                | _ -> true
              FrameworkRestrictions =
                match kvPairs.TryGetValue "framework" with
                | true, s -> Requirements.parseRestrictions s
                | _ -> []
              CopyLocal =         
                match kvPairs.TryGetValue "copy_local" with
                | true, "false" -> false 
                | _ -> true }


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

    member this.AddNuGetReference(packageName : PackageName, copyLocal: bool, importTargets: bool, frameworkRestrictions) =
        let (PackageName referenceName) = packageName
        let normalized = NormalizedPackageName packageName
        if this.NugetPackages |> Seq.exists (fun p -> NormalizedPackageName p.Name = normalized) then
            this
        else
            tracefn "Adding %s to %s" referenceName (this.FileName)
            { this with NugetPackages = this.NugetPackages @ [{ Name = packageName; CopyLocal = copyLocal; ImportTargets = importTargets; FrameworkRestrictions = frameworkRestrictions }] }

    member this.AddNuGetReference(packageName : PackageName) = this.AddNuGetReference(packageName, true, true, [])

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
            (this.NugetPackages |> List.map (fun p -> 
                let s1 = if p.CopyLocal = false then "copy_local: false" else ""
                let s2 = if p.ImportTargets = false then "import_targets: false" else ""
                let s3 =
                      match p.FrameworkRestrictions with
                      | [] -> ""
                      | _  -> "framework: " + (String.Join(", ",p.FrameworkRestrictions))

                let s = String.Join(", ",[s1; s2; s3] |> List.filter (fun s -> s <> ""))
                
                String.Join(" ",[p.Name.ToString(); s] |> List.filter (fun s -> s <> ""))))
            (this.RemoteFiles |> List.map (fun s -> "File:" + s.Name + if s.Link <> ReferencesFile.DefaultLink then " " + s.Link else ""))
            |> String.concat Environment.NewLine