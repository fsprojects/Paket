namespace Paket

open System
open System.IO
open Logging
open Paket.Domain
open Paket.Requirements

type RemoteFileReference = 
    { Name : string
      Link : string
      Settings : RemoteFileInstallSettings }

type PackageInstallSettings = 
    { Name : PackageName
      Settings : InstallSettings }

    static member Default(name) =
        { Name = PackageName name
          Settings = InstallSettings.Default }

type InstallGroup = 
    { Name : GroupName
      NugetPackages : PackageInstallSettings list
      RemoteFiles : RemoteFileReference list }

type ReferencesFile = 
    { FileName: string
      Groups: Map<NormalizedGroupName,InstallGroup> } 
    
    static member DefaultLink = Constants.PaketFilesFolderName

    static member New(fileName) = 
        let groups = [NormalizedGroupName Constants.MainDependencyGroup, { Name = Constants.MainDependencyGroup; NugetPackages = []; RemoteFiles = [] }] |> Map.ofList
        { FileName = fileName
          Groups = groups }

    static member FromLines(lines : string[]) = 
        let groupedLines =
            lines
            |> Array.fold (fun state line -> 
                match state with
                | [] -> failwithf "error while parsing %A" lines
                | ((name,lines) as currentGroup)::otherGroups ->
                    if line.StartsWith "group " then
                        let name = line.Replace("group","").Trim()
                        (GroupName name,[])::currentGroup::otherGroups
                    else 
                        (name,line::lines)::otherGroups) [Constants.MainDependencyGroup,[]]
            |> List.map (fun (name,lines) -> name,lines |> List.rev |> Array.ofList)

        let isSingleFile (line: string) = line.StartsWith "File:"
        let notEmpty (line: string) = not <| String.IsNullOrWhiteSpace line
        let parsePackageInstallSettings (line: string) : PackageInstallSettings = 
            let parts = line.Split(' ')
            { Name = PackageName parts.[0]
              Settings = InstallSettings.Parse(line.Replace(parts.[0],"")) } 

        let groups = 
            groupedLines 
            |> List.map (fun (groupName,lines) ->
                    let remoteLines,nugetLines =
                        lines 
                        |> Array.filter notEmpty 
                        |> Array.map (fun s -> s.Trim())
                        |> Array.toList
                        |> List.partition isSingleFile 

        
                    let nugetPackages =
                        nugetLines
                        |> List.map parsePackageInstallSettings

                    let remoteFiles = 
                        remoteLines
                        |> List.map (fun s -> s.Replace("File:","").Split([|' '|], StringSplitOptions.RemoveEmptyEntries))
                        |> List.map (fun segments ->
                                        let hasPath =
                                            let get x = if segments.Length > x then segments.[x] else ""
                                            segments.Length >= 2 && not ((get 1).Contains(":")) && not ((get 2).StartsWith(":")) 

                                        let rest = 
                                            let skip = if hasPath then 2 else 1
                                            if segments.Length < skip then "" else String.Join(" ",segments |> Seq.skip skip)

                                        { Name = segments.[0]
                                          Link = if hasPath then segments.[1] else ReferencesFile.DefaultLink 
                                          Settings = RemoteFileInstallSettings.Parse rest })
                    NormalizedGroupName groupName, { Name = groupName; NugetPackages = nugetPackages; RemoteFiles = remoteFiles })
            |> Map.ofList

        { FileName = ""
          Groups = groups }

    static member FromFile(fileName : string) =
        let lines = File.ReadAllLines(fileName)
        { ReferencesFile.FromLines lines with FileName = fileName }

    member this.AddNuGetReference(packageName : PackageName, copyLocal: bool, importTargets: bool, frameworkRestrictions, includeVersionInPath, omitContent : bool) =
        let (PackageName referenceName) = packageName
        let normalized = NormalizedPackageName packageName
        let mainGroup = this.Groups.[NormalizedGroupName Constants.MainDependencyGroup] // TODO: Add to correct group
        if mainGroup.NugetPackages |> Seq.exists (fun p -> NormalizedPackageName p.Name = normalized) then
            this
        else
            tracefn "Adding %s to %s" referenceName this.FileName      

            let package: PackageInstallSettings =
                { Name = packageName
                  Settings = 
                    { CopyLocal = if not copyLocal then Some copyLocal else None
                      ImportTargets = if not importTargets then Some importTargets else None
                      FrameworkRestrictions = frameworkRestrictions
                      IncludeVersionInPath = if includeVersionInPath then Some includeVersionInPath else None
                      OmitContent = if omitContent then Some omitContent else None } }

            let newMainGroup = { mainGroup with NugetPackages = mainGroup.NugetPackages @ [ package ] }
            let newGroups = this.Groups |> Map.add (NormalizedGroupName newMainGroup.Name) newMainGroup

            { this with Groups = newGroups }

    member this.AddNuGetReference(packageName : PackageName) = this.AddNuGetReference(packageName, true, true, [], false, false)

    member this.RemoveNuGetReference(packageName : PackageName) =
        let (PackageName referenceName) = packageName
        let normalized = NormalizedPackageName packageName
        let mainGroup = this.Groups.[NormalizedGroupName Constants.MainDependencyGroup] // TODO: Remove from correct group
        if mainGroup.NugetPackages |> Seq.exists (fun p -> NormalizedPackageName p.Name = normalized) |> not then
            this
        else
            tracefn "Removing %s from %s" referenceName (this.FileName)

            let newMainGroup = { mainGroup with  NugetPackages = mainGroup.NugetPackages |> List.filter (fun p -> NormalizedPackageName p.Name <> normalized) }
            let newGroups = this.Groups |> Map.add (NormalizedGroupName newMainGroup.Name) newMainGroup

            { this with Groups = newGroups }

    member this.Save() =
        File.WriteAllText(this.FileName, this.ToString())
        tracefn "References file saved to %s" this.FileName

    override this.ToString() =  // TODO: Clean this up!
        String.Join
            (Environment.NewLine,
             [|let mainGroup = this.Groups.[NormalizedGroupName Constants.MainDependencyGroup]
               yield! (mainGroup.NugetPackages |> List.map (fun p -> String.Join(" ",[p.Name.ToString(); p.Settings.ToString()] |> List.filter (fun s -> s <> ""))))
               yield! (mainGroup.RemoteFiles |> List.map (fun s -> "File:" + s.Name + if s.Link <> ReferencesFile.DefaultLink then " " + s.Link else ""))
               for g in this.Groups do 
                if g.Key <> NormalizedGroupName Constants.MainDependencyGroup then
                    yield "group: " + g.Key.ToString()
                    yield! (g.Value.NugetPackages |> List.map (fun p -> String.Join(" ",[p.Name.ToString(); p.Settings.ToString()] |> List.filter (fun s -> s <> ""))))
                    yield! (g.Value.RemoteFiles |> List.map (fun s -> "File:" + s.Name + if s.Link <> ReferencesFile.DefaultLink then " " + s.Link else "")) |])