namespace Paket

open System
open System.IO
open Pri.LongPath
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
      Groups: Map<GroupName,InstallGroup> } 
    
    static member DefaultLink = Constants.PaketFilesFolderName

    static member New(fileName) = 
        let groups = [Constants.MainDependencyGroup, { Name = Constants.MainDependencyGroup; NugetPackages = []; RemoteFiles = [] }] |> Map.ofList
        { FileName = fileName
          Groups = groups }

    static member FromLines(lines : string[]) = 
        let groupedLines =
            lines
            |> Seq.map removeComment
            |> Seq.fold (fun state line -> 
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
            let line = if line.StartsWith "nuget " then line.Substring(6) else line
               
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
                        let packages = System.Collections.Generic.List<PackageInstallSettings>()
                        for line in nugetLines do
                            match line with
                            | x when x.StartsWith "exclude " ->
                                if packages.Count = 0 then
                                    failwithf "No package defined for '%s'." line
                                let p = packages.[packages.Count-1]
                                let e = line.Substring(7).Trim()
                                packages.[packages.Count-1] <- { p with Settings = { p.Settings with Excludes = p.Settings.Excludes @ [e] }}
                            | x when x.StartsWith "alias " ->
                                if packages.Count = 0 then
                                    failwithf "No package defined for '%s'." line
                                let p = packages.[packages.Count-1]
                                let all = line.Substring(5).Trim()
                                let parts = all.Split(' ')
                                if parts.Length < 2 then
                                    failwithf "Incorrect alias definition '%s'." line
                                let e = parts.[0]
                                let alias = all.Substring(e.Length).TrimStart()
                                packages.[packages.Count-1] <- { p with Settings = { p.Settings with Aliases = Map.add e alias p.Settings.Aliases }}
                            | _-> packages.Add(parsePackageInstallSettings line)
                        packages |> Seq.toList

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
                    { Name = groupName; NugetPackages = nugetPackages; RemoteFiles = remoteFiles })
            |> List.fold (fun m g -> 
                match Map.tryFind g.Name m with
                | None -> Map.add g.Name g m
                | Some group -> 
                    let newGroup = 
                        { Name = g.Name
                          NugetPackages = g.NugetPackages @ group.NugetPackages
                          RemoteFiles = g.RemoteFiles @ group.RemoteFiles }
                    Map.add g.Name newGroup m) Map.empty

        { FileName = ""
          Groups = groups }

    static member FromFile(fileName : string) =
        let lines = File.ReadAllLines(fileName)
        try
            { ReferencesFile.FromLines lines with FileName = fileName }
        with e -> raise <| new Exception(sprintf "Could not parse reference file '%s': %s" fileName e.Message, e)

    member this.AddNuGetReference(groupName, packageName : PackageName, copyLocal: bool, specificVersion: bool, importTargets: bool, frameworkRestrictions, includeVersionInPath, omitContent : bool, createBindingRedirects, referenceCondition) =
        let package: PackageInstallSettings =
            { Name = packageName
              Settings = 
                  { CopyLocal = if not copyLocal then Some copyLocal else None
                    SpecificVersion = if not specificVersion then Some specificVersion else None
                    CopyContentToOutputDirectory = None
                    ImportTargets = if not importTargets then Some importTargets else None
                    FrameworkRestrictions = frameworkRestrictions
                    IncludeVersionInPath = if includeVersionInPath then Some includeVersionInPath else None
                    ReferenceCondition = if String.IsNullOrWhiteSpace referenceCondition |> not then Some referenceCondition else None
                    CreateBindingRedirects = createBindingRedirects
                    Excludes = []
                    Aliases = Map.empty
                    OmitContent = if omitContent then Some ContentCopySettings.Omit else None 
                    GenerateLoadScripts = None } }


        match this.Groups |> Map.tryFind groupName with
        | None -> 
                tracefn "Adding package %O to %s into new group %O" packageName this.FileName groupName

                let newGroup = 
                    { Name = groupName
                      NugetPackages = [ package ]
                      RemoteFiles = [] }
                let newGroups = this.Groups |> Map.add newGroup.Name newGroup

                { this with Groups = newGroups }

        | Some group -> 
            if group.NugetPackages |> Seq.exists (fun p -> p.Name = packageName) then
                this
            else
                tracefn "Adding package %O to %s into group %O" packageName this.FileName groupName

                let newGroup = { group with NugetPackages = group.NugetPackages @ [ package ] }
                let newGroups = this.Groups |> Map.add newGroup.Name newGroup

                { this with Groups = newGroups }

    member this.AddNuGetReference(groupName, packageName : PackageName) = this.AddNuGetReference(groupName, packageName, true, true, true, ExplicitRestriction FrameworkRestriction.NoRestriction, false, false, None, null)

    member this.RemoveNuGetReference(groupName, packageName : PackageName) =
        let group = this.Groups.[groupName]
        if group.NugetPackages |> Seq.exists (fun p ->  p.Name = packageName) |> not then
            this
        else
            tracefn "Removing Package %O from %s" packageName this.FileName

            let newGroup = { group with  NugetPackages = group.NugetPackages |> List.filter (fun p -> p.Name <> packageName) }
            let newGroups = this.Groups |> Map.add newGroup.Name newGroup

            { this with Groups = newGroups }

    member this.Save() =
        File.WriteAllText(this.FileName, this.ToString())
        tracefn "References file saved to %s" this.FileName

    override this.ToString() =
        let printSourceFile (s:RemoteFileReference) = 
            "File:" + s.Name + 
              (if s.Link <> ReferencesFile.DefaultLink then " " + s.Link else "") +
              (match s.Settings.Link with | Some x -> " link: " + x.ToString().ToLower() | _ -> "")

        let printGroup g = 
            (g.NugetPackages |> List.collect (fun p -> 
                 let packageStr = String.Join(" ",[p.Name.ToString(); p.Settings.ToString()] |> List.filter (fun s -> s <> ""))
                 let excludes = (p.Settings.Excludes |> List.map (fun e -> sprintf "  exclude %s" e))
                 let aliases = (p.Settings.Aliases |> Seq.map (fun kv -> sprintf "  alias %s %s" kv.Key kv.Value)) |> Seq.toList
                 packageStr :: excludes @ aliases)) @
              (g.RemoteFiles |> List.map printSourceFile)

        String.Join
            (Environment.NewLine,
             [|let mainGroup = this.Groups.[Constants.MainDependencyGroup]
               yield! printGroup mainGroup
               for g in this.Groups do 
                if g.Key <> Constants.MainDependencyGroup then
                    if g.Value.NugetPackages <> [] || g.Value.RemoteFiles <> [] then
                        yield "group " + g.Key.ToString()
                        yield! printGroup g.Value|])