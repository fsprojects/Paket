/// Contains methods to handle lockfiles.
module Paket.LockFile

open System
open System.IO

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
                      yield sprintf "      %s (%s)" d.Name (ConfigHelpers.formatVersionRange d.VersionRange)]
    
    String.Join(Environment.NewLine, all)

/// Parses a lockfile from lines
let Parse(lines : string seq) = 
    let lines = 
        lines
        |> Seq.filter (fun line -> String.IsNullOrWhiteSpace line |> not)                
        |> Seq.skip 1
    
    let remote = ref "http://nuget.org/api/v2"
    [ use e = lines.GetEnumerator()    
      while e.MoveNext() do
          let line = e.Current
          if line.Contains("remote:") then remote := line.Replace("remote:", "").Replace(" ", "")
          elif line.Contains("specs:") then ()
          elif line.StartsWith "      " then ()
          else
              let splitted = line.Trim(' ').Split(' ')
              let version = splitted.[1].Replace("(", "").Replace(")", "")
              yield { SourceType = "nuget"
                      Source = !remote
                      Name = splitted.[0]
                      DirectDependencies = None
                      VersionRange = VersionRange.Exactly version } ]

/// Analyzes the dependencies from the packageFile.
let Create(force,packageFile) = 
    let cfg = Config.ReadFromFile packageFile
    cfg.Resolve(force,Nuget.NugetDiscovery)

/// Updates the lockfile with the analyzed dependencies from the packageFile.
let Update(force, packageFile, lockFile) = 
    let resolution = Create(force,packageFile)
    File.WriteAllText(lockFile, format resolution)
    printfn "Lockfile written to %s" lockFile
