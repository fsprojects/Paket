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
let format (resolved : PackageResolution) = 
    // TODO: implement conflict handling
    let sources = 
        resolved.ResolvedVersionMap
        |> Seq.map (fun x ->
            match x.Value with
            | Resolved d -> 
                match d.Referenced.VersionRange with
                | Specific v -> d.Referenced.Source,d.Referenced,v
            )
        |> Seq.groupBy (fun (s,_,_) -> s)

    let all = 
        [ yield "NUGET"
          for source, packages in sources do
              yield "  remote: " + source
              yield "  specs:"
              for _, package, version in packages do
                  yield sprintf "    %s (%s)" package.Name (version.ToString()) 
                  for d in resolved.DirectDependencies.[package.Name,version.ToString()] do
                      yield sprintf "      %s (%s)" d.Name (formatVersionRange d.VersionRange)]
    
    String.Join(Environment.NewLine, all)

let private (|Remote|Package|Dependency|Spec|Header|Blank|) (line:string) =
    match line.Trim() with
    | "NUGET" -> Header
    | _ when String.IsNullOrWhiteSpace line -> Blank
    | trimmed when trimmed.StartsWith "remote:" -> Remote (trimmed.Substring(trimmed.IndexOf(": ") + 2))
    | trimmed when trimmed.StartsWith "specs:" -> Spec
    | trimmed when line.StartsWith "      " -> Dependency (trimmed.Split ' ' |> Seq.head)
    | trimmed -> Package trimmed

/// Parses a lockfile from lines
let Parse(lines : string seq) =
    (("http://nuget.org/api/v2", []), lines)
    ||> Seq.fold(fun (currentSource, packages) line ->
        match line with
        | Remote newSource -> newSource, packages
        | Header | Spec | Blank -> (currentSource, packages)
        | Package details ->
            let parts = details.Split(' ')
            let version = parts.[1].Replace("(", "").Replace(")", "")
            currentSource, { SourceType = Nuget
                             Source = currentSource 
                             Name = parts.[0]
                             DirectDependencies = []
                             ResolverStrategy = Max
                             VersionRange = VersionRange.Exactly version } :: packages
        | Dependency details ->
            match packages with
            | currentPackage :: otherPackages -> 
                currentSource,
                { currentPackage with
                    DirectDependencies = [details]
                                         |> List.append currentPackage.DirectDependencies } :: otherPackages
            | _ -> failwith "cannot set a dependency - no package has been specified.")
    |> snd
    |> List.rev

/// Analyzes the dependencies from the packageFile.
let Create(force,packageFile) = 
    let cfg = Config.ReadFromFile packageFile
    cfg.Resolve(force,Nuget.NugetDiscovery)

/// Updates the lockfile with the analyzed dependencies from the packageFile.
let Update(force, packageFile, lockFile) = 
    let resolution = Create(force,packageFile)
    File.WriteAllText(lockFile, format resolution)
    printfn "Lockfile written to %s" lockFile
