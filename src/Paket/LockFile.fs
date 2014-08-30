module Paket.LockFile

open System
open System.IO

let format (resolved:PackageResolution)  =
    // TODO: implement conflict handling
    let sources =
        resolved
        |> Seq.map (fun x ->
            match x.Value with
            | Resolved d -> 
                match d.Referenced.VersionRange with
                | Exactly v -> d.Referenced.Source,d.Referenced.Name,v
            )
        |> Seq.groupBy (fun (s,_,_) -> s)
   
    let all =
        [yield "NUGET"
         for source,packages in sources do
             yield "  remote: " + source
             yield "  specs:"
             for _,name,version in packages do
                 yield sprintf "    %s (%s)" name version]

    String.Join(Environment.NewLine,all)

let Update packageFile lockFile =
    let cfg = Config.ReadFromFile packageFile
    let resolution = cfg.Resolve(Nuget.NugetDiscovery)

    File.WriteAllText(lockFile, format resolution)

    printfn "Lockfile written to %s" lockFile