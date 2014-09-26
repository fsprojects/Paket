namespace Paket

open System
open System.IO
open Paket.Logging

module LockFileSerializer =
    /// [omit]
    let formatVersionRange (version : VersionRequirement) = 
        match version.Range with
        | Minimum v -> ">= " + v.ToString()
        | Specific v -> v.ToString()
        | Range(_, v1, v2, _) -> ">= " + v1.ToString() + ", < " + v2.ToString()

    /// [omit]
    let serializePackages strictMode (resolved : PackageResolution) = 
        let sources = 
            resolved
            |> Seq.map (fun kv ->
                    let package = kv.Value
                    match package.Source with
                    | Nuget source -> source.Url,source.Auth,package
                    | LocalNuget path -> path,None,package
                )
            |> Seq.groupBy (fun (a,b,_) -> a,b)

        let all = 
            let hasReported = ref false
            [ if strictMode then
                yield "REFERENCES: STRICT"
              for (source, auth), packages in sources do
                  if not !hasReported then
                    yield "NUGET"
                    hasReported := true

                  match auth with
                  | None -> yield "  remote: " + source
                  | Some auth -> yield sprintf "  remote: %s username: \"%s\" password: \"%s\"" source auth.Username auth.Password

                  yield "  specs:"
                  for _,_,package in packages |> Seq.sortBy (fun (_,_,p) -> p.Name.ToLower()) do
                      yield sprintf "    %s (%s)" package.Name (package.Version.ToString()) 
                      for name,v in package.Dependencies do
                          yield sprintf "      %s (%s)" name (formatVersionRange v)]
    
        String.Join(Environment.NewLine, all)

    let serializeSourceFiles (files:ResolvedSourceFile list) =    
        let all =
            let hasReported = ref false
            [ for (owner,project), files in files |> Seq.groupBy(fun f -> f.Owner, f.Project) do
                if not !hasReported then
                    yield "GITHUB"
                    hasReported := true

                yield sprintf "  remote: %s/%s" owner project
                yield "  specs:"
                for file in files |> Seq.sortBy (fun f -> f.Owner.ToLower(),f.Project.ToLower(),f.Name.ToLower())  do
                    let path = file.Name.TrimStart '/'
                    yield sprintf "    %s (%s)" path file.Commit ]

        String.Join(Environment.NewLine, all)

module LockFileParser =
    type ParseState =
        { RepositoryType : string option
          RemoteAuth : Auth option
          RemoteUrl :string option
          Packages : ResolvedPackage list
          SourceFiles : ResolvedSourceFile list
          Strict: bool }

    let private (|Remote|NugetPackage|NugetDependency|SourceFile|RepositoryType|Blank|ReferencesMode|) (state, line:string) =
        match (state.RepositoryType, line.Trim()) with
        | _, "NUGET" -> RepositoryType "NUGET"
        | _, "GITHUB" -> RepositoryType "GITHUB"
        | _, _ when String.IsNullOrWhiteSpace line -> Blank
        | _, trimmed when trimmed.StartsWith "remote:" -> Remote(trimmed.Substring(trimmed.IndexOf(": ") + 2).Split(' ').[0], DependenciesFileParser.parseAuth trimmed)
        | _, trimmed when trimmed.StartsWith "specs:" -> Blank
        | _, trimmed when trimmed.StartsWith "REFERENCES:" -> ReferencesMode(trimmed.Replace("REFERENCES:","").Trim() = "STRICT")
        | _, trimmed when line.StartsWith "      " ->
            let parts = trimmed.Split '(' 
            NugetDependency (parts.[0].Trim(),parts.[1].Replace("(", "").Replace(")", "").Trim())
        | Some "NUGET", trimmed -> NugetPackage trimmed
        | Some "GITHUB", trimmed -> SourceFile trimmed
        | Some _, _ -> failwith "unknown Repository Type."
        | _ -> failwith "unknown lock file format."

    let Parse lines =
        let remove textToRemove (source:string) = source.Replace(textToRemove, "")
        let removeBrackets = remove "(" >> remove ")"
        ({ RepositoryType = None; RemoteAuth = None; RemoteUrl = None; Packages = []; SourceFiles = []; Strict = false }, lines)
        ||> Seq.fold(fun state line ->
            match (state, line) with
            | Remote(url,auth) -> { state with RemoteUrl = Some url; RemoteAuth = auth }
            | Blank -> state
            | ReferencesMode mode -> { state with Strict = mode }
            | RepositoryType repoType -> { state with RepositoryType = Some repoType }
            | NugetPackage details ->
                match state.RemoteUrl with
                | Some remote ->
                    let parts = details.Split ' '
                    let version = parts.[1] |> removeBrackets
                    { state with Packages = { Source = PackageSource.Parse(remote,state.RemoteAuth)
                                              Name = parts.[0]
                                              Dependencies = []
                                              Version = SemVer.parse version } :: state.Packages }
                | None -> failwith "no source has been specified."
            | NugetDependency (name, _) ->
                match state.Packages with
                | currentPackage :: otherPackages -> 
                    { state with
                        Packages = { currentPackage with
                                        Dependencies = [name, VersionRequirement.AllReleases] 
                                        |> List.append currentPackage.Dependencies
                                    } :: otherPackages }
                | [] -> failwith "cannot set a dependency - no package has been specified."
            | SourceFile details ->
                match state.RemoteUrl |> Option.map(fun s -> s.Split '/') with
                | Some [| owner; project |] ->
                    let path, commit = match details.Split ' ' with
                                        | [| filePath; commit |] -> filePath, commit |> removeBrackets                                       
                                        | _ -> failwith "invalid file source details."
                    { state with
                        SourceFiles = { Commit = commit
                                        Owner = owner
                                        Project = project
                                        Name = path } :: state.SourceFiles }
                | _ -> failwith "invalid remote details.")


/// Allows to parse and analyze paket.lock files.
type LockFile(fileName:string,strictMode,resolution:PackageResolution,remoteFiles:ResolvedSourceFile list) =
    member __.SourceFiles = remoteFiles
    member __.ResolvedPackages = resolution
    member __.FileName = fileName
    member __.Strict = strictMode

    /// Updates the Lock file with the analyzed dependencies from the paket.dependencies file.
    member __.Save() =
        let output = 
            String.Join
                (Environment.NewLine,                  
                    LockFileSerializer.serializePackages strictMode resolution, 
                    LockFileSerializer.serializeSourceFiles remoteFiles)
        File.WriteAllText(fileName, output)
        tracefn "Locked version resolutions written to %s" fileName

    /// Parses a paket.lock file from lines
    static member LoadFrom(lockFileName) : LockFile =
        let lines = File.ReadAllLines lockFileName
        LockFileParser.Parse lines
        |> fun state -> LockFile(lockFileName,state.Strict,state.Packages |> Seq.fold (fun map p -> Map.add p.Name p map) Map.empty, List.rev state.SourceFiles)