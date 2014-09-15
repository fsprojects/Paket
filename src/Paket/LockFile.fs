/// Contains methods to handle lockfiles.
module Paket.LockFile

open System
open System.IO

/// [omit]
let formatVersionRange (version : VersionRange) = 
    match version with
    | Minimum v -> ">= " + v.ToString()
    | Specific v -> v.ToString()
    | Latest -> ">= 0"
    | Range(_, v1, v2, _) -> ">= " + v1.ToString() + ", < " + v2.ToString()

/// [omit]
let extractErrors (resolved : PackageResolution) = 
    let errors = 
        resolved
        |> Seq.map (fun x ->
            match x.Value with
            | Resolved _ -> ""
            | Conflict(c1,c2) ->
                let d1 = 
                    match c1 with
                    | FromRoot _ -> "Dependencies file"
                    | FromPackage d -> 
                        let v1 = 
                            match d.Defining.VersionRange with
                            | Specific v -> v.ToString()
                        d.Defining.Name + " " + v1
     
                let d2 = 
                    match c2 with
                    | FromRoot _ -> "Dependencies file"
                    | FromPackage d -> 
                        let v1 = 
                            match d.Defining.VersionRange with
                            | Specific v -> v.ToString()
                        d.Defining.Name + " " + v1

                sprintf "%s depends on%s  %s (%s)%s%s depends on%s  %s (%s)" 
                        d1 Environment.NewLine c1.Referenced.Name (formatVersionRange c1.Referenced.VersionRange) Environment.NewLine 
                        d2 Environment.NewLine c2.Referenced.Name (formatVersionRange c2.Referenced.VersionRange) 
            )
        |> Seq.filter ((<>) "")
    String.Join(Environment.NewLine,errors)


/// [omit]
let format strictMode (resolved : PackageResolution) = 
    let sources = 
        resolved
        |> Seq.map (fun x ->
            match x.Value with
            | Resolved package -> 
                match package.Source with
                | Nuget url -> url,package
                | LocalNuget path -> path,package
            | Conflict(c1,c2) ->
                traceErrorfn "%A %A" c1 c2
                failwith ""  // TODO: trace all errors
            )
        |> Seq.groupBy fst

    let all = 
        [ if strictMode then
            yield "REFERENCES: STRICT"
          yield "NUGET"
          for source, packages in sources do
              yield "  remote: " + source
              yield "  specs:"
              for _,package in packages do
                  yield sprintf "    %s (%s)" package.Name (package.Version.ToString()) 
                  for name,v in package.DirectDependencies do
                      yield sprintf "      %s (%s)" name (formatVersionRange v)]
    
    String.Join(Environment.NewLine, all)

let private (|Remote|Package|Dependency|Spec|Header|Blank|ReferencesMode|) (line:string) =
    match line.Trim() with
    | "NUGET" -> Header
    | _ when String.IsNullOrWhiteSpace line -> Blank
    | trimmed when trimmed.StartsWith "remote:" -> Remote (trimmed.Substring(trimmed.IndexOf(": ") + 2))
    | trimmed when trimmed.StartsWith "specs:" -> Spec
    | trimmed when trimmed.StartsWith "REFERENCES:" -> ReferencesMode(trimmed.Replace("REFERENCES:","").Trim() = "STRICT")
    | trimmed when line.StartsWith "      " ->
         let parts = trimmed.Split '(' 
         Dependency (parts.[0].Trim(),parts.[1].Replace("(", "").Replace(")", "").Trim())
    | trimmed -> Package trimmed

/// Parses a Lock file from lines
let Parse(lines : string seq) =
    (("http://nuget.org/api/v2", false,  []), lines)
    ||> Seq.fold(fun (currentSource, referencesMode, packages) line ->
        match line with
        | Remote newSource -> newSource, referencesMode, packages
        | Header | Spec | Blank -> (currentSource, referencesMode, packages)
        | ReferencesMode mode -> (currentSource, mode, packages)
        | Package details ->
            let parts = details.Split(' ')
            let version = parts.[1].Replace("(", "").Replace(")", "")
            currentSource, referencesMode,
                 { Source = PackageSource.Parse currentSource
                   Name = parts.[0]
                   DirectDependencies = []
                   Version = SemVer.parse version } :: packages
        | Dependency(name,version) ->
            match packages with
            | currentPackage :: otherPackages -> 
                currentSource,
                referencesMode,
                { currentPackage with
                    DirectDependencies = [name,Latest] // TODO: parse version if we really need it 
                    |> List.append currentPackage.DirectDependencies } :: otherPackages
            | _ -> failwith "cannot set a dependency - no package has been specified.")
    |> fun (_,referencesMode,xs) -> referencesMode, List.rev xs

/// Analyzes the dependencies from the Dependencies file.
let Create(force,dependenciesFile) =     
    let cfg = DependenciesFile.ReadFromFile dependenciesFile
    tracefn "Analyzing %s" dependenciesFile
    cfg,cfg.Resolve(force,Nuget.NugetDiscovery)

/// Updates the Lock file with the analyzed dependencies from the Dependencies file.
let Update(force, packageFile, lockFile) = 
    let cfg,resolution = Create(force,packageFile)
    let errors = extractErrors resolution
    if errors = "" then
        File.WriteAllText(lockFile, format cfg.Strict resolution)
        tracefn "Locked version resolutions written to %s" lockFile
    else
        failwith <| "Could not resolve dependencies." + Environment.NewLine + errors
