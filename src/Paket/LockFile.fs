/// Contains methods to handle lockfiles.
module Paket.LockFile

open System
open System.IO

/// [omit]
let format (resolved : PackageResolution) = 
    // TODO: implement conflict handling
    let sources = 
        resolved
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
                  let hash = 
                      match package.Hash with
                      | Some hash -> sprintf " %s %s" hash.Algorithm hash.Hash
                      | None -> ""
                  yield sprintf "    %s (%s)%s" package.Name (version.ToString()) hash ]
    
    String.Join(Environment.NewLine, all)

/// Parses a lockfile from lines
let Parse(lines : string seq) = 
    let lines = 
        lines
        |> Seq.filter (fun line -> String.IsNullOrWhiteSpace line |> not)
        |> Seq.map (fun line -> line.Trim(' '))
        |> Seq.skip 1
    
    let remote = ref "http://nuget.org/api/v2"
    [ for line in lines do
          if line.StartsWith("remote:") then remote := line.Replace("remote: ", "")
          elif line.StartsWith("specs:") then ()
          else
              let splitted = line.Split(' ')
              let version = splitted.[1].Replace("(", "").Replace(")", "")
              yield { SourceType = "nuget"
                      Source = !remote
                      Name = splitted.[0]
                      Hash = if splitted.Length > 2 then Some({ Algorithm = splitted.[2]; Hash = splitted.[3] }) else None
                      VersionRange = VersionRange.Exactly version } ]

/// Analyzes the dependencies from the packageFile.
let Create(packageFile) = 
    let cfg = Config.ReadFromFile packageFile
    cfg.Resolve(Nuget.NugetDiscovery)

/// Updates the lockfile with the analyzed dependencies from the packageFile.
let Update(packageFile, lockFile) = 
    let resolution = Create(packageFile)
    File.WriteAllText(lockFile, format resolution)
    printfn "Lockfile written to %s" lockFile